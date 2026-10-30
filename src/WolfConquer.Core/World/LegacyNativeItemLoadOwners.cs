using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// A same-thread lease of genuine owners; no collection or client is fabricated.
internal sealed partial class LegacyNativeItemLoadOwners : IDisposable
{
    [Flags]
    internal enum Destination { None = 0, Inventory = 1, Equipment = 2, Warehouse = 4,
        WardrobeGarment = 8, WardrobeMount = 16, Rune = 32, Relic = 64, Gear = 128 }
    internal readonly record struct Placement(LegacyNativeGameItemRuntime Item, Destination Destination, uint WarehouseId = 0);

    private readonly LegacyNativeGameClientRuntime client;
    private readonly LegacyNativePlayerRuntime player;
    private readonly Destination required;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private readonly List<object> gates = new(6);
    private readonly uint uid;
    private LegacyNativeInventoryRuntime inventory = null!;
    private LegacyNativeEquipItemStorage equipment = null!;
    private LegacyNativeWarehouseRuntime warehouse = null!;
    private LegacyNativeWardrobeRuntime wardrobe = null!;
    private LegacyNativeRuneItemStorage rune = null!;
    private LegacyNativeItemBagRuntime relic = null!, gear = null!;
    private LegacyNativeRuneItemStorage.LoadPublication runeState = null!;
    private LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime>.PreparedAppend relicState = null!, gearState = null!;
    private bool disposed, prepared, validated, published;

    internal static LegacyNativeItemLoadOwners Capture(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, Destination requiredDestinations)
    {
        ArgumentNullException.ThrowIfNull(client); ArgumentNullException.ThrowIfNull(player);
        if (requiredDestinations == Destination.None || ((int)requiredDestinations & ~255) != 0)
            throw new ArgumentOutOfRangeException(nameof(requiredDestinations));
        return new(client, player, requiredDestinations);
    }

    private LegacyNativeItemLoadOwners(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player, Destination required)
    {
        this.client = client; this.player = player; this.required = required; uid = player.UID;
        try
        {
            Enter(client.SpellAdmissionSyncRoot); Enter(player.SpellAdmissionSyncRoot);
            if (!ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) || uid == 0 || player.UID != uid)
                throw Invalid("player");
            inventory = client.Inventory ?? throw Invalid("inventory"); equipment = client.Equipment ?? throw Invalid("equipment");
            warehouse = client.Warehouse ?? throw Invalid("warehouse"); wardrobe = client.MyWardrobe ?? throw Invalid("wardrobe");
            rune = client.Rune ?? throw Invalid("rune"); relic = client.RelicFuse ?? throw Invalid("relic"); gear = client.GearMythsoulBag ?? throw Invalid("gear");
            if (ReferenceEquals(relic, gear)) throw Invalid("shared bags");
            Enter(warehouse); Enter(rune.StorageSyncRoot); Enter(relic.SyncRoot); Enter(gear.SyncRoot);
            if (!OwnersCurrent()) throw Invalid("foreign owner");
            CaptureMaps();
            runeState = rune.CaptureLoadPublication(client, player);
            relicState = relic.CapturePreparedAppend(); gearState = gear.CapturePreparedAppend();
            CaptureExistingItems();
            RequireCurrent();
        }
        catch { Dispose(); throw; }
    }

    private void Enter(object gate) { Monitor.Enter(gate); gates.Add(gate); }
    private static InvalidDataException Invalid(string reason) => new("Invalid native item-load ownership: " + reason);
    private bool OwnersCurrent() => !client.Fake && !client.FullLoading && ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) && player.UID == uid &&
        ReferenceEquals(client.Inventory, inventory) && ReferenceEquals(inventory.Owner, client) &&
        ReferenceEquals(client.Equipment, equipment) && equipment.IsOwnedBy(client) &&
        ReferenceEquals(client.Warehouse, warehouse) && ReferenceEquals(warehouse.User, client) &&
        ReferenceEquals(client.MyWardrobe, wardrobe) && wardrobe.IsOwnedBy(client) &&
        ReferenceEquals(client.Rune, rune) && rune.IsSaveOwnedBy(client) &&
        ReferenceEquals(client.RelicFuse, relic) && ReferenceEquals(client.GearMythsoulBag, gear);

    private void RequireLease()
    {
        if (disposed || Environment.CurrentManagedThreadId != thread) throw new InvalidOperationException("Item-load lease is not held by this thread.");
    }

    public void Dispose()
    {
        if (disposed) return;
        if (Environment.CurrentManagedThreadId != thread) throw new InvalidOperationException("Item-load lease disposal changed threads.");
        disposed = true;
        for (int index = gates.Count - 1; index >= 0; index--) Monitor.Exit(gates[index]);
    }
}
