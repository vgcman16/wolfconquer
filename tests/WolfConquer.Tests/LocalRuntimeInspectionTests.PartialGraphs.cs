namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [WindowsOperatorPipeTheory]
    [InlineData("inventory-key")]
    [InlineData("inventory-position")]
    [InlineData("equipment-slot")]
    public Task MissingOneNativeOwnerDoesNotHideTheOtherMalformedSelectedGraph(string kind) =>
        WithShapeOwner(false, async f =>
        {
            var inventory = f.Native.Inventory!; var equipment = f.Native.Equipment!;
            var item = kind == "equipment-slot" ? equipment.ClientItems[ShapeOtherUid] : Assert.Single(inventory.ClientItems).Value;
            var originalPosition = item.Position;
            var files = Files(f); var sent = f.SentSnapshot(); var high = f.HighWater; var current = f.Current;
            try
            {
                if (kind == "equipment-slot")
                {
                    f.Native.Inventory = null;
                    item.Position = equipment.ClientItems[ShapeRightUid].Position;
                }
                else
                {
                    f.Native.Equipment = null;
                    if (kind == "inventory-key") Assert.True(inventory.ClientItems.TryAdd(uint.MaxValue, item));
                    else item.Position = 4;
                }
                Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
                Assert.Same(current, f.Current); Assert.Equal(high, f.HighWater); Unchanged(f, files, sent);
            }
            finally
            {
                f.Native.Inventory = inventory; f.Native.Equipment = equipment;
                inventory.ClientItems.TryRemove(uint.MaxValue, out _); item.Position = originalPosition;
            }
            Diagnostic(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
        });
}
