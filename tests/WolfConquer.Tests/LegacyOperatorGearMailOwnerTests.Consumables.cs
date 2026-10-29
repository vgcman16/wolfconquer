using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOperatorGearMailOwnerTests
{
    [Fact]
    public Task ConsumablesRecipeDeliversAndClaimsAllThreeTypesWithoutReplacingTheAdmittedConnection() => WithOwnerAsync(async owner =>
    {
        var before = owner.Current;
        var originalUids = before.PlayerItems.InventoryItems.Select(row => row.Uid).ToArray();
        var high = owner.HighWater;
        var request = LegacyOperatorTestKits.Create(Guid.NewGuid(), owner.Domain, Uid, Uid, "consumables");
        var delivered = await owner.Deliver(request);
        Assert.Equal(LegacyOperatorGearMailStatus.CommittedNotificationCompleted, delivered.Status);
        Assert.Equal(3, delivered.MailIds.Length);
        Assert.Equal(new[] { high + 1, high + 2, high + 3 }, delivered.ItemUids);
        Assert.Equal(high + 3, owner.HighWater);
        uint[] types = [1000000, 1001000, 723726];
        for (var i = 0; i < types.Length; i++)
        {
            var mail = Assert.Single(owner.Mail(), row => row.Id == delivered.MailIds[i]);
            Assert.Equal(delivered.ItemUids[i], mail.ItemId);
            var item = Assert.Single(owner.Items().WarehouseItems, row => row.Uid == delivered.ItemUids[i]);
            Assert.Equal(types[i], item.ItemId);
            Assert.Equal((ushort)1, item.StackSize);
            Assert.Equal(1000U, item.WarehouseId);
        }
        var files = owner.Fixture.Files();
        var frames = owner.Sent.Count;
        var replay = await owner.Deliver(request);
        Assert.Equal(LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven, replay.Status);
        Assert.Empty(replay.MailIds); Assert.Empty(replay.ItemUids);
        Assert.Equal(frames, owner.Sent.Count); owner.Fixture.AssertFiles(files);
        foreach (var mailId in delivered.MailIds) await owner.ClaimAsync(mailId);
        for (var i = 0; i < types.Length; i++)
        {
            var saved = Assert.Single(owner.Items().InventoryItems, row => row.Uid == delivered.ItemUids[i]);
            var current = Assert.Single(owner.Current.PlayerItems.InventoryItems, row => row.Uid == saved.Uid);
            Assert.Equal(types[i], saved.ItemId); Assert.Equal((ushort)1, saved.StackSize);
            Assert.Equal(types[i], current.ItemId); Assert.Equal((ushort)1, current.StackSize);
            Assert.Equal(0U, saved.WarehouseId); Assert.Equal((ushort)0, saved.Position);
            Assert.DoesNotContain(owner.Items().WarehouseItems, row => row.Uid == saved.Uid);
        }
        Assert.All(owner.Mail(), mail => Assert.Equal(0U, mail.ItemId));
        Assert.All(originalUids, uid => Assert.Contains(owner.Current.PlayerItems.InventoryItems, row => row.Uid == uid));
        Assert.Equal(before.Character.Money, owner.Current.Character.Money);
        Assert.Equal(before.Character.ConquerPoints, owner.Current.Character.ConquerPoints);
        Assert.Equal(before.Character.BoundConquerPoints, owner.Current.Character.BoundConquerPoints);
        Assert.Same(owner.Native, owner.Current.NativeGameClient);
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));
    }, [ConsumableKitCatalogRow(1001000), ConsumableKitCatalogRow(723726)]);

    [Fact]
    public Task ConsumablesRecipeMissingActualCatalogRowsRefusesBeforeUidOrSaveMutation() => WithOwnerAsync(async owner =>
    {
        var before = owner.Current; var files = owner.Fixture.Files(); var high = owner.HighWater; var frames = owner.Sent.Count;
        var request = LegacyOperatorTestKits.Create(Guid.NewGuid(), owner.Domain, Uid, Uid, "consumables");
        var result = await owner.Deliver(request);
        Assert.Equal(LegacyOperatorGearMailStatus.Rejected, result.Status);
        Assert.Empty(result.MailIds); Assert.Empty(result.ItemUids);
        Assert.Equal(high, owner.HighWater); Assert.Equal(frames, owner.Sent.Count);
        Assert.Same(before, owner.Current); owner.Fixture.AssertFiles(files);
    });

    private static ItemType ConsumableKitCatalogRow(uint id)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedConsumable";
        fields[ItemType.DurabilityFieldIndex] = "1"; fields[ItemType.StackSizeFieldIndex] = "1";
        return new ItemType(checked((int)id), fields[1], fields);
    }
}
