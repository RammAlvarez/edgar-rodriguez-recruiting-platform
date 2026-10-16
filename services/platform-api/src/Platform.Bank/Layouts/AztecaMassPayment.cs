using System.Globalization;

namespace Platform.Bank.Layouts;

/// <summary>Receiving account types and the codes the Azteca layout uses (DATA_MODEL `bank_account_type`).</summary>
public enum AccountType
{
    OwnAccount = 1,
    DebitCard = 3,
    Clabe = 40,
}

/// <summary>The paying account printed on every row (DATA_MODEL §6.1 `paying_accounts`).</summary>
public sealed record AztecaPayingAccount(
    string ChargeAccountNumber,
    string HolderName,
    string HolderRfc,
    string ChargeLegend = "PAGO",
    string CreditLegend = "PAGO",
    string Currency = "MXP");

/// <summary>
/// One payable line: the primary account of the employee and the truncated payable amount. Legends default to the
/// paying account's (`payment_batch_rows.charge_legend / credit_legend`).
/// </summary>
public sealed record AztecaPaymentRow(
    string ReceivingBankCode,
    AccountType AccountType,
    string AccountNumber,
    string BeneficiaryName,
    decimal Amount,
    string? ChargeLegend = null,
    string? CreditLegend = null);

/// <summary>
/// Banco Azteca «pagos masivos» (SPEC §5.5 step 2), as in <c>examples/Lo HFC MAESTRO 14AGOSTO.xlsx</c>: headers in row 1,
/// one row per payment, every cell text. Column E (importe) has no header in the client's master; it is kept blank.
/// </summary>
public static class AztecaMassPayment
{
    public const string AztecaBankCode = "127";

    public static readonly string Version = "azteca-pagos-masivos/2026-08-14";

    /// <summary>`3` traspaso entre cuentas BAZ when the receiving bank is Azteca, `1` SPEI otherwise — derived, never typed.</summary>
    public static string PaymentType(string receivingBankCode) => receivingBankCode == AztecaBankCode ? "3" : "1";

    public static int[] AccountLengths(AccountType type) => type switch
    {
        AccountType.DebitCard => [16],
        AccountType.Clabe => [18],
        _ => [14, 20],
    };

    public static BankLayout<AztecaPaymentRow> Layout(AztecaPayingAccount payer, IReadOnlySet<string> knownBankCodes)
    {
        ArgumentNullException.ThrowIfNull(payer);
        ArgumentNullException.ThrowIfNull(knownBankCodes);

        LayoutColumn<AztecaPaymentRow> Fixed(string letter, string header, string value, params FieldRule[] rules) =>
            new(letter, header, _ => value, rules);

        FieldRule knownBank = v => v.Length == 0 || knownBankCodes.Contains(v) ? null : $"Clave de banco «{v}» desconocida.";

        return new BankLayout<AztecaPaymentRow>
        {
            Name = "Banco Azteca — pagos masivos",
            Version = Version,
            SheetName = "Hoja1",
            HeaderRow = 1,
            FirstDataRow = 2,
            Columns =
            [
                Fixed("A", "NO. DE CUENTA CARGO", BankText.Digits(payer.ChargeAccountNumber), Rules.Required,
                    Rules.ExactDigits(14, 16, 18, 20)),
                Fixed("B", "NOMBRE DEL TITULAR DE LA CUENTA CARGO (LONGITUD 40 CARACTERES MAX)", payer.HolderName.Trim(),
                    Rules.Required, Rules.MaxLength(40)),
                Fixed("C", "RFC DEL TITULAR", BankText.Normalize(payer.HolderRfc), Rules.Required),
                new("D", "LEYENDA DEL CARGO (ALFANUMERICO - 30 CARACTERES MAX)",
                    r => (r.ChargeLegend ?? payer.ChargeLegend).Trim(), [Rules.Required, Rules.MaxLength(30)],
                    Label: "LEYENDA DEL CARGO"),
                new("E", "", r => r.Amount.ToString(CultureInfo.InvariantCulture), [Rules.PositiveAmount, Cents],
                    CellKind.Number, "0.00", Label: "IMPORTE"),
                Fixed("F", "MONEDA", payer.Currency, Rules.Required),
                new("G", "CLAVE DEL BANCO RECEPTOR (CONSULTAR LISTADO ADJUNTO AL CORREO)", r => r.ReceivingBankCode.Trim(),
                    [Rules.Required, Rules.ExactDigits(3), knownBank], Label: "CLAVE DEL BANCO RECEPTOR"),
                new("H", "TIPO DE CUENTA DEL BENEFICIARIO (1= CUENTAS PROPIAS, 3=TARJETA DEBITO, 40=CLABE)",
                    r => ((int)r.AccountType).ToString(CultureInfo.InvariantCulture), [], Label: "TIPO DE CUENTA"),
                new("I", "NUMERO DE CUENTA DEL BENEFICIARIO (14,16,18 O 20 DIGITOS)", r => BankText.Digits(r.AccountNumber),
                    [Rules.Required], Label: "NUMERO DE CUENTA"),
                new("J", "NOMBRE DEL BENEFICIARIO (MAXIMO 40 CARACTERES)", r => r.BeneficiaryName.Trim(),
                    [Rules.Required, Rules.MaxLength(40)], Label: "NOMBRE DEL BENEFICIARIO"),
                new("K", "LEYENDA DEL ABONO (30 CARACTERES MAXIMO)", r => (r.CreditLegend ?? payer.CreditLegend).Trim(),
                    [Rules.Required, Rules.MaxLength(30)], Label: "LEYENDA DEL ABONO"),
                new("L", "RFC DEL BENEFICIARIO (NO OBLIGATORIO)", _ => null, []),
                new("M", "IMPORTE DEL IVA (NO OBLIGATORIO)", _ => null, []),
                new("N", "REFERENCIA DE COBRANZA (MAXIMO 40 CARACTERES) (NO OBLIGATORIO)", _ => null, []),
                new("O", "TIPO DE PAGO (1= TRANSFERENCIA SPEI, 2=TRANSFERENCIA TEF, 3=TRASPASO CUENTAS BAZ)",
                    r => PaymentType(r.ReceivingBankCode.Trim()), [], Label: "TIPO DE PAGO"),
            ],
            BatchRules = (rows, _) => AccountRules(rows, "NUMERO DE CUENTA"),
        };
    }

    /// <summary>Length by type, CLABE control digit and bank prefix, card Luhn digit (a typo is caught before the bank).</summary>
    internal static IEnumerable<LayoutIssue> AccountRules(IReadOnlyList<AztecaPaymentRow> rows, string field)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (AccountIssue(r.AccountType, BankText.Digits(r.AccountNumber), r.ReceivingBankCode.Trim()) is { } message)
            {
                yield return new LayoutIssue(i + 1, field, message);
            }
        }
    }

    public static string? AccountIssue(AccountType type, string digits, string bankCode)
    {
        if (digits.Length == 0)
        {
            return null;
        }
        var lengths = AccountLengths(type);
        if (!lengths.Contains(digits.Length))
        {
            return $"Para este tipo de cuenta el número debe tener {string.Join(" o ", lengths)} dígitos (tiene {digits.Length}).";
        }
        return type switch
        {
            AccountType.Clabe when !BankText.IsClabe(digits) => "CLABE inválida: el dígito verificador no coincide.",
            AccountType.Clabe when !digits.StartsWith(bankCode, StringComparison.Ordinal) =>
                $"La CLABE es de otro banco (empieza con {digits[..3]}, el banco receptor es {bankCode}).",
            AccountType.DebitCard when !BankText.IsLuhn(digits) => "Número de tarjeta inválido: el dígito verificador no coincide.",
            _ => null,
        };
    }

    private static readonly FieldRule Cents = v =>
        decimal.TryParse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d)
        && Math.Round(d, 2, MidpointRounding.ToZero) != d
            ? "El importe debe estar en centavos (el total a pagar ya truncado)."
            : null;
}
