using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Declared source-component MODELs only, not native item construction, raw loading,
// Inventory.Update/send, full DataCore.LoadClient, host registration or client proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeInventoryUidAdmissionTests
{
    [Fact]
    public void WritableUidBelongsToSameConcretePhysicalItemReference()
    {
        var item = new LegacyNativeGameItemRuntime { Uid = 7u };
        var held = item;
        item.Uid = uint.MaxValue;
        Assert.Same(held, item);
        Assert.Equal(uint.MaxValue, held.Uid);
        Assert.NotSame(item, new LegacyNativeGameItemRuntime { Uid = uint.MaxValue });
    }

    [Fact]
    public void ConstructorRetainsOwnerAndCreatesDistinctDictionariesWithoutCounterUse()
    {
        WithCounter(null, () =>
        {
            var client = new LegacyNativeGameClientRuntime();
            var first = new LegacyNativeInventoryRuntime(client);
            var second = new LegacyNativeInventoryRuntime(client);
            Assert.Same(client, first.Owner);
            Assert.Same(client, second.Owner);
            Assert.NotSame(first.ClientItems, second.ClientItems);
            Assert.Empty(first.ClientItems);
            Assert.Empty(second.ClientItems);
            Assert.Null(client.Inventory);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void NullConstructorOwnerIsStoredWithoutNativeAbsentGuard()
    {
        var inventory = new LegacyNativeInventoryRuntime(null);
        Assert.Null(inventory.Owner);
        Assert.Empty(inventory.ClientItems);
    }

    [Fact]
    public void PhysicalClientBirthDoesNotSynthesizeInventory()
    {
        WithCounter(null, () =>
        {
            var client = new LegacyNativeGameClientRuntime();
            Assert.NotNull(client.Player);
            Assert.Null(client.Inventory);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void ExplicitMemberPhaseAssignsConcreteInventoryToPassedPlayersOwner()
    {
        WithCounter(null, () =>
        {
            var client = new LegacyNativeGameClientRuntime();
            LegacyNativeInventoryLoadComposition.AssignInventoryAtNativeMemberPhase(client.Player);
            var inventory = Assert.IsType<LegacyNativeInventoryRuntime>(client.Inventory);
            Assert.Same(client, inventory.Owner);
            Assert.Empty(inventory.ClientItems);
            Assert.Null(client.Equipment);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void RepeatedMemberPhaseReplacesOwnerButRetainsHeldOldDictionaryAndItem()
    {
        WithCounter(null, () =>
        {
            var client = new LegacyNativeGameClientRuntime();
            LegacyNativeInventoryLoadComposition.AssignInventoryAtNativeMemberPhase(client.Player);
            var old = client.Inventory!;
            var item = new LegacyNativeGameItemRuntime { Uid = 17u };
            old.AddDBItem(item);
            LegacyNativeInventoryLoadComposition.AssignInventoryAtNativeMemberPhase(client.Player);
            Assert.NotSame(old, client.Inventory);
            Assert.Same(client, client.Inventory!.Owner);
            Assert.Empty(client.Inventory.ClientItems);
            Assert.Same(item, old.ClientItems[17u]);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void MemberPhaseUsesPassedPhysicalPlayerEvenWhenCurrentPlayerWasReplaced()
    {
        var client = new LegacyNativeGameClientRuntime();
        var heldPlayer = client.Player;
        var replacement = new LegacyNativePlayerRuntime(client);
        client.Player = replacement;
        LegacyNativeInventoryLoadComposition.AssignInventoryAtNativeMemberPhase(heldPlayer);
        Assert.Same(replacement, client.Player);
        Assert.Same(client, client.Inventory!.Owner);
        Assert.Same(client, heldPlayer.Owner);
    }

    [Fact]
    public void NullPlayerMemberPhaseFaultDoesNotAlterExistingClientInventory()
    {
        var client = new LegacyNativeGameClientRuntime();
        var original = new LegacyNativeInventoryRuntime(client);
        client.Inventory = original;
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeInventoryLoadComposition.AssignInventoryAtNativeMemberPhase(null!));
        Assert.Same(original, client.Inventory);
    }

    [Fact]
    public void UniqueNonzeroUidInsertsSameItemWithoutEvenReadingNullCounter()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var item = new LegacyNativeGameItemRuntime { Uid = 42u };
            inventory.CheakUp(item);
            Assert.Same(item, inventory.ClientItems[42u]);
            Assert.Equal(42u, item.Uid);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void TwoPhysicalClientsExplicitInventoriesShareCurrentPoolCounterForZeroUids()
    {
        var counter = new LegacyNativeCounter(10u);
        WithCounter(counter, () =>
        {
            var firstClient = new LegacyNativeGameClientRuntime();
            var secondClient = new LegacyNativeGameClientRuntime();
            LegacyNativeInventoryLoadComposition.AssignInventoryAtNativeMemberPhase(firstClient.Player);
            LegacyNativeInventoryLoadComposition.AssignInventoryAtNativeMemberPhase(secondClient.Player);
            var firstInventory = firstClient.Inventory!;
            var secondInventory = secondClient.Inventory!;
            var one = new LegacyNativeGameItemRuntime { Uid = 0u };
            var two = new LegacyNativeGameItemRuntime { Uid = 0u };
            firstInventory.CheakUp(one);
            secondInventory.CheakUp(two);
            Assert.Same(firstClient, firstInventory.Owner);
            Assert.Same(secondClient, secondInventory.Owner);
            Assert.NotSame(firstInventory.ClientItems, secondInventory.ClientItems);
            Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
            Assert.Equal(12u, counter.Count);
            Assert.Equal(11u, one.Uid);
            Assert.Equal(12u, two.Uid);
            Assert.Same(one, firstInventory.ClientItems[11u]);
            Assert.Same(two, secondInventory.ClientItems[12u]);
            Assert.Single(firstInventory.ClientItems);
            Assert.Single(secondInventory.ClientItems);
            Assert.False(firstInventory.ClientItems.ContainsKey(12u));
            Assert.False(secondInventory.ClientItems.ContainsKey(11u));
        });
    }

    [Theory]
    [InlineData(0u, 1u)]
    [InlineData(1u, 2u)]
    [InlineData(uint.MaxValue, 0u)]
    public void ZeroUidConsumesExactlyOneInitialNextIncludingWrappedZero(uint start, uint inserted)
    {
        var counter = new LegacyNativeCounter(start);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var item = new LegacyNativeGameItemRuntime { Uid = 0u };
            checked { inventory.CheakUp(item); }
            Assert.Equal(inserted, counter.Count);
            Assert.Equal(inserted, item.Uid);
            Assert.Same(item, inventory.ClientItems[inserted]);
            Assert.Single(inventory.ClientItems);
        });
    }

    [Fact]
    public void MultipleOccupiedKeysCauseOneDrawPerRetryAndRetainExistingReferences()
    {
        var counter = new LegacyNativeCounter(10u);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var original = new LegacyNativeGameItemRuntime { Uid = 100u };
            var eleven = new LegacyNativeGameItemRuntime { Uid = 11u };
            var twelve = new LegacyNativeGameItemRuntime { Uid = 12u };
            inventory.AddDBItem(original);
            inventory.AddDBItem(eleven);
            inventory.AddDBItem(twelve);
            var item = new LegacyNativeGameItemRuntime { Uid = 100u };
            inventory.CheakUp(item);
            Assert.Equal(13u, counter.Count);
            Assert.Equal(13u, item.Uid);
            Assert.Same(item, inventory.ClientItems[13u]);
            Assert.Same(original, inventory.ClientItems[100u]);
            Assert.Same(eleven, inventory.ClientItems[11u]);
            Assert.Same(twelve, inventory.ClientItems[12u]);
            Assert.Equal(4, inventory.ClientItems.Count);
        });
    }

    [Fact]
    public void ReadmittingSameObjectRetainsOldKeyAliasPointingToNowMutatedUid()
    {
        var counter = new LegacyNativeCounter(10u);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var item = new LegacyNativeGameItemRuntime { Uid = 5u };
            inventory.AddDBItem(item);
            inventory.CheakUp(item);
            Assert.Equal(11u, item.Uid);
            Assert.Equal(11u, counter.Count);
            Assert.Same(item, inventory.ClientItems[5u]);
            Assert.Same(item, inventory.ClientItems[11u]);
            Assert.Equal(11u, inventory.ClientItems[5u].Uid);
            Assert.Equal(2, inventory.ClientItems.Count);
        });
    }

    [Fact]
    public void CollisionLoopMayWrapAndInsertZeroWithoutAnotherZeroGuard()
    {
        var counter = new LegacyNativeCounter(4294967294u);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            inventory.AddDBItem(new LegacyNativeGameItemRuntime { Uid = 999u });
            inventory.AddDBItem(new LegacyNativeGameItemRuntime { Uid = uint.MaxValue });
            var item = new LegacyNativeGameItemRuntime { Uid = 999u };
            inventory.CheakUp(item);
            Assert.Equal(0u, counter.Count);
            Assert.Equal(0u, item.Uid);
            Assert.Same(item, inventory.ClientItems[0u]);
        });
    }

    [Fact]
    public void InitialZeroDrawAndSubsequentZeroAndOneCollisionsUseSameRetryLoop()
    {
        var counter = new LegacyNativeCounter(uint.MaxValue);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var zero = new LegacyNativeGameItemRuntime { Uid = 0u };
            var one = new LegacyNativeGameItemRuntime { Uid = 1u };
            inventory.AddDBItem(zero);
            inventory.AddDBItem(one);
            var item = new LegacyNativeGameItemRuntime { Uid = 0u };
            inventory.CheakUp(item);
            Assert.Equal(2u, item.Uid);
            Assert.Equal(2u, counter.Count);
            Assert.Same(item, inventory.ClientItems[2u]);
            Assert.Same(zero, inventory.ClientItems[0u]);
            Assert.Same(one, inventory.ClientItems[1u]);
        });
    }

    [Fact]
    public void CounterReferenceReplacementBetweenCallsIsObservedWithoutCaching()
    {
        var first = new LegacyNativeCounter(10u);
        WithCounter(first, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var one = new LegacyNativeGameItemRuntime { Uid = 0u };
            inventory.CheakUp(one);
            var replacement = new LegacyNativeCounter(500u);
            LegacyNativePoolItems.ItemCounter = replacement;
            var two = new LegacyNativeGameItemRuntime { Uid = 0u };
            inventory.CheakUp(two);
            Assert.Equal(11u, first.Count);
            Assert.Equal(501u, replacement.Count);
            Assert.Same(one, inventory.ClientItems[11u]);
            Assert.Same(two, inventory.ClientItems[501u]);
        });
    }

    [Fact]
    public void ReplaceableClientItemsFieldUsesNewDictionaryWithoutNormalizingOldOne()
    {
        var counter = new LegacyNativeCounter(900u);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var oldItem = new LegacyNativeGameItemRuntime { Uid = 7u };
            inventory.AddDBItem(oldItem);
            var oldDictionary = inventory.ClientItems;
            var replacement = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
            inventory.ClientItems = replacement;
            var newItem = new LegacyNativeGameItemRuntime { Uid = 7u };
            inventory.CheakUp(newItem);
            Assert.Same(newItem, replacement[7u]);
            Assert.Same(oldItem, oldDictionary[7u]);
            Assert.Equal(900u, counter.Count);
        });
    }

    [Fact]
    public void SeparateInventoriesCanRetainSameNonzeroUidWithoutGlobalCounterUse()
    {
        WithCounter(null, () =>
        {
            var first = new LegacyNativeInventoryRuntime(null);
            var second = new LegacyNativeInventoryRuntime(null);
            var one = new LegacyNativeGameItemRuntime { Uid = 7u };
            var two = new LegacyNativeGameItemRuntime { Uid = 7u };
            first.CheakUp(one);
            second.CheakUp(two);
            Assert.Same(one, first.ClientItems[7u]);
            Assert.Same(two, second.ClientItems[7u]);
            Assert.NotSame(one, two);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void CheakUpHasNoInventoryCapacityPolicy()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            for (uint uid = 100u; uid < 140u; uid++)
                inventory.AddDBItem(new LegacyNativeGameItemRuntime { Uid = uid });
            var item = new LegacyNativeGameItemRuntime { Uid = 7u };
            inventory.CheakUp(item);
            Assert.Equal(41, inventory.ClientItems.Count);
            Assert.Same(item, inventory.ClientItems[7u]);
        });
    }

    [Fact]
    public void AddDbItemIgnoresDuplicateKeyWithoutMutatingEitherItemOrCounter()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var first = new LegacyNativeGameItemRuntime { Uid = 7u };
            var second = new LegacyNativeGameItemRuntime { Uid = 7u };
            inventory.AddDBItem(first);
            inventory.AddDBItem(second);
            Assert.Single(inventory.ClientItems);
            Assert.Same(first, inventory.ClientItems[7u]);
            Assert.Equal(7u, second.Uid);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void AddDbItemKeepsProvidedZeroWithoutAllocating()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var item = new LegacyNativeGameItemRuntime { Uid = 0u };
            inventory.AddDBItem(item);
            Assert.Same(item, inventory.ClientItems[0u]);
            Assert.Equal(0u, item.Uid);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void AddDbItemUsesCurrentItemUidAndLeavesStaleKeyAliasUntouched()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var item = new LegacyNativeGameItemRuntime { Uid = 7u };
            inventory.AddDBItem(item);
            item.Uid = 8u;
            inventory.AddDBItem(item);
            Assert.Same(item, inventory.ClientItems[7u]);
            Assert.Same(item, inventory.ClientItems[8u]);
            Assert.Equal(8u, inventory.ClientItems[7u].Uid);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        });
    }

    [Fact]
    public void LoadedMaximumDoesNotBootstrapCounterOrChangeNextAdmission()
    {
        var counter = new LegacyNativeCounter(1u);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            inventory.AddDBItem(new LegacyNativeGameItemRuntime { Uid = uint.MaxValue });
            Assert.Equal(1u, counter.Count);
            var item = new LegacyNativeGameItemRuntime { Uid = 0u };
            inventory.CheakUp(item);
            Assert.Equal(2u, item.Uid);
            Assert.Equal(2u, counter.Count);
            Assert.True(inventory.ClientItems.ContainsKey(uint.MaxValue));
        });
    }

    [Fact]
    public void NullCheakUpItemFaultsBeforeNullCounterAndNullDictionaryUse()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null) { ClientItems = null! };
            Assert.Throws<NullReferenceException>(() => inventory.CheakUp(null!));
            Assert.Null(LegacyNativePoolItems.ItemCounter);
            Assert.Null(inventory.ClientItems);
        });
    }

    [Fact]
    public void NullCounterForZeroItemFaultsBeforeFirstTryAdd()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var item = new LegacyNativeGameItemRuntime { Uid = 0u };
            Assert.Throws<NullReferenceException>(() => inventory.CheakUp(item));
            Assert.Equal(0u, item.Uid);
            Assert.Empty(inventory.ClientItems);
        });
    }

    [Fact]
    public void NullCounterAfterCollisionLeavesIncomingUidAndExistingEntryUnchanged()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            var existing = new LegacyNativeGameItemRuntime { Uid = 7u };
            inventory.AddDBItem(existing);
            var incoming = new LegacyNativeGameItemRuntime { Uid = 7u };
            Assert.Throws<NullReferenceException>(() => inventory.CheakUp(incoming));
            Assert.Equal(7u, incoming.Uid);
            Assert.Single(inventory.ClientItems);
            Assert.Same(existing, inventory.ClientItems[7u]);
        });
    }

    [Fact]
    public void NullDictionaryForZeroItemFaultsAfterDrawAndUidMutation()
    {
        var counter = new LegacyNativeCounter(9u);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null) { ClientItems = null! };
            var item = new LegacyNativeGameItemRuntime { Uid = 0u };
            Assert.Throws<NullReferenceException>(() => inventory.CheakUp(item));
            Assert.Equal(10u, counter.Count);
            Assert.Equal(10u, item.Uid);
        });
    }

    [Fact]
    public void NullDictionaryForNonzeroItemFaultsWithoutCounterDraw()
    {
        var counter = new LegacyNativeCounter(9u);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null) { ClientItems = null! };
            var item = new LegacyNativeGameItemRuntime { Uid = 7u };
            Assert.Throws<NullReferenceException>(() => inventory.CheakUp(item));
            Assert.Equal(9u, counter.Count);
            Assert.Equal(7u, item.Uid);
        });
    }

    [Fact]
    public void NullDbItemFaultsWithoutCounterUseOrInsertion()
    {
        WithCounter(null, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null);
            Assert.Throws<NullReferenceException>(() => inventory.AddDBItem(null!));
            Assert.Empty(inventory.ClientItems);
        });
    }

    [Fact]
    public void NullDbDictionaryFaultsWithoutAllocatingOrChangingProvidedZero()
    {
        var counter = new LegacyNativeCounter(9u);
        WithCounter(counter, () =>
        {
            var inventory = new LegacyNativeInventoryRuntime(null) { ClientItems = null! };
            var item = new LegacyNativeGameItemRuntime { Uid = 0u };
            Assert.Throws<NullReferenceException>(() => inventory.AddDBItem(item));
            Assert.Equal(9u, counter.Count);
            Assert.Equal(0u, item.Uid);
        });
    }

    private static void WithCounter(LegacyNativeCounter? declared, Action body)
    {
        var original = LegacyNativePoolItems.ItemCounter;
        LegacyNativePoolItems.ItemCounter = declared!;
        try { body(); }
        finally { LegacyNativePoolItems.ItemCounter = original; }
    }
}
