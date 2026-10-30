using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Physical native representation only. The actual Store/holder binding is the issuer.
internal sealed partial class LegacyNativeOwnedItemGraph
{
    internal readonly LegacyNativeGameClientRuntime Client;
    internal readonly LegacyNativePlayerRuntime Player;
    internal readonly uint Uid;
    internal readonly LegacyNativeInventoryRuntime Inventory;
    internal readonly LegacyNativeEquipItemStorage Equipment;
    internal readonly LegacyNativeWarehouseRuntime Warehouse;
    internal readonly LegacyNativeWardrobeRuntime Wardrobe;
    internal readonly LegacyNativeRuneItemStorage Rune;
    internal readonly LegacyNativeItemBagRuntime Relic, Gear;
    private volatile bool published, retired;
    private LegacyOwnedNativeItemImage image = null!;
    private PhysicalState current = null!;
    internal LegacyOwnedNativeItemImage Image => image;

    private LegacyNativeOwnedItemGraph(
        LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player, uint uid)
    {
        Client = client;
        Player = player;
        Uid = uid;
        Inventory = new(client);
        Equipment = new(client);
        Warehouse = new(client);
        Wardrobe = new(client);
        Rune = new(client);
        Relic = new();
        Gear = new();
        Inventory.JoinedItemGraph = this;
        Equipment.JoinedItemGraph = this;
        Warehouse.JoinedItemGraph = this;
        Wardrobe.JoinedItemGraph = this;
        Rune.JoinedItemGraph = this;
        Relic.JoinItemGraph(this);
        Gear.JoinItemGraph(this);
    }

    private static InvalidOperationException Invalid() =>
        new("ownedNativeItemGraphRequiresCurrentCompleteOwner");

    private static void RequireActors(
        LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player)
    {
        if (!Monitor.IsEntered(client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
            !ReferenceEquals(client.Player, player) ||
            !ReferenceEquals(player.Owner, client) || client.Fake)
            throw Invalid();
    }

    private bool ConstructedOwnersCurrent() =>
        ReferenceEquals(Inventory.Owner, Client) && Equipment.IsOwnedBy(Client) &&
        ReferenceEquals(Warehouse.User, Client) && Wardrobe.IsJoinedConstructedFor(Client, this) &&
        Rune.IsJoinedConstructedFor(Client, this) && ReferenceEquals(Inventory.JoinedItemGraph, this) &&
        ReferenceEquals(Equipment.JoinedItemGraph, this) && ReferenceEquals(Warehouse.JoinedItemGraph, this) &&
        Relic.HasJoinedItemGraph(this) && Gear.HasJoinedItemGraph(this);

    private bool SlotsAbsent() =>
        Client.Inventory is null && Client.Equipment is null && Client.Warehouse is null &&
        Client.MyWardrobe is null && Client.Rune is null &&
        Client.RelicFuse is null && Client.GearMythsoulBag is null;

    private bool SlotsCurrent() =>
        ReferenceEquals(Client.Inventory, Inventory) && ReferenceEquals(Client.Equipment, Equipment) &&
        ReferenceEquals(Client.Warehouse, Warehouse) && ReferenceEquals(Client.MyWardrobe, Wardrobe) &&
        ReferenceEquals(Client.Rune, Rune) && ReferenceEquals(Client.RelicFuse, Relic) &&
        ReferenceEquals(Client.GearMythsoulBag, Gear);

    // Necessary physical identity freshness only; not full membership or a Store grant.
    internal bool PhysicalOwnerIsCurrentPlain() =>
        published && !retired && !Client.Fake && ReferenceEquals(Client.Player, Player) &&
        ReferenceEquals(Player.Owner, Client) && Player.UID == Uid && SlotsCurrent() &&
        ConstructedOwnersCurrent() && current.ReferencesCurrentPlain(this) && image.IsCurrentPlain();

    // Full reusable proof is serialized by the actual actors and storage gates.
    internal bool IsCurrentPlain()
    {
        if (!Monitor.IsEntered(Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(Player.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(Warehouse) || !Monitor.IsEntered(Rune.StorageSyncRoot) ||
            !Monitor.IsEntered(Relic.SyncRoot) || !Monitor.IsEntered(Gear.SyncRoot) ||
            !PhysicalOwnerIsCurrentPlain())
            return false;
        return current.ContentsCurrentPlain();
    }

    internal void RequireCurrentBeforePreparation()
    {
        RequireActors(Client, Player);
        if (!PhysicalOwnerIsCurrentPlain())
            throw Invalid();
        using var gates = new StorageLease(this);
        if (!IsCurrentPlain())
            throw Invalid();
    }

    internal static PreparedPublication PrepareAdmission(
        LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        uint preparedUid, LegacyOwnedNativeItemImage.Preparation preparation)
    {
        RequireActors(client, player);
        if (preparedUid == 0 || preparation.BaseImage is not null || client.FullLoading ||
            (player.UID != 0 && player.UID != preparedUid) || client.Inventory is not null ||
            client.Equipment is not null || client.Warehouse is not null || client.MyWardrobe is not null ||
            client.Rune is not null || client.RelicFuse is not null || client.GearMythsoulBag is not null)
            throw Invalid();
        var graph = new LegacyNativeOwnedItemGraph(client, player, preparedUid);
        return new(graph, preparation, true);
    }

    internal static PreparedPublication PrepareRefresh(
        LegacyNativeOwnedItemGraph graph, LegacyOwnedNativeItemImage.Preparation preparation)
    {
        RequireActors(graph.Client, graph.Player);
        if (!graph.PhysicalOwnerIsCurrentPlain() || !ReferenceEquals(preparation.BaseImage, graph.Image))
            throw Invalid();
        return new(graph, preparation, false);
    }

    // Assignment-only retirement: never erase a foreign replacement slot.
    internal void Retire()
    {
        retired = true;
        published = false;
        if (ReferenceEquals(Client.Inventory, Inventory))
            Client.Inventory = null;
        if (ReferenceEquals(Client.Equipment, Equipment))
            Client.Equipment = null;
        if (ReferenceEquals(Client.Warehouse, Warehouse))
            Client.Warehouse = null;
        if (ReferenceEquals(Client.MyWardrobe, Wardrobe))
            Client.MyWardrobe = null;
        if (ReferenceEquals(Client.Rune, Rune))
            Client.Rune = null;
        if (ReferenceEquals(Client.RelicFuse, Relic))
            Client.RelicFuse = null;
        if (ReferenceEquals(Client.GearMythsoulBag, Gear))
            Client.GearMythsoulBag = null;
    }

    private sealed class StorageLease : IDisposable
    {
        private readonly object[] gates;
        private int held;

        internal StorageLease(LegacyNativeOwnedItemGraph graph)
        {
            gates = new[]
            {
                (object)graph.Warehouse,
                graph.Rune.StorageSyncRoot,
                graph.Relic.SyncRoot,
                graph.Gear.SyncRoot
            };
            try
            {
                for (; held < gates.Length; held++)
                    Monitor.Enter(gates[held]);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            while (held > 0)
                Monitor.Exit(gates[--held]);
        }
    }
}
