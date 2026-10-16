using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Platform.Bank.Layouts;

/// <summary>Normalization and identifier checks shared by the bank layouts.</summary>
public static partial class BankText
{
    /// <summary>
    /// Banorte's capture rules: uppercase, accents removed, Ñ → N, surrounding and repeated spaces removed.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }
        var decomposed = Spaces().Replace(value.Trim(), " ").ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Only digits, spaces and separators removed (account and phone numbers typed with spaces).</summary>
    public static string Digits(string? value) => value is null ? "" : NonDigits().Replace(value, "");

    public static bool IsCurp(string value) => CurpPattern().IsMatch(value) && CurpBirthDate(value) is not null;

    /// <summary>Birth date in positions 5–10 (YYMMDD); position 17 is a digit before 2000 and a letter from 2000.</summary>
    public static DateOnly? CurpBirthDate(string curp)
    {
        if (curp.Length != 18)
        {
            return null;
        }
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

    public static int AgeOn(DateOnly birth, DateOnly today) =>
        today.Year - birth.Year - (today < birth.AddYears(today.Year - birth.Year) ? 1 : 0);

    public static bool IsRfc13(string value) => RfcPattern().IsMatch(value);

    /// <summary>10 digits, not one repeated digit and not a run going up or down (1111111111, 0123456789).</summary>
    public static bool IsMobile(string value)
    {
        if (!TenDigits().IsMatch(value) || value.Distinct().Count() == 1)
        {
            return false;
        }
        bool Run(int step)
        {
            for (var i = 1; i < value.Length; i++)
            {
                if ((value[i] - value[i - 1] + 10) % 10 != (step + 10) % 10)
                {
                    return false;
                }
            }
            return true;
        }
        return !Run(1) && !Run(-1);
    }

    public static bool IsEmail(string value) => EmailPattern().IsMatch(value);

    /// <summary>CLABE: 18 digits, weights 3-7-1 over the first 17, control digit = (10 − Σ mod 10) mod 10.</summary>
    public static bool IsClabe(string value)
    {
        if (value.Length != 18 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }
        int[] weights = [3, 7, 1];
        var sum = 0;
        for (var i = 0; i < 17; i++)
        {
            sum += (value[i] - '0') * weights[i % 3] % 10;
        }
        return (10 - sum % 10) % 10 == value[17] - '0';
    }

    /// <summary>Luhn check of a card number.</summary>
    public static bool IsLuhn(string value)
    {
        if (value.Length == 0 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }
        var sum = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var d = value[value.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d = d * 2 > 9 ? d * 2 - 9 : d * 2;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"^[A-Z]{4}\d{6}[A-Z]{6}[A-Z0-9]{2}$")]
    private static partial Regex CurpPattern();

    [GeneratedRegex(@"^[A-Z&]{4}\d{6}[A-Z0-9]{3}$")]
    private static partial Regex RfcPattern();

    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex TenDigits();

    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$")]
    private static partial Regex EmailPattern();
}
