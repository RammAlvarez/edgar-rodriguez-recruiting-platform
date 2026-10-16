using System.Globalization;
using System.Text.RegularExpressions;

namespace Platform.Core.People;

/// <summary>
/// The one definition of the Mexican identifiers (DATA_MODEL §3.6): the people validators, the database CHECKs and the
/// bank layouts (<c>BankText</c>) all use it. Values are expected trimmed and uppercase.
/// </summary>
public static partial class MexicanIds
{
    /// <summary>The DATA_MODEL §3.6 CHECK pattern; <c>ck_people_curp</c> uses the same text.</summary>
    public const string CurpPatternText = @"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$";

    /// <summary>RFC of a person (13) or a company (12, clients).</summary>
    public const string PersonRfcPatternText = @"^[A-ZÑ&]{4}\d{6}[A-Z0-9]{3}$";

    public const string CompanyRfcPatternText = @"^[A-ZÑ&]{3}\d{6}[A-Z0-9]{3}$";

    /// <summary>Structure plus a real birth date (no 31 de febrero).</summary>
    public static bool IsCurp(string? value) => value is not null && CurpPattern().IsMatch(value) && CurpBirthDate(value) is not null;

    /// <summary>Birth date in positions 5–10 (YYMMDD); position 17 is a digit before 2000 and a letter from 2000.</summary>
    public static DateOnly? CurpBirthDate(string curp)
    {
        if (curp.Length != 18)
            return null;
        var century = char.IsDigit(curp[16]) ? 1900 : 2000;
        return int.TryParse(curp.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var yy)
            && int.TryParse(curp.AsSpan(6, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var mm)
            && int.TryParse(curp.AsSpan(8, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var dd)
            && mm is >= 1 and <= 12 && dd >= 1 && dd <= DateTime.DaysInMonth(century + yy, mm)
                ? new DateOnly(century + yy, mm, dd)
                : null;
    }

    /// <summary>Positions 12–13 are the birth state; <c>NE</c> means born abroad.</summary>
    public static bool CurpIsForeignBorn(string curp) => curp.Length == 18 && curp.AsSpan(11, 2) is "NE";

    public static bool IsPersonRfc(string? value) => value is not null && PersonRfcPattern().IsMatch(value);

    public static bool IsCompanyRfc(string? value) => value is not null && CompanyRfcPattern().IsMatch(value);

    [GeneratedRegex(CurpPatternText)]
    private static partial Regex CurpPattern();

    [GeneratedRegex(PersonRfcPatternText)]
    private static partial Regex PersonRfcPattern();

    [GeneratedRegex(CompanyRfcPatternText)]
    private static partial Regex CompanyRfcPattern();
}
