using System.Globalization;

namespace Platform.Bank.Layouts;

/// <summary>One employee in a Banorte «Alta Masiva NH» batch (from the card-request queue).</summary>
public sealed record BanorteRegistrationRow(
    string EmployeeNumber,
    string Curp,
    string Email,
    string Mobile,
    string Street,
    string ExtNumber,
    string Neighborhood,
    string PostalCode,
    string? CardNumber,
    string Rfc);

/// <summary>Values fixed for the whole batch (DATA_MODEL §6.4 `registration_batches`).</summary>
public sealed record BanorteBatchSettings(
    string OccupationCode = "09-TECNICOS",
    string Product = "705-Nómina Banorte 2",
    string CardType = BanorteMassRegistration.GenericCard,
    decimal GrossMonthlySalary = 100_000m,
    string? BranchToSend = null);

/// <summary>
/// Banorte Portal BEM «Alta Masiva NH» (SPEC §5.7), as in <c>examples/Alta_Nomina_Masiva Banorte 21Ago-1.xlsx</c>:
/// sheet <c>Masiva_xls</c>, group titles in row 1, headers B2–P2, records from row 3 with no blank rows, and the rules
/// of the bank's «Descripción de campos» sheet.
/// </summary>
public static class BanorteMassRegistration
{
    public const string GenericCard = "GENÉRICA";
    public const string NamedCard = "NOMINATIVA";
    public const int MaxRecords = 5_000;

    public static readonly string Version = "banorte-alta-masiva-nh/2026-08-21";

    public static readonly IReadOnlyList<string> Products = ["705-Nómina Banorte 2", "367-Nómina sin chequera"];

    public static readonly IReadOnlyList<string> CardTypes = [GenericCard, NamedCard];

    /// <summary>The bank's catalog, padded exactly as its «Catálogos» sheet (the combo writes these strings).</summary>
    public static readonly IReadOnlyList<string> OccupationCodes =
    [
        "01-COMERCIO            ", "02-CONDUCTORES         ", "03-TRABAJADORES ADMINISTRATIVOS",
        "04-TRABAJADORES DE LA EDUCACION", "05-TRABAJADORES DEL ARTE Y ESPECTACULOS",
        "06-TRABAJADORES DE ACT. AGRICOLAS Y GANADERAS", "07-TRABAJADORES EN SERVICIOS DE PROTECCION",
        "08-TRABAJADORES FABRILES ", "09-TECNICOS            ", "10-PROFESIONISTA ASALARIADO",
        "11-PROFESIONISTA INDEPENDIENTE", "12-FUNCIONARIO DE INST. O EMPRESAS PUBLICAS",
        "13-FUNCIONARIO DE INST. O EMPRESAS PRIVADAS", "14-ESTUDIANTE           ", "15-MENOR DE EDAD        ",
        "16-ECLESIASTICO         ", "17-HOGAR                ", "18-JUBILADOS O PENSIONADO", "19-DESEMPLEADO          ",
        "20-PRESTADOR DE SERVICIOS", "21-TECNOLOGIA EMPRESARIAL",
    ];

    /// <summary>The catalog entry whose trimmed text equals <paramref name="code"/>, or its first two digits.</summary>
    public static string? Occupation(string code)
    {
        var trimmed = code.Trim();
        return OccupationCodes.FirstOrDefault(c => c.TrimEnd() == trimmed)
            ?? OccupationCodes.FirstOrDefault(c => trimmed.Length == 2 && c.StartsWith(trimmed + "-", StringComparison.Ordinal));
    }

    public static BankLayout<BanorteRegistrationRow> Layout(BanorteBatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var occupation = Occupation(settings.OccupationCode) ?? settings.OccupationCode;
        var generic = settings.CardType == GenericCard;
        var salary = settings.GrossMonthlySalary.ToString(CultureInfo.InvariantCulture);

        return new BankLayout<BanorteRegistrationRow>
        {
            Name = "Banorte — Alta Masiva NH",
            Version = Version,
            SheetName = "Masiva_xls",
            HeaderRow = 2,
            FirstDataRow = 3,
            MaxRows = MaxRecords,
            Bands = [new("B", "K", "Datos del Empleado"), new("L", "O", "Datos de la Tarjeta"), new("P", "P", "Datos Fiscales")],
            ExtraSheets =
            [
                new FixedSheet("Catálogos",
                    ["PRODUCTO", .. Products, "", "TIPO DE TARJETA", .. CardTypes, "", "CODIGO NACIONAL DE OCUPACIÓN", .. OccupationCodes]),
            ],
            Columns =
            [
                new("A", "#", _ => null, []),
                new("B", "Número de empleado", r => BankText.Digits(r.EmployeeNumber), [Rules.Required, Rules.MaxDigits(10)]),
                new("C", "Sueldo bruto mensual", _ => salary, [Rules.PositiveAmount, Rules.MaxLength(20)], CellKind.Number,
                    "\"$\"#,##0.00"),
                new("D", "CURP", r => BankText.Normalize(r.Curp), [Rules.Required, Rules.Curp]),
                new("E", "Correo electrónico", r => BankText.Normalize(r.Email), [Rules.Required, Rules.MaxLength(50), Rules.Email]),
                new("F", "Celular", r => BankText.Digits(r.Mobile), [Rules.Required, Rules.Mobile]),
                new("G", "Código nacional de ocupación", _ => occupation, [Rules.Required, Rules.OneOf([.. OccupationCodes])]),
                new("H", "Calle", r => BankText.Normalize(r.Street), [Rules.Required, Rules.MaxLength(24), Rules.PlainText]),
                new("I", "Número", r => BankText.Normalize(r.ExtNumber), [Rules.Required, Rules.MaxLength(7), Rules.PlainText]),
                new("J", "Colonia", r => BankText.Normalize(r.Neighborhood), [Rules.Required, Rules.MaxLength(40), Rules.PlainText]),
                new("K", "Código postal", r => BankText.Digits(r.PostalCode), [Rules.Required, Rules.ExactDigits(5)]),
                new("L", "Producto", _ => settings.Product, [Rules.Required, Rules.OneOf([.. Products])]),
                new("M", "Tipo de tarjeta", _ => settings.CardType, [Rules.Required, Rules.OneOf([.. CardTypes])]),
                new("N", "Número de tarjeta", r => generic ? BankText.Digits(r.CardNumber) : null,
                    generic ? [Rules.Required, Rules.CardNumber] : []),
                new("O", "Sucursal a enviar", _ => generic ? null : BankText.Digits(settings.BranchToSend),
                    generic ? [] : [Rules.Required, Rules.ExactDigits(5)]),
                new("P", "RFC", r => BankText.Normalize(r.Rfc), [Rules.Required, Rules.Rfc13]),
            ],
            BatchRules = BatchRules,
        };
    }

    private static IEnumerable<LayoutIssue> BatchRules(IReadOnlyList<BanorteRegistrationRow> rows, DateOnly today)
    {
        var addresses = new Dictionary<string, int>();
        var keys = new HashSet<string>();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var line = i + 1;
            var curp = BankText.Normalize(r.Curp);
            if (BankText.IsCurp(curp))
            {
                if (BankText.CurpIsForeignBorn(curp))
                {
                    yield return new LayoutIssue(line, "CURP", "El banco no acepta personas nacidas en el extranjero (CURP «NE»).");
                }
                var age = BankText.AgeOn(BankText.CurpBirthDate(curp)!.Value, today);
                if (age is < 18 or > 99)
                {
                    yield return new LayoutIssue(line, "CURP", $"Debe ser mayor de edad y menor de 100 años (tiene {age}).");
                }
            }

            // Municipio and estado are not columns of the layout; the address is calle + número + CP + colonia.
            var address = string.Join('|', BankText.Normalize(r.Street), BankText.Normalize(r.ExtNumber),
                BankText.Digits(r.PostalCode), BankText.Normalize(r.Neighborhood));
            addresses[address] = addresses.GetValueOrDefault(address) + 1;
            if (addresses[address] > 2)
            {
                yield return new LayoutIssue(line, "Calle", "Ya hay 2 empleados con este domicilio en el archivo; el banco rechaza el tercero.");
            }

            if (!keys.Add(BankText.Digits(r.EmployeeNumber) + "|" + curp))
            {
                yield return new LayoutIssue(line, "Número de empleado", "Número de empleado y CURP repetidos en el archivo.");
            }
        }
    }
}
