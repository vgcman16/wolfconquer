using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class Chinese7935XuanbaoPresenceRoundtripTests
{
    [Theory]
    [InlineData("0800", false)]
    [InlineData("08004200", true)]
    public void ParsedAbsentAndPresentEmptyBytesRetainTheirDistinctWireImages(string hex, bool present)
    {
        var bytes = Convert.FromHexString(hex);
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(bytes, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(present, parsed.HasData8);
        if (present) Assert.Empty(parsed.CopyData8()!);
        else Assert.Null(parsed.CopyData8());
        Assert.Equal(bytes, parsed.ToPayloadBytes());
    }
}
