using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;
    private const uint Target = OwnedEquipmentBehaviorFixture.Peer;
    private const int Flashing = 0, FrontBreak = 313;

    private static Task Run(Func<OwnedEquipmentBehaviorFixture, Task> exercise)
    {
        var inputs = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(false, 17_490, false);
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearAsync(inputs.Actor, inputs.World, exercise, inputs.Target);
    }

    private static byte[] Request() => new MsgAttackPacket(new MsgAttackQuery(Uid: Actor,
        OpponentUid: Target, X: 831, Y: 584, AttackType: MsgAttackType.Physical,
        Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes();

    private static bool Flag(WorldSession session, int flag) =>
        LegacyPlayerDeathState.HasStatusFlag(session.Character.Attributes, flag);

    private static bool WireFlag(byte[] bytes, uint uid, int flag) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == uid && packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag &&
            row.Values is { } words && words.Count > flag / 32 && (words[flag / 32] & (1UL << (flag % 32))) != 0);

    private static bool Hp(byte[] bytes, uint uid) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == uid && packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Hitpoints);

    private static bool Animation(byte[] bytes) =>
        LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.SpellId == 17_490;

    private static void AssertTimedFlashing(WorldSession current, DateTime earliest, DateTime latest)
    {
        Assert.True(Flag(current, Flashing));
        Assert.True(LegacyCanAttackFlashingRegistration.TryRead(current, out var registration));
        Assert.Equal(current.Character.Uid, registration.OwnerUid);
        var vector = current.NativeGameClient!.Player.BitVector;
        Assert.True(vector.TryContainFlag(Flashing, out var nativePresent));
        Assert.True(nativePresent);
        var native = Assert.Single(vector.GetFlags(), row => row.Key == Flashing);
        Assert.Equal(30, native.Secounds);
        Assert.Equal(0, native.InvokerSecouds);
        Assert.True(native.RemoveOnDead);
        Assert.Equal(registration.Timer, native.Timer);
        Assert.InRange(native.Timer.Ticks, earliest.Ticks, latest.Ticks);
        Assert.False(native.Expire(native.Timer.AddSeconds(30).AddTicks(-1)));
        Assert.True(native.Expire(native.Timer.AddSeconds(30)));
    }

    private static void AssertNoFlashing(WorldSession current)
    {
        Assert.False(Flag(current, Flashing));
        Assert.False(LegacyCanAttackFlashingRegistration.TryRead(current, out _));
        Assert.True(current.NativeGameClient!.Player.BitVector.TryContainFlag(Flashing, out var nativePresent));
        Assert.False(nativePresent);
    }
}
