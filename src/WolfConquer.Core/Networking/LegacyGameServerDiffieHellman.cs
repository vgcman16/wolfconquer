using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace WolfConquer.Core.Networking;

public sealed partial class LegacyGameServerDiffieHellman
{
    private const int PrivateKeyBytes = 32;
    private const int MaximumGenerationAttempts = 128;

    private readonly BigInteger _p;
    private readonly BigInteger _g;
    private readonly int _modulusHexWidth;
    private readonly object _stateGate = new();
    private ExchangeState _state = new(BigInteger.Zero, BigInteger.Zero, BigInteger.Zero);

    private sealed record ExchangeState(BigInteger PrivateExponent, BigInteger PublicRequest, BigInteger SharedSecret);

    public LegacyGameServerDiffieHellman()
        : this(LegacyGameServerCipher.DiffieHellmanPrimeHex, LegacyGameServerCipher.DiffieHellmanGeneratorHex)
    {
    }

    public LegacyGameServerDiffieHellman(string pHex, string gHex)
    {
        var modulus = ParseParameterHex(pHex, nameof(pHex));
        var generator = ParseParameterHex(gHex, nameof(gHex));
        if (modulus <= 3 || modulus.IsEven)
            throw new ArgumentOutOfRangeException(nameof(pHex), "Legacy DH modulus must be odd and exceed three.");
        if (generator <= BigInteger.One || generator >= modulus - BigInteger.One)
            throw new ArgumentOutOfRangeException(nameof(gHex), "Legacy DH generator must be nontrivial and in range.");
        if (BigInteger.GreatestCommonDivisor(generator, modulus) != BigInteger.One)
            throw new ArgumentException("Legacy DH generator must be coprime to the modulus.", nameof(gHex));
        _p = modulus;
        _g = generator;
        _modulusHexWidth = ToLegacyHex(modulus).Length;
    }

    public string PublicRequestHex => ToLegacyHex(Volatile.Read(ref _state).PublicRequest);

    public string SharedSecretHex => ToLegacyHex(Volatile.Read(ref _state).SharedSecret);

    public byte[] SharedSecretBytes => ToLegacyBytes(Volatile.Read(ref _state).SharedSecret);

    public string GenerateRequest()
    {
        Span<byte> privateBytes = stackalloc byte[PrivateKeyBytes];
        try
        {
            for (var attempt = 0; attempt < MaximumGenerationAttempts; attempt++)
            {
                RandomNumberGenerator.Fill(privateBytes);
                privateBytes[0] &= 0x7F;
                if (!TryPrepareRequest(ToBigEndianUnsigned(privateBytes), out var candidate)) continue;
                return CommitState(candidate!);
            }
            throw new CryptographicException("Unable to generate a nontrivial legacy DH public key.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateBytes);
        }
    }

    public string GenerateRequest(BigInteger privateExponent)
    {
        RequirePositiveExponent(privateExponent);
        if (!TryPrepareRequest(privateExponent, out var candidate))
            throw new CryptographicException("Legacy DH exponent produces a trivial public key.");
        return CommitState(candidate!);
    }

    public string GenerateResponse(string publicKeyHex, BigInteger privateExponent)
    {
        RequirePositiveExponent(privateExponent);
        var peerPublic = ReadPeerPublicKey(publicKeyHex);
        if (!TryPrepareRequest(privateExponent, out var candidate))
            throw new CryptographicException("Legacy DH exponent produces a trivial public key.");
        var shared = BigInteger.ModPow(peerPublic, privateExponent, _p);
        RequireNontrivialShared(shared);
        return CommitState(candidate! with { SharedSecret = shared });
    }

    public void HandleResponse(string publicKeyHex)
    {
        lock (_stateGate)
        {
            var prior = Volatile.Read(ref _state);
            if (prior.PrivateExponent <= BigInteger.Zero)
                throw new InvalidOperationException("GenerateRequest must be called before handling a legacy DH response.");
            var peerPublic = ReadPeerPublicKey(publicKeyHex);
            var shared = BigInteger.ModPow(peerPublic, prior.PrivateExponent, _p);
            RequireNontrivialShared(shared);
            Volatile.Write(ref _state, prior with { SharedSecret = shared });
        }
    }

    public int DeriveKeyMaterial(Span<byte> destination)
    {
        var shared = Volatile.Read(ref _state).SharedSecret;
        if (shared <= BigInteger.Zero)
            throw new InvalidOperationException("A legacy DH response has not been handled yet.");
        return LegacyGameServerCipher.DeriveKeyMaterialFromDiffieHellmanSharedSecret(ToLegacyBytes(shared), destination);
    }

    private string CommitState(ExchangeState candidate)
    {
        // Publish one fully validated generation. Rejection never changes any
        // previously established exponent, public value or shared value.
        var publicHex = ToLegacyHex(candidate.PublicRequest);
        lock (_stateGate) Volatile.Write(ref _state, candidate);
        return publicHex;
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
