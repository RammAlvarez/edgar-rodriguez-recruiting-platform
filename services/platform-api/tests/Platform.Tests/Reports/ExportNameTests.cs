using Platform.Infrastructure.Storage;
using Platform.Payroll.Prepayroll;

namespace Platform.Tests.Reports;

/// <summary>E2E run: the titles and file names the exports show.</summary>
public sealed class ExportNameTests
{
    private const string Partial = "MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS";

    [Theory]
    [InlineData(12, "MONTO DE HORAS INCOMPLETAS SOBRE 12 HORAS")]
    [InlineData(8, "MONTO DE HORAS INCOMPLETAS SOBRE 8 HORAS")]
    [InlineData(8.5, "MONTO DE HORAS INCOMPLETAS SOBRE 8.5 HORAS")]
    public void The_partial_hours_column_names_the_groups_turno(double hours, string title) =>
        Assert.Equal(title, PrepayrollLabels.ColumnTitle(Partial, (decimal)hours));

    [Fact]
    public void The_other_columns_keep_the_clients_names() =>
        Assert.Equal("SUELDO", PrepayrollLabels.ColumnTitle("SUELDO", 8));

    [Theory]
    [InlineData("PRENOMINA SEM 41 E2E LIMPIEZA OPERACIÓN.xlsx", "PRENOMINA SEM 41 E2E LIMPIEZA OPERACION.xlsx")]
    [InlineData("Ñandú ✓ \"x\".pdf", "Nandu _ _x_.pdf")]
    public void The_plain_file_name_drops_accents_instead_of_replacing_them(string name, string plain) =>
        Assert.Equal(plain, S3ObjectStorage.AsciiFileName(name));
}
