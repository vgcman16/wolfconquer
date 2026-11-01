using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFloorDropTests
{
    [Theory]
    [InlineData((ushort)17_490, true)]
    [InlineData((ushort)17_650, false)]
    public Task ActualAllocatedUidCollisionWithExpiredVisibleEntryRetainsEntryAndFencesCommittedPrefix(
        ushort spellId, bool money) => Run(spellId, money, walkable: true, async f =>
    {
        uint beforeUid = FloorUid(), conflictingUid = unchecked(beforeUid + 1U);
        var created = f.Clock.GetUtcNow() - FloorItemRuntimeStateStore.LegacyLifetime;
        // Legitimate manual-visible API DATA chooses the next observed identity.
        // Do not reset or call the real issuer and do not mutate its counter.
        var conflicting = f.Seed(conflictingUid, 11, 10, created);
        Assert.Equal(beforeUid, FloorUid());
        Assert.Same(conflicting, Assert.Single(f.Floor.SnapshotVisible(created)));
        var files = ExpectedFiles(f, reward: false);
        await f.Attack();
        AssertFenced(f);
        Assert.Equal(conflictingUid, FloorUid());
        Assert.Empty(FloorPackets(f));
        // A now-time query would itself expire the row, concealing an overwrite
        // or illegal removal by the producer. Observe its original-time view.
        Assert.Same(conflicting, Assert.Single(f.Floor.SnapshotVisible(created)));
        Assert.True(f.Floor.TryGetVisibleEntry(conflictingUid, created, out var retained));
        Assert.Same(conflicting, retained);
        f.Owner.Data.AssertFiles(files);
        await AssertReplayFenced(f, files, conflictingUid);
        Assert.Same(conflicting, Assert.Single(f.Floor.SnapshotVisible(created)));
    });

    private static async Task AssertReplayFenced(FloorFixture f, Dictionary<string, byte[]> expectedFiles, uint expectedUid)
    {
        int outputCount = f.Owner.ActorFrames.Count;
        var committedStamp = f.Binding.Player.AttackStamp;
        f.Clock.Advance(TimeSpan.FromMilliseconds(700));
        await f.Owner.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
        await f.RawAttack();
        Assert.Equal(outputCount, f.Owner.ActorFrames.Count);
        Assert.Equal(expectedUid, FloorUid());
        Assert.Equal(committedStamp, f.Binding.Player.AttackStamp);
        Assert.False(f.Binding.Client.OnAutoAttack); Assert.True(f.Binding.IsRetired);
        Assert.False(f.Monsters.TryGetVisibleInstance(f.Monster, f.Clock.GetUtcNow(), out _));
        f.Owner.Data.AssertFiles(expectedFiles);
    }
}
