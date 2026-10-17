using System.Security.Cryptography;
using System.Text;

namespace Platform.Core.Security;

/// <summary>
/// Time-based one-time codes (RFC 6238: HMAC-SHA1, 30-second steps, 6 digits) — what every authenticator app produces from the
/// secret it scanned (PLAN D89). A code is accepted for the step before and after (clock drift) and only once.
/// </summary>
public static class Totp
{
    public const int StepSeconds = 30;
    public const int Digits = 6;

    /// <summary>A new 160-bit secret, the size RFC 4226 recommends.</summary>
    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20);

    public static long Step(DateTimeOffset at) => at.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>The code of <paramref name="step"/> with <paramref name="digits"/> digits (RFC 4226 §5.3 dynamic truncation).</summary>
    public static string Code(byte[] secret, long step, int digits = Digits)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xff);
            step >>= 8;
        }
        Span<byte> hash = stackalloc byte[20];
        // RFC 6238's default and what authenticator apps compute; HMAC-SHA1 is not weakened by SHA-1 collisions.
#pragma warning disable CA5350
        HMACSHA1.HashData(secret, counter, hash);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        var modulo = (int)Math.Pow(10, digits);
        return (binary % modulo).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    /// <summary>
    /// The step <paramref name="code"/> belongs to, if it is one of now's ±1 steps and later than <paramref name="lastUsedStep"/>
    /// (a code is good once); otherwise null. Compared in constant time.
    /// </summary>
    public static long? Verify(byte[] secret, string code, DateTimeOffset now, long? lastUsedStep)
    {
        var digits = new string(code.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length != Digits)
            return null;
        var current = Step(now);
        for (var step = current - 1; step <= current + 1; step++)
        {
            if (lastUsedStep is { } last && step <= last)
                continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code(secret, step)), Encoding.ASCII.GetBytes(digits)))
                return step;
        }
        return null;
    }

    /// <summary>«otpauth://totp/…»: what the QR code carries for the authenticator app.</summary>
    public static string Uri(string issuer, string account, byte[] secret) =>
        $"otpauth://totp/{System.Uri.EscapeDataString(issuer)}:{System.Uri.EscapeDataString(account)}?secret={Base32.Encode(secret)}"
        + $"&issuer={System.Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
}

/// <summary>RFC 4648 base32 without padding, as authenticator apps take secrets.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }
        if (bits > 0)
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] Decode(string text)
    {
        var clean = text.Trim().TrimEnd('=').Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        var output = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            var value = Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (value < 0)
                throw new FormatException("Not base32.");
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }
        return [.. output];
    }
}
