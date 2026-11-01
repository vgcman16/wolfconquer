using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Genuine admitted attack and later status query. This deliberately describes
// the required rebuilt query result; it is not a forged completed-query stamp.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedCombatExperienceAutoStatsTests
{
    [Fact]
    public Task AutoStatLevelUpRebuildsCompletedEquipmentStatusBeforeLaterStatusQuery()
    {
        var head = OwnedEquipmentBehaviorFixture.Item(111_095, fields: new Dictionary<int, string>
            { [18] = "4000", [19] = "100" });
        var necklace = OwnedEquipmentBehaviorFixture.Item(120_095, fields: new Dictionary<int, string>
            { [18] = "1000", [19] = "50" });
        var equipped = new PlayerItemSnapshotItem[]
        {
            new() { Uid = 99101, ItemId = 111_095, Position = 1, Durability = 100, MaximumDurability = 100, StackSize = 1 },
            new() { Uid = 99102, ItemId = 120_095, Position = 2, Durability = 100, MaximumDurability = 100, StackSize = 1 },
        };
        var family = new Monster(910, "OwnedAutoStats", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "10",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var rows = new[]
        {
            new LevelExperience(0, 10, 1, 0, 0, 1, "0 10 1 0 0", new[] { "0", "10", "1", "0", "0" }),
            new LevelExperience(0, 11, 1_000_000, 0, 0, 2, "0 11 1000000 0 0", new[] { "0", "11", "1000000", "0", "0" }),
        };
        var stats = new[] { new ClassLevelStat("Taoist", 11, 0, 0, 20, 20,
            "fixture://Taoist.ini", 1, "11", "0,0,20,20", "11=0,0,20,20", new[] { "0", "0", "20", "20" }) };
        var catalog = LegacyItemWorldDataFixture.Create(new ItemType[] { head, necklace }, [],
            classLevelStats: stats, monsters: new[] { family }, levelExperience: rows);
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, level: 10, reborn: 0,
            strength: 0, agility: 0, mapId: 1011, x: 430, y: 380, minHitPoints: 200) with
        {
            Vitality = 0, Spirit = 10, AttributePoints = 12, MinMana = 400, MaxMana = 450,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
        };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, new PlayerItemSnapshot([], [], [], equipped, [])), catalog, async f =>
        {
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(before, out var oldMaximum));
            Assert.Equal(5030u, oldMaximum);
            Assert.Equal(450u, LegacyPhysicalStatusOverlay.ForOwner(before).MaxMana);
            Assert.Equal(400L, before.Character.MinMana); Assert.Equal(400u, binding.Player.Mana);
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-auto-stats/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgAttackPacket(new MsgAttackQuery(
                Uid: OwnedEquipmentBehaviorFixture.Actor, OpponentUid: monster.Uid, X: monster.X, Y: monster.Y,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes());
            var after = f.Current();
            Assert.Equal(11, after.Character.Level);
            Assert.Equal(20, after.Character.Vitality); Assert.Equal(20, after.Character.Spirit);
            Assert.Equal((ushort)20, binding.Player.Vitality); Assert.Equal((ushort)20, binding.Player.Spirit);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damaged));
            Assert.True(damaged.Hitpoints < monster.Hitpoints);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(after, out var maximum));
            Assert.Equal(5540u, maximum);
            Assert.Equal(750u, LegacyPhysicalStatusOverlay.ForOwner(after).MaxMana);
            Assert.Equal(400L, after.Character.MinMana); Assert.Equal(400u, binding.Player.Mana);
            int first = f.ActorFrames.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor,
                new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
            var response = Assert.Single(f.ActorFrames.Skip(first));
            Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(response), out var status));
            Assert.Equal(5540u, status.MaxHP); Assert.Equal(750u, status.MaxMana); Assert.Equal(21u, status.MagicAttack);
        });
    }
}
