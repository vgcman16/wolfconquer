using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacySpellAnimationTarget(
    uint Uid,
    uint Damage,
    bool Hit,
    MsgAttackEffect Effect,
    ushort NewX = 0,
    ushort NewY = 0,
    uint Unknown = 0,
    uint TailedBeast = 0);

public sealed record LegacySpellAnimationPacket(
    uint AttackerUid,
    uint AttackedUid,
    ushort X,
    ushort Y,
    ushort SpellId,
    byte SpellLevel,
    ushort NextSpell,
    byte LevelHu,
    byte SpellEffect,
    IReadOnlyList<LegacySpellAnimationTarget> Targets)
{
    private const int AttackerUidField = 1;
    private const int AttackedUidField = 2;
    private const int XField = 3;
    private const int YField = 4;
    private const int SpellIdField = 5;
    private const int SpellLevelField = 6;
    private const int NextSpellField = 7;
    private const int LevelHuField = 8;
    // Decompiled MsgSpellAnimation stores MoveLine's bomb flag in ProtoMember 9.
    private const int SpellEffectField = 9;
    private const int TargetsField = 10;
    private const int MaxTargetsPerFrame = 30;

    private const int TargetUidField = 1;
    private const int TargetDamageField = 2;
    private const int TargetHitField = 3;
    private const int TargetEffectField = 4;
    private const int TargetNewXField = 6;
    private const int TargetNewYField = 7;
    private const int TargetUnknownField = 8;
    private const int TargetTailedBeastField = 9;

    public const ushort Opcode = WorldPacketOpcodes.LegacySpellUse;

    public byte BombFlag => SpellEffect;

    public static LegacySpellAnimationPacket ForMonsterMagic(
        uint monsterUid,
        uint targetUid,
        ushort targetX,
        ushort targetY,
        ushort spellId,
        uint damage,
        MsgAttackEffect effect = MsgAttackEffect.None)
    {
        return new LegacySpellAnimationPacket(
            monsterUid,
            AttackedUid: 0,
            targetX,
            targetY,
            spellId,
            SpellLevel: 0,
            NextSpell: 0,
            LevelHu: 0,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(targetUid, damage, Hit: true, effect)]);
    }

    public static LegacySpellAnimationPacket ForTargetedMonsterMagic(
        uint monsterUid,
        uint targetUid,
        ushort spellId,
        uint damage,
        byte spellLevel = 0,
        MsgAttackEffect effect = MsgAttackEffect.None)
    {
        return new LegacySpellAnimationPacket(
            monsterUid,
            targetUid,
            X: 0,
            Y: 0,
            spellId,
            spellLevel,
            NextSpell: 0,
            LevelHu: 0,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(targetUid, damage, Hit: true, effect)]);
    }

    public static LegacySpellAnimationPacket ForPlayerMagic(
        uint playerUid,
        ushort x,
        ushort y,
        ushort spellId,
        byte spellLevel,
        byte levelHu,
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        ushort nextSpell = 0,
        byte spellEffect = 0,
        byte bombFlag = 0)
    {
        return new LegacySpellAnimationPacket(
            playerUid,
            AttackedUid: 0,
            x,
            y,
            spellId,
            spellLevel,
            nextSpell,
            levelHu,
            bombFlag != 0 ? bombFlag : spellEffect,
            targets);
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Targets);
        return ToFrameBytes(Targets, targetOffset: 0, targetCount: Targets.Count);
    }

    public IReadOnlyList<byte[]> ToFrames()
    {
        ArgumentNullException.ThrowIfNull(Targets);
        if (Targets.Count <= MaxTargetsPerFrame)
        {
            return [ToFrameBytes()];
        }

        var frames = new List<byte[]>((Targets.Count + MaxTargetsPerFrame - 1) / MaxTargetsPerFrame);
        for (var targetOffset = 0; targetOffset < Targets.Count; targetOffset += MaxTargetsPerFrame)
        {
            var targetCount = Math.Min(MaxTargetsPerFrame, Targets.Count - targetOffset);
            frames.Add(ToFrameBytes(Targets, targetOffset, targetCount));
        }

        return frames;
    }

    private byte[] ToFrameBytes(
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        int targetOffset,
        int targetCount)
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, AttackerUidField, AttackerUid);
        if (AttackedUid != 0)
        {
            LegacyProtoWire.WriteVarintField(payload, AttackedUidField, AttackedUid);
        }

        if (AttackedUid == 0 || X != 0 || Y != 0)
        {
            LegacyProtoWire.WriteVarintField(payload, XField, X);
            LegacyProtoWire.WriteVarintField(payload, YField, Y);
        }

        LegacyProtoWire.WriteVarintField(payload, SpellIdField, SpellId);
        LegacyProtoWire.WriteVarintField(payload, SpellLevelField, SpellLevel);
        LegacyProtoWire.WriteVarintField(payload, NextSpellField, NextSpell);
        LegacyProtoWire.WriteVarintField(payload, LevelHuField, LevelHu);
        LegacyProtoWire.WriteVarintField(payload, SpellEffectField, SpellEffect);

        for (var index = 0; index < targetCount; index++)
        {
            var target = targets[targetOffset + index];
            var targetPayload = new List<byte>();
            LegacyProtoWire.WriteVarintField(targetPayload, TargetUidField, target.Uid);
            LegacyProtoWire.WriteVarintField(targetPayload, TargetDamageField, target.Damage);
            LegacyProtoWire.WriteBoolField(targetPayload, TargetHitField, target.Hit);
            LegacyProtoWire.WriteVarintField(targetPayload, TargetEffectField, (uint)target.Effect);
            LegacyProtoWire.WriteVarintField(targetPayload, TargetNewXField, target.NewX);
            LegacyProtoWire.WriteVarintField(targetPayload, TargetNewYField, target.NewY);
            LegacyProtoWire.WriteVarintField(targetPayload, TargetUnknownField, target.Unknown);
            LegacyProtoWire.WriteVarintField(targetPayload, TargetTailedBeastField, target.TailedBeast);
            LegacyProtoWire.WriteBytesField(payload, TargetsField, targetPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    public static bool TryParse(PacketFrame frame, out LegacySpellAnimationPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var reader = new LegacyProtoWire.Reader(payload);
        uint attackerUid = 0;
        uint attackedUid = 0;
        uint x = 0;
        uint y = 0;
        uint spellId = 0;
        uint spellLevel = 0;
        uint nextSpell = 0;
        uint levelHu = 0;
        uint spellEffect = 0;
        var targets = new List<LegacySpellAnimationTarget>();

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case AttackerUidField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out attackerUid))
                    {
                        return false;
                    }

                    break;
                case AttackedUidField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out attackedUid))
                    {
                        return false;
                    }

                    break;
                case XField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out x))
                    {
                        return false;
                    }

                    break;
                case YField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out y))
                    {
                        return false;
                    }

                    break;
                case SpellIdField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out spellId))
                    {
                        return false;
                    }

                    break;
                case SpellLevelField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out spellLevel))
                    {
                        return false;
                    }

                    break;
                case NextSpellField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out nextSpell))
                    {
                        return false;
                    }

                    break;
                case LevelHuField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out levelHu))
                    {
                        return false;
                    }

                    break;
                case SpellEffectField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out spellEffect))
                    {
                        return false;
                    }

                    break;
                case TargetsField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var targetPayload) ||
                        !TryParseTarget(targetPayload, out var target))
                    {
                        return false;
                    }

                    targets.Add(target);
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (attackerUid == 0 ||
            spellId == 0 ||
            x > ushort.MaxValue ||
            y > ushort.MaxValue ||
            spellId > ushort.MaxValue ||
            spellLevel > byte.MaxValue ||
            nextSpell > ushort.MaxValue ||
            levelHu > byte.MaxValue ||
            spellEffect > byte.MaxValue)
        {
            return false;
        }

        packet = new LegacySpellAnimationPacket(
            attackerUid,
            attackedUid,
            (ushort)x,
            (ushort)y,
            (ushort)spellId,
            (byte)spellLevel,
            (ushort)nextSpell,
            (byte)levelHu,
            (byte)spellEffect,
            targets);
        return true;
    }

    private static bool TryParseTarget(
        ReadOnlySpan<byte> payload,
        out LegacySpellAnimationTarget target)
    {
        target = default!;
        var reader = new LegacyProtoWire.Reader(payload);
        uint uid = 0;
        uint damage = 0;
        var hit = false;
        uint effect = 0;
        uint newX = 0;
        uint newY = 0;
        uint unknown = 0;
        uint tailedBeast = 0;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TargetUidField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out uid))
                    {
                        return false;
                    }

                    break;
                case TargetDamageField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out damage))
                    {
                        return false;
                    }

                    break;
                case TargetHitField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadBool(out hit))
                    {
                        return false;
                    }

                    break;
                case TargetEffectField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out effect))
                    {
                        return false;
                    }

                    break;
                case TargetNewXField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out newX))
                    {
                        return false;
                    }

                    break;
                case TargetNewYField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out newY))
                    {
                        return false;
                    }

                    break;
                case TargetUnknownField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out unknown))
                    {
                        return false;
                    }

                    break;
                case TargetTailedBeastField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out tailedBeast))
                    {
                        return false;
                    }

                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (uid == 0 ||
            newX > ushort.MaxValue ||
            newY > ushort.MaxValue)
        {
            return false;
        }

        target = new LegacySpellAnimationTarget(
            uid,
            damage,
            hit,
            (MsgAttackEffect)effect,
            (ushort)newX,
            (ushort)newY,
            unknown,
            tailedBeast);
        return true;
    }
}
