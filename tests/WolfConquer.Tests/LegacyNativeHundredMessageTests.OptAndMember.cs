using System.Reflection;
using WolfConquer.Core.World;
using Opt = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsOpt;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredMessageTests
{
    [Fact]
    public Task OptEncoderUsesSameStreamAndPreservesSignedAndFullWidthOptionalFieldsWithoutMutatingSource() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var exchange = new Opt.ExchangePositions { Position1 = int.MinValue, Position2 = int.MaxValue };
            var value = new Opt.MsgHundredWeaponsOptProto { Type = Opt.ActionID.Trigger, WeaponSubtype = Weapon.Sword,
                Position = int.MinValue, Combination = ulong.MaxValue, TriggeredStage = byte.MaxValue,
                TriggeringDuration = int.MinValue, Exchange = exchange };
            Assert.Same(scope.Packet, scope.Packet.CreateHundredWeaponsOpt(value));
            var decoded = scope.Read<Opt.MsgHundredWeaponsOptProto>(2353);
            Assert.NotSame(value, decoded); Assert.NotSame(exchange, decoded.Exchange);
            Assert.Equal(Opt.ActionID.Trigger, decoded.Type); Assert.Equal(Weapon.Sword, decoded.WeaponSubtype);
            Assert.Equal(int.MinValue, decoded.Position); Assert.Equal(ulong.MaxValue, decoded.Combination);
            Assert.Equal(byte.MaxValue, decoded.TriggeredStage); Assert.Equal(int.MinValue, decoded.TriggeringDuration);
            Assert.Equal(int.MinValue, decoded.Exchange.Position1); Assert.Equal(int.MaxValue, decoded.Exchange.Position2);
            Assert.Same(exchange, value.Exchange); Assert.Equal(int.MinValue, value.Position);
            Assert.Equal(int.MinValue, exchange.Position1); Assert.Equal(int.MaxValue, exchange.Position2);
        });

    [Fact]
    public Task OptDefaultAndNullObjectsRemainDistinctWireShapesButDecodeToFreshNativeDefaults() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var value = new Opt.MsgHundredWeaponsOptProto();
            scope.Packet.CreateHundredWeaponsOpt(value); int requiredLength = scope.Packet.Size;
            var decoded = scope.Read<Opt.MsgHundredWeaponsOptProto>(2353); Assert.NotSame(value, decoded);
            Assert.Equal(Opt.ActionID.Equip, decoded.Type); Assert.Equal(0UL, decoded.Combination);
            Assert.Equal(0, decoded.Position); Assert.Equal((byte)0, decoded.TriggeredStage); Assert.Null(decoded.Exchange);
            scope.Packet.CreateHundredWeaponsOpt(null!); Assert.Equal(4, scope.Packet.Size); Assert.True(requiredLength > 4);
            var empty = scope.Read<Opt.MsgHundredWeaponsOptProto>(2353); Assert.NotSame(decoded, empty);
            Assert.Equal(0, empty.TriggeringDuration); Assert.Null(empty.Exchange);
            Assert.Throws<NullReferenceException>(() => Opt.CreateHundredWeaponsOpt(null!, value));
        });

    [Fact]
    public void UnwiredLoadCharacterSlotCreatesOneGenuineOwnerOnlyWhenPhysicalFieldIsNull()
    {
        var client = new LegacyNativeGameClientRuntime(); Assert.Null(client.HundredWeapons);
        LegacyNativeHundredWeaponsMemberPhases.AssignHundredWeaponsAtNativeLoadCharacterSlot(client);
        var owner = client.HundredWeapons!; Assert.NotNull(owner);
        Assert.Same(client, typeof(LegacyNativeHundredWeapons).GetField("Owner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner));
        Assert.Empty(owner.Objects); Assert.Empty(owner.TODO_Spells); Assert.False(owner.Unlocked);
        Assert.Equal(new byte[] { 2, 3 }, owner.TriggerStamp.Keys); Assert.Equal(new byte[] { 2, 3 }, owner.TriggerSeconds.Keys);
        Assert.All(owner.TriggerSeconds.Values, seconds => Assert.Equal((byte)0, seconds));
        Assert.All(owner.TriggerStamp.Values, stamp => Assert.Equal(0, stamp.Value));
        LegacyNativeHundredWeaponsMemberPhases.AssignHundredWeaponsAtNativeLoadCharacterSlot(client);
        Assert.Same(owner, client.HundredWeapons);
    }

    [Fact]
    public void ExistingOwnerAndAllItsEscapedStateSurviveConditionalSlotWithoutRebindingItsOriginalClient()
    {
        var client = new LegacyNativeGameClientRuntime(); var foreign = new LegacyNativeGameClientRuntime();
        var owner = new LegacyNativeHundredWeapons(foreign) { Unlocked = true, Objects = null!, TODO_Spells = null!,
            TriggerStamp = null!, TriggerSeconds = null! }; client.HundredWeapons = owner;
        LegacyNativeHundredWeaponsMemberPhases.AssignHundredWeaponsAtNativeLoadCharacterSlot(client);
        Assert.Same(owner, client.HundredWeapons); Assert.True(owner.Unlocked); Assert.Null(owner.Objects);
        Assert.Null(owner.TODO_Spells); Assert.Null(owner.TriggerStamp); Assert.Null(owner.TriggerSeconds);
        Assert.Same(foreign, typeof(LegacyNativeHundredWeapons).GetField("Owner", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner));
        Assert.Throws<NullReferenceException>(() => LegacyNativeHundredWeaponsMemberPhases.AssignHundredWeaponsAtNativeLoadCharacterSlot(null!));
    }
}
