using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace ShreeJewellers.Infrastructure.Services;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    string MaskSensitive(string value, int visibleChars = 4);
}

/// <summary>
/// AES-256-CBC encryption for sensitive PII fields (Aadhaar, PAN).
/// Key and IV are loaded from environment variables / appsettings — never hard-coded.
///
/// Encrypt output format: Base64(IV + CipherBytes)
/// The IV is prepended so each encryption is uniquely random even for identical inputs.
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IConfiguration config)
    {
        var keyBase64 = config["EncryptionSettings:AESKey"]
            ?? throw new InvalidOperationException(
                "EncryptionSettings:AESKey is not configured. Set it as an environment variable (EncryptionSettings__AESKey) or user-secrets.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("EncryptionSettings:AESKey is not a valid Base64 string. Provide a 32-byte (256-bit) key encoded in Base64.");
        }

        if (key.Length != 32)
            throw new InvalidOperationException("AES key must be exactly 32 bytes (256-bit). Provide Base64 of 32 bytes.");

        _key = key;
    }

    // ── Encrypt ───────────────────────────────────────────────────────────

    /// <summary>
    /// Encrypts plainText using AES-256-CBC with a fresh random IV each time.
    /// Returns Base64(IV[16 bytes] + CipherText).
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _key;
        aes.GenerateIV();   // New random IV per encryption — critical for security

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to cipher text so we can extract it during decryption
        var combined = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }

    // ── Decrypt ───────────────────────────────────────────────────────────

    /// <summary>
    /// Decrypts a Base64-encoded string produced by Encrypt().
    /// Only call this for Admin-level KYC review — never expose to Customer API responses.
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentNullException(nameof(cipherText));

        var combined = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _key;

        // Extract IV from the first 16 bytes
        var iv = new byte[16];
        var cipherBytes = new byte[combined.Length - 16];
        Buffer.BlockCopy(combined, 0, iv, 0, 16);
        Buffer.BlockCopy(combined, 16, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    // ── Masking (for logs and API responses) ──────────────────────────────

    /// <summary>
    /// Masks sensitive values for logs and API responses.
    /// MaskSensitive("123456789012", 4) → "XXXXXXXX9012"
    /// MaskSensitive("ABCDE1234F", 4)   → "XXXXXX1234F"
    /// </summary>
    public string MaskSensitive(string value, int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= visibleChars)
            return new string('X', value?.Length ?? 0);

        var masked = new string('X', value.Length - visibleChars);
        var visible = value[^visibleChars..];
        return masked + visible;
    }
}
