using System.Text;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [Theory]
    [InlineData("inventoryPosition")]
    [InlineData("inventoryWarehouse")]
    [InlineData("equipmentZero")]
    [InlineData("equipmentOverflow")]
    [InlineData("equipmentWarehouse")]
    [InlineData("wardrobeFamily")]
    [InlineData("wardrobePosition")]
    [InlineData("duplicateSlot")]
    [InlineData("duplicateUid")]
    [InlineData("unordered")]
    public void DetachedMalformedRowsCannotBeReportedAsAnObservation(string kind)
    {
        var inventory = DiagnosticRow(1, "inventory", 0, 0, 1001000);
        var equip = DiagnosticRow(2, "equipment", 4, 0, 410003);
        LegacyOwnedPlayerItem[] malformed = kind switch
        {
            "inventoryPosition" => [inventory with { Position = 4 }],
            "inventoryWarehouse" => [inventory with { WarehouseId = 100 }],
            "equipmentZero" => [equip with { Position = 0 }],
            "equipmentOverflow" => [equip with { Position = 64 }],
            "equipmentWarehouse" => [equip with { WarehouseId = 999 }],
            "wardrobeFamily" => [equip with { WarehouseId = 100 }],
            "wardrobePosition" => [equip with { ItemId = 181305, WarehouseId = 100, Position = 60 }],
            "duplicateSlot" => [equip, equip with { Uid = 3 }],
            "duplicateUid" => [inventory, equip with { Uid = 1 }],
            "unordered" => [equip, inventory],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        foreach (var managed in new[] { true, false })
        {
            var result = DetachedResult(managed ? malformed : [], managed ? [] : malformed);
            Assert.Throws<JsonException>(() => DecodeDiagnostic(result));
        }
    }

    [Fact]
    public void DetachedCanonicalRowsRetainOrdinaryAndWardrobeEquipmentShapes()
    {
        LegacyOwnedPlayerItem[] rows = [DiagnosticRow(1, "inventory", 0, 0, 1001000),
            DiagnosticRow(2, "equipment", 63, 0, 410003),
            DiagnosticRow(3, "equipment", 59, 100, 181305),
            DiagnosticRow(4, "equipment", 17, 100, 200000)];
        var equal = DecodeDiagnostic(DetachedResult(rows, rows));
        Assert.True(equal.Snapshot!.SelectedValuesEqual);
        Assert.Equal(rows, equal.Snapshot.NativeItems);
        var unequal = DecodeDiagnostic(DetachedResult(rows, []));
        Assert.False(unequal.Snapshot!.SelectedValuesEqual);
        Assert.False(equal.AtomicNativeTupleProven); Assert.False(equal.LiveStoreSynchronized);
        Assert.False(unequal.AtomicNativeTupleProven); Assert.False(unequal.LiveStoreSynchronized);
    }

    private static LegacyOwnedPlayerItem DiagnosticRow(uint uid, string container, ushort position, uint warehouse, uint item) =>
        new(container, uid, item, position, warehouse, 0, 1, 1, 1, 0, 0, 0, 0);

    private static LegacyRuntimeInspectionResult DetachedResult(LegacyOwnedPlayerItem[] managed, LegacyOwnedPlayerItem[] native) =>
        new(1, Guid.NewGuid(), LegacyRuntimeInspectionStatus.Observed, "detachedCodecFixture",
            new(Domain, 1, 2, DateTimeOffset.UtcNow, "VisibleDx8D13",
                new(2, 1002, 1, 2, 1008, 1000, 120, 100, 50, 9, 3, 5), new(2, 9, 3),
                managed, native, managed.SequenceEqual(native)));

    private static LegacyRuntimeInspectionResult DecodeDiagnostic(LegacyRuntimeInspectionResult value) =>
        LocalRuntimeInspectionProtocol.ParseResult(Encoding.UTF8.GetBytes(LocalRuntimeInspectionProtocol.FormatResult(value)));
}
