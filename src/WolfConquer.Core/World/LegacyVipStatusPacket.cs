using System.Buffers.Binary;
using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyVipStatusFlags : uint
{
    VipLevelOne = 34863,
    FullVip = 65535,
    None = 65536,
}

public sealed record LegacyVipStatusPacket(LegacyVipStatusFlags Flags)
{
    private const int PayloadLength = sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyMsgVipStatus;

    public static LegacyVipStatusPacket FromSnapshot(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var vipLevel = ReadVipLevel(snapshot);
        return new LegacyVipStatusPacket(FlagsForVipLevel(vipLevel));
    }

    public static LegacyVipStatusFlags FlagsForVipLevel(ulong vipLevel)
    {
        if (vipLevel >= 6)
        {
            return LegacyVipStatusFlags.FullVip;
        }

        return vipLevel >= 1
            ? LegacyVipStatusFlags.VipLevelOne
            : LegacyVipStatusFlags.None;
    }

    public static bool TryParse(PacketFrame frame, out LegacyVipStatusPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var flags = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        packet = new LegacyVipStatusPacket((LegacyVipStatusFlags)flags);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(0, (uint)Flags);
        return writer.ToArray();
    }

    private static ulong ReadVipLevel(CharacterSnapshot snapshot)
    {
        foreach (var key in new[] { "VipLevel", "VIPLevel" })
        {
            if (snapshot.Attributes.TryGetValue(key, out var raw) &&
                ulong.TryParse(raw, out var value))
            {
                return value;
            }
        }

        return 0;
    }
}
