using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [WindowsOperatorPipeTheory]
    [InlineData("inventory", "position")]
    [InlineData("inventory", "warehouse")]
    [InlineData("equipment", "zero")]
    [InlineData("equipment", "outside")]
    [InlineData("equipment", "warehouse")]
    [InlineData("equipment", "wardrobe-family")]
    [InlineData("equipment", "duplicate-slot")]
    public Task MalformedActualManagedSelectedRootsRefuseWithoutWrites(string container, string kind) =>
        WithShapeOwner(false, async f =>
        {
            var current = f.Current;
            var rows = Assert.IsType<PlayerItemSnapshotItem[]>(container == "inventory"
                ? current.PlayerItems.InventoryItems : current.PlayerItems.EquipmentItems);
            var index = kind == "duplicate-slot" ? 1 : 0; var original = rows[index];
            var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
            rows[index] = kind switch
            {
                "position" => original.WithPosition(1),
                "warehouse" => original.WithWarehouseId(42, original.Position),
                "zero" => original.WithPosition(0),
                "outside" => original.WithPosition(64),
                "wardrobe-family" => original.WithWarehouseId(100, original.Position),
                "duplicate-slot" => original.WithPosition(rows[0].Position),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            try
            {
                Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
                Assert.Same(current, f.Current); Assert.False(f.Native.OrdinarySpellBinding!.IsRetired);
                Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
            }
            finally { rows[index] = original; }
            Diagnostic(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
        });

    [WindowsOperatorPipeTheory]
    [InlineData("inventory", "position")]
    [InlineData("inventory", "warehouse")]
    [InlineData("equipment", "zero")]
    [InlineData("equipment", "outside")]
    [InlineData("equipment", "warehouse")]
    [InlineData("equipment", "wardrobe-family")]
    [InlineData("equipment", "duplicate-slot")]
    public Task MalformedActualNativeSelectedRootsRefuseWithoutWrites(string container, string kind) =>
        WithShapeOwner(false, async f =>
        {
            var current = f.Current;
            var rows = (container == "inventory" ? f.Native.Inventory!.ClientItems : f.Native.Equipment!.ClientItems)
                .Values.OrderBy(row => row.Uid).ToArray();
            var item = rows[kind == "duplicate-slot" ? 1 : 0]; var position = item.Position; var warehouse = item.WH_ID;
            var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
            switch (kind)
            {
                case "position": item.Position = 1; break;
                case "warehouse": item.WH_ID = 42; break;
                case "zero": item.Position = 0; break;
                case "outside": item.Position = 64; break;
                case "wardrobe-family": item.WH_ID = 100; break;
                case "duplicate-slot": item.Position = rows[0].Position; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            try
            {
                Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
                Assert.Same(current, f.Current); Assert.False(f.Native.OrdinarySpellBinding!.IsRetired);
                Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
            }
            finally { item.Position = position; item.WH_ID = warehouse; }
            Diagnostic(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
        });

    [Fact]
    public void ExplicitCodecDisagreementIsIndependentOfAnyProductionManaWriterContract()
    {
        var result = new LegacyRuntimeInspectionResult(1, Guid.NewGuid(), LegacyRuntimeInspectionStatus.Observed, "codecFixture",
            new(Domain, 1, 2, DateTimeOffset.UtcNow, "VisibleDx8D13", new(2, 1002, 1, 2, 1008, 1000, 120, 100, 50, 9, 3, 5),
                new(2, 9, 4), [], [], false));
        var decoded = LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(LocalRuntimeInspectionProtocol.FormatResult(result)));
        Diagnostic(decoded); Assert.False(decoded.Snapshot!.SelectedValuesEqual);
        Assert.Equal(3L, decoded.Snapshot.ManagedCharacter.MinMana); Assert.Equal((ushort)4, decoded.Snapshot.NativeVitals.Mana);
    }
}
