using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.Accounts;

internal static class SqlAccountWorldHandoffToken
{
    internal static string Create()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        try { return Convert.ToHexString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static bool IsCanonical(string? token)
    {
        if (token is null || token.Length != 64) return false;
        foreach (char c in token)
            if (!(c is >= '0' and <= '9' or >= 'A' and <= 'F')) return false;
        return true;
    }

    internal static byte[] Hash(string token)
    {
        if (!IsCanonical(token)) throw new ArgumentException("Invalid handoff token.", nameof(token));
        Span<byte> ascii = stackalloc byte[64];
        try
        {
            Encoding.ASCII.GetBytes(token.AsSpan(), ascii);
            return SHA256.HashData(ascii);
        }
        finally { CryptographicOperations.ZeroMemory(ascii); }
    }
}