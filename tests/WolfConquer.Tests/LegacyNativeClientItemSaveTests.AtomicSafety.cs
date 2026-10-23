using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientItemSaveTests
{
    [Theory]
    [InlineData(181305u, (ushort)9)] [InlineData(200000u, (ushort)17)]
    public void GenuineWardrobeEquipmentIndexSerializesOnePhysicalRootAndOnePerfection(uint id, ushort position)
    {
        using var owned = new OwnedSave(); var client = ClientWithNativeItemOwners();
        var row = Item(91, id, position, level: 2, owner: 77, name: "index", signature: "one-owner");
        Assert.True(client.MyWardrobe!.AddItem(row));
        Assert.True(client.Equipment!.ClientItems.TryAdd(row.Uid, row));
        Assert.Equal(2, client.AllMyItems().Count()); // Demonstrates the old two-index enumeration.
        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Committed, result.Status);
        Assert.Equal(Image(1, [Db(91, id, position, warehouse: 100)],
            [Perfection(91, 2, 0, 77, "index", "one-owner")]), File.ReadAllBytes(owned.FilePath));
        Assert.Same(row, client.Equipment.ClientItems[91]); Assert.True(client.MyWardrobe.ContainUID(91));
    }

    [Theory]
    [InlineData("wrong-wh")] [InlineData("zero-position")] [InlineData("position60")]
    [InlineData("wrong-family")] [InlineData("wrong-bucket")] [InlineData("distinct-row")]
    public void WardrobeAliasExceptionDoesNotAuthorizeArbitraryWh100RowsOrOtherPhysicalObjects(string corruption)
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        var row = Item(91, 181305, 9); Assert.True(client.MyWardrobe!.AddItem(row));
        Assert.True(client.Equipment!.ClientItems.TryAdd(91, row));
        switch (corruption)
        {
            case "wrong-wh": row.WH_ID = 44; break;
            case "zero-position": row.Position = 0; break;
            case "position60": row.Position = 60; break;
            case "wrong-family": row.ITEM_ID = 410003; break;
            case "wrong-bucket":
                Assert.True(client.MyWardrobe.Items[0].TryRemove(91, out _));
                Assert.True(client.MyWardrobe.Items[1].TryAdd(91, row)); break;
            case "distinct-row":
                client.MyWardrobe.Items[0][91] = Item(91, 181305, 9);
                client.MyWardrobe.Items[0][91].WH_ID = 100; break;
        }
        AssertRejectedUnchanged(owned, client);
    }

    [Theory]
    [InlineData("same-root")] [InlineData("root-deposit")]
    [InlineData("same-child")] [InlineData("distinct-children-same-uid")]
    public void DuplicateOwnedLocationsRejectRatherThanPublishingAFirstAliasPrefix(string duplicate)
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        var first = Item(101, 410003); var second = Item(102, 410004); var child = Item(103, 410005);
        client.Inventory!.AddDBItem(first);
        switch (duplicate)
        {
            case "same-root": client.Equipment!.ClientItems.TryAdd(101, first); break;
            case "root-deposit":
                first.Deposite.TryAdd(103, child); client.Equipment!.ClientItems.TryAdd(103, child); break;
            case "same-child":
                client.Equipment!.ClientItems.TryAdd(102, second);
                first.Deposite.TryAdd(103, child); second.Deposite.TryAdd(103, child); break;
            case "distinct-children-same-uid":
                client.Equipment!.ClientItems.TryAdd(102, second);
                first.Deposite.TryAdd(103, child); second.Deposite.TryAdd(103, Item(103, 410006)); break;
        }
        AssertRejectedUnchanged(owned, client);
    }

    [Fact]
    public void NestedOwnedDepositRejectsWithoutSilentlyDiscardingItsGrandchild()
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        var parent = Item(101, 1001000); var child = Item(102, 410003); var grandchild = Item(103, 410004);
        parent.Deposite.TryAdd(102, child); child.Deposite.TryAdd(103, grandchild);
        client.Inventory!.AddDBItem(parent);
        AssertRejectedUnchanged(owned, client);
        Assert.Same(child, parent.Deposite[102]); Assert.Same(grandchild, child.Deposite[103]);
    }

    [Theory]
    [InlineData("null-row")] [InlineData("wrong-key")] [InlineData("zero-uid")]
    [InlineData("null-deposits")] [InlineData("null-attributes")] [InlineData("short-attributes")]
    [InlineData("extra-attributes")] [InlineData("null-name")] [InlineData("null-signature")]
    [InlineData("long-name")] [InlineData("long-signature")]
    public void InvalidLateActualRowsOrPerfectionTextLeaveNoSerializedPrefix(string corruption)
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        client.Inventory!.AddDBItem(Item(101, 1001000));
        var row = Item(102, 410003, level: 1, owner: 77, name: "owner", signature: "signature");
        client.Equipment!.ClientItems.TryAdd(102, row);
        switch (corruption)
        {
            case "null-row": client.Equipment.ClientItems[102] = null!; break;
            case "wrong-key": row.Uid = 103; break;
            case "zero-uid": row.Uid = 0; break;
            case "null-deposits": row.Deposite = null!; break;
            case "null-attributes": row.RelicAttributes = null!; break;
            case "short-attributes": row.RelicAttributes = new LegacyNativeRelicAttribute[4]; break;
            case "extra-attributes": row.RelicAttributes = new LegacyNativeRelicAttribute[6]; break;
            case "null-name": row.OwnerName = null!; break;
            case "null-signature": row.Signature = null!; break;
            case "long-name": row.OwnerName = new string('X', 33); break;
            case "long-signature": row.Signature = new string('X', 33); break;
        }
        AssertRejectedUnchanged(owned, client);
    }

    [Theory]
    [InlineData("foreign-inventory")] [InlineData("foreign-equipment")]
    [InlineData("foreign-wardrobe")] [InlineData("foreign-warehouse")]
    [InlineData("retrofitted-rune")] [InlineData("foreign-player")]
    public void SaveDoesNotInferAuthorityFromMatchingUidOrRetrofittedOwnerSlots(string corruption)
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        var foreign = new LegacyNativeGameClientRuntime(); foreign.Player.UID = client.Player.UID;
        switch (corruption)
        {
            case "foreign-inventory": client.Inventory = new(foreign); break;
            case "foreign-equipment": client.Equipment = new(foreign); break;
            case "foreign-wardrobe": client.MyWardrobe = new(foreign); break;
            case "foreign-warehouse": client.Warehouse = new(foreign); break;
            case "retrofitted-rune": client.Rune = new(null) { Owner = client }; break;
            case "foreign-player": client.Player = foreign.Player; break;
        }
        AssertRejectedUnchanged(owned, client);
    }

    [Fact]
    public void RealReadSharingThatDeniesDeleteRefusesAtomicReplaceAfterStagingWithoutChangingDestination()
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        client.Inventory!.AddDBItem(Item(101, 1001000));
        byte[] before = File.ReadAllBytes(owned.FilePath);
        using (var reader = new FileStream(owned.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
            Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
            Assert.StartsWith("replaceUnchanged:", result.Reason);
            Assert.Equal(before.Length, reader.Length);
        }
        Assert.Equal(before, File.ReadAllBytes(owned.FilePath));
        Assert.Equal(new[] { owned.FilePath }, Directory.GetFiles(owned.PlayersItemsDirectory));
        using var exclusive = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void VoidSaveEntryCannotSilentlyClaimSuccessForRejectedGraph()
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        client.Inventory!.ClientItems[101] = null!;
        Assert.Throws<IOException>(() => LegacyNativeClientItemSave.SaveClientItems(client));
        AssertRejectedUnchanged(owned, client);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LegalFullWidthPerfectionFieldsKeepExactNativeFileBytes(bool deposited)
    {
        using var owned = new OwnedSave(); var client = ClientWithNativeItemOwners();
        string name = new('A', 32); string signature = new('B', 32);
        var item = Item(102, 410003, level: 2, owner: 77, name: name, signature: signature);
        byte[] expected;
        if (deposited)
        {
            var parent = Item(101, 1001000); parent.Deposite.TryAdd(item.Uid, item);
            client.Inventory!.AddDBItem(parent);
            expected = Image(1, [Db(101, 1001000, deposits: 1), Db(102, 410003)],
                [Perfection(102, 2, 0, 77, name, signature)]);
        }
        else
        {
            client.Equipment!.ClientItems.TryAdd(item.Uid, item);
            expected = Image(1, [Db(102, 410003)], [Perfection(102, 2, 0, 77, name, signature)]);
        }
        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Committed, result.Status);
        Assert.Equal(expected, File.ReadAllBytes(owned.FilePath));
        Assert.Equal(name, item.OwnerName); Assert.Equal(signature, item.Signature);
    }

    [Fact]
    public void OversizedExistingImageRefusesBeforeAnyStageOrDestinationWrite()
    {
        using var owned = SeededSave(); var client = ClientWithNativeItemOwners();
        const long size = (64L * 1024 * 1024) + 1;
        using (var file = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            file.SetLength(size);
        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
        using (var file = new FileStream(owned.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal(size, file.Length);
            Assert.Equal(0x45, file.ReadByte()); file.Position = size - 1; Assert.Equal(0, file.ReadByte());
        }
        Assert.Equal(new[] { owned.FilePath }, Directory.GetFiles(owned.PlayersItemsDirectory));
    }

    private static OwnedSave SeededSave()
    {
        var owned = new OwnedSave(); owned.Seed([0x45, 0x19, 0xAB, 0x00, 0xFE]); return owned;
    }

    private static void AssertRejectedUnchanged(OwnedSave owned, LegacyNativeGameClientRuntime client)
    {
        byte[] before = File.ReadAllBytes(owned.FilePath);
        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);
        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
        Assert.Equal(before, File.ReadAllBytes(owned.FilePath));
        Assert.Equal(new[] { owned.FilePath }, Directory.GetFiles(owned.PlayersItemsDirectory));
        using var exclusive = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
