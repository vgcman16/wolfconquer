using System.Numerics;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerDiffieHellmanTests
{
    [Fact]
    public void GenerateRequestUsesLegacyUppercaseHexWithoutPadding()
    {
        var dh = new LegacyGameServerDiffieHellman("17", "05");

        var request = dh.GenerateRequest(new BigInteger(3));

        Assert.Equal("A", request);
        Assert.Equal("A", dh.PublicRequestHex);
    }

    [Fact]
    public void MatchingPeersDeriveSameSharedSecretBytes()
    {
        var server = new LegacyGameServerDiffieHellman("17", "05");
        var client = new LegacyGameServerDiffieHellman("17", "05");

        var serverPublic = server.GenerateRequest(new BigInteger(6));
        var clientPublic = client.GenerateResponse(serverPublic, new BigInteger(15));
        server.HandleResponse(clientPublic);

        Assert.Equal("2", server.SharedSecretHex);
        Assert.Equal(client.SharedSecretHex, server.SharedSecretHex);
        Assert.Equal([0x02], server.SharedSecretBytes);
        Assert.Equal(client.SharedSecretBytes, server.SharedSecretBytes);
    }

    [Fact]
    public void HandleResponseRequiresGeneratedRequest()
    {
        var dh = new LegacyGameServerDiffieHellman("17", "05");

        Assert.Throws<InvalidOperationException>(() => dh.HandleResponse("A"));
    }

    [Fact]
    public void DeriveKeyMaterialUsesExistingLegacyPostProcessing()
    {
        var server = new LegacyGameServerDiffieHellman("17", "05");
        var client = new LegacyGameServerDiffieHellman("17", "05");
        var serverPublic = server.GenerateRequest(new BigInteger(6));
        var clientPublic = client.GenerateResponse(serverPublic, new BigInteger(15));
        server.HandleResponse(clientPublic);
        Span<byte> expected = stackalloc byte[LegacyGameServerCipher.KeyMaterialSize];
        Span<byte> actual = stackalloc byte[LegacyGameServerCipher.KeyMaterialSize];

        LegacyGameServerCipher.DeriveKeyMaterialFromDiffieHellmanSharedSecret([0x02], expected);
        var length = server.DeriveKeyMaterial(actual);

        Assert.Equal(LegacyGameServerCipher.KeyMaterialSize, length);
        Assert.Equal(Encoding.ASCII.GetString(expected), Encoding.ASCII.GetString(actual));
    }

    [Fact]
    public void GenerateRequestRejectsNonPositivePrivateExponent()
    {
        var dh = new LegacyGameServerDiffieHellman("17", "05");

        Assert.Throws<ArgumentOutOfRangeException>(() => dh.GenerateRequest(BigInteger.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => dh.GenerateRequest(new BigInteger(-1)));
    }

    [Fact]
    public void DeriveKeyMaterialRequiresSharedSecret()
    {
        var dh = new LegacyGameServerDiffieHellman("17", "05");
        dh.GenerateRequest(new BigInteger(3));
        var destination = new byte[LegacyGameServerCipher.KeyMaterialSize];

        Assert.Throws<InvalidOperationException>(() => dh.DeriveKeyMaterial(destination));
    }
}
