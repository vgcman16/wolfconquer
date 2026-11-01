using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedCombatExperienceRetentionTests
{
    [Fact]
    public Task NonLevelHitPersistsExperienceBeforeItsExactOutput()
    {
        var family = new Monster(910, "RetainedExperience", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var threshold = new LevelExperience(0, 139, 1_000_000_000, 0, 0, 1,
            "0 139 1000000000 0 0", new[] { "0", "139", "1000000000", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, levelExperience: new[] { threshold });
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            Experience = 37,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
        };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-retained-xp/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            Assert.Equal(37L, before.Character.Experience);
            var expectedFiles = f.Data.Files();
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string initial = CharacterSnapshotFileEncoding.Decode(expectedFiles[key], out var encoding);
            Assert.Contains("\r\nExperience=37\r\n", initial);
            f.ActorFrames.Clear();
            byte[]? expectedFrame = null;
            long expectedXp = 0;
            int observed = 0;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != OwnedEquipmentBehaviorFixture.Actor ||
                    !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) ||
                    !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience)) return;
                Assert.Equal(0, observed++);
                var attack = Assert.Single(f.ActorFrames.Select(OwnedEquipmentBehaviorFixture.ReadFrame)
                    .Where(frame => MsgAttackPacket.TryParse(frame, out var hit) && hit.Query.OpponentUid == monster.Uid &&
                        hit.Query.AttackType == MsgAttackType.Physical)
                    .Select(frame => { Assert.True(MsgAttackPacket.TryParse(frame, out var hit)); return hit; }));
                Assert.True(attack.Query.Damage > 0 && attack.Query.Damage < monster.MaxLife);
                // Equal level gives name type 2 and no multiplier; no gems, blessing,
                // double XP or SoulChant and UserExpRate=1. Minimum gain is 10.
                expectedXp = 37 + Math.Max(10, attack.Query.Damage);
                Assert.True(expectedXp < 1_000_000_000);
                expectedFrame = ExperienceFrame(OwnedEquipmentBehaviorFixture.Actor, checked((ulong)expectedXp));
                Assert.Equal(expectedFrame, bytes);
                Assert.Equal(expectedXp, f.Current().Character.Experience);
                Assert.Equal(checked((ulong)expectedXp), binding.Player.Experience);
                Assert.Equal(139L, f.Current().Character.Level);
                var changed = initial.Replace("\r\nExperience=37\r\n", "\r\nExperience=" +
                    expectedXp.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
                expectedFiles[key] = encoding.GetPreamble().Concat(encoding.GetBytes(changed)).ToArray();
                f.Data.AssertFiles(expectedFiles);
            };
            try
            {
                await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgAttackPacket(new MsgAttackQuery(
                    OwnedEquipmentBehaviorFixture.Actor, monster.Uid, monster.X, monster.Y, MsgAttackType.Physical,
                    0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes());
            }
            finally { f.BeforeSend = null; }
            Assert.Equal(1, observed);
            Assert.NotNull(expectedFrame);
            Assert.Single(f.ActorFrames, frame => frame.AsSpan().SequenceEqual(expectedFrame));
            Assert.Equal(expectedXp, f.Current().Character.Experience);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            f.Data.AssertFiles(expectedFiles);
        });
    }

    // Independent protobuf wire construction: uid field 1, single update field 2,
    // update type 5 (experience), value field 2, D13 MsgUpdate opcode 2176.
    private static byte[] ExperienceFrame(uint uid, ulong value)
    {
        var update = new List<byte> { 0x08, 0x05, 0x10 };
        Varint(update, value);
        var payload = new List<byte> { 0x08 };
        Varint(payload, uid);
        payload.Add(0x12); Varint(payload, checked((ulong)update.Count)); payload.AddRange(update);
        var frame = new byte[payload.Count + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2176);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }
}
