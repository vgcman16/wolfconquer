using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace WolfConquer.Core.Networking;

public sealed class LegacyGameServerDiffieHellman
{
    private const int PrivateKeyBytes = 32;

    private readonly BigInteger _p;
    private readonly BigInteger _g;
    private BigInteger _privateExponent;
    private BigInteger _publicRequest;
    private BigInteger _sharedSecret;

    public LegacyGameServerDiffieHellman()
        : this(LegacyGameServerCipher.DiffieHellmanPrimeHex, LegacyGameServerCipher.DiffieHellmanGeneratorHex)
    {
    }

    public LegacyGameServerDiffieHellman(string pHex, string gHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pHex);
        ArgumentException.ThrowIfNullOrWhiteSpace(gHex);

        _p = ParseHex(pHex);
        _g = ParseHex(gHex);
    }

    public string PublicRequestHex => ToLegacyHex(_publicRequest);

    public string SharedSecretHex => ToLegacyHex(_sharedSecret);

    public byte[] SharedSecretBytes => ToLegacyBytes(_sharedSecret);

    public string GenerateRequest()
    {
        Span<byte> privateBytes = stackalloc byte[PrivateKeyBytes];
        RandomNumberGenerator.Fill(privateBytes);
        privateBytes[0] &= 0x7F;

        return GenerateRequest(ToBigEndianUnsigned(privateBytes));
    }

    public string GenerateRequest(BigInteger privateExponent)
    {
        if (privateExponent <= BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(privateExponent), "Legacy DH private exponent must be positive.");
        }

        _privateExponent = privateExponent;
        _publicRequest = BigInteger.ModPow(_g, _privateExponent, _p);
        _sharedSecret = BigInteger.Zero;
        return PublicRequestHex;
    }

    public string GenerateResponse(string publicKeyHex, BigInteger privateExponent)
    {
        if (privateExponent <= BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(privateExponent), "Legacy DH private exponent must be positive.");
        }

        var peerPublic = ParseHex(publicKeyHex);
        _privateExponent = privateExponent;
        _publicRequest = BigInteger.ModPow(_g, _privateExponent, _p);
        _sharedSecret = BigInteger.ModPow(peerPublic, _privateExponent, _p);
        return PublicRequestHex;
    }

    public void HandleResponse(string publicKeyHex)
    {
        if (_privateExponent <= BigInteger.Zero)
        {
            throw new InvalidOperationException("GenerateRequest must be called before handling a legacy DH response.");
        }

        var peerPublic = ParseHex(publicKeyHex);
        _sharedSecret = BigInteger.ModPow(peerPublic, _privateExponent, _p);
    }

    public int DeriveKeyMaterial(Span<byte> destination)
    {
        if (_sharedSecret <= BigInteger.Zero)
        {
            throw new InvalidOperationException("A legacy DH response has not been handled yet.");
        }

        return LegacyGameServerCipher.DeriveKeyMaterialFromDiffieHellmanSharedSecret(SharedSecretBytes, destination);
    }

    private static BigInteger ParseHex(string hex)
    {
        return BigInteger.Parse("0" + hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
    }

    private static BigInteger ToBigEndianUnsigned(ReadOnlySpan<byte> bytes)
    {
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    private static string ToLegacyHex(BigInteger value)
    {
        if (value < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Legacy DH values must be non-negative.");
        }

        if (value.IsZero)
        {
            return "0";
        }

        var hex = value.ToString("X", CultureInfo.InvariantCulture).TrimStart('0');
        return hex.Length == 0 ? "0" : hex;
    }

    private static byte[] ToLegacyBytes(BigInteger value)
    {
        if (value < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Legacy DH values must be non-negative.");
        }

        return value.ToByteArray(isUnsigned: true, isBigEndian: true);
    }
}
