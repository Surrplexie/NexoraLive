using System.Security.Cryptography;
using System.Text;

namespace NL.Identity;

/// <summary>RFC 6238 TOTP (30s step, 6 digits, SHA-1).</summary>
public static class TotpHelper
{
    private const int StepSeconds = 30;
    private const int Digits = 6;

    public static string GenerateSecretBase32()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(bytes);
    }

    public static string BuildOtpAuthUri(string accountLabel, string secretBase32, string issuer = "NexoraLive") =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountLabel)}?secret={secretBase32}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={StepSeconds}";

    public static bool VerifyCode(string secretBase32, string code, DateTimeOffset nowUtc, int window = 1)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != Digits || !code.Trim().All(char.IsDigit))
        {
            return false;
        }

        var secret = Base32Decode(secretBase32);
        if (secret.Length == 0)
        {
            return false;
        }

        var counter = nowUtc.ToUnixTimeSeconds() / StepSeconds;
        for (var offset = -window; offset <= window; offset++)
        {
            if (GenerateCode(secret, counter + offset) == code.Trim())
            {
                return true;
            }
        }

        return false;
    }

    public static string CurrentCode(string secretBase32, DateTimeOffset nowUtc)
    {
        var secret = Base32Decode(secretBase32);
        var counter = nowUtc.ToUnixTimeSeconds() / StepSeconds;
        return GenerateCode(secret, counter);
    }

    private static string GenerateCode(byte[] secret, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xFF);
            counter >>= 8;
        }

        var hash = HMACSHA1.HashData(secret, counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString(new string('0', Digits));
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                var index = (buffer >> (bitsLeft - 5)) & 31;
                bitsLeft -= 5;
                result.Append(alphabet[index]);
            }
        }

        if (bitsLeft > 0)
        {
            var index = (buffer << (5 - bitsLeft)) & 31;
            result.Append(alphabet[index]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.Trim().TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in cleaned)
        {
            var index = alphabet.IndexOf(c);
            if (index < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
