using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private const long CombinedWarehouseGold = 555;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CombinedOwnedMailboxKeepsSavedWarehouseGoldAndWholeWalletCoherentBeforeEveryReply(bool claimAll) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            SeedCombinedWarehouseGold(fixture);
            using var metadata = new NativeLoginBeastMetadataScope(1000);
            var owners = await join.PrepareCombinedAdmissionOwnersAsync();
            var native = await join.StartCombinedNativeAdmissionAsync(owners);
            Assert.NotNull(native.OrdinarySpellBinding); Assert.False(native.FullLoading);
            Assert.Equal(CombinedWarehouseGold, native.Player.WHMoney);
            var before = CapturePackageJoin(fixture, join);
            var expectedMoney = checked(before.Owner.Character.Money + (claimAll ? (long)AtomicMailMoney : 0L));
            var expectedCps = checked(before.Owner.Character.ConquerPoints + (claimAll ? AtomicMailCp : 0L));
            Exception? observed = null; int replies = 0;
            join.SendObserver = _ =>
            {
                replies++;
                try { AssertCombinedWarehouseWallet(fixture, join, native, expectedMoney, expectedCps); }
                catch (Exception error) { observed ??= error; }
            };
            var started = DateTime.Now;

            await join.FrameAsync(PackageJoinRequest(claimAll));

            var ended = DateTime.Now; join.SendObserver = null;
            if (observed is not null) ExceptionDispatchInfo.Capture(observed).Throw();
            Assert.True(replies > 0);
            AssertCombinedWarehouseWallet(fixture, join, native, expectedMoney, expectedCps);
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
            // This is the real cold-client sequence; the warehouse participant
            // must neither invoke an emitting setter nor add a field-9 frame.
            AssertColdCpClaimFrames(fixture, join, before, claimAll, started, ended);
            var committed = fixture.PackageFiles(); int replay = fixture.Sent.Count;

            await join.FrameAsync(PackageJoinRequest(claimAll));

            AssertCombinedWarehouseWallet(fixture, join, native, expectedMoney, expectedCps);
            AssertPackageJoinFiles(fixture, committed); AssertPackageJoinReplay(fixture, claimAll, replay);
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ActualCombinedMailboxWarehouseDriftRefusesBeforeUidIntentAndRestoredOwnerRetriesOnce(
        bool claimAll, bool catalogCallback)
    {
        var fixture = new Fixture(); var probe = new DeliveryCatalogProbe();
        PackageJoin? join = null; var failures = new List<Exception>();
        try
        {
            fixture.SeedMailboxPackageJoin(); SeedCombinedWarehouseGold(fixture);
            join = new PackageJoin(fixture, DeliveryProbeCatalog(probe), bindProducer: true);
            await join.StartAsync(bootstrap: true);
            using var metadata = new NativeLoginBeastMetadataScope(1000);
            var owners = await join.PrepareCombinedAdmissionOwnersAsync();
            var native = await join.StartCombinedNativeAdmissionAsync(owners);
            Assert.NotNull(native.OrdinarySpellBinding);
            Assert.Equal(CombinedWarehouseGold, native.Player.WHMoney);
            var before = CapturePackageJoin(fixture, join);
            var uidState = File.ReadAllBytes(fixture.PackageUidState);
            Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var initial, out var initialReason), initialReason);
            Assert.Empty(initial);
            try
            {
                if (catalogCallback)
                {
                    probe.Mutation = () =>
                    {
                        Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                        Assert.True(Monitor.IsEntered(native.SpellAdmissionSyncRoot));
                        Assert.True(Monitor.IsEntered(native.Player.SpellAdmissionSyncRoot));
                        Assert.Same(before.Owner, join.Current);
                        native.Player.WHMoney = CombinedWarehouseGold + 1;
                    };
                    probe.Armed = true;
                }
                else native.Player.WHMoney = CombinedWarehouseGold + 1;

                await join.FrameAsync(PackageJoinRequest(claimAll));

                Assert.Equal(catalogCallback, probe.Fired);
                if (catalogCallback) Assert.True(probe.AfterCommitReads > 0); // Historical probe name; this operation did not commit.
                Assert.Equal(CombinedWarehouseGold + 1, native.Player.WHMoney);
                Assert.Same(before.Owner, join.Current);
                Assert.Equal(before.Json, JsonSerializer.Serialize(join.Current));
                Assert.Equal(before.FirstFrame, fixture.Sent.Count);
                AssertPackageJoinFiles(fixture, before.Files);
                Assert.Equal(uidState, File.ReadAllBytes(fixture.PackageUidState));
                Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var intents, out var reason), reason);
                Assert.Empty(intents); Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
            }
            finally
            {
                probe.Armed = false; probe.Mutation = null;
                native.Player.WHMoney = CombinedWarehouseGold;
            }

            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            var ended = DateTime.Now;
            AssertPackageJoinCommitted(fixture, join, before, claimAll, 10);
            AssertCombinedWarehouseWallet(fixture, join, native,
                checked(before.Owner.Character.Money + (claimAll ? (long)AtomicMailMoney : 0L)),
                checked(before.Owner.Character.ConquerPoints + (claimAll ? AtomicMailCp : 0L)));
            AssertColdCpClaimFrames(fixture, join, before, claimAll, started, ended);
            var committed = fixture.PackageFiles(); int replay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            AssertPackageJoinFiles(fixture, committed); AssertPackageJoinReplay(fixture, claimAll, replay);
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
            failures.Add(new IOException("Owned warehouse catalog fixture retained because owner closure is unproven."));
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Owned warehouse catalog assertion and/or cleanup failed.", failures);
    }

    private static void SeedCombinedWarehouseGold(Fixture fixture)
    {
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.CharacterPath));
        Assert.DoesNotContain("WHMoney=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WarehouseMoney=", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, text.Split("[Unrelated]", StringSplitOptions.None).Length);
        var values = "WHMoney=" + CombinedWarehouseGold.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
            "WarehouseMoney=" + CombinedWarehouseGold.ToString(CultureInfo.InvariantCulture) + Environment.NewLine;
        File.WriteAllBytes(fixture.CharacterPath, new UTF8Encoding(false).GetBytes(
            text.Replace("[Unrelated]", values + "[Unrelated]", StringComparison.Ordinal)));
    }

    private static void AssertCombinedWarehouseWallet(Fixture fixture, PackageJoin join,
        LegacyNativeGameClientRuntime native, long money, long cps)
    {
        var current = join.Current.Character;
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
            File.ReadAllBytes(fixture.CharacterPath), out _));
        Assert.Equal((money, cps, 7L, 3L),
            (current.Money, current.ConquerPoints, current.BoundConquerPoints, current.BankConquerPoints));
        Assert.Equal((money, cps, 7L, 3L),
            (saved.Money, saved.ConquerPoints, saved.BoundConquerPoints, saved.BankConquerPoints));
        Assert.Equal((money, checked((uint)cps), 7, 3UL, CombinedWarehouseGold),
            (native.Player.Money, native.Player.ConquerPoints, native.Player.BoundConquerPoints,
                native.ConquerPointsSave, native.Player.WHMoney));
        foreach (var attributes in new[] { current.Attributes, saved.Attributes })
        {
            Assert.Equal("555", attributes["WHMoney"]);
            Assert.Equal("555", attributes["WarehouseMoney"]);
        }
    }
}
