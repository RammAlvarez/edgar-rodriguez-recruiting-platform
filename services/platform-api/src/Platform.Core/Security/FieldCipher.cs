using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Platform.Core.Security;

/// <summary>
/// Env <c>ENCRYPTION_KEY</c> (base64 of 32 random bytes) and <c>ENCRYPTION_KEY_ID</c> (1–255, default 1). The id is written in
/// front of every ciphertext so a new key can be introduced without re-encrypting at once (DATA_MODEL §3.7). During a rotation
/// <c>ENCRYPTION_PREVIOUS_KEYS</c> (<c>id:base64,id:base64</c>) keeps the old keys readable until the <c>reencrypt-fields</c>
/// job has moved every value to the current one (docs/HANDOFF.md §6).
/// </summary>
public sealed record EncryptionSettings(byte[] Key, byte KeyId)
{
    /// <summary>Decrypt-only keys by id; never used to encrypt.</summary>
    public IReadOnlyDictionary<byte, byte[]> PreviousKeys { get; init; } = new Dictionary<byte, byte[]>();

    public static EncryptionSettings Parse(string base64Key, string? keyId, string? previousKeys = null)
    {
        var id = keyId is { Length: > 0 } ? ParseId(keyId, "ENCRYPTION_KEY_ID") : (byte)1;
        var previous = new Dictionary<byte, byte[]>();
        foreach (var entry in (previousKeys ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new InvalidOperationException("ENCRYPTION_PREVIOUS_KEYS must be a list of id:base64 separated by commas.");
            var previousId = ParseId(parts[0], "ENCRYPTION_PREVIOUS_KEYS");
            if (previousId == id)
                throw new InvalidOperationException($"ENCRYPTION_PREVIOUS_KEYS repeats the current key id {id}: a new key needs a new id.");
            if (!previous.TryAdd(previousId, DecodeKey(parts[1], $"ENCRYPTION_PREVIOUS_KEYS ({previousId})")))
                throw new InvalidOperationException($"ENCRYPTION_PREVIOUS_KEYS has key id {previousId} twice.");
        }
        return new EncryptionSettings(DecodeKey(base64Key, "ENCRYPTION_KEY"), id) { PreviousKeys = previous };
    }

    private static byte[] DecodeKey(string base64Key, string name)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException($"{name} must be base64 (32 random bytes).", e);
        }
        if (key.Length != 32)
            throw new InvalidOperationException($"{name} must decode to 32 bytes (it has {key.Length}).");
        return key;
    }

    private static byte ParseId(string value, string name) =>
        byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id != 0
            ? id
            : throw new InvalidOperationException($"{name}: a key id must be between 1 and 255.");
}

/// <summary>Encrypts single fields (account and card numbers) with AES-256-GCM.</summary>
public interface IFieldCipher
{
    byte[] Encrypt(string plaintext);

    /// <summary>Only the bank-file writers decrypt (S10); every read logs <c>decrypt_account</c> in <c>data_access_log</c>.</summary>
    string Decrypt(byte[] stored);
}

/// <summary>Stored as <c>key id (1) ‖ nonce (12) ‖ tag (16) ‖ ciphertext</c>; the nonce is random per value.</summary>
public sealed class AesGcmFieldCipher(EncryptionSettings settings) : IFieldCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public byte[] Encrypt(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var stored = new byte[1 + NonceSize + TagSize + plain.Length];
        stored[0] = settings.KeyId;
        var nonce = stored.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(settings.Key, TagSize);
        aes.Encrypt(nonce, plain, stored.AsSpan(1 + NonceSize + TagSize), stored.AsSpan(1 + NonceSize, TagSize), [settings.KeyId]);
        return stored;
    }

    public string Decrypt(byte[] stored)
    {
        if (stored.Length < 1 + NonceSize + TagSize || KeyFor(stored[0]) is not { } key)
            throw new CryptographicException("The value was encrypted with another key.");
        var plain = new byte[stored.Length - 1 - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(stored.AsSpan(1, NonceSize), stored.AsSpan(1 + NonceSize + TagSize), stored.AsSpan(1 + NonceSize, TagSize), plain,
            [stored[0]]);
        return Encoding.UTF8.GetString(plain);
    }

    private byte[]? KeyFor(byte keyId) =>
        keyId == settings.KeyId ? settings.Key : settings.PreviousKeys.GetValueOrDefault(keyId);
}
