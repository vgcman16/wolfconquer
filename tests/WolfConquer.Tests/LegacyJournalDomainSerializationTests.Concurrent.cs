using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyJournalDomainSerializationTests
{
    [Fact]
    public async Task SameUsersDisjointV2V8V9V11WritersAndPendingProbesSerializeWithoutFalseRecovery()
    {
        // Real journal/files only. No runtime/registry owner is opened and no
        // permission to replay an active World's files is manufactured.
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var sale = SeedOwner(data, 1); var autosave = SeedOwner(data, 2); var itemSpell = SeedOwner(data, 3);
        var first = SeedOwner(data, 4, 40); var second = SeedOwner(data, 5, 100);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var start = new Barrier(5);
        var remaining = 4;
        Task OwnerWriter(OwnerFiles owner, int version) => Task.Run(() =>
        {
            start.SignalAndWait(cancellation.Token);
            try
            {
                for (var index = 0; index < 24; index++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    Assert.True(CommitOwner(data, owner, version, out var reason), reason);
                }
            }
            finally { Interlocked.Decrement(ref remaining); }
        });
        var tasks = new[]
        {
            OwnerWriter(sale, 2), OwnerWriter(autosave, 8), OwnerWriter(itemSpell, 9),
            Task.Run(() =>
            {
                start.SignalAndWait(cancellation.Token);
                try
                {
                    for (var index = 0; index < 24; index++)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        Assert.True(LegacyTradeJournal.TryCommitCharacterResources(data.Users,
                            PairChanges(first, second, reverse: index % 2 != 0), out var reason), reason);
                    }
                }
                finally { Interlocked.Decrement(ref remaining); }
            }),
            Task.Run(() =>
            {
                start.SignalAndWait(cancellation.Token);
                while (Volatile.Read(ref remaining) != 0)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    Assert.False(LegacyTradeJournal.HasPending(data.Users));
                    Thread.Yield();
                }
            }),
        };
        // Observe every actual terminal task before the fixture can be removed.
        await Task.WhenAll(tasks);
        Assert.False(LegacyTradeJournal.HasPending(data.Users));
        foreach (var owner in new[] { sale, autosave, itemSpell })
            Assert.Equal(801L, WolfConquer.Core.Data.CharacterSnapshotParser.ParseText(File.ReadAllText(owner.Character)).Money);
        Assert.Equal("40", WolfConquer.Core.Data.CharacterSnapshotParser.ParseText(File.ReadAllText(first.Character)).Attributes["Stamina"]);
        Assert.Equal("100", WolfConquer.Core.Data.CharacterSnapshotParser.ParseText(File.ReadAllText(second.Character)).Attributes["Stamina"]);
        foreach (var owner in new[] { sale, autosave, itemSpell, first, second }) AssertCharacterPreserved(owner);
    }

    [Fact]
    public async Task ConcurrentFreshRecoveryCompletesOneExactPendingRecordOnlyOnce()
    {
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        var first = SeedOwner(data, 1, 40); var second = SeedOwner(data, 2, 100);
        var changes = PairChanges(first, second);
        Assert.False(LegacyTradeJournal.TryCommitCharacterResources(data.Users, changes, out _,
            LegacyCharacterResourcesJournalInterruption.AfterPending));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var start = new Barrier(4);
        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            start.SignalAndWait(cancellation.Token);
            Assert.True(LegacyTradeJournal.TryRecover(data.Users, data.Items, out var count, out var reason), reason);
            return count;
        })).ToArray();
        var counts = await Task.WhenAll(tasks);
        Assert.Equal(1, counts.Sum());
        Assert.Equal(changes[0].After, File.ReadAllBytes(first.Character));
        Assert.Equal(changes[1].After, File.ReadAllBytes(second.Character));
        Assert.False(LegacyTradeJournal.HasPending(data.Users));
    }
}
