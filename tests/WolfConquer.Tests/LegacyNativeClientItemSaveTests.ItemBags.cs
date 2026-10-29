using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientItemSaveTests
{
    [Fact]
    public void RealBagInterfaceReplacementRemovalAndClearPersistActualUidRows()
    {
        using var owned = new OwnedSave(); var client = ClientWithNativeItemOwners();
        var bag = client.RelicFuse!; IDictionary<uint, LegacyNativeGameItemRuntime> contract = bag;
        var prior = Item(501, 1001000); var replacement = Item(502, 1001000); var added = Item(503, 1001000);
        bag.Add(77, prior); var escaped = bag.GetValues();
        contract[77] = replacement; contract.Add(88, added); long revision = bag.Revision;
        var escapedCurrent = bag.GetValues(); escapedCurrent[0] = prior;

        LegacyNativeClientItemSave.SaveClientItems(client);

        Assert.Equal(Image(2, [Db(502, 1001000), Db(503, 1001000)], []), File.ReadAllBytes(owned.FilePath));
        Assert.Equal(revision, bag.Revision); Assert.Same(replacement, bag[77]); Assert.Same(added, bag[88]);
        Assert.Same(prior, Assert.Single(escaped)); Assert.Equal(502u, replacement.Uid);
        Assert.True(contract.Remove(88)); LegacyNativeClientItemSave.SaveClientItems(client);
        Assert.Equal(Image(1, [Db(502, 1001000)], []), File.ReadAllBytes(owned.FilePath));
        contract.Clear(); LegacyNativeClientItemSave.SaveClientItems(client);
        Assert.Equal(Image(0, [], []), File.ReadAllBytes(owned.FilePath)); Assert.Empty(bag.GetValues());
    }

    [Fact]
    public void RealSaveOwnerRejectsStoredNullBagRowsWithoutReplacingExistingBytes()
    {
        using var owned = new OwnedSave(); byte[] sentinel = [9, 2, 8, 3]; owned.Seed(sentinel);
        var client = ClientWithNativeItemOwners(); var bag = client.GearMythsoulBag!;
        bag.Add(91, null!); long revision = bag.Revision;

        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);

        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
        Assert.Equal(sentinel, File.ReadAllBytes(owned.FilePath)); Assert.Equal(revision, bag.Revision);
        Assert.True(bag.Update); Assert.True(bag.ContainsKey(91)); Assert.Null(bag[91]);
        Assert.Null(Assert.Single(bag.GetValues()));
        using var exclusive = new FileStream(owned.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void RealSaveOwnerStillRejectsDuplicateItemUidsAcrossDistinctBagRows()
    {
        using var owned = new OwnedSave(); byte[] sentinel = [7, 4, 6, 5]; owned.Seed(sentinel);
        var client = ClientWithNativeItemOwners(); var relic = Item(551, 1001000); var gear = Item(551, 1001000);
        client.RelicFuse![1] = relic; client.GearMythsoulBag![2] = gear;
        long relicRevision = client.RelicFuse.Revision, gearRevision = client.GearMythsoulBag.Revision;

        var result = LegacyNativeClientItemSave.TrySaveClientItems(client);

        Assert.Equal(LegacyNativeClientItemSave.Outcome.Rejected, result.Status);
        Assert.Equal(sentinel, File.ReadAllBytes(owned.FilePath));
        Assert.Same(relic, client.RelicFuse[1]); Assert.Same(gear, client.GearMythsoulBag[2]); Assert.NotSame(relic, gear);
        Assert.Equal(relicRevision, client.RelicFuse.Revision); Assert.Equal(gearRevision, client.GearMythsoulBag.Revision);
        Assert.Single(client.RelicFuse); Assert.Single(client.GearMythsoulBag);
    }
}