using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyNewSynWarPacket(
    uint Type,
    uint DwParam,
    uint SyndicateId,
    uint UserId = 0,
    uint Unknown10 = 0,
    uint Unknown16 = 0,
    uint Unknown17 = 0,
    uint Unknown18 = 0)
{
    private const int TypeField = 1;
    private const int DwParamField = 3;
    private const int UserIdField = 5;
    private const int Unknown10Field = 10;
    private const int SyndicateIdField = 15;
    private const int Unknown16Field = 16;
    private const int Unknown17Field = 17;
    private const int Unknown18Field = 18;

    public const ushort Opcode = WorldPacketOpcodes.LegacyNewSynWar;
    public const uint OwnershipChangedType = 9;
    public const uint DestroyedType = 10;
    public const uint ExorcimOwnershipDwParam = 100_009;
    public const uint TenacityOwnershipDwParam = 100_008;
    public const uint LeftGateDestroyedDwParam = 3_000;
    public const uint RightGateDestroyedDwParam = 3_001;

    public bool IsOwnershipChanged => Type == OwnershipChangedType;

    public bool IsDestroyed => Type == DestroyedType;

    public static LegacyNewSynWarPacket OwnershipChanged(uint dwParam, uint syndicateId)
    {
        return new LegacyNewSynWarPacket(OwnershipChangedType, dwParam, syndicateId);
    }

    public static LegacyNewSynWarPacket ExorcimOwnershipChanged(uint syndicateId)
    {
        return OwnershipChanged(ExorcimOwnershipDwParam, syndicateId);
    }

    public static LegacyNewSynWarPacket TenacityOwnershipChanged(uint syndicateId)
    {
        return OwnershipChanged(TenacityOwnershipDwParam, syndicateId);
    }

    public static LegacyNewSynWarPacket Destroyed(uint dwParam, uint syndicateId)
    {
        return new LegacyNewSynWarPacket(DestroyedType, dwParam, syndicateId);
    }

    public static LegacyNewSynWarPacket LeftGateDestroyed(uint syndicateId)
    {
        return Destroyed(LeftGateDestroyedDwParam, syndicateId);
    }

    public static LegacyNewSynWarPacket RightGateDestroyed(uint syndicateId)
    {
        return Destroyed(RightGateDestroyedDwParam, syndicateId);
    }

    public static bool TryParse(PacketFrame frame, out LegacyNewSynWarPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var type = 0U;
        var dwParam = 0U;
        var userId = 0U;
        var unknown10 = 0U;
        var syndicateId = 0U;
        var unknown16 = 0U;
        var unknown17 = 0U;
        var unknown18 = 0U;
        var hasType = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (!TryReadUInt32(ref reader, wireType, out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;
                case DwParamField:
                    if (!TryReadUInt32(ref reader, wireType, out dwParam))
                    {
                        return false;
                    }

                    break;
                case UserIdField:
                    if (!TryReadUInt32(ref reader, wireType, out userId))
                    {
                        return false;
                    }

                    break;
                case Unknown10Field:
                    if (!TryReadUInt32(ref reader, wireType, out unknown10))
                    {
                        return false;
                    }

                    break;
                case SyndicateIdField:
                    if (!TryReadUInt32(ref reader, wireType, out syndicateId))
                    {
                        return false;
                    }

                    break;
                case Unknown16Field:
                    if (!TryReadUInt32(ref reader, wireType, out unknown16))
                    {
                        return false;
                    }

                    break;
                case Unknown17Field:
                    if (!TryReadUInt32(ref reader, wireType, out unknown17))
                    {
                        return false;
                    }

                    break;
                case Unknown18Field:
                    if (!TryReadUInt32(ref reader, wireType, out unknown18))
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

        if (!hasType)
        {
            return false;
        }

        packet = new LegacyNewSynWarPacket(
            type,
            dwParam,
            syndicateId,
            userId,
            unknown10,
            unknown16,
            unknown17,
            unknown18);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, Type);
        WriteOptionalUInt32(payload, DwParamField, DwParam);
        WriteOptionalUInt32(payload, UserIdField, UserId);
        WriteOptionalUInt32(payload, Unknown10Field, Unknown10);
        WriteOptionalUInt32(payload, SyndicateIdField, SyndicateId);
        WriteOptionalUInt32(payload, Unknown16Field, Unknown16);
        WriteOptionalUInt32(payload, Unknown17Field, Unknown17);
        WriteOptionalUInt32(payload, Unknown18Field, Unknown18);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.ToArray().CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryReadUInt32(ref LegacyProtoWire.Reader reader, int wireType, out uint value)
    {
        value = 0;
        return wireType == LegacyProtoWire.VarintWireType && reader.TryReadUInt32(out value);
    }

    private static void WriteOptionalUInt32(List<byte> payload, int fieldNumber, uint value)
    {
        if (value != 0)
        {
            LegacyProtoWire.WriteVarintField(payload, fieldNumber, value);
        }
    }
}
