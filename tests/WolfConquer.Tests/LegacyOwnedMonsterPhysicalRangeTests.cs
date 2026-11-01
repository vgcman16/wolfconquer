using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedMonsterPhysicalRangeTests
{
    [Theory]
    [InlineData(1, 18)] [InlineData(2, 12)]
    public Task ActualRetainedTargetPhaseOwnsOnlyBossOneEighteenAndKeepsItAcrossDamageAndRespawn(int boss, int expected) =>
        RunRangeAsync(boss, async f =>
        {
            Assert.Equal(12, f.ReadRange());
            Assert.True(f.States.TryGetTargetLockState(f.Birth, f.Clock.GetUtcNow(), out var initial));
            Assert.Null(initial.LastTargetUid);
            await f.Phase("SendDueMonsterAttacksAsync");
            Assert.True(f.States.TryGetTargetLockState(f.Birth, f.Clock.GetUtcNow(), out var acquired));
            Assert.Equal(OwnedEquipmentBehaviorFixture.Actor, acquired.LastTargetUid);
            Assert.Equal(12, f.ReadRange()); // Acquisition alone does not mint18.
            Assert.Equal((ushort)449, f.Visible().X); Assert.Equal((ushort)380, f.Visible().Y);
            f.Clock.Advance(TimeSpan.FromSeconds(1));
            await f.Phase("SendDueMonsterAttacksAsync");
            Assert.Equal(expected, f.ReadRange());
            // The target starts20 tiles away. Both real AI passes remain in
            // RetainTarget/move, so no monster-hit writer is being claimed.
            Assert.Equal((ushort)448, f.Visible().X); Assert.Equal((ushort)380, f.Visible().Y);
            var damage = f.States.ApplyDamage(f.Visible(), 750_001, f.Clock.GetUtcNow());
            Assert.False(damage.Killed); Assert.Equal(1_749_999U, damage.RemainingHitpoints);
            Assert.Equal(expected, f.ReadRange());
            // Component damage is a real store lifecycle API, explicitly not
            // an assertion about complete player combat-kill authorization.
            Assert.True(f.States.ApplyDamage(f.Visible(), uint.MaxValue, f.Clock.GetUtcNow()).Killed);
            Assert.False(f.Owner.Store.TryReadMonsterPhysicalRange(f.Birth, f.Family, out _));
            Assert.False(f.States.TryGetVisibleInstance(f.Birth, f.Clock.GetUtcNow(), out _));
            f.Clock.Advance(TimeSpan.FromSeconds(8));
            await f.Phase("SendDueMonsterDeathRemovalsAsync");
            f.Clock.Advance(TimeSpan.FromHours(1));
            await f.Phase("SendDueMonsterRespawnsAsync");
            var respawned = f.Visible(); Assert.Equal(f.Birth.MaxLife, respawned.Hitpoints);
            Assert.Equal(f.Birth.Uid, respawned.Uid); Assert.Equal(f.Birth.X, respawned.X); Assert.Equal(f.Birth.Y, respawned.Y);
            Assert.Equal(expected, f.ReadRange()); f.AssertPlayerUnchanged();
        });

    [Fact]
    public Task UnknownPhysicalRangeReadsCannotRegisterABirthOrTargetLock() => RunRangeAsync(1, f =>
    {
        var unknown = f.Birth with { Uid = uint.MaxValue }; // Declared negative DATA only.
        Assert.False(f.States.TryGetTargetLockState(unknown, f.Clock.GetUtcNow(), out _));
        for (int attempt = 0; attempt < 2; attempt++)
            Assert.False(f.Owner.Store.TryReadMonsterPhysicalRange(unknown, f.Family, out _));
        Assert.False(f.States.TryGetTargetLockState(unknown, f.Clock.GetUtcNow(), out _));
        Assert.False(f.States.TryGetDynamicMonsterByUid(Map, Dynamic, unknown.Uid, out _, out _));
        Assert.Single(f.States.GetVisibleDynamicMonsters(Map, Dynamic, f.Clock.GetUtcNow()));
        Assert.Equal(12, f.ReadRange()); Assert.Empty(f.Owner.ActorFrames); f.AssertPlayerUnchanged();
        return Task.CompletedTask;
    });

    [Fact]
    public Task AdoptedFamilyDriftCannotBecomeARefreshedLiveRangeOrAiEvent() => RunRangeAsync(1, async f =>
    {
        Assert.Equal(12, f.ReadRange());
        var attributes = Assert.IsType<Dictionary<string, string>>(f.Family.Attributes);
        string original = attributes["attack_range"];
        try
        {
            attributes["attack_range"] = "51"; // Declared negative adopted-row mutation.
            Assert.False(f.Owner.Store.TryReadMonsterPhysicalRange(f.Birth, f.Family, out _));
            await f.Phase("SendDueMonsterAttacksAsync");
            Assert.True(f.States.TryGetTargetLockState(f.Birth, f.Clock.GetUtcNow(), out var state));
            Assert.Null(state.LastTargetUid); Assert.Null(state.LastAttackAt);
            Assert.False(f.Owner.Store.TryReadMonsterPhysicalRange(f.Birth, f.Family, out _));
            Assert.Empty(f.Owner.ActorFrames); f.AssertPlayerUnchanged();
        }
        finally { attributes["attack_range"] = original; }
    });
}
