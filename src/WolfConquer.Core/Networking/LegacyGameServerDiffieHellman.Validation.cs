using System.Globalization;
using System.Numerics;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Networking;

public sealed partial class LegacyGameServerDiffieHellman
{
    private static BigInteger ParseParameterHex(string hex, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex, parameterName);
        foreach (var character in hex)
            if (!char.IsAsciiHexDigit(character))
                throw new ArgumentException("Legacy DH parameter must contain only hexadecimal digits.", parameterName);
        return BigInteger.Parse("0" + hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
    }

    private BigInteger ReadPeerPublicKey(string hex)
    {
        // Retain valid case/padding within the existing protocol ceiling. Only
        // the bounded significant suffix may enter BigInteger parsing.
        if (hex is null || hex.Length is 0 or > PacketHeader.DefaultMaxFrameSize)
            throw new LegacyGameServerPeerKeyRejectedException();
        foreach (var character in hex)
            if (!char.IsAsciiHexDigit(character)) throw new LegacyGameServerPeerKeyRejectedException();
        var first = 0;
        while (first < hex.Length && hex[first] == '0') first++;
        if (first == hex.Length || hex.Length - first > _modulusHexWidth)
            throw new LegacyGameServerPeerKeyRejectedException();
        var peer = BigInteger.Parse("0" + hex[first..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        if (!IsNontrivial(peer)) throw new LegacyGameServerPeerKeyRejectedException();
        return peer;
    }

    private bool TryPrepareRequest(BigInteger exponent, out ExchangeState? candidate)
    {
        candidate = null;
        if (exponent <= BigInteger.Zero) return false;
        var publicValue = BigInteger.ModPow(_g, exponent, _p);
        if (!IsNontrivial(publicValue)) return false;
        candidate = new(exponent, publicValue, BigInteger.Zero);
        return true;
    }

    private static void RequirePositiveExponent(BigInteger exponent)
    {
        if (exponent <= BigInteger.Zero)
            throw new ArgumentOutOfRangeException("privateExponent", "Legacy DH private exponent must be positive.");
    }

    private bool IsNontrivial(BigInteger value) => value > BigInteger.One && value < _p - BigInteger.One;

    private void RequireNontrivialShared(BigInteger value)
    {
        if (!IsNontrivial(value)) throw new LegacyGameServerPeerKeyRejectedException();
    }
}
