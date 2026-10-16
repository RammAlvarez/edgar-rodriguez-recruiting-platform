using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Abstractions;
using Platform.Core.Catalogs;
using Platform.Core.Http;
using Platform.Core.Security;

namespace Platform.Core.People;

/// <summary>Postgres enum <c>bank_account_type</c>; the Azteca layout writes them as 1 / 3 / 40.</summary>
public enum BankAccountType
{
    OwnAccount,
    DebitCard,
    Clabe,
}

/// <summary>
/// DATA_MODEL §3.7, a «cuenta de pago» (SPEC §5.8): the number is stored encrypted (AES-256-GCM) with its last four digits in
/// clear; the API never returns the number. One primary account per person receives the pay.
/// </summary>
public sealed class PersonBankAccount : IStamped
{
    public long Id { get; set; }
    public Guid Uid { get; set; } = Guid.CreateVersion7();
    public Guid PersonId { get; set; }
    public Guid BankId { get; set; }
    public BankAccountType AccountType { get; set; }
    public required byte[] AccountNumberEnc { get; set; }
    public required string AccountLast4 { get; set; }
    public required string HolderName { get; set; }
    public string? Alias { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsFrequentAccountRegistered { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsCurrent(DateOnly today) => ValidTo is null || ValidTo >= today;
}

/// <summary>
/// Account number checks at capture (SPEC §5.8, PLAN D82): length by type, CLABE control digit and bank prefix, card Luhn digit
/// — a typo found here never reaches a bank file. The bank layouts (S2) run the same checks.
/// </summary>
public static class AccountNumbers
{
    public static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    public static int[] Lengths(BankAccountType type) => type switch
    {
        BankAccountType.DebitCard => [16],
        BankAccountType.Clabe => [18],
        _ => [14, 20],
    };

    /// <summary>The problem with <paramref name="digits"/> for an account of <paramref name="type"/> at the bank with that SPEI code.</summary>
    public static string? Issue(BankAccountType type, string digits, string bankCode)
    {
        var lengths = Lengths(type);
        if (!lengths.Contains(digits.Length))
            return $"Para este tipo de cuenta el número debe tener {string.Join(" o ", lengths)} dígitos (tiene {digits.Length}).";
        return type switch
        {
            BankAccountType.Clabe when !IsClabe(digits) => "CLABE inválida: el dígito verificador no coincide.",
            BankAccountType.Clabe when !digits.StartsWith(bankCode, StringComparison.Ordinal) =>
                $"La CLABE es de otro banco (empieza con {digits[..3]}, el banco receptor es {bankCode}).",
            BankAccountType.DebitCard when !IsLuhn(digits) => "Número de tarjeta inválido: el dígito verificador no coincide.",
            _ => null,
        };
    }

    /// <summary>CLABE: 18 digits, weights 3-7-1 over the first 17, control digit = (10 − Σ mod 10) mod 10.</summary>
    public static bool IsClabe(string value)
    {
        if (value.Length != 18 || !value.All(char.IsAsciiDigit))
            return false;
        int[] weights = [3, 7, 1];
        var sum = 0;
        for (var i = 0; i < 17; i++)
            sum += (value[i] - '0') * weights[i % 3] % 10;
        return (10 - sum % 10) % 10 == value[17] - '0';
    }

    /// <summary>Luhn check of a card number.</summary>
    public static bool IsLuhn(string value)
    {
        if (value.Length == 0 || !value.All(char.IsAsciiDigit))
            return false;
        var sum = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var d = value[value.Length - 1 - i] - '0';
            if (i % 2 == 1)
                d = d * 2 > 9 ? d * 2 - 9 : d * 2;
            sum += d;
        }
        return sum % 10 == 0;
    }
}

/// <summary>«Agregar cuenta»: the number is typed once; to change it, add a new account and close the old one.</summary>
public sealed record BankAccountRequest(Guid BankId, BankAccountType AccountType, string AccountNumber, string? HolderName,
    string? Alias, DateOnly? ValidFrom, bool MakePrimary);

/// <summary>Titular, alias and «vigente hasta» (closing an account keeps it in the history).</summary>
public sealed record UpdateBankAccountRequest(string HolderName, string? Alias, DateOnly? ValidTo);

/// <summary>
/// An account as every screen shows it: «•••• 4562», never the number. <see cref="IsThirdParty"/> = the titular is not the
/// person (paid to a spouse, a relative — SPEC §5.8).
/// </summary>
public sealed record BankAccountDto(Guid Id, Guid BankId, string BankName, string BankCode, BankAccountType AccountType, string Last4,
    string HolderName, string? Alias, bool IsPrimary, bool IsThirdParty, DateOnly ValidFrom, DateOnly? ValidTo, bool IsCurrent,
    bool IsFrequentAccountRegistered);

/// <summary>A bank in a picker.</summary>
public sealed record BankOptionDto(Guid Id, string Name, string Code);

internal sealed class BankAccountRequestValidator : AbstractValidator<BankAccountRequest>
{
    public BankAccountRequestValidator()
    {
        RuleFor(r => r.AccountNumber).Must(n => AccountNumbers.Digits(n ?? "").Length > 0).WithMessage("Escribe el número.");
        RuleFor(r => r.HolderName).OptionalText(BankAccounts.HolderMax);
        RuleFor(r => r.Alias).OptionalText(60);
    }
}

internal sealed class UpdateBankAccountRequestValidator : AbstractValidator<UpdateBankAccountRequest>
{
    public UpdateBankAccountRequestValidator()
    {
        RuleFor(r => r.HolderName).Must(n => n.Clean() is not null).WithMessage("Escribe el nombre del titular.");
        RuleFor(r => r.HolderName).OptionalText(BankAccounts.HolderMax);
        RuleFor(r => r.Alias).OptionalText(60);
    }
}

/// <summary>
/// The account operations the Personal tab (S7) and the Candidates app share; callers check access and save. Field errors
/// come back as a dictionary for <see cref="ApiResults.Validation"/>.
/// </summary>
public static class BankAccounts
{
    /// <summary>The Azteca layout's NOMBRE DEL BENEFICIARIO holds 40 characters.</summary>
    public const int HolderMax = 40;

    public const string NotFound = "La cuenta no existe.";
    public const string ClosedCannotBePrimary = "La cuenta ya no está vigente; no puede ser la principal.";
    public const string UnknownBank = "Elige un banco de la lista.";

    public static async Task<(PersonBankAccount? Account, Dictionary<string, string[]>? Errors)> AddAsync(DbContext db, IFieldCipher cipher,
        Person person, BankAccountRequest r, DateOnly today, CancellationToken ct)
    {
        var bank = await db.Set<Bank>().SingleOrDefaultAsync(b => b.Uid == r.BankId && b.IsActive, ct);
        if (bank is null)
            return (null, new() { ["bankId"] = [UnknownBank] });
        var digits = AccountNumbers.Digits(r.AccountNumber);
        if (AccountNumbers.Issue(r.AccountType, digits, bank.SpeiCode) is { } issue)
            return (null, new() { ["accountNumber"] = [issue] });

        var account = new PersonBankAccount
        {
            PersonId = person.Uid,
            BankId = bank.Uid,
            AccountType = r.AccountType,
            AccountNumberEnc = cipher.Encrypt(digits),
            AccountLast4 = digits[^4..],
            HolderName = Holder(r.HolderName.Clean() ?? person.FullName),
            Alias = r.Alias.Clean(),
            ValidFrom = r.ValidFrom ?? today,
        };
        var hasPrimary = await db.Set<PersonBankAccount>().AnyAsync(a => a.PersonId == person.Uid && a.IsPrimary, ct);
        if (r.MakePrimary || !hasPrimary)
            await SetPrimaryAsync(db, account, ct);
        db.Add(account);
        return (account, null);
    }

    /// <summary>Makes <paramref name="account"/> the one primary of its person (the pay goes there from the next unpaid period).</summary>
    public static async Task SetPrimaryAsync(DbContext db, PersonBankAccount account, CancellationToken ct)
    {
        var others = await db.Set<PersonBankAccount>().Where(a => a.PersonId == account.PersonId && a.IsPrimary && a.Uid != account.Uid)
            .ToListAsync(ct);
        foreach (var other in others)
            other.IsPrimary = false;
        // The partial unique index checks each statement: clear the old primary first.
        if (others.Count > 0)
            await db.SaveChangesAsync(ct);
        account.IsPrimary = true;
    }

    public static void Update(PersonBankAccount account, UpdateBankAccountRequest r)
    {
        account.HolderName = Holder(r.HolderName);
        account.Alias = r.Alias.Clean();
        account.ValidTo = r.ValidTo;
        if (r.ValidTo is not null)
            account.IsPrimary = false;
    }

    public static async Task<List<BankAccountDto>> ListAsync(DbContext db, Guid personId, DateOnly today, CancellationToken ct)
    {
        var person = await db.Set<Person>().Where(p => p.Uid == personId).Select(p => p.FullName).SingleAsync(ct);
        var rows = await db.Set<PersonBankAccount>().AsNoTracking().Where(a => a.PersonId == personId)
            .Join(db.Set<Bank>(), a => a.BankId, b => b.Uid, (a, b) => new { a, b.Name, b.SpeiCode })
            .ToListAsync(ct);
        return rows.Select(x => Dto(x.a, x.Name, x.SpeiCode, person, today))
            .OrderByDescending(a => a.IsPrimary).ThenByDescending(a => a.IsCurrent).ThenByDescending(a => a.ValidFrom).ToList();
    }

    public static BankAccountDto Dto(PersonBankAccount a, string bankName, string bankCode, string personName, DateOnly today) =>
        new(a.Uid, a.BankId, bankName, bankCode, a.AccountType, a.AccountLast4, a.HolderName, a.Alias, a.IsPrimary,
            !SameName(a.HolderName, personName), a.ValidFrom, a.ValidTo, a.IsCurrent(today), a.IsFrequentAccountRegistered);

    /// <summary>Uppercase, single spaces, at most 40 characters.</summary>
    public static string Holder(string name)
    {
        var clean = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        return clean.Length <= HolderMax ? clean : clean[..HolderMax];
    }

    /// <summary>Same words in any order, ignoring accents and case («PÉREZ LÓPEZ ANA» = «Ana Perez Lopez»).</summary>
    public static bool SameName(string a, string b)
    {
        static string[] Words(string s) => Unaccent(s).ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();
        return Words(a).SequenceEqual(Words(b));
    }

    private static string Unaccent(string s) => new(s.Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
}
