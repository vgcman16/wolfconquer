using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyInterActionWalkMode : ushort
{
    Walk = 1,
    Jump = 2,
}

public readonly record struct LegacyInterActionWalkPacket(
    LegacyInterActionWalkMode Mode,
    byte DirectionOne,
    byte DirectionTwo,
    ushort Y,
    ushort Unknown,
    uint Count,
    uint Uid,
    uint OpponentUid)
{
    private const int ModeOffset = 0;
    private const int DirectionOneOffset = ModeOffset + sizeof(ushort);
    private const int DirectionTwoOffset = DirectionOneOffset + sizeof(byte);
    private const int YOffset = DirectionTwoOffset + sizeof(byte);
    private const int UnknownOffset = YOffset + sizeof(ushort);
    private const int CountOffset = UnknownOffset + sizeof(ushort);
    private const int UidOffset = CountOffset + sizeof(uint);
    private const int OpponentUidOffset = UidOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyInterActionWalk;
    public const uint LegacyCount = 2;
    public const int PayloadLength = OpponentUidOffset + sizeof(uint);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public ushort X => (ushort)(DirectionOne | (DirectionTwo << 8));

    public static LegacyInterActionWalkPacket ForWalk(
        uint uid,
        uint opponentUid,
        byte directionOne,
        byte directionTwo = 0)
    {
        return new LegacyInterActionWalkPacket(
            LegacyInterActionWalkMode.Walk,
            directionOne,
            directionTwo,
            Y: 0,
            Unknown: 0,
            LegacyCount,
            uid,
            opponentUid);
    }

    public static LegacyInterActionWalkPacket ForJump(
        uint uid,
        uint opponentUid,
        ushort x,
        ushort y)
    {
        return new LegacyInterActionWalkPacket(
            LegacyInterActionWalkMode.Jump,
            (byte)(x & 0xFF),
            (byte)(x >> 8),
            y,
            Unknown: 0,
            LegacyCount,
            uid,
            opponentUid);
    }

    public static bool TryParse(PacketFrame frame, out LegacyInterActionWalkPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        Span<byte> payload = stackalloc byte[PayloadLength];
        var copied = 0;
        foreach (var memory in frame.Payload)
        {
            var span = memory.Span;
            span.CopyTo(payload[copied..]);
            copied += span.Length;
        }

        var modeValue = BinaryPrimitives.ReadUInt16LittleEndian(payload[ModeOffset..DirectionOneOffset]);
        if (modeValue is not (ushort)LegacyInterActionWalkMode.Walk and not (ushort)LegacyInterActionWalkMode.Jump)
        {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(payload[CountOffset..UidOffset]);
        if (count != LegacyCount)
        {
            return false;
        }

        packet = new LegacyInterActionWalkPacket(
            (LegacyInterActionWalkMode)modeValue,
            payload[DirectionOneOffset],
            payload[DirectionTwoOffset],
            BinaryPrimitives.ReadUInt16LittleEndian(payload[YOffset..UnknownOffset]),
            BinaryPrimitives.ReadUInt16LittleEndian(payload[UnknownOffset..CountOffset]),
            count,
            BinaryPrimitives.ReadUInt32LittleEndian(payload[UidOffset..OpponentUidOffset]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[OpponentUidOffset..PayloadLength]));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt16(ModeOffset, (ushort)Mode);
        writer.Payload[DirectionOneOffset] = DirectionOne;
        writer.Payload[DirectionTwoOffset] = DirectionTwo;
        writer.WriteUInt16(YOffset, Y);
        writer.WriteUInt16(UnknownOffset, Unknown);
        writer.WriteUInt32(CountOffset, Count);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(OpponentUidOffset, OpponentUid);
        return writer.ToArray();
    }
}
