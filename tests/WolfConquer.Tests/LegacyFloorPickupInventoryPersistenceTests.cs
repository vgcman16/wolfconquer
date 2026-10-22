using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Recomputed inventory-plan persistence; not native SQL or live-client proof.
public sealed partial class LegacyFloorPickupInventoryPersistenceTests : IDisposable
{
    private const long Uid = 1_293_600;
    private const int ItemId = 1_000_100;
    private static readonly DateTime Now = new(2026, 10, 21, 18, 30, 0, DateTimeKind.Local);
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-pickup-inventory-" + Guid.NewGuid().ToString("N"));
    private string Users => Path.Combine(root, "Users");
    private string Items => Path.Combine(root, "PlayersItems");
    private string CharacterPath => Path.Combine(Users, Uid + ".ini");
    private string ItemPath => Path.Combine(Items, Uid + ".bin");

    [Theory]
    [InlineData("utf8-bom", "\r\n")]
    [InlineData("windows1252", "\r")]
    public void MergeChangesOnlyTheFirstDestinationQuantityAndKeepsCharacterExact(string encodingName, string newline)
    {
        var original = WriteSession(encodingName: encodingName, newline: newline);
        var character = File.ReadAllBytes(CharacterPath);
        var expectedItems = File.ReadAllBytes(ItemPath);
        Assert.Equal(10u, BinaryPrimitives.ReadUInt32LittleEndian(expectedItems.AsSpan(sizeof(int))));
        BinaryPrimitives.WriteUInt16LittleEndian(expectedItems.AsSpan(sizeof(int) + 52), 3);
        var (entry, inventory) = CreatePlan(original);
        Assert.True(inventory.IsMerge);
        Assert.Equal(10u, inventory.Item.Uid);
        Assert.True(Persist(original, entry, inventory, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath));
        var destination = Assert.Single(PlayerItemSnapshotLoader.Parse(expectedItems).InventoryItems, item => item.Uid == 10);
        Assert.Equal("keeper10", destination.OwnerName);
        Assert.Equal("signature10", destination.Signature);
        Assert.Equal(995u, destination.Attribute5);
        Assert.DoesNotContain(inventory.UpdatedItems.InventoryItems, item => item.Uid == 900);
    }

    [Fact]
    public void TimedZeroStackAppendPersistsExactlyTheRecomputedActivationAndExpiry()
    {
        var original = WriteSession(mergeCandidate: false);
        var character = File.ReadAllBytes(CharacterPath);
        var beforeItems = File.ReadAllBytes(ItemPath);
        var (entry, inventory) = CreatePlan(original, stack: 0, limit: 1, minutes: 60);
        var expectedItems = AppendSendRecordOracle(beforeItems,
            SendDbRecordOracle(inventory.IncomingItem, position: 0, stack: 1, activate: 1,
                minutes: 0, expiration: Now.AddMinutes(60).ToBinary()),
            SendPerfectionRecordOracle(inventory.IncomingItem));
        Assert.False(inventory.IsMerge);
        Assert.Equal((ushort)0, inventory.IncomingItem.StackSize);
        Assert.Equal(77u, inventory.IncomingItem.TimeLeftInMinutes);
        Assert.Equal((ushort)1, inventory.Item.StackSize);
        Assert.Equal(1u, inventory.Item.Activate);
        Assert.Equal(0u, inventory.Item.TimeLeftInMinutes);
        Assert.Equal(Now.AddMinutes(60).ToBinary(), inventory.Item.Expiration);
        Assert.True(Persist(original, entry, inventory, out var reason), reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath));
        var saved = Assert.Single(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(ItemPath)).InventoryItems, item => item.Uid == 900);
        Assert.Equal((ushort)1, saved.StackSize);
        Assert.Equal(Now.AddMinutes(60).ToBinary(), saved.Expiration);
    }

    [Theory]
    [InlineData("destination")]
    [InlineData("merge-flag")]
    [InlineData("metadata")]
    [InlineData("extra-mutation")]
    public void CallerCannotForgeAnyPartOfTheRecomputedPlan(string mode)
    {
        var original = WriteSession();
        var (entry, inventory) = CreatePlan(original);
        if (mode == "destination") inventory = inventory with { Item = inventory.IncomingItem };
        else if (mode == "merge-flag") inventory = inventory with { IsMerge = false };
        else if (mode == "metadata")
        {
            var forged = inventory.Item.WithDurability(1);
            inventory = inventory with
            {
                Item = forged,
                UpdatedItems = inventory.UpdatedItems with
                { InventoryItems = inventory.UpdatedItems.InventoryItems.Select(item => item.Uid == forged.Uid ? forged : item).ToArray() },
            };
        }
        else inventory = inventory with
        {
            UpdatedItems = inventory.UpdatedItems with
            { WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(71).WithDurability(1)])] },
        };
        AssertRejectedUnchanged(original, entry, inventory);
    }

    [Fact]
    public void IncomingUidCannotAlreadyBelongToTheDestinationEvenWhenItsImageWouldMerge()
    {
        var original = WriteSession();
        var (entry, inventory) = CreatePlan(original);
        var collision = Item(10, itemId: ItemId, stack: 1);
        entry = entry with { Item = collision };
        inventory = inventory with { IncomingItem = collision };
        AssertRejectedUnchanged(original, entry, inventory);
    }

    [Fact]
    public void StaleDestinationStackOnDiskIsNotOverwrittenByAnOlderMergePlan()
    {
        var original = WriteSession();
        var (entry, inventory) = CreatePlan(original);
        var changed = File.ReadAllBytes(ItemPath);
        BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(sizeof(int) + 52), 4);
        File.WriteAllBytes(ItemPath, changed);
        AssertRejectedUnchanged(original, entry, inventory);
    }

    [Fact]
    public void OrdinaryWindowsReplaceFailureKeepsExactPreimagesAndAllowsRetry()
    {
        if (!OperatingSystem.IsWindows()) return;
        var original = WriteSession();
        var (entry, inventory) = CreatePlan(original);
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        var expectedItems = items.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(expectedItems.AsSpan(sizeof(int) + 52), 3);
        using (new FileStream(ItemPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(Persist(original, entry, inventory, out var reason));
            Assert.DoesNotContain(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        }
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
        Assert.True(Persist(original, entry, inventory, out var retryReason), retryReason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void InterruptedMergePrefixesRecoverTheSameQuantityExactlyOnce(int replacementCount)
    {
        var original = WriteSession();
        var (entry, inventory) = CreatePlan(original);
        var character = File.ReadAllBytes(CharacterPath);
        var expectedItems = File.ReadAllBytes(ItemPath);
        BinaryPrimitives.WriteUInt16LittleEndian(expectedItems.AsSpan(sizeof(int) + 52), 3);
        var updated = original with { PlayerItems = inventory.UpdatedItems };
        Assert.False(LegacyNpcSalePersistence.TryPersistFloorPickupInventory(original, updated, Items, entry,
            inventory, count => count == replacementCount, out var reason));
        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out var recovered, out reason), reason);
        Assert.Equal(1, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath));
        Assert.Equal((ushort)3, PlayerItemSnapshotLoader.Parse(expectedItems).InventoryItems[0].StackSize);
        Assert.True(LegacyTradeJournal.TryRecover(Users, Items, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(expectedItems, File.ReadAllBytes(ItemPath));
    }

    private WorldSession WriteSession(bool mergeCandidate = true, string encodingName = "utf8-bom", string newline = "\n")
    {
        Directory.CreateDirectory(Users);
        Directory.CreateDirectory(Items);
        var zeroFields = new[] { "Body", "Face", "Haire", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
            "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Strength", "Agility", "Vitaliti", "Spirit",
            "Atributes", "MinHitPoints", "MinMana", "BoundConquerPoints", "PkPoints", "Online" };
        var text = ("; café\n[Character]\nUID=" + Uid + "\nName=Andreé\n" +
            string.Join('\n', zeroFields.Select(key => key + "=0")) +
            "\nMoney=0001000\nConquerPoints=00050\nBankCps=00010\nSecurityPass=1234,0,0,\n" +
            "Sentinel=preserved\n\n[Unrelated]\nMoney=do-not-touch\nNote=café\n").Replace("\n", newline, StringComparison.Ordinal);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = encodingName == "windows1252" ? Encoding.GetEncoding(1252) : new UTF8Encoding(true, true);
        File.WriteAllText(CharacterPath, text, encoding);
        var items = new PlayerItemSnapshot([Item(10, itemId: (uint)(mergeCandidate ? ItemId : 1_000_010), stack: 2), Item(11)],
            [], [], [Item(40, position: 4)], [], [])
        { WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(71)])] };
        File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(items));
        return new(Uid, "fixture", CharacterSnapshotParser.ParseText(text, CharacterPath), items,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Users);
    }

    private static (FloorItemRuntimeEntry Entry, LegacyFloorPickupInventoryChange Inventory) CreatePlan(
        WorldSession original, ushort stack = 1, ushort limit = 10, uint minutes = 0)
    {
        var ground = Item(900, itemId: ItemId, stack: stack, position: 9);
        var packet = LegacyFloorItemPacket.FromDroppedItem(ground, original.Character, 5000);
        var entry = FloorItemRuntimeEntry.ForItem(original.Character.MapId, packet, ground);
        var fields = Enumerable.Repeat("0", 47).ToArray();
        fields[38] = minutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[46] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var catalog = new ItemType(ItemId, "Pickup", fields);
        Assert.True(LegacyFloorPickupInventory.TryPlan(original.PlayerItems, ground.WithPosition(0), catalog, Now,
            out var inventory, out var reason), reason);
        return (entry, inventory);
    }

    private bool Persist(WorldSession original, FloorItemRuntimeEntry entry, LegacyFloorPickupInventoryChange inventory,
        out string reason) => LegacyNpcSalePersistence.TryPersistFloorPickupInventory(original,
        original with { PlayerItems = inventory.UpdatedItems }, Items, entry, inventory, out reason);

    private void AssertRejectedUnchanged(WorldSession original, FloorItemRuntimeEntry entry, LegacyFloorPickupInventoryChange inventory)
    {
        var character = File.ReadAllBytes(CharacterPath);
        var items = File.ReadAllBytes(ItemPath);
        Assert.False(Persist(original, entry, inventory, out var reason));
        Assert.NotEmpty(reason);
        Assert.Equal(character, File.ReadAllBytes(CharacterPath));
        Assert.Equal(items, File.ReadAllBytes(ItemPath));
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint? itemId = null, ushort stack = 1, ushort position = 0, uint warehouse = 0) => new()
    {
        Uid = uid, ItemId = itemId ?? 1_000_000 + uid, StackSize = stack, Position = position, WarehouseId = warehouse,
        Durability = 73, MaximumDurability = 100, TimeLeftInMinutes = 77, Attribute5 = 995, PerfectionLevel = 23,
        OwnerUid = 999, OwnerName = "keeper" + uid, Signature = "signature" + uid,
    };

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
