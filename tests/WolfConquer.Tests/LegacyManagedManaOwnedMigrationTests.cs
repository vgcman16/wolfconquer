using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

// These replace only the three historical Mana-potion success methods whose
// handoff-only entry supplied no ordinary/physical/UID owner. Keep this class
// separate so unrelated legacy tests do not acquire native fixture prerequisites.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyManagedManaOwnedMigrationTests
{
    private const uint PotionType = 1_001_000;

    [Fact]
    public Task HandlerItemUsageManaPotionRestoresManaAndConsumesItem() => Run(2001, 0, async f =>
    {
        var first = f.ActorFrames.Count;
        await f.DispatchAsync(new MsgItemUsagePacket(2001, 0, MsgItemUsageAction.Equip, 1234, 0, 0, 0));
        Assert.Equal(first + 2, f.ActorFrames.Count);
        AssertRemoval(f.ActorFrames[first], 2001);
        AssertManaUpdate(f.ActorFrames[first + 1]);
        AssertManagedAndSaved(f); Assert.Empty(f.Current().PlayerItems.InventoryItems);
        Assert.Empty(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(Fixture.Actor).InventoryItems);
        AssertNoGaps(f);
    });

    [Fact]
    public Task HandlerItemUsageManaPotionRestoresManaConsumesItemAndSendsManaUpdate() => Run(5_337_020, 0, async f =>
    {
        var first = f.ActorFrames.Count;
        await f.DispatchAsync(new MsgItemUsagePacket(5_337_020, 0, MsgItemUsageAction.Equip, 511_043_495, 0, 0, 0));
        Assert.Equal(first + 2, f.ActorFrames.Count);
        AssertRemoval(f.ActorFrames[first], 5_337_020);
        AssertManaUpdate(f.ActorFrames[first + 1]);
        AssertManagedAndSaved(f); Assert.Empty(f.Current().PlayerItems.InventoryItems);
        Assert.Empty(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(Fixture.Actor).InventoryItems);
        AssertNoGaps(f);
    });

    [Fact]
    public Task ManaPotionRestoresManaAndPersistsCharacterAndStackedItemToDisk() => Run(5_337_120, 2, async f =>
    {
        var first = f.ActorFrames.Count;
        await f.DispatchAsync(new MsgItemUsagePacket(5_337_120, 0, MsgItemUsageAction.Equip, 511_043_495, 0, 0, 0));
        Assert.Equal(first + 2, f.ActorFrames.Count);
        Assert.True(LegacyGameItemPacket.TryParse(Fixture.ReadFrame(f.ActorFrames[first]), out var item));
        Assert.Equal(5_337_120U, item.Uid); Assert.Equal(PotionType, item.ItemId);
        Assert.Equal((ushort)1, item.StackSize); Assert.Equal(LegacyGameItemPacket.UpdateItemMode, item.Mode);
        AssertManaUpdate(f.ActorFrames[first + 1]); AssertManagedAndSaved(f);
        var saved = Assert.Single(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(Fixture.Actor).InventoryItems);
        Assert.Equal(5_337_120U, saved.Uid); Assert.Equal(PotionType, saved.ItemId); Assert.Equal((ushort)1, saved.StackSize);
        var current = Assert.Single(f.Current().PlayerItems.InventoryItems);
        Assert.Equal(saved.Uid, current.Uid); Assert.Equal(saved.ItemId, current.ItemId); Assert.Equal(saved.StackSize, current.StackSize);
        AssertNoGaps(f);
    });

    private static Task Run(uint uid, ushort stack, Func<Fixture, Task> body)
    {
        var character = Fixture.Character(name: "OwnedManaMigration", reborn: 0, strength: 100, agility: 100,
            x: 826, y: 565, minHitPoints: 10_000) with
            { MinMana = 40, MaxMana = 5000, Money = 50000, ConquerPoints = 0, BoundConquerPoints = 0 };
        var items = new PlayerItemSnapshot([new() { Uid = uid, ItemId = PotionType, StackSize = stack,
            Durability = 1, MaximumDurability = 1 }], [], [], [], []);
        var catalog = Fixture.Catalog(Fixture.Item(PotionType, "OwnedManaMigrationPotion", new Dictionary<int, string>
            { [ItemType.ItemMPFieldIndex] = "70", [ItemType.StackSizeFieldIndex] = "10", [ItemType.DurabilityFieldIndex] = "1" }));
        return Fixture.RunAsync(new(character, items), catalog, async f =>
        {
            var initial = f.Current(); Assert.Equal(40L, initial.Character.MinMana);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(initial.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired);
            AssertNoGaps(f);
            bool observed = false;
            f.BeforeSend = (recipient, _) =>
            {
                if (recipient != Fixture.Actor || observed) return;
                observed = true; AssertManagedAndSaved(f);
                var saved = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(Fixture.Actor);
                if (stack <= 1) Assert.Empty(saved.InventoryItems);
                else Assert.Equal((ushort)(stack - 1), Assert.Single(saved.InventoryItems).StackSize);
                Assert.Same(binding, f.Current().NativeGameClient!.OrdinarySpellBinding);
                Assert.False(binding.IsRetired); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(f.Recovery.IsBlocked);
            };
            await body(f); f.BeforeSend = null;
            Assert.True(observed, f.Log.ToString());
            Assert.Equal(initial.Character.Money, f.Current().Character.Money);
            Assert.Equal(initial.Character.ConquerPoints, f.Current().Character.ConquerPoints);
            Assert.Equal(initial.Character.BoundConquerPoints, f.Current().Character.BoundConquerPoints);
            Assert.NotNull(f.Current().LastLegacyPotionUseUtc);
        });
    }

    private static void AssertRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(Fixture.ReadFrame(bytes), out var item));
        Assert.Equal(new MsgItemUsagePacket(uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0), item);
    }
    private static void AssertManaUpdate(byte[] bytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(Fixture.ReadFrame(bytes), out var mana));
        Assert.Equal(Fixture.Actor, mana.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 110)], mana.Updates);
    }
    private static void AssertManagedAndSaved(Fixture f)
    {
        var current = f.Current(); Assert.Equal(110L, current.Character.MinMana); Assert.Equal(5000L, current.Character.MaxMana);
        Assert.Equal("110", current.Character.Attributes["MinMana"]);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath()), out _));
        Assert.Equal(110L, saved.MinMana); Assert.Equal(5000L, saved.MaxMana);
        Assert.Equal(10_000L, saved.MinHitPoints); Assert.Equal(10_000L, current.Character.MinHitPoints);
        Assert.Equal(10_000, Assert.IsType<LegacyNativePlayerRuntime>(current.NativePlayer).HitPoints);
        // No required stale/zero native Mana or absent native item containers:
        // this slice proves managed/file publication and retains the real HP owner.
    }
    private static void AssertNoGaps(Fixture f)
    {
        var log = f.Log.ToString();
        Assert.DoesNotContain("PARITY-BUG", log, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime exception", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage mana consumable missing ItemMP", log);
        Assert.DoesNotContain("PARITY-GAP MsgItemUsage consumable missing itemtype", log);
        Assert.DoesNotContain("non-equipment item effects are not modeled", log);
    }
}
