using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDemonExterminatorTests
{
    [Fact]
    public void FreshStorageHasNativeDefaultsAndIndependentMutableFields()
    {
        var first = new LegacyNativeDemonExterminatorRuntime();
        var second = new LegacyNativeDemonExterminatorRuntime();
        Assert.Equal(0U, first.ItemUID);
        Assert.Equal((byte)0, first.FinishToday);
        Assert.Equal((ushort)0, first.HuntKills);
        Assert.Equal(0U, first.QuestTyp);
        Assert.False(first.OnQuest);
        first.ItemUID = 7;
        first.FinishToday = 2;
        first.HuntKills = 3;
        first.QuestTyp = 4;
        first.OnQuest = true;
        Assert.Equal(0U, second.ItemUID);
        Assert.Equal((byte)0, second.FinishToday);
        Assert.Equal((ushort)0, second.HuntKills);
        Assert.Equal(0U, second.QuestTyp);
        Assert.False(second.OnQuest);
    }

    [Fact]
    public void CurrentPartialBirthDoesNotEagerlyAllocateDemon()
    {
        var client = new LegacyNativeGameClientRuntime();
        Assert.Null(client.DemonExterminator);
        Assert.Null(client.Inventory);
        Assert.NotNull(client.Player);
    }

    [Fact]
    public void ExplicitConstructorSlotReplacesOnlyDemonOwner()
    {
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        var inventory = new LegacyNativeInventoryRuntime(client);
        client.Inventory = inventory;
        LegacyNativeItemContainerMemberPhases.AssignDemonExterminatorAtNativeConstructorSlot(client);
        var old = client.DemonExterminator!;
        old.ItemUID = 8;
        LegacyNativeItemContainerMemberPhases.AssignDemonExterminatorAtNativeConstructorSlot(client);
        Assert.NotSame(old, client.DemonExterminator);
        Assert.Equal(0U, client.DemonExterminator!.ItemUID);
        Assert.Equal(8U, old.ItemUID);
        Assert.Same(player, client.Player);
        Assert.Same(inventory, client.Inventory);
        Assert.Null(client.Equipment);
        Assert.Null(client.Rune);
    }

    [Fact]
    public void InventoryLookupUsesKeyAndReturnsSamePhysicalItem()
    {
        var inventory = new LegacyNativeInventoryRuntime(null);
        var item = new LegacyNativeGameItemRuntime { Uid = 99 };
        inventory.ClientItems[7] = item;
        Assert.True(inventory.TryGetItem(7, out var found));
        Assert.Same(item, found);
        Assert.False(inventory.TryGetItem(99, out var missing));
        Assert.Null(missing);
        Assert.Null(inventory.Owner);
    }

    [Fact]
    public void InventoryLookupRereadsReplaceableFieldAndPreservesNullFieldFault()
    {
        var inventory = new LegacyNativeInventoryRuntime(null);
        var first = new LegacyNativeGameItemRuntime();
        var second = new LegacyNativeGameItemRuntime();
        inventory.ClientItems[7] = first;
        Assert.True(inventory.TryGetItem(7, out var initial));
        Assert.Same(first, initial);
        inventory.ClientItems = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
        inventory.ClientItems[7] = second;
        Assert.True(inventory.TryGetItem(7, out var replaced));
        Assert.Same(second, replaced);
        inventory.ClientItems = null!;
        Assert.Throws<NullReferenceException>(() => inventory.TryGetItem(7, out _));
    }

    [Fact]
    public void CheckUpKillCoversEveryNativeSpecialAlternativeAndEqualityFallback()
    {
        var demon = new LegacyNativeDemonExterminatorRuntime();
        var alternatives = new Dictionary<ushort, uint[]>
        {
            [58] = [8219, 8319, 8119, 83],
            [57] = [8218, 8318, 8118, 82],
            [56] = [8217, 8317, 8117, 81],
            [55] = [84, 77, 79],
            [14] = [73, 8209, 8309, 8109],
            [15] = [74, 8210, 8310, 8110],
            [16] = [75, 8211, 8311, 8111],
            [17] = [76, 8212, 8312, 8112],
            [18] = [77, 8213, 8313, 8113],
            [19] = [78, 8214, 8314, 8114],
            [20] = [79, 8215, 8315, 8115],
            [13] = [72, 8208, 8308, 8108],
            [12] = [71, 8207, 8307, 8107],
            [11] = [70, 8206, 8306, 8106],
            [10] = [69, 8205, 8305, 8105],
            [8] = [67, 8203, 8103, 8303],
            [7] = [8102, 66],
            [6] = [65, 8101, 8301, 8201],
            [5] = [64, 25]
        };
        Assert.Equal(71, alternatives.Values.Sum(values => values.Length));
        foreach (var entry in alternatives)
        {
            foreach (var monster in entry.Value)
                Assert.True(demon.CheckUpKill(entry.Key, monster));
            Assert.True(demon.CheckUpKill(entry.Key, entry.Key));
            Assert.False(demon.CheckUpKill(entry.Key, uint.MaxValue));
        }
        Assert.True(demon.CheckUpKill(0, 0));
        Assert.True(demon.CheckUpKill(9, 9));
        Assert.False(demon.CheckUpKill(9, 8204)); // Native has no case9 family expansion.
        Assert.True(demon.CheckUpKill(ushort.MaxValue, ushort.MaxValue));
    }

    [Fact]
    public void StageReadsOnlyTheSuppliedItemDurabilityOnEachCall()
    {
        var demon = new LegacyNativeDemonExterminatorRuntime();
        Assert.Equal(byte.MaxValue, demon.GetDemonExterminatorStage(null));
        var item = new LegacyNativeGameItemRuntime { MaximDurability = 600 };
        (ushort Durability, byte Stage)[] cases =
            [(600, 0), (1200, 1), (1500, 2), (1800, 3), (0, 255), (599, 255), (601, 255), (65535, 255)];
        foreach (var entry in cases)
        {
            item.Durability = entry.Durability;
            Assert.Equal(entry.Stage, demon.GetDemonExterminatorStage(item));
        }
    }

    [Fact]
    public void ZeroItemUidReturnsBeforeDereferencingUserAndKeepsOtherState()
    {
        var demon = new LegacyNativeDemonExterminatorRuntime
            { FinishToday = 2, HuntKills = 3, QuestTyp = 4, OnQuest = true };
        demon.UppdateJar(null!, 72);
        Assert.Equal((byte)2, demon.FinishToday);
        Assert.Equal((ushort)3, demon.HuntKills);
        Assert.Equal(4U, demon.QuestTyp);
        Assert.True(demon.OnQuest);
    }

    [Fact]
    public void JarLookupCountsEvenWhenNotOnQuestAndDoesNotRequireJarItemId()
    {
        var client = ClientWithInventory();
        var item = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13, Durability = 600 };
        client.Inventory!.ClientItems[7] = item;
        var demon = new LegacyNativeDemonExterminatorRuntime
            { ItemUID = 7, FinishToday = 2, QuestTyp = 999, OnQuest = false };
        demon.UppdateJar(client, 72);
        Assert.Equal((ushort)1, demon.HuntKills);
        Assert.Equal((ushort)600, item.Durability);
        Assert.Equal(0U, item.ITEM_ID);
        Assert.Equal(7U, demon.ItemUID);
        Assert.Equal((byte)2, demon.FinishToday);
        Assert.Equal(999U, demon.QuestTyp);
        Assert.False(demon.OnQuest);
    }

    [Fact]
    public void NonmatchingMonsterDoesNotIncrementOrChangeJar()
    {
        var client = ClientWithInventory();
        var item = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        client.Inventory!.ClientItems[7] = item;
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7, HuntKills = 9 };
        demon.UppdateJar(client, 71);
        Assert.Equal((ushort)9, demon.HuntKills);
        Assert.Equal(7U, demon.ItemUID);
        Assert.Same(item, client.Inventory.ClientItems[7]);
    }

    [Fact]
    public void MatchingCachedItemSurvivesInventoryRemovalReplacementAndNullInventory()
    {
        var client = ClientWithInventory();
        var original = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        client.Inventory!.ClientItems[7] = original;
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7 };
        demon.UppdateJar(client, 72);
        client.Inventory.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 58 };
        original.MaximDurability = 12;
        client.Inventory = null;
        demon.UppdateJar(client, 71);
        Assert.Equal((ushort)2, demon.HuntKills);
        Assert.Equal(7U, demon.ItemUID);
    }

    [Fact]
    public void CachedItemUidMutationCausesFreshLookupOfCurrentInventory()
    {
        var client = ClientWithInventory();
        var original = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        client.Inventory!.ClientItems[7] = original;
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7 };
        demon.UppdateJar(client, 72);
        original.Uid = 99;
        var replacement = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 12 };
        client.Inventory.ClientItems[7] = replacement;
        demon.UppdateJar(client, 71);
        Assert.Equal((ushort)2, demon.HuntKills);
        replacement.MaximDurability = 14;
        demon.UppdateJar(null!, 73);
        Assert.Equal((ushort)3, demon.HuntKills);
    }

    [Fact]
    public void CachedUidMismatchLookupFaultDoesNotResetItemUidOrCounter()
    {
        var client = ClientWithInventory();
        var original = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        client.Inventory!.ClientItems[7] = original;
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7 };
        demon.UppdateJar(client, 72);
        original.Uid = 99;
        client.Inventory = null;
        Assert.Throws<NullReferenceException>(() => demon.UppdateJar(client, 72));
        Assert.Equal(7U, demon.ItemUID);
        Assert.Equal((ushort)1, demon.HuntKills);
        client.Inventory = new LegacyNativeInventoryRuntime(client);
        client.Inventory.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 12 };
        demon.UppdateJar(client, 71);
        Assert.Equal((ushort)2, demon.HuntKills);
    }

    [Fact]
    public void KeyValueUidMismatchIsNeitherNormalizedNorRejectedAfterLookup()
    {
        var client = ClientWithInventory();
        var item = new LegacyNativeGameItemRuntime { Uid = 99, MaximDurability = 13 };
        client.Inventory!.ClientItems[7] = item;
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7 };
        demon.UppdateJar(client, 72);
        Assert.Equal((ushort)1, demon.HuntKills);
        Assert.Equal(7U, demon.ItemUID);
        Assert.Equal(99U, item.Uid);
        Assert.Same(item, client.Inventory.ClientItems[7]);
    }

    [Fact]
    public void MissingItemResetsOnlyItemUidAndDoesNotAutomaticallyRetryLater()
    {
        var client = ClientWithInventory();
        var demon = new LegacyNativeDemonExterminatorRuntime
            { ItemUID = 7, HuntKills = 9, FinishToday = 2, QuestTyp = 4, OnQuest = true };
        demon.UppdateJar(client, 72);
        Assert.Equal(0U, demon.ItemUID);
        client.Inventory!.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        demon.UppdateJar(client, 72);
        Assert.Equal((ushort)9, demon.HuntKills);
        Assert.Equal((byte)2, demon.FinishToday);
        Assert.Equal(4U, demon.QuestTyp);
        Assert.True(demon.OnQuest);
    }

    [Fact]
    public void UserAndInventoryFaultOnlyWhenTheNativeLookupIsNeeded()
    {
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7, HuntKills = 4 };
        Assert.Throws<NullReferenceException>(() => demon.UppdateJar(null!, 72));
        Assert.Equal(7U, demon.ItemUID);
        var client = new LegacyNativeGameClientRuntime();
        Assert.Throws<NullReferenceException>(() => demon.UppdateJar(client, 72));
        Assert.Equal(7U, demon.ItemUID);
        client.Inventory = new LegacyNativeInventoryRuntime(client);
        client.Inventory.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        demon.UppdateJar(client, 72);
        demon.UppdateJar(null!, 72); // Matching held Jar avoids user access.
        Assert.Equal((ushort)6, demon.HuntKills);
    }

    [Fact]
    public void ZeroItemUidEarlyReturnDoesNotClearTheHeldSameItem()
    {
        var client = ClientWithInventory();
        client.Inventory!.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7 };
        demon.UppdateJar(client, 72);
        demon.ItemUID = 0;
        demon.UppdateJar(null!, 72);
        client.Inventory.ClientItems.Clear();
        demon.ItemUID = 7;
        demon.UppdateJar(null!, 72);
        Assert.Equal((ushort)2, demon.HuntKills);
    }

    [Fact]
    public void ChangedUidMissingLookupClearsCacheBeforeLaterExplicitReacquisition()
    {
        var client = ClientWithInventory();
        client.Inventory!.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7 };
        demon.UppdateJar(client, 72);
        demon.ItemUID = 8;
        demon.UppdateJar(client, 72);
        Assert.Equal(0U, demon.ItemUID);
        client.Inventory.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 12 };
        demon.ItemUID = 7;
        demon.UppdateJar(client, 71);
        Assert.Equal((ushort)2, demon.HuntKills);
    }

    [Fact]
    public void HuntKillsWrapsWithoutChangingItemDurability()
    {
        var client = ClientWithInventory();
        var item = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13, Durability = 1200 };
        client.Inventory!.ClientItems[7] = item;
        var demon = new LegacyNativeDemonExterminatorRuntime { ItemUID = 7, HuntKills = ushort.MaxValue };
        demon.UppdateJar(client, 72);
        Assert.Equal((ushort)0, demon.HuntKills);
        Assert.Equal((ushort)1200, item.Durability);
    }

    [Fact]
    public void DifferentClientDemonOwnersKeepIndependentCachesAndCounters()
    {
        var first = ClientWithInventory();
        var second = ClientWithInventory();
        first.Inventory!.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 13 };
        second.Inventory!.ClientItems[7] = new LegacyNativeGameItemRuntime { Uid = 7, MaximDurability = 12 };
        LegacyNativeItemContainerMemberPhases.AssignDemonExterminatorAtNativeConstructorSlot(first);
        LegacyNativeItemContainerMemberPhases.AssignDemonExterminatorAtNativeConstructorSlot(second);
        first.DemonExterminator!.ItemUID = second.DemonExterminator!.ItemUID = 7;
        first.DemonExterminator.UppdateJar(first, 72);
        second.DemonExterminator.UppdateJar(second, 72);
        Assert.Equal((ushort)1, first.DemonExterminator.HuntKills);
        Assert.Equal((ushort)0, second.DemonExterminator.HuntKills);
        second.DemonExterminator.UppdateJar(second, 71);
        Assert.Equal((ushort)1, second.DemonExterminator.HuntKills);
    }

    [Fact]
    public void NativeUidAndGuildIdAreIndependentDirectPlayerStorage()
    {
        var first = new LegacyNativeGameClientRuntime();
        var second = new LegacyNativeGameClientRuntime();
        Assert.Equal(0U, first.Player.UID);
        Assert.Equal(0U, first.Player.GuildID);
        var random = first.Player.MyRandom;
        var flags = first.Player.BitVector;
        first.Player.UID = uint.MaxValue;
        first.Player.GuildID = 7;
        Assert.Equal(uint.MaxValue, first.Player.UID);
        Assert.Equal(7U, first.Player.GuildID);
        Assert.Equal(0U, second.Player.UID);
        Assert.Equal(0U, second.Player.GuildID);
        Assert.Same(first, first.Player.Owner);
        Assert.Same(random, first.Player.MyRandom);
        Assert.Same(flags, first.Player.BitVector);
    }

    [Fact]
    public void ExistingDataCoreItemOwnerPrefixDoesNotSilentlyAllocateDemon()
    {
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        LegacyNativeItemContainerMemberPhases.AssignLoadClientItemOwnersPrefix(player);
        Assert.NotNull(client.Inventory);
        Assert.NotNull(client.Equipment);
        Assert.NotNull(client.Warehouse);
        Assert.NotNull(client.Rune);
        Assert.Null(client.DemonExterminator);
        Assert.Same(player, client.Player);
    }

    private static LegacyNativeGameClientRuntime ClientWithInventory()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Inventory = new LegacyNativeInventoryRuntime(client);
        return client;
    }
}
