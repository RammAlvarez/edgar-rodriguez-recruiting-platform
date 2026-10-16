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
/// A client tag, B name, C tipo <c>03</c>, D account, E <c>MXP</c>, F bank code, G–I empty, J <c>03</c>. Both fixed
/// <c>03</c> values are copied from the client's file (SPEC Q13 asks what they must be for a CLABE).
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
                new("C", "Tipo", _ => "03", []),
                new("D", "Cuenta", r => BankText.Digits(r.AccountNumber), [Rules.Required]),
                new("E", "Moneda", _ => "MXP", []),
                new("F", "Banco", r => r.BankCode.Trim(), [Rules.Required, Rules.ExactDigits(3), knownBank]),
                new("J", "Tipo (J)", _ => "03", []),
            ],
            BatchRules = (rows, _) => rows.Select((r, i) => (i, Issue: AztecaMassPayment.AccountIssue(
                    r.AccountType, BankText.Digits(r.AccountNumber), r.BankCode.Trim())))
                .Where(x => x.Issue is not null)
                .Select(x => new LayoutIssue(x.i + 1, "Cuenta", x.Issue!)),
        };
    }
}
