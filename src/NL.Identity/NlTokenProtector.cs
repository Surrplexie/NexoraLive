using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace NL.Identity;

/// <summary>Protects OAuth refresh tokens at rest (DPAPI on Windows, AES-GCM elsewhere).</summary>
public sealed class NlTokenProtector
{
    private readonly byte[]? _aesKey;

    public NlTokenProtector()
    {
        var keyB64 = Environment.GetEnvironmentVariable("NL_IDENTITY_ENCRYPTION_KEY");
        if (!string.IsNullOrWhiteSpace(keyB64))
        {
            _aesKey = Convert.FromBase64String(keyB64.Trim());
            if (_aesKey.Length != 32)
            {
                throw new InvalidOperationException("NL_IDENTITY_ENCRYPTION_KEY must be 32 bytes (base64).");
            }
        }
    }

    public string Protect(string plaintext)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _aesKey is null)
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
                bytes, null, DataProtectionScope.CurrentUser);
            return "dpapi:" + Convert.ToBase64String(protectedBytes);
        }

        if (_aesKey is null)
        {
            throw new InvalidOperationException(
                "Set NL_IDENTITY_ENCRYPTION_KEY (32-byte base64) for token encryption on this platform.");
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_aesKey, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return "aes:" + Convert.ToBase64String(nonce) + ":"
            + Convert.ToBase64String(cipher) + ":"
            + Convert.ToBase64String(tag);
    }

    public string Unprotect(string blob)
    {
        if (blob.StartsWith("dpapi:", StringComparison.Ordinal))
        {
            var protectedBytes = Convert.FromBase64String(blob["dpapi:".Length..]);
            var plain = System.Security.Cryptography.ProtectedData.Unprotect(
                protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        if (blob.StartsWith("aes:", StringComparison.Ordinal))
        {
            if (_aesKey is null)
            {
                throw new InvalidOperationException("NL_IDENTITY_ENCRYPTION_KEY required to decrypt aes tokens.");
            }

            var parts = blob["aes:".Length..].Split(':');
            var nonce = Convert.FromBase64String(parts[0]);
            var cipher = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_aesKey, tagSizeInBytes: 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }

        throw new FormatException("Unknown protected token format.");
    }
}
