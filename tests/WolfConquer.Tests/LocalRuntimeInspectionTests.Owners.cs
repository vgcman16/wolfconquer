using System.Reflection;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [Fact]
    public Task GenuineOwnedObservationCopiesActualValuesWithoutWritesAndNeverClaimsAtomicTupleEvenIfEqual() =>
        LegacyOperatorGearMailOwnerTests.WithOwnerAsync(async f =>
        {
            var sent = f.SentSnapshot(); var files = Files(f); var current = f.Current; var water = f.HighWater;
            for (var index = 0; index < 2; index++)
            {
                var result = await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f));
                Diagnostic(result); var snapshot = Assert.IsType<LegacyRuntimeInspectionSnapshot>(result.Snapshot);
                Assert.Equal(f.Native.Player.HitPoints, snapshot.NativeVitals.Hitpoints);
                Assert.Equal(f.Native.Player.Mana, snapshot.NativeVitals.Mana);
                Assert.Equal(current.Character.MinHitPoints, snapshot.ManagedCharacter.Hitpoints);
                Assert.Equal(current.Character.MinMana, snapshot.ManagedCharacter.MinMana);
                Assert.Equal(current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(),
                    snapshot.ManagedItems.Where(row => row.Container == "inventory").Select(row => row.Uid).Order());
                Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment);
                Assert.Null(snapshot.NativeItems); Assert.Null(snapshot.SelectedValuesEqual);
                Assert.Equal("nativeInventoryAndEquipmentNotInitialized", snapshot.NativeItemsUnavailableReason);
            }
            Assert.Same(current, f.Current); Assert.False(f.Native.OrdinarySpellBinding!.IsRetired);
            Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
        });

    [Fact]
    public Task RealGrantAndClaimProvideIssuedManagedInventoryWithoutInventingNativeRoots() =>
        LegacyOperatorGearMailOwnerTests.WithOwnerAsync(async f =>
        {
            var grant = await f.Deliver(f.Request(Guid.NewGuid()));
            Assert.Equal(LegacyOperatorGearMailStatus.CommittedNotificationCompleted, grant.Status);
            await f.ClaimAsync(Assert.Single(grant.MailIds));
            var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
            var result = await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)); Diagnostic(result);
            var uid = Assert.Single(grant.ItemUids);
            Assert.Contains(result.Snapshot!.ManagedItems, row => row.Uid == uid && row.Container == "inventory");
            Assert.Null(result.Snapshot.NativeItems); Assert.Null(result.Snapshot.SelectedValuesEqual);
            Assert.Equal("nativeInventoryAndEquipmentNotInitialized", result.Snapshot.NativeItemsUnavailableReason);
            Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
        });

    [Fact]
    public Task ActualManaWriterSetupReportsBothCurrentSourcesWithoutRequiringWriterDriftAsAContract() =>
        LegacyOperatorGearMailOwnerTests.WithOwnerAsync(async f =>
        {
            var mana = f.Native.Player.Mana;
            // Use the real existing setter on this owned fixture, not a fabricated owner.
            // Ordinary Frame currently leaves native Mana unhydrated; a zero initial value is explicit.
            try
            {
                // The present raw writer can create controlled disagreement; that
                // seam is setup, never desired gameplay behavior. A repaired
                // coordinated setter may update Store too and produce equality.
                // If it later requires another admitted writer entry, update
                // this setup to that entry; do not restore the unsafe seam.
                f.Native.Player.Mana = mana == ushort.MaxValue ? (ushort)(mana - 1) : (ushort)(mana + 1);
                var actualMana = f.Native.Player.Mana; var actualManaged = f.Current.Character.MinMana;
                var actualNativeHp = f.Native.Player.HitPoints; var actualManagedHp = f.Current.Character.MinHitPoints;
                var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
                var result = await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)); Diagnostic(result);
                Assert.Equal(actualMana, result.Snapshot!.NativeVitals.Mana);
                Assert.Equal(actualManaged, result.Snapshot.ManagedCharacter.MinMana);
                Assert.Equal(actualNativeHp, result.Snapshot.NativeVitals.Hitpoints);
                Assert.Equal(actualManagedHp, result.Snapshot.ManagedCharacter.Hitpoints);
                Assert.Null(result.Snapshot.NativeItems); Assert.Null(result.Snapshot.SelectedValuesEqual);
                Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
            }
            finally { f.Native.Player.Mana = mana; }
        });

    [WindowsOperatorPipeFact]
    public Task ForeignDisconnectedAndMalformedCurrentNativeGraphsRefuseAndMissingRootsStayExplicit() =>
        WithShapeOwner(false, async f =>
        {
            var files = Files(f); var sent = f.SentSnapshot();
            foreach (var request in new[] { Query(f) with { ExpectedDomain = Guid.NewGuid() }, Query(f) with { CharacterUid = OperatorGearMailFixture.Uid + 1 },
                Query(f) with { AccountId = OperatorGearMailFixture.AccountId + 1 } }) Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, request));
            var inventory = Assert.IsType<LegacyNativeInventoryRuntime>(f.Native.Inventory);
            f.Native.Inventory = null;
            try
            {
                var partial = await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)); Diagnostic(partial);
                Assert.Null(partial.Snapshot!.NativeItems); Assert.Null(partial.Snapshot.SelectedValuesEqual);
                Assert.Equal("nativeInventoryNotInitialized", partial.Snapshot.NativeItemsUnavailableReason);
            }
            finally { f.Native.Inventory = inventory; }
            var item = Assert.Single(inventory.ClientItems).Value;
            Assert.True(inventory.ClientItems.TryAdd(uint.MaxValue, item)); // Same real row under a wrong key; never a fabricated item/owner.
            try { Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f))); }
            finally { Assert.True(inventory.ClientItems.TryRemove(uint.MaxValue, out _)); }
            Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection, f.Current, out var pin, out _));
            LegacySecondaryPasswordRuntime.Invalidate(f.Connection, f.Current);
            try { Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f))); }
            finally { Assert.True(LegacySecondaryPasswordRuntime.TryInitialize(f.Connection, f.Current, pin)); }
            Unchanged(f, files, sent);
            await f.CloseAsync();
            Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
        });

    [Fact]
    public Task ActualRetirementWhileReadWaitsAndCancellationBehindARealWriterReleaseTheirLeases() =>
        LegacyOperatorGearMailOwnerTests.WithOwnerAsync(async f =>
        {
            var operations = (WorldOperationBarrier)typeof(WorldServerFrameHandler).GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler)!;
            using (var held = await operations.EnterExclusiveAsync())
            using (var cancel = new CancellationTokenSource())
            {
                var pending = f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f), cancel.Token).AsTask();
                Assert.False(pending.IsCompleted); cancel.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            }
            var heldAgain = await operations.EnterExclusiveAsync();
            try
            {
                var pending = f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)).AsTask(); Assert.False(pending.IsCompleted);
                Assert.True(f.Store.TryRemove(f.Connection, out _));
                heldAgain.Dispose(); heldAgain = null!;
                Rejected(await pending);
            }
            finally { heldAgain?.Dispose(); }
        });

    private static LegacyRuntimeInspectionRequest Query(OperatorGearMailFixture f) => Request(f.Domain, OperatorGearMailFixture.AccountId, OperatorGearMailFixture.Uid);
    private static void Diagnostic(LegacyRuntimeInspectionResult value)
    {
        Assert.True(value.Status == LegacyRuntimeInspectionStatus.Observed, LocalRuntimeInspectionProtocol.FormatResult(value));
        Assert.True(value.ReadOnly); Assert.True(value.RuntimeObserved);
        Assert.False(value.AtomicNativeTupleProven); Assert.False(value.LiveStoreSynchronized);
        Assert.Contains("unlocked native Mana", value.Snapshot!.ConsistencyBound);
        Assert.Contains("deposits", value.Snapshot.Coverage);
    }
    private static void Rejected(LegacyRuntimeInspectionResult value)
    {
        Assert.Equal(LegacyRuntimeInspectionStatus.Rejected, value.Status); Assert.Null(value.Snapshot);
        Assert.False(value.RuntimeObserved); Assert.False(value.AtomicNativeTupleProven); Assert.False(value.LiveStoreSynchronized);
    }
    private static Dictionary<string, byte[]> Files(OperatorGearMailFixture f) => Directory.GetFiles(f.Fixture.Root, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetFileName(path) != "owner.lock").ToDictionary(path => path, File.ReadAllBytes);
    private static void Unchanged(OperatorGearMailFixture f, Dictionary<string, byte[]> files, byte[][] sent)
    {
        var actual = Files(f); Assert.Equal(files.Keys.Order(), actual.Keys.Order());
        foreach (var (path, bytes) in files) Assert.Equal(bytes, actual[path]);
        Assert.Equal(sent, f.SentSnapshot());
    }
}
