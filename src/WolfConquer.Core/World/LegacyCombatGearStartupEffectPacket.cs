using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static class LegacyCombatGearStartupEffectPacket
{
    public const ushort RevelatorSpellId = 0x5410;
    public const uint RevelatorFlag = 0x198;
    public const uint RevelatorDamage = 500;
    public const int StatusFlagVectorLength = 18;

    public static bool TryCreatePirateRevelatorStartupFrames(
        CharacterSnapshot snapshot,
        PlayerSpellSnapshot playerSpells,
        out IReadOnlyList<byte[]> frames)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerSpells);

        frames = [];
        if (snapshot.Class / 1000 != 7 ||
            !TryFindSpell(playerSpells, RevelatorSpellId, out var spell))
        {
            return false;
        }

        var uid = LegacyCombatGearPacket.CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        var x = CheckedUInt16(snapshot.X, nameof(snapshot.X));
        var y = CheckedUInt16(snapshot.Y, nameof(snapshot.Y));

        frames =
        [
            CreateRevelatorInteraction(uid, x, y, spell.Level).ToFrameBytes(),
            CreateStatusFlagVectorUpdate(uid).ToFrameBytes(),
            CreateArchiveSkillUpdate(uid).ToFrameBytes(),
            CreateSpellAnimation(uid, spell.Level, spell.UseSpellSoul),
        ];
        return true;
    }

    public static MsgAttackPacket CreateRevelatorInteraction(uint uid, ushort x, ushort y, ushort spellLevel)
    {
        return new MsgAttackPacket(
            new MsgAttackQuery(
                uid,
                uid,
                x,
                y,
                MsgAttackType.None,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                MsgAttackEffect.None,
                Unknown: 0,
                SpellLevel: spellLevel,
                SpellId: RevelatorSpellId));
    }

    public static LegacyMsgUpdatePacket CreateStatusFlagVectorUpdate(uint uid)
    {
        var values = new ulong[StatusFlagVectorLength];
        var index = RevelatorFlag / 32;
        values[index] = 1UL << (int)(RevelatorFlag % 32);
        return new LegacyMsgUpdatePacket(
            uid,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.StatusFlag, Value: 0, Values: values)]);
    }

    public static LegacyMsgUpdatePacket CreateArchiveSkillUpdate(uint uid)
    {
        return new LegacyMsgUpdatePacket(
            uid,
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.ArchiveSkill,
                    Value: 0,
                    Flag: RevelatorFlag,
                    Damage: RevelatorDamage)
            ]);
    }

    public static byte[] CreateSpellAnimation(uint uid, ushort spellLevel, byte levelHu)
    {
        var targetPayload = new List<byte>();
        LegacyCombatGearPacket.WriteRequiredVarintField(targetPayload, 1, uid);
        LegacyCombatGearPacket.WriteRequiredVarintField(targetPayload, 2, 0);
        LegacyCombatGearPacket.WriteRequiredVarintField(targetPayload, 3, 1);
        LegacyCombatGearPacket.WriteRequiredVarintField(targetPayload, 4, 0);
        LegacyCombatGearPacket.WriteRequiredVarintField(targetPayload, 6, 0);
        LegacyCombatGearPacket.WriteRequiredVarintField(targetPayload, 7, 0);
        LegacyCombatGearPacket.WriteRequiredVarintField(targetPayload, 8, 0);
        LegacyCombatGearPacket.WriteRequiredVarintField(targetPayload, 9, 0);

        var payload = new List<byte>();
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, 1, uid);
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, 2, uid);
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, 5, RevelatorSpellId);
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, 6, checked((uint)(byte)spellLevel));
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, 7, 0);
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, 8, levelHu);
        LegacyCombatGearPacket.WriteRequiredVarintField(payload, 9, 0);
        LegacyCombatGearPacket.WriteLengthDelimitedField(payload, 10, targetPayload);

        var writer = new PacketWriter(WorldPacketOpcodes.LegacySpellUse, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryFindSpell(
        PlayerSpellSnapshot playerSpells,
        ushort spellId,
        out PlayerSpellSnapshotEntry spell)
    {
        foreach (var candidate in playerSpells.Spells)
        {
            if (candidate.Id == spellId)
            {
                spell = candidate;
                return true;
            }
        }

        spell = default!;
        return false;
    }

    private static ushort CheckedUInt16(long value, string name)
    {
        if (value < 0 || value > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must fit UInt16.");
        }

        return (ushort)value;
    }
}
