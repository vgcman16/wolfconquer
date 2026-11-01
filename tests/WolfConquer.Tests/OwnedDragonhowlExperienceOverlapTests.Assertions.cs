using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlExperienceOverlapTests
{
    // Independent D13 protobuf2176 goldens: recipient1293601 -> A1 FA 4E;
    // max540 ->9C04. No bytes are learned from the operation under test.
    private static readonly byte[] PeerLevelEleven = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 12, 16, 11];
    private static readonly byte[] PeerExperienceNine = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 5, 16, 9];
    private static readonly byte[] PeerRestoredHp = [20, 0, 128, 8, 8, 161, 250, 78, 18, 5, 8, 1, 16, 156, 4, 18, 3, 16, 156, 4];
    private static readonly byte[] PeerDeadHp = [17, 0, 128, 8, 8, 161, 250, 78, 18, 5, 8, 1, 16, 156, 4, 18, 0];

    private static Dictionary<string, byte[]> ExpectedFiles(OwnedEquipmentBehaviorFixture f)
    {
        var files = f.Data.Files();
        void Set(uint uid, params (string Before, string After)[] replacements)
        {
            var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath(uid));
            string text = Encoding.UTF8.GetString(files[key]);
            foreach (var (before, after) in replacements)
            {
                var line = "\r\n" + before + "\r\n";
                Assert.Contains(line, text);
                text = text.Replace(line, "\r\n" + after + "\r\n", StringComparison.Ordinal);
            }
            files[key] = Encoding.UTF8.GetBytes(text);
        }
        Set(Actor, ("MinMana=500", "MinMana=493"), ("ConquerPoints=639778904", "ConquerPoints=639778909"));
        Set(Peer, ("Level=10", "Level=11"), ("Experience=0", "Experience=9"),
            ("Vitaliti=0", "Vitaliti=20"), ("Spirit=10", "Spirit=20"), ("MinHitPoints=30", "MinHitPoints=0"));
        return files;
    }

    private static void AssertWireOrder(List<(uint Recipient, byte[] Bytes)> wire, uint monsterUid, ushort spellId, uint playerDamage)
    {
        int level = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(PeerLevelEleven));
        int experience = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(PeerExperienceNine));
        Assert.True(level >= 0 && experience > level);
        int restored = wire.FindLastIndex(experience - 1, experience,
            row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(PeerRestoredHp));
        Assert.True(restored > level && restored < experience);
        var statuses = Enumerable.Range(0, wire.Count).Where(index => wire[index].Recipient == Peer &&
            OwnedEquipmentBehaviorFixture.ReadFrame(wire[index].Bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode).ToArray();
        Assert.Equal(2, statuses.Length); // Level query, then the actual death query.
        Assert.True(statuses[0] > level && statuses[0] < restored);
        Assert.True(statuses[1] > experience);
        foreach (int index in statuses)
        {
            Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(wire[index].Bytes), out var status));
            Assert.Equal(540U, status.MaxHP); Assert.Equal(600U, status.MaxMana); Assert.Equal(21U, status.MagicAttack);
        }
        int zero = wire.FindIndex(row => row.Recipient == Peer && row.Bytes.AsSpan().SequenceEqual(PeerDeadHp));
        Assert.True(zero > statuses[1]);
        int cps = wire.FindIndex(row => row.Recipient == Actor && IsUpdate(row.Bytes, Actor, LegacyMsgUpdateDataType.ConquerPoints, 639_778_909));
        Assert.True(cps >= 0 && cps < level); // Actual reward writer precedes Team XP.
        var animations = wire.Where(row => row.Recipient == Actor).Select(row =>
            LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var packet) ? packet : null)
            .Where(packet => packet?.SpellId == spellId).ToArray();
        var animation = Assert.Single(animations)!;
        Assert.Equal(Actor, animation.AttackerUid);
        Assert.Collection(animation.Targets,
            hit => { Assert.Equal(monsterUid, hit.Uid); Assert.Equal(4_200U, hit.Damage); Assert.True(hit.Hit); },
            hit => { Assert.Equal(Peer, hit.Uid); Assert.Equal(playerDamage, hit.Damage); Assert.True(hit.Hit); });
        int animated = wire.FindIndex(row => row.Recipient == Actor &&
            LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var packet) && packet.SpellId == spellId);
        Assert.True(animated > zero);
        Assert.DoesNotContain(wire, row => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical); // The physical command was replaced by this real proc.
    }

    private static bool IsUpdate(byte[] bytes, uint uid, LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == uid && packet.Updates.Any(row => row.Type == type && row.Value == value);
}
