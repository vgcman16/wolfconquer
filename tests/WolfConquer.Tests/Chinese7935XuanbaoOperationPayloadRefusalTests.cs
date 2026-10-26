using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class Chinese7935XuanbaoOperationPayloadRefusalTests
{
    [Theory]
    [InlineData("")]
    [InlineData("1001")]
    [InlineData("00")]
    [InlineData("0880")]
    [InlineData("088080808010")]
    [InlineData("0A00")]
    [InlineData("08014001")]
    [InlineData("0801420341")]
    [InlineData("08014800")]
    [InlineData("08014A050801")]
    [InlineData("08014A020A00")]
    [InlineData("08014A06088080808010")]
    [InlineData("08014A021801")]
    [InlineData("08015200")]
    [InlineData("0801508080808010")]
    [InlineData("08015801")]
    [InlineData("080188808080800101")]
    [InlineData("080142FFFFFFFFFFFFFFFFFF02")]
    [InlineData("08014A80")]
    [InlineData("08010C")]
    public void MalformedOrUnsupportedKnownSchemaInputReturnsNoPartialPacket(string hex)
    {
        var input = Convert.FromHexString(hex);
        var preserved = input.ToArray();
        Assert.False(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(input, out var packet));
        Assert.Null(packet);
        Assert.Equal(preserved, input);
    }

    [Fact]
    public void FinitePayloadBoundHasAnExactPositiveControlAndRejectsOneMoreByte()
    {
        var data = new byte[65525]; // Two required bytes + tag8 + three length bytes = six overhead bytes.
        var packet = new Chinese7935XuanbaoOperationPayload(0, data8: data);
        var bytes = packet.ToPayloadBytes();
        Assert.Equal(Chinese7935XuanbaoOperationPayload.MaximumPayloadBytes, bytes.Length);
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(bytes, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(bytes, parsed.ToPayloadBytes());
        Assert.Throws<ArgumentException>(() => new Chinese7935XuanbaoOperationPayload(0, data8: new byte[65526]));
        Assert.False(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(new byte[bytes.Length + 1], out parsed));
        Assert.Null(parsed);
    }

    [Fact]
    public void FiniteEntryBoundIsAnOfflineGuardRatherThanAFiveSlotGameplayClaim()
    {
        var allowed = new Chinese7935XuanbaoAccInfo[Chinese7935XuanbaoOperationPayload.MaximumAccInfoEntries];
        var packet = new Chinese7935XuanbaoOperationPayload(1, accInfo: allowed);
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(packet.ToPayloadBytes(), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(allowed.Length, parsed.AccInfo.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Chinese7935XuanbaoOperationPayload(1,
            accInfo: new Chinese7935XuanbaoAccInfo[allowed.Length + 1]));
        var oneExtra = packet.ToPayloadBytes().Concat(new byte[] { 0x4a, 0 }).ToArray();
        Assert.False(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(oneExtra, out parsed));
        Assert.Null(parsed);
    }
}
