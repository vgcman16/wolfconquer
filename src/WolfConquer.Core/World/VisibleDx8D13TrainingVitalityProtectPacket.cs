using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum VisibleDx8D13TrainingVitalityProtectAction : ushort
{
    Retreating = 1,
    Seal = 2,
    SealAcknowledged = 3,
    Restore = 4,
    RestoreAcknowledged = 5,
    Extended = 7,
    CancelRetreat = 8,
}

public readonly record struct VisibleDx8D13TrainingVitalityProtectPacket(
    ushort Action,
    byte Mode,
    uint Reserved)
{
    private const int ActionOffset = 0;
    private const int ModeOffset = ActionOffset + sizeof(ushort);
    private const int ReservedOffset = ModeOffset + sizeof(byte);

    // Exact visible-DX8 D13 FUN_010a1208 request evidence: length 11, opcode 0x08DC.
    public const ushort Opcode = 2268;
    // Exact CMsgTrainingVitalityProtect receive validator uses opcode 0x08FE.
    public const ushort ResponseOpcode = 2302;
    public const int FrameLength = 11;
    public const int PayloadLength = FrameLength - PacketHeader.Size;

    public bool HasKnownAction => Action is
        (ushort)VisibleDx8D13TrainingVitalityProtectAction.Seal or
        (ushort)VisibleDx8D13TrainingVitalityProtectAction.Restore or
        (ushort)VisibleDx8D13TrainingVitalityProtectAction.CancelRetreat;

    public static bool TryParse(
        PacketFrame frame,
        out VisibleDx8D13TrainingVitalityProtectPacket packet)
    {
        return TryParse(frame, Opcode, out packet);
    }

    public static bool TryParseResponse(
        PacketFrame frame,
        out VisibleDx8D13TrainingVitalityProtectPacket packet)
    {
        return TryParse(frame, ResponseOpcode, out packet);
    }

    private static bool TryParse(
        PacketFrame frame,
        ushort expectedOpcode,
        out VisibleDx8D13TrainingVitalityProtectPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != expectedOpcode ||
            frame.Header.Length != FrameLength ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new VisibleDx8D13TrainingVitalityProtectPacket(
            BinaryPrimitives.ReadUInt16LittleEndian(
                payload.AsSpan(ActionOffset, sizeof(ushort))),
            payload[ModeOffset],
            BinaryPrimitives.ReadUInt32LittleEndian(
                payload.AsSpan(ReservedOffset, sizeof(uint))));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        return ToFrameBytes(Opcode);
    }

    public byte[] ToResponseFrameBytes()
    {
        return ToFrameBytes(ResponseOpcode);
    }

    private byte[] ToFrameBytes(ushort opcode)
    {
        var writer = new PacketWriter(opcode, PayloadLength);
        writer.WriteUInt16(ActionOffset, Action);
        writer.Payload[ModeOffset] = Mode;
        writer.WriteUInt32(ReservedOffset, Reserved);
        return writer.ToArray();
    }
}
