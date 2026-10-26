using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigRequestedGzipTests
{
    [Theory]
    [InlineData("fixed")]
    [InlineData("stored")]
    [InlineData("optional-header")]
    public void StandardMembersReturnOwnedBytesAndPreserveTheirSyntheticInput(string kind)
    {
        var bytes = kind switch
        {
            "stored" => ClientConfigRequestedGzipFixtures.SmallStored(),
            "optional-header" => ClientConfigRequestedGzipFixtures.OptionalHeader(),
            _ => ClientConfigRequestedGzipFixtures.SmallFixed()
        };
        var before = bytes.ToArray();
        var decoded = ClientConfigRequestedGzip.Decode(bytes, 3);
        Assert.Equal(new byte[] { 97, 98, 99 }, decoded);
        Array.Fill(decoded, (byte)0);
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StandardFixedAndDynamicBackreferencesCrossTheHistoryWindow(bool dynamic)
    {
        var bytes = dynamic ? ClientConfigRequestedGzipFixtures.LargeDynamicA() : ClientConfigRequestedGzipFixtures.LargeFixedA();
        Assert.Equal(dynamic ? 2 : 1, (bytes[10] >> 1) & 3);
        Assert.Equal(RepeatedA(40000), ClientConfigRequestedGzip.Decode(bytes, 40000));
    }

    [Fact]
    public void ConcatenationUsesTheFinalRawRequestRatherThanSummingMemberSizes()
    {
        var first = ClientConfigRequestedGzipFixtures.SmallStored();
        var second = ClientConfigRequestedGzipFixtures.SmallFixed();
        var members = first.Concat(second).ToArray();
        Assert.Equal(new byte[] { 97, 98, 99 }, ClientConfigRequestedGzip.Decode(members, 6));
        Assert.Equal(new byte[] { 97, 98, 99, 97, 98, 99 },
            ClientConfigRequestedGzip.Decode(Request(members, 6), 6));
    }

    [Fact]
    public void NonGzipSuffixIsIgnoredRatherThanReturnedAsTransparentBytes()
    {
        var member = ClientConfigRequestedGzipFixtures.SmallFixed();
        Assert.Equal(new byte[] { 97, 98, 99 }, ClientConfigRequestedGzip.Decode(Request(member, 3), 4));
        Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(Request(member, 4), 4));
    }

    [Fact]
    public void ASecondMemberIsValidatedOnlyWhenTheRequestedReadReachesIt()
    {
        var badSecond = ClientConfigRequestedGzipFixtures.SmallFixed();
        badSecond[2] = 0; // Unsupported compression method in the second member only.
        var members = ClientConfigRequestedGzipFixtures.SmallFixed().Concat(badSecond).ToArray();
        Assert.Equal(new byte[] { 97, 98, 99 }, ClientConfigRequestedGzip.Decode(Request(members, 3), 4));
        Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(Request(members, 4), 4));
    }

    [Theory]
    [InlineData("1F8B00000000")]
    [InlineData("1F8BFFFE00000000")]
    public void AZeroRequestSkipsInflationAfterTheOuterCandidateGate(string hex) =>
        Assert.Empty(ClientConfigRequestedGzip.Decode(Convert.FromHexString(hex), 0));

    [Theory]
    [InlineData("method")]
    [InlineData("reserved-flags")]
    [InlineData("header-checksum")]
    public void ReachedInvalidHeadersRefuse(string corruption)
    {
        var bytes = corruption == "header-checksum"
            ? ClientConfigRequestedGzipFixtures.OptionalHeader()
            : ClientConfigRequestedGzipFixtures.SmallFixed();
        if (corruption == "method") bytes[2] = 0;
        else if (corruption == "reserved-flags") bytes[3] |= 0x20;
        else bytes[33] ^= 1; // Independent vector's first FHCRC byte, before its raw DEFLATE.
        Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(bytes, 3));
    }

    private static byte[] RepeatedA(int count) => Enumerable.Repeat((byte)65, count).ToArray();
}
