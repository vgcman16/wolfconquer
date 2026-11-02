using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public Task BoundVigorSuccessionRejectsActualSteedPlusCatalogDrift()
    {
        var steed = OwnedEquipmentBehaviorFixture.Item(300_000, "VigorSteed");
        var bonus = ItemAddParser.ParseLine("300000 1 100 20 20 0 20 0 50 5", 5_041);
        var values = Assert.IsType<int[]>(bonus.Values);
        // Use the actual parser's row and a concrete owned catalog container.
        // An interface-targeted collection expression is not this input shape.
        ItemAddBonus[] bonuses = [bonus];
        var catalog = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [], items: [steed], itemAddBonuses: bonuses);
        Assert.Same(bonuses, catalog.ItemAddBonuses);
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0, strength: 0, minHitPoints: 30)
            with { Vitality = 0, Spirit = 10, AttributePoints = 12,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Vigor"] = "4294967295" } };
        var input = new OwnedEquipmentBehaviorInput(character, PlayerItemSnapshot.Empty with
        {
            EquipmentItems = [new PlayerItemSnapshotItem { Uid = 99140, ItemId = 300_000, Position = 12,
                Plus = 1, Durability = 100, MaximumDurability = 100 }],
        });
        return OwnedEquipmentBehaviorFixture.RunAsync(input, catalog, async f =>
        {
            var first = f.Current(); var binding = first.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Equal("50", first.Character.Attributes["Vigor"]);
            var fields = new Dictionary<string, string>(first.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), first with { Character = first.Character with { Attributes = fields } });
            var current = f.Current();
            Assert.True(f.ObserveCurrentVigorQualification());
            var files = f.Data.Files(); int start = f.ActorFrames.Count;
            values[6] = 51; // Actual adopted catalog row, not a replacement owner or query receipt.
            Assert.False(f.ObserveCurrentVigorQualification());
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            Assert.Same(current, f.Current());
            Assert.Equal(start, f.ActorFrames.Count);
            f.Data.AssertFiles(files);
            Assert.Equal((ushort)10, binding.Player.Spirit);
            Assert.Equal((ushort)12, binding.Player.Atributes);
        });
    }
}
