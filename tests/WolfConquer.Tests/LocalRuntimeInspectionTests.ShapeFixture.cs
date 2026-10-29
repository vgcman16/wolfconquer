using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    private const uint ShapeRightUid = 1_000_092, ShapeOtherUid = 1_000_093;
    private const uint ShapeGarmentId = 181_305, ShapeMountId = 200_000;

    private static async Task WithShapeOwner(bool wardrobe, Func<OperatorGearMailFixture, Task> body)
    {
        OperatorGearMailFixture? f = null; Exception? primary = null;
        try
        {
            f = new OperatorGearMailFixture();
            // Real owned input preparation precedes bootstrap/reconciliation and
            // ordinary admission. The issuer observes every historical UID here.
            var original = f.Items();
            var equipped = wardrobe
                ? new[] { ShapeRow(ShapeRightUid, ShapeGarmentId, 59, 100), ShapeRow(ShapeOtherUid, ShapeMountId, 17, 100) }
                : new[] { ShapeRow(ShapeRightUid, OperatorGearMailFixture.GearId, 4, 0), ShapeRow(ShapeOtherUid, OperatorGearMailFixture.GearId, 54, 0) };
            new PlayerItemSnapshotLoader(f.Fixture.Items).SaveByCharacterUid(OperatorGearMailFixture.Uid,
                original with { EquipmentItems = equipped });
            f.Initialize(wardrobe ? [ShapeCatalog(ShapeGarmentId), ShapeCatalog(ShapeMountId)] : null);
            await f.LoginAsync();
            Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("runtime-exception", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("runtime exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
            await ComposeOwnedNativeItemComponents(f);
            Diagnostic(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
            await body(f);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (f is not null)
            try { f.RetainEvidence = primary is not null; await f.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    [WindowsOperatorPipeFact]
    public Task ExplicitGenuineNativeItemPhasesAndLoaderSupportADiagnosticWithoutClaimingNormalFrameWiring() =>
        WithShapeOwner(false, async f =>
        {
            var current = f.Current; var binding = f.Native.OrdinarySpellBinding;
            var files = Files(f); var sent = f.SentSnapshot(); var high = f.HighWater;
            var result = await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)); Diagnostic(result);
            var snapshot = result.Snapshot!; var actual = f.Native.Inventory!.ClientItems.Values
                .Concat(f.Native.Equipment!.ClientItems.Values).OrderBy(row => row.Uid).ToArray();
            Assert.NotNull(snapshot.NativeItems);
            Assert.Equal(actual.Select(row => row.Uid), snapshot.NativeItems!.Select(row => row.Uid));
            Assert.Contains(snapshot.NativeItems, row => row.Uid == ShapeRightUid && row.Position == 4);
            Assert.Contains(snapshot.NativeItems, row => row.Uid == ShapeOtherUid && row.Position == 54);
            Assert.Equal(f.Native.Player.Mana, snapshot.NativeVitals.Mana); // Actual unhydrated value; never seed saved Mana.
            Assert.Equal<bool?>(snapshot.ManagedCharacter.Hitpoints == snapshot.NativeVitals.Hitpoints &&
                snapshot.ManagedCharacter.MinMana == snapshot.NativeVitals.Mana && snapshot.ManagedItems.SequenceEqual(snapshot.NativeItems),
                snapshot.SelectedValuesEqual);
            Assert.Same(current, f.Current); Assert.Same(binding, f.Native.OrdinarySpellBinding);
            Assert.Equal(high, f.HighWater); Unchanged(f, files, sent);
        });

    private static async Task ComposeOwnedNativeItemComponents(OperatorGearMailFixture f)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Actual native item ReadFile composition requires Windows.");
        var operations = (WorldOperationBarrier)typeof(WorldServerFrameHandler)
            .GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler)!;
        using (await operations.EnterExclusiveAsync())
        lock (f.Native.SpellAdmissionSyncRoot)
        lock (f.Native.Player.SpellAdmissionSyncRoot)
        {
            var player = f.Native.Player; var current = f.Current;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(player, binding.Player);
            var spells = f.Native.MySpells; var profs = f.Native.MyProfs;
            var raw = f.Native.RawEquipmentObservation; var identity = binding.NativeIdentity;
            // This explicit component setup requires the currently absent
            // owners. When normal Frame wiring exists, reuse its real owners
            // instead of overwriting them or preserving their absence as a rule.
            Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Equipment);
            var path = Path.GetFullPath(Path.Combine(f.Fixture.Items, player.UID.ToString(CultureInfo.InvariantCulture) + ".bin"));
            Assert.Equal(OperatorGearMailFixture.Uid, player.UID);
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            var bytes = File.ReadAllBytes(path); LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(bytes);
            var files = Files(f); var sent = f.SentSnapshot(); var high = f.HighWater;
            var previousDb = LegacyNativeServerConfig.DbLocation; var metadata = LegacyNativePoolItems.ItemsBase;
            var counter = LegacyNativePoolItems.ItemCounter; var count = counter?.Count;
            try
            {
                LegacyNativeServerConfig.DbLocation = f.Fixture.Root;
                Assert.Equal(path, Path.GetFullPath(LegacyNativeServerConfig.DbLocation + "\\PlayersItems\\" + player.UID + ".bin"));
                // Explicit component composition only. Whole DataCore.LoadClient
                // would replace the already-issued ordinary Spells/Profs owners.
                LegacyNativeItemContainerMemberPhases.InitializeGearMythsoulBagAtNativeFieldSlot(f.Native);
                LegacyNativeItemContainerMemberPhases.InitializeRelicFuseAtNativeFieldSlot(f.Native);
                LegacyNativeItemContainerMemberPhases.AssignWardrobeAtNativeConstructorSlot(f.Native);
                LegacyNativeItemContainerMemberPhases.AssignDemonExterminatorAtNativeConstructorSlot(f.Native);
                LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(player);
                LegacyNativeServerDatabasePersistence.LoadClientItems(f.Native);
                Assert.Same(metadata, LegacyNativePoolItems.ItemsBase);
            }
            finally { LegacyNativeServerConfig.DbLocation = previousDb; LegacyNativePoolItems.ItemsBase = metadata; }
            Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(count, LegacyNativePoolItems.ItemCounter?.Count);
            Assert.Same(player, f.Native.Player); Assert.Same(current, f.Current);
            Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.Same(identity, binding.NativeIdentity);
            Assert.Same(spells, f.Native.MySpells); Assert.Same(profs, f.Native.MyProfs); Assert.Same(raw, f.Native.RawEquipmentObservation);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired);
            Assert.Same(f.Native, f.Native.Inventory!.Owner); Assert.True(f.Native.Equipment!.IsOwnedBy(f.Native));
            Assert.Equal(current.PlayerItems.InventoryItems.Count, f.Native.Inventory.ClientItems.Count);
            Assert.Equal(current.PlayerItems.EquipmentItems.Count, f.Native.Equipment.ClientItems.Count);
            Assert.Equal(high, f.HighWater); Unchanged(f, files, sent);
        }
    }

    private static PlayerItemSnapshotItem ShapeRow(uint uid, uint id, ushort position, uint warehouse) => new()
    { Uid = uid, ItemId = id, Position = position, WarehouseId = warehouse, Durability = 321, MaximumDurability = 321, StackSize = 1 };

    private static ItemType ShapeCatalog(uint id)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray(); fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedShape" + fields[0]; fields[ItemType.DurabilityFieldIndex] = "321"; fields[ItemType.StackSizeFieldIndex] = "1";
        return new(checked((int)id), fields[1], fields);
    }
}
