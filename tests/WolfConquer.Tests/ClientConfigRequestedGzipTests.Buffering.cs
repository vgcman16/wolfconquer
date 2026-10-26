using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigRequestedGzipTests
{
    [Theory]
    [InlineData("crc")]
    [InlineData("size")]
    public void ShortMemberTrailerErrorsAreReachedDuringInitialBufferingEvenForOneRequestedByte(string corruption)
    {
        var member = ClientConfigRequestedGzipFixtures.SmallFixed();
        member[member.Length - (corruption == "crc" ? 8 : 4)] ^= 1;
        Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(Request(member, 1), 3));
    }

    [Theory]
    [InlineData(false, "crc")]
    [InlineData(true, "crc")]
    [InlineData(false, "size")]
    [InlineData(true, "size")]
    public void LargeMatchPrefixCanReturnBeforeAnInvalidUnreachedTrailer(bool dynamic, string corruption)
    {
        var member = dynamic ? ClientConfigRequestedGzipFixtures.LargeDynamicA() : ClientConfigRequestedGzipFixtures.LargeFixedA();
        member[member.Length - (corruption == "crc" ? 8 : 4)] ^= 1;
        Assert.Equal(RepeatedA(32768), ClientConfigRequestedGzip.Decode(Request(member, 32768), 40000));
        Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(Request(member, 40000), 40000));
    }

    [Theory]
    [InlineData(false, 16384)]
    [InlineData(false, 16385)]
    [InlineData(true, 16384)]
    [InlineData(true, 16385)]
    public void InitialBufferBoundaryPreservesPendingStoredAndLiteralOutput(bool literal, int payloadSize)
    {
        var member = literal ? LiteralA(payloadSize) : StoredA(payloadSize);
        member[^8] ^= 1;
        var requested = Request(member, 16384);
        if (payloadSize == 16384)
            Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(requested, 16384));
        else
            Assert.Equal(RepeatedA(16384), ClientConfigRequestedGzip.Decode(requested, 16384));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(8, false)]
    public void InputExhaustionRetainsProducedPrefixButStillRequiresExactRequestedCount(int requested, bool succeeds)
    {
        // A final stored block declares100 bytes but has only "abc" plus the
        // outer request bytes. It exhausts input mid-block, never reaches CRC.
        byte[] prefix = [0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 0, 3, 1, 100, 0, 155, 255, 97, 98, 99];
        var bytes = new byte[prefix.Length + 4];
        prefix.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(prefix.Length), (uint)requested);
        if (succeeds)
            Assert.Equal(new byte[] { 97, 98, 99 }, ClientConfigRequestedGzip.Decode(bytes, 8));
        else
            Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(bytes, 8));
    }
}
