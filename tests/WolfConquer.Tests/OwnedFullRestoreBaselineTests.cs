using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedFullRestoreBaselineTests
{
    // Uses only existing target APIs, so Root can capture old-product failure
    // before any FullRestore production hunk. Both-full does not settle the
    // unresolved HP-full/Mana-low gameplay rule.
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task MissingRequiredStackMetadataOrNoResourceImprovementCannotDebit(bool missingMetadata)
    {
        const uint uid = 2001, type = 723726;
        var character = Fixture.Character(name: "OwnedFullRestoreBaseline", reborn: 0,
            minHitPoints: missingMetadata ? 10 : 100000) with
        {
            MinMana = missingMetadata ? 40 : 5000, MaxMana = 5000,
            Attributes = new Dictionary<string, string> { ["MaxHitpoints"] = "100000" }
        };
        var items = new PlayerItemSnapshot([new() { Uid = uid, ItemId = type, StackSize = 3,
            Durability = 1, MaximumDurability = 1 }], [], [], [], []);
        var catalog = missingMetadata ? Fixture.Catalog() : Fixture.Catalog(Fixture.Item(type, "OwnedFullRestoreBaseline",
            new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = "10", [ItemType.DurabilityFieldIndex] = "1" }));
        return Fixture.RunAsync(new(character, items), catalog, async f =>
        {
            var before = f.Current(); var player = Assert.IsType<LegacyNativePlayerRuntime>(before.NativePlayer);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(100000U, LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(before.Character,
                checked((uint)before.Character.MinHitPoints), before.PlayerItems,
                before.CombatGear ?? CombatGearSnapshot.Empty, catalog, before.AlternateEquipmentActive));
            var files = f.Data.Files(); var first = f.ActorFrames.Count; var nativeMana = player.Mana;
            await f.DispatchAsync(new MsgItemUsagePacket(uid, 0, MsgItemUsageAction.Equip, 1234, 0, 0, 0));
            Assert.Equal(before.Character.MinHitPoints, f.Current().Character.MinHitPoints);
            Assert.Equal(before.Character.MinMana, f.Current().Character.MinMana);
            Assert.Same(before, f.Current()); Assert.Null(f.Current().LastLegacyPotionUseUtc);
            Assert.Same(Assert.Single(before.PlayerItems.InventoryItems), Assert.Single(f.Current().PlayerItems.InventoryItems));
            Assert.Equal((ushort)3, Assert.Single(f.Current().PlayerItems.InventoryItems).StackSize);
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
            Assert.Equal(checked((int)before.Character.MinHitPoints), player.HitPoints); Assert.Equal(nativeMana, player.Mana);
            Assert.False(binding.IsRetired); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.DoesNotContain("runtime exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
        });
    }
}
