using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyGameUpdateDataType : uint
{
    Accelerated = 52,
    Decelerated = 53,
    Flustered = 54,
    Sprint = 55,
    DivineShield = 57,
    Stun = 58,
    Freeze = 59,
    Dizzy = 60,
    AzureShield = 93,
}

public readonly record struct LegacyGameUpdatePacket(
    uint Uid,
    LegacyGameUpdateDataType Type,
    bool Active,
    uint ShownAmount,
    uint Time,
    uint Amount)
{
    private const int UidOffset = 0;
    private const int CountOffset = UidOffset + sizeof(uint);
    private const int TypeOffset = CountOffset + sizeof(uint);
    private const int ActiveOffset = TypeOffset + sizeof(uint);
    private const int ShownAmountOffset = ActiveOffset + sizeof(uint);
    private const int TimeOffset = ShownAmountOffset + sizeof(uint);
    private const int AmountOffset = TimeOffset + sizeof(uint);
    private const uint LegacyEntryCount = 1;
    private const uint ActiveMarker = 256;
    private const uint InactiveMarker = 1;

    public const ushort Opcode = WorldPacketOpcodes.LegacyGameUpdate;
    public const int PayloadLength = AmountOffset + sizeof(uint);

    public static LegacyGameUpdatePacket Freeze(uint uid, uint seconds)
    {
        return new LegacyGameUpdatePacket(
            uid,
            LegacyGameUpdateDataType.Freeze,
            Active: true,
            ShownAmount: 0,
            Time: seconds,
            Amount: 0);
    }

    public static bool TryParse(PacketFrame frame, out LegacyGameUpdatePacket packet)
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

        if (BinaryPrimitives.ReadUInt32LittleEndian(payload[CountOffset..TypeOffset]) != LegacyEntryCount)
        {
            return false;
        }

        var type = BinaryPrimitives.ReadUInt32LittleEndian(payload[TypeOffset..ActiveOffset]);
        if (!Enum.IsDefined(typeof(LegacyGameUpdateDataType), type))
        {
            return false;
        }

        var active = BinaryPrimitives.ReadUInt32LittleEndian(payload[ActiveOffset..ShownAmountOffset]);
        if (active is not ActiveMarker and not InactiveMarker)
        {
            return false;
        }

        packet = new LegacyGameUpdatePacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload[UidOffset..CountOffset]),
            (LegacyGameUpdateDataType)type,
            active == ActiveMarker,
            BinaryPrimitives.ReadUInt32LittleEndian(payload[ShownAmountOffset..TimeOffset]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[TimeOffset..AmountOffset]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[AmountOffset..PayloadLength]));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(CountOffset, LegacyEntryCount);
        writer.WriteUInt32(TypeOffset, (uint)Type);
        writer.WriteUInt32(ActiveOffset, Active ? ActiveMarker : InactiveMarker);
        writer.WriteUInt32(ShownAmountOffset, ShownAmount);
        writer.WriteUInt32(TimeOffset, Time);
        writer.WriteUInt32(AmountOffset, Amount);
        return writer.ToArray();
    }
}
