using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class D13RandomDailyTaskPacketTests
{
    // Closed managed codec policy. These cases are not claims that native
    // protobuf rejects every malformed or unsupported sequence the same way.
    [Theory]
    [InlineData("")]
    [InlineData("1000")]
    [InlineData("1000180030004001")]
    [InlineData("00")]
    [InlineData("08")]
    [InlineData("0880")]
    [InlineData("088080808010")]
    [InlineData("080608")]
    [InlineData("080600")]
    [InlineData("080610")]
    [InlineData("08061080")]
    [InlineData("0806108080808010")]
    [InlineData("0806188080808010")]
    [InlineData("0806208080808010")]
    [InlineData("0806288080808010")]
    [InlineData("0806388080808010")]
    [InlineData("0806408080808010")]
    [InlineData("0806308080808010")]
    [InlineData("080630FFFFFFFFFFFFFFFF7F")]
    [InlineData("080630FFFFFFFFF7FFFFFFFF01")]
    [InlineData("080630FFFFFFFFFFFFFFFFFF02")]
    [InlineData("08063080808080808080808080")]
    [InlineData("080642")]
    [InlineData("08064202FF")]
    [InlineData("08064201FF")]
    [InlineData("080642058080808010")]
    [InlineData("080642050080808080")]
    [InlineData("080642FFFFFFFFFFFFFFFFFF01")]
    [InlineData("08064280808080808080808002")]
    [InlineData("080688808080800101")]
    public void TruncatedOverflowingOrMissingRequiredInputReturnsNoPartialModelAndPreservesInput(string hex)
    {
        var input = Convert.FromHexString(hex);
        var before = input.ToArray();
        D13RandomDailyTaskPacket? packet = new(6);
        Assert.False(D13RandomDailyTaskPacket.TryParseKnownPayload(input, out packet));
        Assert.Null(packet);
        Assert.Equal(before, input);
    }

    [Theory]
    [InlineData("0A00")]
    [InlineData("110000000000000000")]
    [InlineData("1A00")]
    [InlineData("2500000000")]
    [InlineData("2A00")]
    [InlineData("3200")]
    [InlineData("3A00")]
    [InlineData("4500000000")]
    public void KnownFieldsWithWrongWireTypesRefuseRatherThanLosingTheirPresence(string suffix)
    {
        var bytes = Convert.FromHexString("0806" + suffix);
        var before = bytes.ToArray();
        D13RandomDailyTaskPacket? packet = new(6);
        Assert.False(D13RandomDailyTaskPacket.TryParseKnownPayload(bytes, out packet));
        Assert.Null(packet);
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData("4801")]
    [InlineData("490000000000000000")]
    [InlineData("4A00")]
    [InlineData("4D00000000")]
    [InlineData("4B4C")]
    [InlineData("4C")]
    public void UnknownExtensionFieldsRefuseUnderTheExplicitClosedCodecPolicy(string suffix)
    {
        // Native has an unknown-field path. This deliberately tests the narrow
        // known-schema API's refusal policy, not native extension equivalence.
        var bytes = Convert.FromHexString("0806" + suffix);
        var before = bytes.ToArray();
        D13RandomDailyTaskPacket? packet = new(6);
        Assert.False(D13RandomDailyTaskPacket.TryParseKnownPayload(bytes, out packet));
        Assert.Null(packet);
        Assert.Equal(before, bytes);
    }
}
