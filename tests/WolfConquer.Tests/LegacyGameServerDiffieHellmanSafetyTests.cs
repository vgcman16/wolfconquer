using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerDiffieHellmanSafetyTests
{
    [Fact]
    public void IndependentSmallGroupGoldenRetainsLegacyWireSpellingAndSharedValue()
    {
        var left = new LegacyGameServerDiffieHellman("17", "05");
        var right = new LegacyGameServerDiffieHellman("17", "05");
        Assert.Equal("8", left.GenerateRequest(6));
        Assert.Equal("13", right.GenerateResponse("00000008", 15));
        left.HandleResponse("0000000000000013");
        Assert.Equal("2", left.SharedSecretHex); Assert.Equal("2", right.SharedSecretHex);
        Assert.Equal(new byte[] { 2 }, left.SharedSecretBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("16")]
    [InlineData("17")]
    [InlineData("18")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("GG")]
    [InlineData("-2")]
    public void InvalidPeerCannotChangeNegotiatedStateOrPriorPrivateExponent(string? peer)
    {
        var dh = Negotiated();
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.HandleResponse(peer!));
        Assert.Equal("8", dh.PublicRequestHex); Assert.Equal("2", dh.SharedSecretHex);
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.GenerateResponse(peer!, 3));
        Assert.Equal("8", dh.PublicRequestHex); Assert.Equal("2", dh.SharedSecretHex);
        dh.HandleResponse("000a");
        Assert.Equal("8", dh.PublicRequestHex); Assert.Equal("6", dh.SharedSecretHex);
    }

    [Fact]
    public void OversizedPeerNumberIsRejectedWithoutChangingState()
    {
        var dh = Negotiated(); var oversized = new string('F', 4096);
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.HandleResponse(oversized));
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.GenerateResponse(oversized, 3));
        Assert.Equal("8", dh.PublicRequestHex); Assert.Equal("2", dh.SharedSecretHex);
    }

    [Fact]
    public void WireLengthBoundaryAcceptsPaddingAndRejectsExcessWithoutChangingState()
    {
        var dh = Negotiated();
        var cap = PacketHeader.DefaultMaxFrameSize;
        dh.HandleResponse(new string('0', cap - 1) + "a");
        Assert.Equal("6", dh.SharedSecretHex);
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.HandleResponse(new string('0', cap) + "a"));
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.GenerateResponse(new string('0', cap) + "a", 3));
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.HandleResponse("100"));
        Assert.Equal("8", dh.PublicRequestHex); Assert.Equal("6", dh.SharedSecretHex);
    }

    [Theory]
    [InlineData("", "05")]
    [InlineData("03", "02")]
    [InlineData("08", "03")]
    [InlineData("17", "00")]
    [InlineData("17", "01")]
    [InlineData("17", "16")]
    [InlineData("17", "17")]
    [InlineData("0F", "03")]
    [InlineData("ZZ", "05")]
    public void InvalidGroupCannotConstructUsableInstance(string modulus, string generator) =>
        Assert.ThrowsAny<ArgumentException>(() => new LegacyGameServerDiffieHellman(modulus, generator));

    [Theory]
    [InlineData(11)]
    [InlineData(22)]
    public void ExplicitDegeneratePublicRequestDoesNotReplacePreviousNegotiation(int exponent)
    {
        var dh = Negotiated();
        Assert.ThrowsAny<CryptographicException>(() => dh.GenerateRequest(exponent));
        Assert.Equal("8", dh.PublicRequestHex); Assert.Equal("2", dh.SharedSecretHex);
        Assert.ThrowsAny<CryptographicException>(() => dh.GenerateResponse("2", exponent));
        Assert.Equal("8", dh.PublicRequestHex); Assert.Equal("2", dh.SharedSecretHex);
        dh.HandleResponse("A"); Assert.Equal("6", dh.SharedSecretHex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidPrivateExponentCannotReplaceAnEstablishedState(int exponent)
    {
        var dh = Negotiated();
        Assert.Throws<ArgumentOutOfRangeException>(() => dh.GenerateRequest(exponent));
        Assert.Throws<ArgumentOutOfRangeException>(() => dh.GenerateResponse("2", exponent));
        Assert.Equal("8", dh.PublicRequestHex); Assert.Equal("2", dh.SharedSecretHex);
        dh.HandleResponse("A"); Assert.Equal("6", dh.SharedSecretHex);
    }

    [Fact]
    public void DegenerateComputedSharedValueRefusesWithoutPublishingEitherCandidate()
    {
        // Modulus 15, generator 2: exponent 1 / peer 2 gives public 2, shared 2.
        // Exponent 2 produces public 4, but peer 4 gives 4^2 mod 15 = 1.
        var dh = new LegacyGameServerDiffieHellman("0F", "02");
        Assert.Equal("2", dh.GenerateRequest(1)); dh.HandleResponse("2");
        Assert.Equal("2", dh.SharedSecretHex);
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.GenerateResponse("4", 2));
        Assert.Equal("2", dh.PublicRequestHex); Assert.Equal("2", dh.SharedSecretHex);
        dh.HandleResponse("4"); Assert.Equal("4", dh.SharedSecretHex);
    }

    [Fact]
    public void DegenerateHandledSharedValuePreservesEstablishedSecret()
    {
        var dh = new LegacyGameServerDiffieHellman("0F", "02");
        Assert.Equal("4", dh.GenerateRequest(2)); dh.HandleResponse("2");
        Assert.Equal("4", dh.SharedSecretHex);
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.HandleResponse("4"));
        Assert.Equal("4", dh.PublicRequestHex); Assert.Equal("4", dh.SharedSecretHex);
        dh.HandleResponse("2"); Assert.Equal("4", dh.SharedSecretHex);
    }

    [Fact]
    public void RealRandomGenerationRecoversAfterRejectedPeerAndUsesValidPublicValue()
    {
        var dh = new LegacyGameServerDiffieHellman();
        var publicHex = dh.GenerateRequest();
        var value = BigInteger.Parse("0" + publicHex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var modulus = BigInteger.Parse("0" + LegacyGameServerCipher.DiffieHellmanPrimeHex,
            NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        Assert.True(value > BigInteger.One && value < modulus - BigInteger.One);
        Assert.Throws<LegacyGameServerPeerKeyRejectedException>(() => dh.HandleResponse("1"));
        Assert.Equal(publicHex, dh.PublicRequestHex);
        // Peer g^1 is valid, and its shared value equals our generated public value.
        dh.HandleResponse(LegacyGameServerCipher.DiffieHellmanGeneratorHex);
        Assert.Equal(publicHex, dh.SharedSecretHex);
    }

    private static LegacyGameServerDiffieHellman Negotiated()
    {
        var dh = new LegacyGameServerDiffieHellman("17", "05");
        Assert.Equal("8", dh.GenerateRequest(6)); dh.HandleResponse("13");
        Assert.Equal("2", dh.SharedSecretHex); return dh;
    }
}
