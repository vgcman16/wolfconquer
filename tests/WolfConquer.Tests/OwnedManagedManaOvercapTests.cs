using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedManagedManaOvercapTests
{
    [Fact]
    public Task ActualAdmittedOverCapManaPotionIsNoChangeWithoutAnItemCooldownOrOutputDebit()
    {
        const uint itemUid = 2001, type = 1_001_000;
        var character = Fixture.Character(name: "OwnedOvercapMana", reborn: 0, minHitPoints: 10) with
            { MinMana = 6000, MaxMana = 5000, Money = 1000, ConquerPoints = 100 };
        var items = new PlayerItemSnapshot([new() { Uid = itemUid, ItemId = type, StackSize = 3,
            Durability = 1, MaximumDurability = 1 }], [], [], [], []);
        var catalog = Fixture.Catalog(Fixture.Item(type, "OwnedOvercapManaPotion", new Dictionary<int, string>
            { [ItemType.ItemMPFieldIndex] = "70", [ItemType.StackSizeFieldIndex] = "10", [ItemType.DurabilityFieldIndex] = "1" }));
        return Fixture.RunAsync(new(character, items), catalog, async f =>
        {
            var before = f.Current(); var player = Assert.IsType<LegacyNativePlayerRuntime>(before.NativePlayer);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var row = Assert.Single(before.PlayerItems.InventoryItems); var physicalMana = player.Mana;
            Assert.Equal(6000L, before.Character.MinMana); Assert.Equal(5000L, before.Character.MaxMana);
            Assert.Equal(6000L, ReadSaved(f).MinMana); Assert.Equal(5000L, ReadSaved(f).MaxMana);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired);
            Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection(Fixture.Actor), before, out var pin, out var checkedPin));
            var files = f.Data.Files(); var first = f.ActorFrames.Count;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                await f.DispatchAsync(new MsgItemUsagePacket(itemUid, 0, MsgItemUsageAction.Equip, 1234, 0, 0, 0));
                Assert.Equal(6000L, f.Current().Character.MinMana); Assert.Equal(6000L, ReadSaved(f).MinMana);
                Assert.Equal(5000L, f.Current().Character.MaxMana); Assert.Equal(5000L, ReadSaved(f).MaxMana);
                Assert.Same(before, f.Current()); Assert.Null(f.Current().LastLegacyPotionUseUtc);
                Assert.Same(row, Assert.Single(f.Current().PlayerItems.InventoryItems)); Assert.Equal((ushort)3, row.StackSize);
                Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
                Assert.Equal(10, player.HitPoints); Assert.Equal(physicalMana, player.Mana);
                Assert.Equal(10L, f.Current().Character.MinHitPoints); Assert.Equal(10L, ReadSaved(f).MinHitPoints);
                Assert.Same(binding, f.Current().NativeGameClient!.OrdinarySpellBinding);
                Assert.False(binding.IsRetired); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection(Fixture.Actor), f.Current(), out var retained, out var retainedChecked));
                Assert.Equal(pin, retained); Assert.Equal(checkedPin, retainedChecked);
            }
            Assert.DoesNotContain("runtime exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
        });
    }

    private static CharacterSnapshot ReadSaved(Fixture f) => CharacterSnapshotParser.ParseText(
        CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath()), out _));
}
