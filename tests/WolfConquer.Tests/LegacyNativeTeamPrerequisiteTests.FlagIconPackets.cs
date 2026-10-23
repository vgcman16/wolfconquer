using ProtoBuf;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Icon = WolfConquer.Core.World.LegacyNativeMsgFlagIcon.ShowIcon;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeamPrerequisiteTests
{
    [Theory]
    [InlineData(1u)] [InlineData(2u)] [InlineData(3u)] [InlineData(4u)] [InlineData(5u)]
    [InlineData(6u)] [InlineData(7u)] [InlineData(8u)] [InlineData(9u)] [InlineData(10u)]
    public Task FlagIconCreateUsesEveryNativeIconAndExactRequiredFieldWire(uint icon) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Same(scope.Packet, scope.Packet.FlagIconCreate(0, (Icon)icon, 0, uint.MaxValue));
        Assert.Equal(AuraWire(3, 0, icon, 0, 30, uint.MaxValue), NativeSpellTestFixture.Bytes(scope.Packet));
        scope.Packet.Seek(237); scope.Packet.GetFlagIcon(out var proto); //Native deserializer resets to header0.
        Assert.Equal(3u, proto.Type); Assert.Equal(0u, proto.UID); Assert.Equal((Icon)icon, proto.Icon);
        Assert.Equal(0u, proto.Level); Assert.Equal(30u, proto.Mode); Assert.Equal(uint.MaxValue, proto.Damage);
        Assert.Equal(scope.Packet.Size, scope.Packet.Position);
    });

    [Fact]
    public Task FlagIconHandlerEchoesUntrustedPayloadUidAndOverwritesTypeModeInNativeOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var request = AuraWire(99, 987654, (uint)Icon.NobleSpirit, 255, 123456, 0);
        using var input = new LegacyNativePacket(request);
        input.Seek(77);
        LegacyNativeMsgFlagIcon.MsgFlagIconHandler(scope.Client, input);
        Assert.Equal(AuraWire(3, 987654, (uint)Icon.NobleSpirit, 255, 30, 0), Assert.Single(scope.Queue));
        Assert.Equal(17u, scope.Player.UID); //No reassignment or authority inference from echo UID.
        Assert.Equal(AuraWire(3, 987654, (uint)Icon.NobleSpirit, 255, 30, 0), NativeSpellTestFixture.Bytes(input));
    });

    [Fact]
    public Task FlagIconRequiredZeroScalarsAreSerializedRatherThanDropped() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.InitWriter();
        scope.Packet.ProtoBufferSerialize(new LegacyNativeFlagIconPacket.MsgAuraProto { Icon = Icon.MetalAura });
        scope.Packet.Finalize(2175);
        Assert.Equal(AuraWire(0, 0, (uint)Icon.MetalAura, 0, 0, 0), NativeSpellTestFixture.Bytes(scope.Packet));
    });

    [Fact]
    public Task FlagIconGetAssignsBornOutOwnerBeforeTruncatedProtobufFaultAndDoesNotSend() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var malformed = new LegacyNativePacket(new byte[] { 5, 0, 127, 8, 128 });
        LegacyNativeFlagIconPacket.MsgAuraProto? result = null;
        Assert.ThrowsAny<Exception>(() => malformed.GetFlagIcon(out result));
        Assert.NotNull(result); Assert.Equal(0u, result!.UID); Assert.Equal(0u, result.Type);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public void AuraMetadataUsesSixRequiredUIntScalarsAndLiteralIconValues()
    {
        Assert.Equal(1, System.Runtime.InteropServices.Marshal.SizeOf<LegacyNativeMsgFlagIcon>());
        Assert.Equal(Enumerable.Range(1, 10).Select(value => (uint)value), Enum.GetValues<Icon>().Select(value => (uint)value));
        var fields = typeof(LegacyNativeFlagIconPacket.MsgAuraProto).GetFields();
        Assert.Equal(6, fields.Length);
        Assert.Equal(Enumerable.Range(1, 6), fields.Select(field => ((ProtoMemberAttribute)Attribute.GetCustomAttribute(field, typeof(ProtoMemberAttribute))!).Tag));
        Assert.All(fields, field => Assert.True(((ProtoMemberAttribute)Attribute.GetCustomAttribute(field, typeof(ProtoMemberAttribute))!).IsRequired));
    }
}
