using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedNpcStatHoldTests
{
    [Fact]
    public Task BoundLevelGrantWithUnchangedStatsStillStopsBeforePersistence()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 7051, level: 119, reborn: 0,
            strength: 10, agility: 10, mapId: 1000, minHitPoints: 200) with { AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Equal(119L, before.Character.Level); Assert.Equal(12L, before.Character.AttributePoints);
            var files = f.Data.Files(); int first = f.ActorFrames.Count;
            var context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(OwnedEquipmentBehaviorFixture.Actor), before));
            Assert.True(await WorldPacketHandlers.TryHandleLegacySocialCraftingServicesNpcAsync(
                context, before, 19039, 3, "", CancellationToken.None));
            Assert.Contains("held bound LevelAbdo grant", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Same(before, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(119L, f.Current().Character.Level); Assert.Equal((ushort)12, binding.Player.Atributes);
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
        });
    }

    [Theory]
    [InlineData("reallot-reset")]
    [InlineData("reallot-allocation")]
    [InlineData("level-abdo")]
    [InlineData("admin-disabled")]
    public Task RealAdmittedNpcHandlerDoesNotPersistOrPublishHeldStatMutation(string operation)
    {
        const uint actor = OwnedEquipmentBehaviorFixture.Actor, dragonBall = 1_088_000;
        var baseCharacter = OwnedEquipmentBehaviorFixture.Character(classCode: 7051, level: 130, reborn: 2,
            strength: 10, agility: 10, mapId: 1000, minHitPoints: 200);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["ExtraAtributes"] = "0", ["FRL"] = "130", ["SRL"] = "130" };
        var character = baseCharacter with { Attributes = attributes, AttributePoints = 12 };
        var item = new PlayerItemSnapshotItem { Uid = 2_100_101, ItemId = dragonBall, StackSize = 1 };
        var input = new OwnedEquipmentBehaviorInput(character, new([item], [], [], [], []));
        return OwnedEquipmentBehaviorFixture.RunAsync(input,
            OwnedEquipmentBehaviorFixture.Catalog(OwnedEquipmentBehaviorFixture.Item(dragonBall)), async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var stats = (binding.Player.Strength, binding.Player.Agility, binding.Player.Vitality,
                binding.Player.Spirit, binding.Player.Atributes);
            var files = f.Data.Files(); int first = f.ActorFrames.Count;
            var context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(actor), before));
            bool handled = operation switch
            {
                "reallot-reset" => await WorldPacketHandlers.TryHandleLegacyUtilityNpcAsync(context, before, 2542, 9, "", CancellationToken.None),
                "reallot-allocation" => await WorldPacketHandlers.TryHandleLegacyUtilityNpcAsync(context, before, 2542, 4, "3", CancellationToken.None),
                "level-abdo" => await WorldPacketHandlers.TryHandleLegacySocialCraftingServicesNpcAsync(context, before, 19039, 3, "", CancellationToken.None),
                _ => await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(context, before, 77839, 60, CancellationToken.None),
            };
            Assert.True(handled);
            Assert.Same(before, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(stats, (binding.Player.Strength, binding.Player.Agility, binding.Player.Vitality,
                binding.Player.Spirit, binding.Player.Atributes));
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
            Assert.Equal(item.Uid, Assert.Single(f.Current().PlayerItems.InventoryItems).Uid);
            Assert.Contains(operation switch
            {
                "reallot-reset" => "held bound ReallotPoints reset",
                "reallot-allocation" => "held bound ReallotPoints allocation",
                "level-abdo" => "held bound LevelAbdo grant",
                _ => "disabled LegacyNpc admin utility",
            }, f.Log.ToString(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public Task BoundNativeExtraPointsRefuseBeforeEitherFieldOrOutputChanges()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 7051, level: 130, reborn: 2,
            strength: 10, agility: 10, mapId: 1000, minHitPoints: 200) with { AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var files = f.Data.Files(); int first = f.ActorFrames.Count;
            ushort extra = binding.Player.ExtraAtributes, points = binding.Player.Atributes;
            // A bound mutation must refuse before it can touch either the
            // extra field or an output packet; no native buffer is needed.
            var error = Assert.Throws<InvalidOperationException>(() => binding.Player.AddExtraAtributes(null!, 3));
            Assert.Equal("ordinaryActorScalarsRequireOwnedStorePlan", error.Message);
            Assert.Equal(extra, binding.Player.ExtraAtributes); Assert.Equal(points, binding.Player.Atributes);
            Assert.Same(before, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
            return Task.CompletedTask;
        });
    }
}
