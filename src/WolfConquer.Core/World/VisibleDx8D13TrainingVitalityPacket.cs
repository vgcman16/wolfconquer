using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum VisibleDx8D13TrainingVitalityAction : ushort
{
    Unlock = 0,
    Open = 1,
    Study = 2,
    BuyPoints = 3,
    Buy200 = 6,
    RetreatChiPowers = 7,
}

public readonly record struct VisibleDx8D13TrainingVitalityPacket(
    uint Uid,
    ushort Action,
    byte Power,
    byte LockedFlags,
    uint Reserved0,
    uint Reserved1,
    uint Reserved2)
{
    private const int UidOffset = 0;
    private const int ActionOffset = UidOffset + sizeof(uint);
    private const int PowerOffset = ActionOffset + sizeof(ushort);
    private const int LockedFlagsOffset = PowerOffset + sizeof(byte);
    private const int Reserved0Offset = LockedFlagsOffset + sizeof(byte);
    private const int Reserved1Offset = Reserved0Offset + sizeof(uint);
    private const int Reserved2Offset = Reserved1Offset + sizeof(uint);

    // Exact visible-DX8 D13 CMsgTrainingVitality builders use length 24 and opcode 0x0949.
    public const ushort Opcode = 2377;
    public const int FrameLength = 24;
    public const int PayloadLength = FrameLength - PacketHeader.Size;

    public bool HasKnownAction => Action is
        (ushort)VisibleDx8D13TrainingVitalityAction.Unlock or
        (ushort)VisibleDx8D13TrainingVitalityAction.Open or
        (ushort)VisibleDx8D13TrainingVitalityAction.Study or
        (ushort)VisibleDx8D13TrainingVitalityAction.BuyPoints or
        (ushort)VisibleDx8D13TrainingVitalityAction.Buy200 or
        (ushort)VisibleDx8D13TrainingVitalityAction.RetreatChiPowers;

    public bool HasZeroReserved => Reserved0 == 0 && Reserved1 == 0 && Reserved2 == 0;

    public static bool TryParse(PacketFrame frame, out VisibleDx8D13TrainingVitalityPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Header.Length != FrameLength ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new VisibleDx8D13TrainingVitalityPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(ActionOffset, sizeof(ushort))),
            payload[PowerOffset],
            payload[LockedFlagsOffset],
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(Reserved0Offset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(Reserved1Offset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(Reserved2Offset, sizeof(uint))));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt16(ActionOffset, Action);
        writer.Payload[PowerOffset] = Power;
        writer.Payload[LockedFlagsOffset] = LockedFlags;
        writer.WriteUInt32(Reserved0Offset, Reserved0);
        writer.WriteUInt32(Reserved1Offset, Reserved1);
        writer.WriteUInt32(Reserved2Offset, Reserved2);
        return writer.ToArray();
    }
}
