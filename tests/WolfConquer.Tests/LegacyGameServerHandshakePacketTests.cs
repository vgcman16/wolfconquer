using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerHandshakePacketTests
{
    private static readonly byte[] P = Encoding.ASCII.GetBytes("E7A69E");
    private static readonly byte[] G = Encoding.ASCII.GetBytes("05");
    private static readonly byte[] PublicKey = Encoding.ASCII.GetBytes("SERVERPUBLIC");

    [Fact]
    public void BuildUsesLegacyTotalLengthFormula()
    {
        var packet = LegacyGameServerHandshakePacket.Build(P, G, PublicKey);

        Assert.Equal(75 + P.Length + G.Length + PublicKey.Length, packet.Length);
    }

    [Fact]
    public void BuildWritesAdjustedLengthAndHandshakeTypeAtOffsetEleven()
    {
        var packet = LegacyGameServerHandshakePacket.Build(P, G, PublicKey);

        Assert.Equal(packet.Length - 11, ReadInt32(packet, 11));
        Assert.Equal(10, ReadInt32(packet, 15));
    }

    [Fact]
    public void BuildRoundTripsLengthPrefixedDhBlobsAtExpectedOffsets()
    {
        var packet = LegacyGameServerHandshakePacket.Build(P, G, PublicKey);

        var pLengthOffset = 53;
        Assert.Equal(P.Length, ReadInt32(packet, pLengthOffset));
        Assert.Equal(P, packet.AsSpan(pLengthOffset + 4, P.Length).ToArray());

        var gLengthOffset = pLengthOffset + 4 + P.Length;
        Assert.Equal(G.Length, ReadInt32(packet, gLengthOffset));
        Assert.Equal(G, packet.AsSpan(gLengthOffset + 4, G.Length).ToArray());

        var publicKeyLengthOffset = gLengthOffset + 4 + G.Length;
        Assert.Equal(PublicKey.Length, ReadInt32(packet, publicKeyLengthOffset));
        Assert.Equal(PublicKey, packet.AsSpan(publicKeyLengthOffset + 4, PublicKey.Length).ToArray());
    }

    [Fact]
    public void BuildEndsWithTqServerSeal()
    {
        var packet = LegacyGameServerHandshakePacket.Build(P, G, PublicKey);

        Assert.Equal("TQServer", Encoding.ASCII.GetString(packet.AsSpan(packet.Length - 8)));
    }

    [Fact]
    public void BuildRejectsEmptyDhValues()
    {
        Assert.Throws<ArgumentException>(() => LegacyGameServerHandshakePacket.Build(Array.Empty<byte>(), G, PublicKey));
        Assert.Throws<ArgumentException>(() => LegacyGameServerHandshakePacket.Build(P, Array.Empty<byte>(), PublicKey));
        Assert.Throws<ArgumentException>(() => LegacyGameServerHandshakePacket.Build(P, G, Array.Empty<byte>()));
    }

    [Fact]
    public void BuildWithLegacyParametersUsesAsciiPrimeGeneratorAndPublicKey()
    {
        var packet = LegacyGameServerHandshakePacket.BuildWithLegacyParameters("SERVERPUBLIC");
        var offset = 53;

        Assert.Equal(LegacyGameServerCipher.DiffieHellmanPrimeHex, Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)));
        Assert.Equal(LegacyGameServerCipher.DiffieHellmanGeneratorHex, Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)));
        Assert.Equal("SERVERPUBLIC", Encoding.ASCII.GetString(ReadLengthPrefixedBlob(packet, ref offset)));
    }

    private static byte[] ReadLengthPrefixedBlob(byte[] packet, ref int offset)
    {
        var length = ReadInt32(packet, offset);
        offset += sizeof(int);
        var blob = packet.AsSpan(offset, length).ToArray();
        offset += length;
        return blob;
    }

    private static int ReadInt32(byte[] packet, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(offset, sizeof(int)));
    }
}
