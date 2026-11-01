using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static void AssertOwnedHawksOriginalResponses(WorldSession updatedSession, byte[][] sentSnapshot, string output)
    {
        const uint duration = 33;
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, HawksAmbitionStatusFlag));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(HawksAmbitionAttachStatusSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level); Assert.Equal(0, updatedSpell.Experience);
        var interactionIndex = SingleFrameIndex(sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) && packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == HawksAmbitionAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, HawksAmbitionStatusFlag)));
        var archiveIndex = SingleFrameIndex(sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == HawksAmbitionStatusFlag &&
                entry.Time == duration && entry.Damage == 0 && entry.SpellLevel == 0));
        var animationIndex = SingleFrameIndex(sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == HawksAmbitionAttachStatusSpellId);
        var equipmentIndex = SingleFrameIndex(sentSnapshot, frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        var manaIndex = SingleFrameIndex(sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Mana && entry.Value == 493));
        Assert.True(manaIndex < interactionIndex);
        Assert.True(interactionIndex < statusIndex); Assert.True(statusIndex < archiveIndex);
        Assert.True(archiveIndex < animationIndex); Assert.True(animationIndex < equipmentIndex);

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid); Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(0, spell.X); Assert.Equal(0, spell.Y); Assert.Equal(HawksAmbitionAttachStatusSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid); Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit); Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot)).Action);
        var spellUpdate = Assert.Single(sentSnapshot.Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
            ? packet : (LegacySpellPacket?)null).Where(packet => packet is not null).Select(packet => packet!.Value));
        Assert.Equal(HawksAmbitionAttachStatusSpellId, spellUpdate.Id); Assert.Equal(1, spellUpdate.Level);
        Assert.Equal(0, spellUpdate.Experience);
        Assert.Contains("WorldServer handled legacy HawksAmbition AttachStatus magic", output);
        Assert.Contains("source=AttachStatus.cs case 15920", output); Assert.Contains("appliedExperience=500", output);

        // Complete independent current2135/2176/2239/2137/2101 codec images.
        // These are D13 owned handler assertions, not D11 native acceptance.
        Assert.Equal(OwnedHawksInteractionGolden(), sentSnapshot[interactionIndex]);
        Assert.Equal(OwnedHawksUpdateGolden((1, 2UL), (2, 493UL)), sentSnapshot[manaIndex]);
        var status = new List<(int Field, ulong Value)> { (1, 25) };
        for (int word = 0; word < 18; word++) status.Add((3, word == 8 ? 1UL : 0UL));
        Assert.Equal(OwnedHawksUpdateGolden(status.ToArray()), sentSnapshot[statusIndex]);
        Assert.Equal(OwnedHawksUpdateGolden((1, 120UL), (4, 256UL), (5, 33UL)), sentSnapshot[archiveIndex]);
        var animation = new List<byte>();
        foreach (var pair in new (int, ulong)[] { (1, PlayerUid), (2, PlayerUid), (5, 15920), (6, 0), (7, 0), (8, 0), (9, 0) })
            OwnedHawksField(animation, pair.Item1, pair.Item2);
        var hit = new List<byte>();
        foreach (var pair in new (int, ulong)[] { (1, PlayerUid), (2, 0), (3, 1), (4, 0), (6, 0), (7, 0), (8, 0), (9, 0) })
            OwnedHawksField(hit, pair.Item1, pair.Item2);
        OwnedHawksBytes(animation, 10, hit);
        Assert.Equal(OwnedHawksFrame(2239, animation.ToArray()), sentSnapshot[animationIndex]);
        var equipment = new byte[83]; BinaryPrimitives.WriteUInt16LittleEndian(equipment.AsSpan(12), 46);
        Assert.Equal(OwnedHawksFrame(2137, equipment), sentSnapshot[equipmentIndex]);
        var progress = new byte[21]; BinaryPrimitives.WriteUInt16LittleEndian(progress.AsSpan(4), 15920);
        BinaryPrimitives.WriteUInt16LittleEndian(progress.AsSpan(8), 1);
        var progressIndex = SingleFrameIndex(sentSnapshot, frame => LegacySpellPacket.TryParse(frame, out _));
        Assert.True(equipmentIndex < progressIndex);
        Assert.Equal(OwnedHawksFrame(2101, progress), sentSnapshot[progressIndex]);
    }

    private static byte[] OwnedHawksInteractionGolden()
    {
        var payload = new List<byte>();
        foreach (var pair in new (int, ulong)[] { (4, PlayerUid), (5, PlayerUid), (7, 0), (8, 0), (13, 24),
            (15, 0), (17, 0), (19, 15920), (21, 0), (25, 0), (26, 0) })
            OwnedHawksField(payload, pair.Item1, pair.Item2);
        return OwnedHawksFrame(2135, payload.ToArray());
    }

    private static byte[] OwnedHawksUpdateGolden(params (int Field, ulong Value)[] entries)
    {
        var payload = new List<byte>(); var entry = new List<byte>();
        OwnedHawksField(payload, 1, PlayerUid);
        foreach (var value in entries) OwnedHawksField(entry, value.Field, value.Value);
        OwnedHawksBytes(payload, 2, entry);
        return OwnedHawksFrame(2176, payload.ToArray());
    }

    private static byte[] OwnedHawksFrame(ushort opcode, byte[] payload)
    {
        var frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), opcode); payload.CopyTo(frame, 4); return frame;
    }

    private static void OwnedHawksField(List<byte> bytes, int field, ulong value)
    { OwnedHawksVarint(bytes, checked((ulong)field * 8)); OwnedHawksVarint(bytes, value); }
    private static void OwnedHawksBytes(List<byte> bytes, int field, List<byte> payload)
    { OwnedHawksVarint(bytes, checked((ulong)field * 8 + 2)); OwnedHawksVarint(bytes, checked((ulong)payload.Count)); bytes.AddRange(payload); }
    private static void OwnedHawksVarint(List<byte> bytes, ulong value)
    {
        while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }
}
