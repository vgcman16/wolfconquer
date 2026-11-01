using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    [Fact]
    public Task SubsequentRealLethalPhysicalHitRemovesTheEarnedNativeFlashingRow()
    {
        var inputs = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(true, 17_490, false);
        // Saved inputs before genuine bootstrap/admission. A living one-HP
        // caster earns flashing through the proc; its peer then uses ordinary
        // Physical with real Strength1000 and its existing empty gear snapshot.
        var victim = inputs.Actor with { Character = inputs.Actor.Character with { MinHitPoints = 1 } };
        var killer = inputs.Target with { Character = inputs.Target.Character with { Strength = 1_000 } };
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearAsync(victim, inputs.World, async f =>
        {
            Assert.Equal(1, f.Current(Actor).Character.MinHitPoints);
            Assert.Empty(f.Current(Target).CombatGear!.Items);
            var victimBinding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current(Actor).NativeGameClient!.OrdinarySpellBinding);
            var killerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current(Target).NativeGameClient!.OrdinarySpellBinding);
            var earliest = DateTime.Now;
            await f.SendAsync(Actor, Request());
            AssertTimedFlashing(f.Current(Actor), earliest, DateTime.Now);
            Assert.Equal(1, f.Current(Actor).Character.MinHitPoints);
            Assert.Equal(99_709, f.Current(Target).Character.MinHitPoints);
            var statusRows = victimBinding.Player.BitVector.GetFlags();
            Assert.True(Assert.Single(statusRows, row => row.Key == Flashing).RemoveOnDead);
            await f.SendAsync(Target, new MsgAttackPacket(new MsgAttackQuery(Uid: Target, OpponentUid: Actor,
                X: 830, Y: 584, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes());
            var dead = f.Current(Actor);
            Assert.True(dead.Character.MinHitPoints == 0, f.Log.ToString());
            Assert.Equal(0, victimBinding.Player.HitPoints);
            Assert.True(Flag(dead, LegacyPlayerDeathState.DeadStatusFlag));
            AssertNoFlashing(dead);
            Assert.DoesNotContain(victimBinding.Player.BitVector.GetFlags(), row => row.Key == Flashing);
            var loaded = f.Data.Loader.TryLoadByAccountId(Actor);
            Assert.True(loaded.Succeeded);
            var saved = Assert.IsType<CharacterSnapshot>(loaded.Snapshot);
            Assert.Equal(0, saved.MinHitPoints);
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(saved.Attributes, Flashing));
            Assert.False(victimBinding.Client.OnAutoAttack);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(victimBinding));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(killerBinding));
            Assert.False(f.Recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }, killer);
    }
}
