namespace Platform.Bank.Layouts;

/// <summary>One beneficiary account to register at Banco Azteca before it can be paid.</summary>
public sealed record FrequentAccountRow(
    string ClientTag,
    string HolderName,
    AccountType AccountType,
    string AccountNumber,
    string BankCode);

/// <summary>
/// Banco Azteca «cuentas frecuentes» (SPEC §5.5 step 3), as in <c>examples/cuentasfrec21sept.xlsx</c>: no header row;
/// A client tag, B name, C tipo, D account, E <c>MXP</c>, F bank code, G–I empty, J tipo again. The client registers debit
/// cards, CLABEs and Azteca own accounts (SPEC Q13); the sample's debit cards carry <c>03</c> in C and J, so each type
/// writes its pagos masivos code (column H there) in two digits: <c>01</c> own account, <c>03</c> debit card, <c>40</c> CLABE.
/// </summary>
public static class AztecaFrequentAccounts
{
    public static readonly string Version = "azteca-cuentas-frecuentes/2026-09-21";

    public static BankLayout<FrequentAccountRow> Layout(IReadOnlySet<string> knownBankCodes)
    {
        ArgumentNullException.ThrowIfNull(knownBankCodes);
        FieldRule knownBank = v => v.Length == 0 || knownBankCodes.Contains(v) ? null : $"Clave de banco «{v}» desconocida.";

        return new BankLayout<FrequentAccountRow>
        {
            Name = "Banco Azteca — cuentas frecuentes",
            Version = Version,
            SheetName = "Hoja1",
            HeaderRow = null,
            FirstDataRow = 1,
            Columns =
            [
                new("A", "Etiqueta", r => r.ClientTag.Trim().ToLowerInvariant(), [Rules.Required, Rules.MaxLength(20)]),
                new("B", "Nombre", r => r.HolderName.Trim(), [Rules.Required, Rules.MaxLength(40)]),
                new("C", "Tipo", r => TypeCode(r.AccountType), []),
                new("D", "Cuenta", r => BankText.Digits(r.AccountNumber), [Rules.Required]),
                new("E", "Moneda", _ => "MXP", []),
                new("F", "Banco", r => r.BankCode.Trim(), [Rules.Required, Rules.ExactDigits(3), knownBank]),
                new("J", "Tipo (J)", r => TypeCode(r.AccountType), []),
            ],
            BatchRules = (rows, _) => rows.Select((r, i) =>
                    (i, Issue: AztecaMassPayment.AccountIssue(r.AccountType, BankText.Digits(r.AccountNumber), r.BankCode.Trim())))
                .Where(x => x.Issue is not null)
                .Select(x => new LayoutIssue(x.i + 1, "Cuenta", x.Issue!)),
        };
    }

    /// <summary>The account type in two digits, as in the sample file: <c>01</c>, <c>03</c> or <c>40</c>.</summary>
    public static string TypeCode(AccountType type) => ((int)type).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
}
