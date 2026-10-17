using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyVipTeleportType : uint
{
    SelfTeleport = 0,
    TeamTeleport = 1,
    TeammateConfirmation = 2,
    TeammateTeleport = 3,
}

public enum LegacyVipTeleportLocation : uint
{
    TwinCity = 1,
    PhoenixCastle = 2,
    ApeCity = 3,
    DesertCity = 4,
    BirdIsland = 5,
    TCSquare = 6,
    WPAltar = 7,
    WPApparition = 8,
    WPPoltergeist = 9,
    WPTurtledove = 10,
    PCSquare = 11,
    MFWaterCave = 12,
    MFVillage = 13,
    MFLake = 14,
    MFMineCave = 15,
    MFBridge = 16,
    MFToApeCity = 17,
    ACSquare = 18,
    ACSouth = 19,
    ACEast = 20,
    ACNorth = 21,
    ACWest = 22,
    BISquare = 23,
    BINorth = 24,
    BIWest = 25,
    BIEast = 26,
    IslandVillage = 27,
    DCSquare = 28,
    DCSouth = 29,
    DCMoonSpring = 30,
    DCAncientMaze = 31,
}

public sealed record LegacyVipHandlerPacket(
    LegacyVipTeleportType TeleportType,
    LegacyVipTeleportLocation Location,
    uint Countdown,
    string Name)
{
    private const int TeleportTypeOffset = 0;
    private const int LocationOffset = TeleportTypeOffset + sizeof(uint);
    private const int CountdownOffset = LocationOffset + sizeof(uint);
    private const int NameLengthOffset = CountdownOffset + sizeof(uint);
    private const int NameOffset = NameLengthOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyVipHandler;
    public const int MinimumPayloadLength = NameOffset;

    public static bool TryParse(PacketFrame frame, out LegacyVipHandlerPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length < MinimumPayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var teleportType = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(TeleportTypeOffset, sizeof(uint)));
        if (!Enum.IsDefined(typeof(LegacyVipTeleportType), teleportType))
        {
            return false;
        }

        var location = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(LocationOffset, sizeof(uint)));
        if (!Enum.IsDefined(typeof(LegacyVipTeleportLocation), location))
        {
            return false;
        }

        var countdown = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(CountdownOffset, sizeof(uint)));
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(NameLengthOffset, sizeof(ushort)));
        if (NameOffset > payload.Length - nameLength)
        {
            return false;
        }

        if (NameOffset + nameLength != payload.Length)
        {
            return false;
        }

        var name = Encoding.UTF8.GetString(payload.AsSpan(NameOffset, nameLength));
        packet = new LegacyVipHandlerPacket(
            (LegacyVipTeleportType)teleportType,
            (LegacyVipTeleportLocation)location,
            countdown,
            name);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var nameBytes = Encoding.UTF8.GetBytes(Name);
        if (nameBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Name), "Legacy VIP handler names cannot exceed 65,535 bytes.");
        }

        var writer = new PacketWriter(Opcode, checked(MinimumPayloadLength + nameBytes.Length));
        writer.WriteUInt32(TeleportTypeOffset, (uint)TeleportType);
        writer.WriteUInt32(LocationOffset, (uint)Location);
        writer.WriteUInt32(CountdownOffset, Countdown);
        writer.WriteUInt16(NameLengthOffset, (ushort)nameBytes.Length);
        nameBytes.CopyTo(writer.Payload[NameOffset..]);
        return writer.ToArray();
    }
}
