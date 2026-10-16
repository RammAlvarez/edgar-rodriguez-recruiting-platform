using System.Globalization;
using System.Text.RegularExpressions;

namespace Platform.Bank.Layouts;

/// <summary>Field rules with the messages shown in the batch preview.</summary>
public static partial class Rules
{
    public static readonly FieldRule Required = v => v.Length == 0 ? "Es obligatorio." : null;

    public static FieldRule MaxLength(int max) =>
        v => v.Length > max ? $"Máximo {max} caracteres (tiene {v.Length})." : null;

    public static FieldRule ExactDigits(params int[] lengths) =>
        v => v.Length == 0 || (v.All(char.IsAsciiDigit) && lengths.Contains(v.Length))
            ? null
            : $"Debe tener {string.Join(" o ", lengths)} dígitos.";

    public static FieldRule MaxDigits(int max) =>
        v => v.Length == 0 || (v.All(char.IsAsciiDigit) && v.Length <= max) ? null : $"Solo dígitos, máximo {max}.";

    /// <summary>Letters A–Z, digits and spaces: what is left after <see cref="BankText.Normalize"/> must be plain.</summary>
    public static readonly FieldRule PlainText =
        v => PlainPattern().IsMatch(v) ? null : "Sin caracteres especiales (solo letras, números y espacios).";

    public static readonly FieldRule Curp =
        v => v.Length == 0 || BankText.IsCurp(v) ? null : "CURP con estructura inválida.";

    public static readonly FieldRule Rfc13 =
        v => v.Length == 0 || BankText.IsRfc13(v) ? null : "RFC inválido (13 posiciones).";

    public static readonly FieldRule Email =
        v => v.Length == 0 || BankText.IsEmail(v) ? null : "Correo inválido (debe tener @ y extensión).";

    public static readonly FieldRule Mobile =
        v => v.Length == 0 || BankText.IsMobile(v)
            ? null
            : "Celular de 10 dígitos, sin un mismo dígito repetido ni consecutivos.";

    public static readonly FieldRule CardNumber =
        v => v.Length == 0 || (v.Length == 16 && BankText.IsLuhn(v)) ? null : "Número de tarjeta inválido (16 dígitos).";

    public static FieldRule OneOf(IReadOnlyCollection<string> allowed) =>
        v => v.Length == 0 || allowed.Contains(v) ? null : "Valor fuera del catálogo del banco.";

    public static readonly FieldRule PositiveAmount =
        v => decimal.TryParse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) && d > 0m
            ? null
            : "El importe debe ser mayor a cero.";

    [GeneratedRegex(@"^[A-Z0-9 ]*$")]
    private static partial Regex PlainPattern();
}
