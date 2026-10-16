using System.Security.Cryptography;
using System.Text;

namespace Platform.Core.Security;

/// <summary>
/// Env <c>ENCRYPTION_KEY</c> (base64 of 32 random bytes) and <c>ENCRYPTION_KEY_ID</c> (1–255, default 1). The id is written in
/// front of every ciphertext so a new key can be introduced later without re-encrypting at once (DATA_MODEL §3.7).
/// </summary>
public sealed record EncryptionSettings(byte[] Key, byte KeyId)
{
    public static EncryptionSettings Parse(string base64Key, string? keyId)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException("ENCRYPTION_KEY must be base64 (32 random bytes).", e);
        }
        if (key.Length != 32)
            throw new InvalidOperationException($"ENCRYPTION_KEY must decode to 32 bytes (it has {key.Length}).");
        var id = keyId is { Length: > 0 } ? byte.Parse(keyId, System.Globalization.CultureInfo.InvariantCulture) : (byte)1;
        if (id == 0)
            throw new InvalidOperationException("ENCRYPTION_KEY_ID must be between 1 and 255.");
        return new EncryptionSettings(key, id);
    }
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
        if (stored.Length < 1 + NonceSize + TagSize || stored[0] != settings.KeyId)
            throw new CryptographicException("The value was encrypted with another key.");
        var plain = new byte[stored.Length - 1 - NonceSize - TagSize];
        using var aes = new AesGcm(settings.Key, TagSize);
        aes.Decrypt(stored.AsSpan(1, NonceSize), stored.AsSpan(1 + NonceSize + TagSize), stored.AsSpan(1 + NonceSize, TagSize), plain,
            [stored[0]]);
        return Encoding.UTF8.GetString(plain);
    }
}
