using System.Globalization;

namespace Platform.Bank.Layouts;

public enum Gender
{
    Male,
    Female,
}

/// <summary>One employee in a Banregio card registration batch.</summary>
public sealed record BanregioRegistrationRow(
    string EmployeeNumber,
    string FirstNames,
    string LastName1,
    string LastName2,
    string Rfc,
    string Curp,
    string Street,
    string ExtNumber,
    string Neighborhood,
    string PostalCode,
    string Mobile,
    DateOnly? BirthDate,
    Gender? Gender,
    string Position,
    string Email,
    string? CardNumber,
    string BirthState);

/// <summary>
/// Banregio alta (SPEC §5.7), as in <c>examples/ALTA BANREGIO EARL 19AGO26.xlsx</c>: headers A1–Q1, records from row 2,
/// every cell text, FechaNac <c>dd/mm/yyyy</c>. The bank's own rules are not documented in the examples; the checks are
/// the identifiers' structure, lengths and the birth state list. Tarjeta may be empty: the number is typed when the card
/// arrives (PLAN D85), and checked when present.
/// </summary>
public static class BanregioRegistration
{
    public static readonly string Version = "banregio-alta/2026-08-19";

    /// <summary>States as the bank's list writes them (uppercase, no accents).</summary>
    public static readonly IReadOnlyList<string> BirthStates =
    [
        "AGUASCALIENTES", "BAJA CALIFORNIA", "BAJA CALIFORNIA SUR", "CAMPECHE", "CHIAPAS", "CHIHUAHUA", "CIUDAD DE MEXICO",
        "COAHUILA", "COLIMA", "DURANGO", "ESTADO DE MEXICO", "GUANAJUATO", "GUERRERO", "HIDALGO", "JALISCO", "MICHOACAN",
        "MORELOS", "NAYARIT", "NUEVO LEON", "OAXACA", "PUEBLA", "QUERETARO", "QUINTANA ROO", "SAN LUIS POTOSI", "SINALOA",
        "SONORA", "TABASCO", "TAMAULIPAS", "TLAXCALA", "VERACRUZ", "YUCATAN", "ZACATECAS",
    ];

    public static BankLayout<BanregioRegistrationRow> Layout() => new()
    {
        Name = "Banregio — alta de tarjetas",
        Version = Version,
        SheetName = "Hoja1",
        HeaderRow = 1,
        FirstDataRow = 2,
        Columns =
        [
            new("A", "Nómina", r => BankText.Digits(r.EmployeeNumber), [Rules.Required, Rules.MaxDigits(10)]),
            new("B", "Nombre", r => BankText.Normalize(r.FirstNames), [Rules.Required, Rules.PlainText]),
            new("C", "ApePaterno", r => BankText.Normalize(r.LastName1), [Rules.Required, Rules.PlainText]),
            new("D", "ApeMaterno", r => BankText.Normalize(r.LastName2), [Rules.PlainText]),
            new("E", "RFC", r => BankText.Normalize(r.Rfc), [Rules.Required, Rules.Rfc13]),
            new("F", "CURP", r => BankText.Normalize(r.Curp), [Rules.Required, Rules.Curp]),
            new("G", "Calle", r => BankText.Normalize(r.Street), [Rules.Required]),
            new("H", "NumCalle", r => BankText.Normalize(r.ExtNumber), [Rules.Required]),
            new("I", "Colonia", r => BankText.Normalize(r.Neighborhood), [Rules.Required]),
            new("J", "CodPostal", r => BankText.Digits(r.PostalCode), [Rules.Required, Rules.ExactDigits(5)]),
            new("K", "TelefonoCelular", r => BankText.Digits(r.Mobile), [Rules.Required, Rules.Mobile]),
            new("L", "FechaNac", r => r.BirthDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), [Rules.Required]),
            new("M", "Genero", r => r.Gender switch { Gender.Male => "MASCULINO", Gender.Female => "FEMENINO", _ => null }, [Rules.Required]),
            new("N", "Puesto", r => BankText.Normalize(r.Position), [Rules.Required]),
            new("O", "CorreoElectronico", r => r.Email.Trim(), [Rules.Required, Rules.MaxLength(50), Rules.Email]),
            new("P", "Tarjeta", r => BankText.Digits(r.CardNumber), [Rules.CardNumber]),
            new("Q", "Estado de nacimiento", r => BankText.Normalize(r.BirthState), [Rules.Required, Rules.OneOf([.. BirthStates])]),
        ],
        BatchRules = (rows, _) => rows
            .Select((r, i) => (Line: i + 1, Curp: BankText.Normalize(r.Curp), r.BirthDate))
            .Where(x => x.BirthDate is not null && BankText.IsCurp(x.Curp) && BankText.CurpBirthDate(x.Curp) != x.BirthDate)
            .Select(x => new LayoutIssue(x.Line, "FechaNac", "La fecha de nacimiento no coincide con la CURP.")),
    };
}
