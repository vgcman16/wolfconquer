using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedDragonhowlWornSuccessorTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Target = OwnedEquipmentBehaviorFixture.Peer;
    private const int Flashing = 0, FrontBreak = 313;

    private static Task Run(bool targetRune, Func<OwnedEquipmentBehaviorFixture, Task> exercise)
    {
        var input = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(targetRune, 17_490, false);
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearAsync(input.Actor, input.World, exercise, input.Target);
    }

    private static byte[] Request() => new MsgAttackPacket(new MsgAttackQuery(Uid: Actor, OpponentUid: Target,
        X: 831, Y: 584, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
        ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes();

    private static bool WireFlag(byte[] bytes, int flag) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) && update.Uid == Actor &&
        update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.StatusFlag && row.Values is { } words &&
            words.Count > flag / 32 && (words[flag / 32] & (1UL << (flag % 32))) != 0);

    private static bool Stage(byte[] bytes, string stage) => stage == "flashing"
        ? WireFlag(bytes, Flashing) && !WireFlag(bytes, FrontBreak) : WireFlag(bytes, FrontBreak);

    private static LegacyNativeGameItemRuntime WornAlias(LegacyOrdinarySpellBinding binding)
    {
        var alias = Assert.Single(binding.Items.Rune.Objects);
        Assert.Equal((ushort)103, alias.Position);
        Assert.Contains(binding.ItemSnapshot.RuneItems, item => item.Uid == alias.Uid && item.ItemId == alias.ITEM_ID);
        return alias;
    }

    private static bool IsAnimation(byte[] bytes) => LegacySpellAnimationPacket.TryParse(
        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) && animation.SpellId == 17_490;

    private static bool IsTargetHp(byte[] bytes) => LegacyMsgUpdatePacket.TryParse(
        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) && update.Uid == Target &&
        update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Hitpoints);

    private static void AssertFencedBeforeReceive(OwnedEquipmentBehaviorFixture f,
        LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding target,
        Dictionary<string, byte[]> filesAtObservation, DateTime committedStamp, DateTime targetStamp,
        int actorStart, int targetStart)
    {
        Assert.True(f.Recovery.IsBlocked);
        Assert.True(actor.IsRetired); Assert.True(target.IsRetired);
        Assert.False(f.Store.TryGet(f.Connection(Actor), out _));
        Assert.False(f.Store.TryGet(f.Connection(Target), out _));
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(actor)); Assert.False(f.Store.IsBoundSpellOwnerCurrent(target));
        Assert.Equal(committedStamp, actor.Player.AttackStamp); Assert.Equal(targetStamp, target.Player.AttackStamp);
        Assert.Equal(100_000, actor.Player.HitPoints); Assert.Equal(100_000, target.Player.HitPoints);
        Assert.Equal(500, (int)actor.Player.Mana); Assert.Equal(100, (int)actor.Player.Stamina);
        Assert.False(actor.Client.OnAutoAttack);
        Assert.DoesNotContain(f.ActorFrames.Skip(actorStart), IsAnimation);
        Assert.DoesNotContain(f.Frames(Target).Skip(targetStart), IsTargetHp);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        f.Data.AssertFiles(filesAtObservation);
    }

    private static async Task AssertRestoredDataCannotReviveTheRetiredCast(OwnedEquipmentBehaviorFixture f,
        LegacyOrdinarySpellBinding actor, LegacyOrdinarySpellBinding target, Dictionary<string, byte[]> files)
    {
        var outputs = f.ActorFrames.Count + f.Frames(Target).Count;
        await f.Handler.HandleFrameAsync(f.Connection(Actor), OwnedEquipmentBehaviorFixture.ReadFrame(Request()));
        Assert.Equal(outputs, f.ActorFrames.Count + f.Frames(Target).Count);
        Assert.True(actor.IsRetired); Assert.True(target.IsRetired);
        Assert.False(actor.Client.OnAutoAttack); Assert.Equal(100_000, target.Player.HitPoints);
        Assert.False(f.Store.TryGet(f.Connection(Actor), out _));
        Assert.False(f.Store.TryGet(f.Connection(Target), out _));
        f.Data.AssertFiles(files);
    }
}
