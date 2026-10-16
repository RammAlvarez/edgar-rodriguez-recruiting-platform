namespace Platform.Bank.Layouts;

public enum CellKind
{
    /// <summary>Written as a text cell (format <c>@</c>) so account numbers and postal codes keep their leading zeros.</summary>
    Text,
    /// <summary>Written as a number cell; the value is an invariant decimal string.</summary>
    Number,
}

/// <summary>Checks one written value; returns the Spanish message, or null when the value is valid.</summary>
public delegate string? FieldRule(string value);

/// <summary>
/// One column of a bank file: where it goes, its header, how a row fills it and the bank's rules for it.
/// <paramref name="Label"/> names the column in messages when the bank's header is blank or long.
/// </summary>
public sealed record LayoutColumn<TRow>(
    string Letter,
    string Header,
    Func<TRow, string?> Value,
    IReadOnlyList<FieldRule> Rules,
    CellKind Kind = CellKind.Text,
    string? NumberFormat = null,
    string? Label = null)
{
    public string Name => Label ?? Header;
}

/// <summary>A merged title above a group of columns (Banorte's «Datos del Empleado» band).</summary>
public sealed record HeaderBand(string FirstLetter, string LastLetter, string Title);

/// <summary>An extra sheet copied as-is into the file (the bank's own catalog lists).</summary>
public sealed record FixedSheet(string Name, IReadOnlyList<string> Lines);

/// <summary>A rule broken by the batch. <see cref="Line"/> is the 1-based record number (0 for the whole file).</summary>
public sealed record LayoutIssue(int Line, string Field, string Message);

/// <summary>
/// A bank file layout as a definition (ARCHITECTURE: a portal change is an edit here plus a test). Values are produced
/// already normalized, and the rules check exactly what will be written.
/// </summary>
public sealed class BankLayout<TRow>
{
    public required string Name { get; init; }

    /// <summary>Bumped on every change to the definition; stored with each generated batch.</summary>
    public required string Version { get; init; }

    public required string SheetName { get; init; }

    /// <summary>Row of the column headers; null when the bank's file has none (cuentas frecuentes).</summary>
    public int? HeaderRow { get; init; }

    public required int FirstDataRow { get; init; }

    public required IReadOnlyList<LayoutColumn<TRow>> Columns { get; init; }

    public IReadOnlyList<HeaderBand> Bands { get; init; } = [];

    public IReadOnlyList<FixedSheet> ExtraSheets { get; init; } = [];

    /// <summary>Rules that need the whole record or the whole batch (age from the CURP, repeated addresses …).</summary>
    public Func<IReadOnlyList<TRow>, DateOnly, IEnumerable<LayoutIssue>> BatchRules { get; init; } = (_, _) => [];

    public int? MaxRows { get; init; }

    public IReadOnlyList<LayoutIssue> Validate(IReadOnlyList<TRow> rows, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var issues = new List<LayoutIssue>();
        if (rows.Count == 0)
        {
            issues.Add(new LayoutIssue(0, "", "El archivo no tiene registros."));
        }
        if (MaxRows is { } max && rows.Count > max)
        {
            issues.Add(new LayoutIssue(0, "", $"El banco acepta hasta {max:N0} registros por archivo; divide el lote."));
        }
        for (var i = 0; i < rows.Count; i++)
        {
            foreach (var column in Columns)
            {
                var value = column.Value(rows[i]) ?? "";
                foreach (var rule in column.Rules)
                {
                    if (rule(value) is { } message)
                    {
                        issues.Add(new LayoutIssue(i + 1, column.Name, message));
                        break;
                    }
                }
            }
        }
        issues.AddRange(BatchRules(rows, today));
        return issues.OrderBy(i => i.Line).ToList();
    }
}
