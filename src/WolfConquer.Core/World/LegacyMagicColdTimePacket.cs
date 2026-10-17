using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyMagicColdTimeSpell(
    uint SpellId,
    int Time);

public sealed record LegacyMagicColdTimePacket(
    IReadOnlyList<LegacyMagicColdTimeSpell> Spells)
{
    private const int SpellsField = 1;
    private const int SpellIdField = 1;
    private const int SpellTimeField = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyMagicColdTime;
    public const uint EagleEyeSpellId = 11_030;

    public static LegacyMagicColdTimePacket FromStartedCooldown(LegacyMagicRuntimeSpell spell)
    {
        return new LegacyMagicColdTimePacket(
            [new LegacyMagicColdTimeSpell(spell.Id, Math.Max(0, spell.ColdTime))]);
    }

    public static LegacyMagicColdTimePacket ZeroEagleEye()
    {
        return new LegacyMagicColdTimePacket(
            [new LegacyMagicColdTimeSpell(EagleEyeSpellId, Time: 0)]);
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Spells);

        var payload = new List<byte>();
        foreach (var spell in Spells)
        {
            if (spell.Time < 0)
            {
                throw new InvalidOperationException("Legacy magic cold-time entries cannot have negative time.");
            }

            var spellPayload = new List<byte>();
            LegacyProtoWire.WriteVarintField(spellPayload, SpellIdField, spell.SpellId);
            LegacyProtoWire.WriteVarintField(spellPayload, SpellTimeField, checked((uint)spell.Time));
            LegacyProtoWire.WriteBytesField(payload, SpellsField, spellPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    public static bool TryParse(PacketFrame frame, out LegacyMagicColdTimePacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var spells = new List<LegacyMagicColdTimeSpell>();
        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case SpellsField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var spellPayload) ||
                        !TryParseSpell(spellPayload, out var spell))
                    {
                        return false;
                    }

                    spells.Add(spell);
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        packet = new LegacyMagicColdTimePacket(spells);
        return true;
    }

    private static bool TryParseSpell(
        ReadOnlySpan<byte> payload,
        out LegacyMagicColdTimeSpell spell)
    {
        spell = default;
        var reader = new LegacyProtoWire.Reader(payload);
        uint spellId = 0;
        uint time = 0;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case SpellIdField:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out spellId))
                    {
                        return false;
                    }

                    break;
                case SpellTimeField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out time) ||
                        time > int.MaxValue)
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

        if (spellId == 0)
        {
            return false;
        }

        spell = new LegacyMagicColdTimeSpell(spellId, checked((int)time));
        return true;
    }
}
