using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935ItemInfoViewTests
{
    [Theory]
    [InlineData("")]
    [InlineData("08001000")]
    [InlineData("08001800")]
    [InlineData("10001800")]
    public void MissingAnyRequiredFirstThreeFieldsRefusesWithoutOutput(string hex)
    {
        Assert.False(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(hex), out var view));
        Assert.Null(view);
    }

    [Theory]
    [InlineData("00")]
    [InlineData("07")]
    [InlineData("E00200")]
    [InlineData("0A00")]
    [InlineData("D80100")]
    [InlineData("FA010180")]
    [InlineData("DA010241")]
    [InlineData("208080808010")]
    [InlineData("C0028080808010")]
    [InlineData("A002FFFFFFFFFFFFFFFFFF02")]
    [InlineData("8080808080808080808000")]
    public void UnsupportedWrongWireTruncatedAndOverflowDataRefuseWithoutOutput(string suffix)
    {
        Assert.False(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(RequiredZero + suffix), out var view));
        Assert.Null(view);
    }

    [Fact]
    public void Repeated31ExactLimitPassesAndOneMoreRefuses()
    {
        var prefix = Convert.FromHexString(RequiredZero);
        var bytes = new byte[prefix.Length + 3 * Chinese7935ItemInfoView.MaximumRepeated31Values];
        prefix.CopyTo(bytes, 0);
        for (var offset = prefix.Length; offset < bytes.Length; offset += 3)
        {
            bytes[offset] = 0xF8;
            bytes[offset + 1] = 1;
            bytes[offset + 2] = 0;
        }
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(bytes, out var view));
        Assert.NotNull(view);
        Assert.Equal(Chinese7935ItemInfoView.MaximumRepeated31Values, view.Values31.Length);
        Assert.False(Chinese7935ItemInfoView.TryParseKnownPayload([.. bytes, 0xF8, 1, 0], out view));
        Assert.Null(view);
    }

    [Fact]
    public void PayloadExactOfflineBoundPassesAndOneMoreRefuses()
    {
        var prefix = Convert.FromHexString(RequiredZero + "DA01F0FF03");
        var bytes = new byte[Chinese7935ItemInfoView.MaximumPayloadBytes];
        prefix.CopyTo(bytes, 0);
        Assert.Equal(65520, bytes.Length - prefix.Length);
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(bytes, out var view));
        Assert.NotNull(view);
        Assert.Equal(65520, view.CopyData27()!.Length);
        Assert.False(Chinese7935ItemInfoView.TryParseKnownPayload([.. bytes, 0], out view));
        Assert.Null(view);
    }

    [Fact]
    public void EchoTupleRequiresBothOptionalFieldsRatherThanInventingNativeDefaults()
    {
        foreach (var suffix in new[] { "", "38E001", "C00205" })
        {
            Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(RequiredZero + suffix), out var view));
            Assert.NotNull(view);
            Assert.False(view.TryGetEchoRelatedFields(out var tuple));
            Assert.Equal(default, tuple);
        }
    }

    [Fact]
    public void ClientCategoryByteIsExplicitProjectionNotA2350AdmissionRule()
    {
        Assert.True(Chinese7935ItemInfoView.TryParseKnownPayload(Convert.FromHexString(RequiredZero + "38E003C00205"), out var view));
        Assert.NotNull(view);
        Assert.True(view.TryGetEchoRelatedFields(out var tuple));
        Assert.Equal(480u, tuple.CategoryField7);
        Assert.Equal((byte)224, tuple.ClientCategoryByte);
        Assert.Equal(5, tuple.PositionField40);
    }
}
