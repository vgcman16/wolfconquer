using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedManiacAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualCastRequiresXpListBeforeAnyCost(bool hasXpList)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = fields[1] = "12700"; fields[2] = "83"; fields[3] = "ManiacDance";
        fields[10] = "30100"; fields[13] = "1"; fields[14] = "1"; fields[15] = "5";
        fields[29] = "9"; fields[33] = "1000";
        var magic = new MagicType(12700, 12700, "ManiacDance", fields);
        var family = new Monster(910, "ManiacAdmission", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, magic: new[] { magic });
        var flags = new ulong[16]; if (hasXpList) flags[0] = 1UL << LegacyRuntimeUpdateTicker.XpListFlag;
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["StatusFlags"] = string.Join('/', flags), ["XPCount"] = "100" },
        };
        var spells = new PlayerSpellSnapshot([new PlayerSpellSnapshotEntry(12700, 0, 0, 0, 0, 0)]);
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty, spells), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://maniac-admission/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var files = f.Data.Files();
            f.ActorFrames.Clear();
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgAttackPacket(new MsgAttackQuery(
                Uid: OwnedEquipmentBehaviorFixture.Actor, OpponentUid: monster.Uid, X: monster.X, Y: monster.Y,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 12700)).ToFrameBytes());
            if (!hasXpList)
            {
                Assert.Same(before, f.Current());
                Assert.Equal(0L, before.Character.MinMana); Assert.Equal(0u, binding.Player.Mana);
                Assert.Equal("100", f.Current().Character.Attributes["Stamina"]);
                Assert.Equal((ushort)100, binding.Player.Stamina);
                Assert.Equal(before.MagicCooldowns, f.Current().MagicCooldowns);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.Empty(f.ActorFrames); f.Data.AssertFiles(files);
                Assert.Contains("xpAdmissionRejected=ManiacDance: XPList status flag is not active", f.Log.ToString());
            }
            else
            {
                Assert.Equal("91", f.Current().Character.Attributes["Stamina"]);
                Assert.Equal((ushort)91, binding.Player.Stamina);
                Assert.True(f.Current().Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute));
                Assert.NotEmpty(f.ActorFrames);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            }
        });
    }
}
