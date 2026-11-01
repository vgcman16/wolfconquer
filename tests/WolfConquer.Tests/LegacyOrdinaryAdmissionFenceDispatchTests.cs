using System.Diagnostics;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryAdmissionFenceDispatchTests
{
    [Fact]
    public void ActualSavedWriteKeepsOneOwnerBucketAsUnrelatedRetainedDomainsGrow()
    {
        using var target = new OwnerFixture();
        var files = target.Data.Files();
        var bucket = Bucket(target.Roots.Proficiencies!);
        Assert.Equal([(target.Owner, LegacyTradeJournalFileKind.Proficiencies)], Rows(bucket));
        var beforeCount = RegisteredCount();
        long beforeTicks = MeasureSameSavedWrites(target);
        var unrelated = new List<OwnerFixture>();
        try
        {
            for (var index = 0; index < 8; index++)
            {
                var other = new OwnerFixture();
                unrelated.Add(other);
                if (index % 2 == 0) other.Registry.Dispose();
            }
            Assert.True(RegisteredCount() >= beforeCount + unrelated.Count);
            Assert.Same(bucket, Bucket(target.Roots.Proficiencies!));
            Assert.Equal([(target.Owner, LegacyTradeJournalFileKind.Proficiencies)], Rows(bucket));
            Assert.Equal((target.Owner, (LegacyOrdinaryAdmissionUidOwner?)null),
                SessionOwners(target.Roots.Users, target.Roots.Users));
            long afterTicks = MeasureSameSavedWrites(target);
            target.Data.AssertFiles(files);
            foreach (var other in unrelated)
                Assert.Equal([(other.Owner, LegacyTradeJournalFileKind.Proficiencies)],
                    Rows(Bucket(other.Roots.Proficiencies!)));
            // Same genuine saved path, image, registry and writer workload.
            // Timings are observations, not a flaky correctness/speed threshold.
            output.WriteLine($"20 real same-image saves: before={beforeTicks} ticks, after={afterTicks} ticks; " +
                $"frequency={Stopwatch.Frequency}; unrelated owners added={unrelated.Count}; matching owners=1.");
        }
        finally { foreach (var other in unrelated) other.Dispose(); }
    }

    private static long MeasureSameSavedWrites(OwnerFixture fixture)
    {
        var start = Stopwatch.GetTimestamp();
        for (var index = 0; index < 20; index++) fixture.SaveSameProficiencies();
        return Stopwatch.GetTimestamp() - start;
    }

    [Fact]
    public void DisposedOwnerStillDeniesRealWritesAndGenuineReopenReplacesOnlyItsSlot()
    {
        using var first = new OwnerFixture();
        using var second = new OwnerFixture(sharedSpells: first.Roots.Proficiencies);
        var retained = Bucket(first.Roots.Proficiencies!);
        var old = first.Owner;
        Assert.Equal([(old, LegacyTradeJournalFileKind.Proficiencies),
            (second.Owner, LegacyTradeJournalFileKind.Spells)], Rows(retained));
        var files = first.Data.Files();
        first.Registry.Dispose();
        Assert.True(old.IsPending(Uid));
        Assert.Throws<IOException>(first.SaveSameProficiencies);
        first.Data.AssertFiles(files);

        first.Open(first.Roots);
        var current = Bucket(first.Roots.Proficiencies!);
        Assert.NotSame(old, first.Owner);
        Assert.NotSame(retained, current);
        Assert.Equal([(old, LegacyTradeJournalFileKind.Proficiencies),
            (second.Owner, LegacyTradeJournalFileKind.Spells)], Rows(retained));
        Assert.Equal([(first.Owner, LegacyTradeJournalFileKind.Proficiencies),
            (second.Owner, LegacyTradeJournalFileKind.Spells)], Rows(current));
        first.SaveSameProficiencies();
        first.Data.AssertFiles(files);
        LegacyOrdinaryAdmissionMutationFence.Register(first.Owner);
        Assert.Same(current, Bucket(first.Roots.Proficiencies!));

        second.Registry.Dispose();
        Assert.Throws<IOException>(first.SaveSameProficiencies); // Foreign retained shared owner still denies.
        first.Data.AssertFiles(files);
    }

    [Fact]
    public void ActualRegistryInterruptionAfterCleanLookupIsNotCachedAsNoDebt()
    {
        using var fixture = new OwnerFixture();
        fixture.SaveSameProficiencies();
        var bucket = Bucket(fixture.Roots.Proficiencies!);
        Assert.False(fixture.Registry.TryReserve(1, LegacyItemUidInterruption.AfterPendingFlush,
            out var reservation, out var reason));
        Assert.Null(reservation);
        Assert.Contains(LegacyItemUidRegistry.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.True(fixture.Owner.IsPending(Uid));
        var files = fixture.Data.Files();
        Assert.Same(bucket, Bucket(fixture.Roots.Proficiencies!));
        Assert.Throws<IOException>(fixture.SaveSameProficiencies);
        Assert.False(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(null, fixture.Session(), null));
        fixture.Data.AssertFiles(files);
    }

    [Fact]
    public void ReconfiguredOwnerLeavesPriorConfiguredRouteProtectedByLaterActualJournalDebt()
    {
        using var fixture = new OwnerFixture();
        var oldRoot = fixture.Roots.Proficiencies!;
        var newRoot = Path.Combine(fixture.Data.Root, "NewProficiencies");
        Directory.CreateDirectory(newRoot);
        var oldRoots = fixture.Roots;
        var replacement = oldRoots with { Proficiencies = newRoot };
        Assert.Equal(oldRoot, fixture.Owner.Roots.Proficiencies); // with did not mutate cached routes.
        fixture.Open(replacement);
        Assert.Empty(Bucket(oldRoot));
        Assert.Equal([(fixture.Owner, LegacyTradeJournalFileKind.Proficiencies)], Rows(Bucket(newRoot)));
        LegacyOrdinaryAdmissionMutationFence.RequireFileMutation(oldRoots.PathFor(LegacyTradeJournalFileKind.Proficiencies, Uid));
        fixture.Data.StagePendingSnapshotJournal();
        var files = fixture.Data.Files();
        Assert.Throws<IOException>(() => new PlayerProficiencySnapshotLoader(oldRoot)
            .SaveByCharacterUid(Uid, PlayerProficiencySnapshot.Empty));
        Assert.Throws<IOException>(() => LegacyOrdinaryAdmissionMutationFence.RequireFileMutation(
            replacement.PathFor(LegacyTradeJournalFileKind.Proficiencies, Uid)));
        fixture.Data.AssertFiles(files);
    }
}
