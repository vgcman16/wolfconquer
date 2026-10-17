using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyItemLockPacket(
    uint Uid,
    byte Type,
    byte LockedMode,
    uint Parameter)
{
    private const int UidOffset = 0;
    private const int TypeOffset = UidOffset + sizeof(uint);
    private const int LockedModeOffset = TypeOffset + sizeof(byte);
    private const int ReservedOffset = LockedModeOffset + sizeof(byte);
    private const int ParameterOffset = ReservedOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyItemLock;
    public const byte UnlockDateType = 2;
    public const byte UnlockDateLockedMode = 0;
    public const int PayloadLength = ParameterOffset + sizeof(uint);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static bool TryCreateUnlockDate(
        PlayerItemSnapshotItem item,
        DateTime now,
        out LegacyItemLockPacket packet)
    {
        ArgumentNullException.ThrowIfNull(item);
        packet = default;

        if (item.Locked != 2 ||
            item.UnlockTimer == 0 ||
            !TryParseLegacyDate(item.UnlockTimer, out var unlockDate) ||
            now > unlockDate)
        {
            return false;
        }

        packet = new LegacyItemLockPacket(
            item.Uid,
            UnlockDateType,
            UnlockDateLockedMode,
            unchecked((uint)item.UnlockTimer));
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyItemLockPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt16(ReservedOffset) != 0)
        {
            return false;
        }

        packet = new LegacyItemLockPacket(
            reader.ReadUInt32(UidOffset),
            reader.Payload[TypeOffset],
            reader.Payload[LockedModeOffset],
            reader.ReadUInt32(ParameterOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.Payload[TypeOffset] = Type;
        writer.Payload[LockedModeOffset] = LockedMode;
        writer.WriteUInt32(ParameterOffset, Parameter);
        return writer.ToArray();
    }

    private static bool TryParseLegacyDate(int timer, out DateTime value)
    {
        var year = timer / 10000;
        var month = timer / 100 - year * 100;
        var day = timer - year * 10000 - month * 100;

        try
        {
            value = new DateTime(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            value = default;
            return false;
        }
    }
}
