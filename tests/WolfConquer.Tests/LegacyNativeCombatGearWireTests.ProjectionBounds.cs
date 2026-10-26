using WolfConquer.Core.World;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearWireTests
{
    [Fact]
    public void ActualSerializerAndFixedProjectionPreserveExactMaximumObjectFrame()
    {
        using var scope = new NativeCombatGearQueryScope();
        // Five required scalars produce11 bytes with UID128; each unpacked U0
        // contributes30 00.11 +32760*2 =65531 payload, plus4 header =65535.
        var values = new uint[32760];
        var message = new Opt { UID = 128, U = values };
        var payload = new byte[65531];
        Convert.FromHexString("0800100018002080012800").CopyTo(payload, 0);
        for (var index = 11; index < payload.Length; index += 2) { payload[index] = 0x30; payload[index + 1] = 0; }

        var actual = LegacyNativeCombatGearWire.EncodeObject(message, 2421);
        var projected = LegacyNativeCombatGearWire.Encode(message, 2421);

        AssertBoundedFrame(actual, payload, 2421);
        Assert.Equal(actual, projected);
        Assert.Same(values, message.U); Assert.All(values, value => Assert.Equal(0U, value));
        Assert.Equal(128U, message.UID);
        Assert.Null(message.SelectMaterial);
    }

    [Fact]
    public void ActualSerializerAndFixedProjectionRefuseOversizedObjectWithoutChangingMessage()
    {
        using var scope = new NativeCombatGearQueryScope();
        var values = new uint[32761]; //65533 payload bytes, before frame/seal.
        var message = new Opt { UID = 128, U = values };
        Assert.Equal("stream", Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyNativeCombatGearWire.EncodeObject(message, 2421)).ParamName);
        Assert.Equal("stream", Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyNativeCombatGearWire.Encode(message, 2421)).ParamName);
        Assert.Same(values, message.U); Assert.All(values, value => Assert.Equal(0U, value));
        Assert.Equal(128U, message.UID);
        Assert.Equal(Opt.Types.UPGRADE, message.Type); Assert.Equal(0U, message.Transform); Assert.Equal(0U, message.Exp);
        Assert.Null(message.SelectMaterial);
    }
}
