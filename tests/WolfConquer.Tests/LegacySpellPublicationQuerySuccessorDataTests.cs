using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// This tests immutable packet-selection DATA only, not the runtime successor
// owner. Runtime advancement requires an actual completed startup query.
public sealed class LegacySpellPublicationQuerySuccessorDataTests
{
    private static readonly PlayerSpellSnapshotEntry Startup = new(1110, 0, 0, 0, 0, 0);
    private static readonly PlayerSpellSnapshotEntry Learned = new(1000, 2, 12, 0, 0, 0);

    [Fact]
    public void DerivedBookChangeRetainsOnlyOriginalStartupGrantBytes()
    {
        var before = new PlayerSpellSnapshot(new[] { Learned, Startup });
        Assert.True(LegacySpellPublicationPlan.TryPrepare(before, new HashSet<ushort> { 1110 }, out var initial));
        var plan = Assert.IsType<LegacySpellPublicationPlan>(initial);
        var after = new PlayerSpellSnapshot(new[] { Learned, Startup, new PlayerSpellSnapshotEntry(20_000, 3, 0, 0, 0, 0) });
        Assert.True(plan.TryPrepareQuerySuccessor(before, after, out var projected));
        var successor = Assert.IsType<LegacySpellPublicationPlan>(projected);
        Assert.True(plan.Matches(before)); Assert.False(plan.Matches(after));
        Assert.True(successor.Matches(after)); Assert.False(successor.Matches(before));
        Assert.Single(successor.Frames);
        Assert.Equal(LegacySpellPacket.FromSnapshot(Startup).ToFrameBytes(), successor.Frames[0]);
        Assert.Equal(plan.Frames[0], successor.Frames[0]);
    }

    [Fact]
    public void ChangedSelectedGrantCopiedSourceOrTamperedFrameCannotBecomeADataSuccessor()
    {
        var before = new PlayerSpellSnapshot(new[] { Startup });
        Assert.True(LegacySpellPublicationPlan.TryPrepare(before, new HashSet<ushort> { 1110 }, out var initial));
        var plan = Assert.IsType<LegacySpellPublicationPlan>(initial);
        var changed = new PlayerSpellSnapshot(new[] { Startup with { Level = 1 } });
        Assert.False(plan.TryPrepareQuerySuccessor(before, changed, out _));
        Assert.False(plan.TryPrepareQuerySuccessor(new PlayerSpellSnapshot(new[] { Startup }), before, out _));
        plan.Frames[0][4] ^= 1; // Declared corrupted DATA, never a live publication.
        Assert.False(plan.TryPrepareQuerySuccessor(before, before, out _));
    }
}
