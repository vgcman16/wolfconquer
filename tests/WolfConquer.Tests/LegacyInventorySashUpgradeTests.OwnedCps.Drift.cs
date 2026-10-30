using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ActualCombinedMailboxBankDriftRefusesBeforeUidIntentAllocationOrAnyReward(bool claimAll, bool catalogCallback)
    {
        var fixture = new Fixture(); var probe = new DeliveryCatalogProbe();
        PackageJoin? join = null; var failures = new List<Exception>();
        try
        {
            fixture.SeedMailboxPackageJoin();
            join = new PackageJoin(fixture, DeliveryProbeCatalog(probe), bindProducer: true);
            await join.StartAsync(bootstrap: true);
            using var metadata = new NativeLoginBeastMetadataScope(1000);
            var owners = await join.PrepareCombinedAdmissionOwnersAsync();
            var native = await join.StartCombinedNativeAdmissionAsync(owners);
            Assert.NotNull(native.OrdinarySpellBinding);
            var before = CapturePackageJoin(fixture, join);
            var uidState = File.ReadAllBytes(fixture.PackageUidState);
            var bankBefore = native.ConquerPointsSave;
            Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var initialIntents, out var initialReason), initialReason);
            Assert.Empty(initialIntents);
            try
            {
                if (catalogCallback)
                {
                    probe.Mutation = () =>
                    {
                        // The real package catalog is read while the actual
                        // deferred Store publication owns its existing actors.
                        Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                        Assert.True(Monitor.IsEntered(native.SpellAdmissionSyncRoot));
                        Assert.True(Monitor.IsEntered(native.Player.SpellAdmissionSyncRoot));
                        Assert.Same(before.Owner, join.Current);
                        native.ConquerPointsSave = checked(bankBefore + 1);
                    };
                    probe.Armed = true;
                }
                else native.ConquerPointsSave = checked(bankBefore + 1);

                await join.FrameAsync(PackageJoinRequest(claimAll));

                Assert.Equal(catalogCallback, probe.Fired);
                if (catalogCallback) Assert.True(probe.AfterCommitReads > 0); // Historical probe name; no commit is claimed here.
                Assert.Equal(checked(bankBefore + 1), native.ConquerPointsSave);
                Assert.Same(before.Owner, join.Current);
                Assert.Equal(before.Owner.Character.ConquerPoints, join.Current.Character.ConquerPoints);
                Assert.Equal(before.Owner.Character.BoundConquerPoints, join.Current.Character.BoundConquerPoints);
                Assert.Equal(before.Owner.Character.BankConquerPoints, join.Current.Character.BankConquerPoints);
                Assert.Equal(before.FirstFrame, fixture.Sent.Count);
                AssertPackageJoinFiles(fixture, before.Files);
                Assert.Equal(uidState, File.ReadAllBytes(fixture.PackageUidState));
                Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var intents, out var reason), reason);
                Assert.Empty(intents); Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
            }
            finally
            {
                probe.Armed = false; probe.Mutation = null;
                native.ConquerPointsSave = bankBefore;
            }
            Assert.Equal(before.Json, JsonSerializer.Serialize(join.Current));
        }
        catch (Exception error) { failures.Add(error); }
        if (join is not null)
            try { await join.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        if (join is null || join.IsClosed)
        {
            try { await fixture.CleanupAsync(failures); }
            catch (Exception error) { failures.Add(error); Unfinished.Add(fixture); }
        }
        else
        {
            Unfinished.Add(fixture);
            failures.Add(new IOException("Owned CP catalog fixture retained because owner closure is unproven."));
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Owned CP catalog assertion and/or cleanup failed.", failures);
    }
}
