using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.World;
using Effect = WolfConquer.Core.World.LegacyNativeMsgRefineEffect.RefineEffects;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Fact]
    public void RefinementContractRetainsAllActualTagsRequiredFieldsByteFlagEnumAndSeventeenLiteralNames()
    {
        var type = typeof(LegacyNativeMsgRefineEffect.RefineEffectProto); Assert.NotNull(type.GetCustomAttribute<ProtoContractAttribute>());
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Id", "dwParam", "Effect" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { 1, 2, 3 }, fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        Assert.All(fields, field => Assert.True(field.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired));
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(Effect))); Assert.True(typeof(Effect).IsDefined(typeof(FlagsAttribute), false));
        Assert.Equal(new[] { "None", "ToxinEraserLevel", "StrikeLockLevel", "LuckyStrike", "CalmWind", "DrainingTouch", "BloodSpawn", "LightOfStamina", "ShiledBreak", "KillingFlash", "MirrorOfSin", "DivineGuard", "CoreStrike", "InvisbleArrow", "FreeSoul", "StraightLife", "AbsoluteLuck" }, Enum.GetNames<Effect>());
        Assert.Equal(Enumerable.Range(0, 17).Select(value => (byte)value), Enum.GetValues<Effect>().Select(value => (byte)value));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(4)] [InlineData(7)] [InlineData(9)] [InlineData(10)]
    [InlineData(11)] [InlineData(13)] [InlineData(15)] [InlineData(16)] [InlineData(255)]
    public Task RefinementWriterUsesActualPinnedProtobufAndResetsDirtyCursorWithoutOverridingMutableRecord(int effect) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var record = new LegacyNativeMsgRefineEffect.RefineEffectProto { Id = uint.MaxValue, dwParam = 17, Effect = (Effect)effect };
        s.Packet.Size = 321; s.Packet.Seek(237); Assert.Same(s.Packet, s.Packet.MsgRefineEffectCreate(record));
        Assert.Equal(Refine(uint.MaxValue, 17, (byte)effect), NativeSpellTestFixture.Bytes(s.Packet)); Assert.Equal(4, s.Packet.Position);
        Assert.Equal((Effect)effect, record.Effect); Assert.Equal(uint.MaxValue, record.Id); Assert.Equal(17u, record.dwParam);
        s.Packet.MsgRefineEffectCreate(new()); Assert.Equal(Refine(0, 0, 0), NativeSpellTestFixture.Bytes(s.Packet));
    });

    [Theory]
    [InlineData(0u)] [InlineData(1u)] [InlineData(2147483647u)] [InlineData(2147483648u)] [InlineData(4294967295u)]
    public Task AzureShieldBlueDamageUsesActualTargetViewAndSignedDamageWidthWithoutChangingHpOrFlags(uint damage) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var queue = new Queue<byte[]>(); var target = s.Peer(queue);
        LegacyNativeAzureShield.CreateDmg(s.Player, target.Player, damage);
        Assert.Equal(Blue(17, 21, 12, 22, damage), Assert.Single(queue)); Assert.Empty(s.Queue);
        Assert.Equal(0, s.Player.HitPoints); Assert.Equal(0, target.Player.HitPoints); Assert.Empty(target.Player.BitVector.GetFlags());
    });

    [Fact]
    public Task AzureShieldRoutesViaForeignViewOwnerButReadsPassedTargetIdentityCoordinatesAndPlayerIdentity()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var targetQueue = new Queue<byte[]>(); var target = s.Peer(targetQueue);
        var foreignQueue = new Queue<byte[]>(); var foreign = s.Peer(foreignQueue, 99); target.Player.View = foreign.Player.View;
        LegacyNativeAzureShield.CreateDmg(s.Player, target.Player, uint.MaxValue);
        Assert.Equal(Blue(17, 21, 12, 22, uint.MaxValue), Assert.Single(foreignQueue)); Assert.Empty(targetQueue); Assert.Empty(s.Queue);
        target.Player.View = null!; Assert.Throws<NullReferenceException>(() => LegacyNativeAzureShield.CreateDmg(s.Player, target.Player, 1));
        Assert.Single(foreignQueue);
    });
}
