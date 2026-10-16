using FluentValidation;

namespace Platform.Core.Http;

/// <summary>Field rules shared by the request validators; messages follow the design system's error style.</summary>
public static class FieldRules
{
    public static IRuleBuilderOptions<T, string?> OptionalEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(120).WithMessage("El correo admite hasta 120 caracteres.")
            .EmailAddress().WithMessage("Introduce un correo válido.");

    /// <summary>Celular: 10 dígitos, without spaces or country code (DATA_MODEL §3.6).</summary>
    public static IRuleBuilderOptions<T, string?> OptionalMobile<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^\d{10}$").WithMessage("El celular debe tener 10 dígitos.");

    public static IRuleBuilderOptions<T, string?> OptionalPostalCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^\d{5}$").WithMessage("El código postal tiene 5 dígitos.");

    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> rule, int max) =>
        rule.MaximumLength(max).WithMessage($"Admite hasta {max} caracteres.");

    /// <summary>Empty or whitespace becomes null; anything else is trimmed.</summary>
    public static string? Clean(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
