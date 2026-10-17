using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyFlowerPacket(
    uint Action,
    uint Uid,
    uint ItemUid,
    uint RedRoses,
    uint RedRosesToday,
    uint Lilies,
    uint LiliesToday,
    uint Orchids,
    uint OrchidsToday,
    uint Tulips,
    uint TulipsToday,
    uint OrchidsMirror,
    uint FlowerType,
    uint Effect)
{
    public const ushort Opcode = WorldPacketOpcodes.LegacyFlower;
    public const int PayloadLength = 56;
    public const uint FlowerSenderAction = 2;
    public const uint FlowerAction = 3;

    public static bool TryCreateLoginFreeFlowers(CharacterSnapshot snapshot, out LegacyFlowerPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        packet = default;
        if (!snapshot.Attributes.TryGetValue("Flowers", out var rawFlowers) ||
            !TryReadSlashUInt32(rawFlowers, 0, out var freeFlowers) ||
            freeFlowers == 0)
        {
            return false;
        }

        packet = new LegacyFlowerPacket(
            Action: IsLegacyBoyBody(snapshot.Body) ? FlowerSenderAction : FlowerAction,
            Uid: 0,
            ItemUid: 0,
            RedRoses: freeFlowers,
            RedRosesToday: 0,
            Lilies: 0,
            LiliesToday: 0,
            Orchids: 0,
            OrchidsToday: 0,
            Tulips: 0,
            TulipsToday: 0,
            OrchidsMirror: 0,
            FlowerType: 0,
            Effect: 0);
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyFlowerPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode || frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new LegacyFlowerPacket(
            ReadUInt32(payload, 0),
            ReadUInt32(payload, 4),
            ReadUInt32(payload, 8),
            ReadUInt32(payload, 12),
            ReadUInt32(payload, 16),
            ReadUInt32(payload, 20),
            ReadUInt32(payload, 24),
            ReadUInt32(payload, 28),
            ReadUInt32(payload, 32),
            ReadUInt32(payload, 36),
            ReadUInt32(payload, 40),
            ReadUInt32(payload, 44),
            ReadUInt32(payload, 48),
            ReadUInt32(payload, 52));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(0, Action);
        writer.WriteUInt32(4, Uid);
        writer.WriteUInt32(8, ItemUid);
        writer.WriteUInt32(12, RedRoses);
        writer.WriteUInt32(16, RedRosesToday);
        writer.WriteUInt32(20, Lilies);
        writer.WriteUInt32(24, LiliesToday);
        writer.WriteUInt32(28, Orchids);
        writer.WriteUInt32(32, OrchidsToday);
        writer.WriteUInt32(36, Tulips);
        writer.WriteUInt32(40, TulipsToday);
        writer.WriteUInt32(44, OrchidsMirror);
        writer.WriteUInt32(48, FlowerType);
        writer.WriteUInt32(52, Effect);
        return writer.ToArray();
    }

    private static bool TryReadSlashUInt32(string raw, int index, out uint value)
    {
        value = 0;
        var parts = raw.Split('/', StringSplitOptions.None);
        return index >= 0 &&
            index < parts.Length &&
            uint.TryParse(parts[index], out value);
    }

    private static bool IsLegacyBoyBody(long body)
    {
        return body < 2000;
    }

    private static uint ReadUInt32(byte[] payload, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, sizeof(uint)));
    }
}
