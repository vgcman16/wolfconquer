using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyLoaderHeroInfoPacket(
    uint Uid,
    uint Mesh,
    ushort ServerId,
    byte Level,
    byte NobilityRank,
    byte VipLevel,
    uint Class,
    string Name)
{
    private const int UidOffset = 0;
    private const int MeshOffset = UidOffset + sizeof(uint);
    private const int ServerIdOffset = MeshOffset + sizeof(uint);
    private const int LevelOffset = ServerIdOffset + sizeof(ushort);
    private const int NobilityRankOffset = LevelOffset + sizeof(byte);
    private const int VipLevelOffset = NobilityRankOffset + sizeof(byte);
    private const int ClassOffset = VipLevelOffset + sizeof(byte);
    private const int Unknown100Offset = ClassOffset + sizeof(uint);
    private const int StringCountOffset = Unknown100Offset + sizeof(ushort);
    private const int NameLengthOffset = StringCountOffset + sizeof(byte);
    private const int NameBytesOffset = NameLengthOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyLoaderHeroInfo;
    public const ushort Unknown100 = 100;

    public int PayloadLength => NameBytesOffset + Encoding.UTF8.GetByteCount(Name);
    public int FrameLength => PacketHeader.Size + PayloadLength;

    public static LegacyLoaderHeroInfoPacket FromSnapshot(CharacterSnapshot snapshot, ushort serverId = 2)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new LegacyLoaderHeroInfoPacket(
            CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            BuildMesh(snapshot),
            serverId,
            CheckedByte(snapshot.Level, nameof(snapshot.Level)),
            ReadNobilityRank(snapshot),
            ReadAttributeByte(snapshot.Attributes, "VipLevel"),
            CheckedUInt32(snapshot.Class, nameof(snapshot.Class)),
            snapshot.Name);
    }

    public static bool TryParse(PacketFrame frame, out LegacyLoaderHeroInfoPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < NameBytesOffset ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.ReadUInt16(Unknown100Offset) != Unknown100 ||
            reader.Payload[StringCountOffset] != 1)
        {
            return false;
        }

        var nameLength = reader.ReadUInt16(NameLengthOffset);
        if (reader.Payload.Length != NameBytesOffset + nameLength)
        {
            return false;
        }

        packet = new LegacyLoaderHeroInfoPacket(
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt32(MeshOffset),
            reader.ReadUInt16(ServerIdOffset),
            reader.Payload[LevelOffset],
            reader.Payload[NobilityRankOffset],
            reader.Payload[VipLevelOffset],
            reader.ReadUInt32(ClassOffset),
            Encoding.UTF8.GetString(reader.Payload[NameBytesOffset..]));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var nameBytes = Encoding.UTF8.GetBytes(Name);
        if (nameBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Name), "Legacy hero name is too long.");
        }

        var writer = new PacketWriter(Opcode, NameBytesOffset + nameBytes.Length);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(MeshOffset, Mesh);
        writer.WriteUInt16(ServerIdOffset, ServerId);
        writer.Payload[LevelOffset] = Level;
        writer.Payload[NobilityRankOffset] = NobilityRank;
        writer.Payload[VipLevelOffset] = VipLevel;
        writer.WriteUInt32(ClassOffset, Class);
        writer.WriteUInt16(Unknown100Offset, Unknown100);
        writer.Payload[StringCountOffset] = 1;
        writer.WriteUInt16(NameLengthOffset, (ushort)nameBytes.Length);
        nameBytes.CopyTo(writer.Payload[NameBytesOffset..]);
        return writer.ToArray();
    }

    private static uint BuildMesh(CharacterSnapshot snapshot)
    {
        var transformationId = ReadAttributeInt64(snapshot.Attributes, "TransformationID");
        var mesh = checked((transformationId * 10_000_000) + (snapshot.Face * 10_000) + snapshot.Body);
        return CheckedUInt32(mesh, "Mesh");
    }

    private static byte ReadNobilityRank(CharacterSnapshot snapshot)
    {
        if (snapshot.PaidNobility.TryGetValue("PaidRank", out var numeric) &&
            byte.TryParse(numeric, out var paidRank))
        {
            return paidRank;
        }

        if (!snapshot.PaidNobility.TryGetValue("Rank", out var rank))
        {
            return 0;
        }

        return rank.Trim() switch
        {
            "Serf" => 0,
            "Knight" => 1,
            "Baron" => 3,
            "Earl" => 5,
            "Duke" => 7,
            "Prince" => 9,
            "King" => 12,
            _ => 0,
        };
    }

    private static byte ReadAttributeByte(IReadOnlyDictionary<string, string> attributes, string key)
    {
        var value = ReadAttributeInt64(attributes, key);
        return CheckedByte(value, key);
    }

    private static long ReadAttributeInt64(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value) ||
            !long.TryParse(value, out var parsed))
        {
            return 0;
        }

        return parsed;
    }

    private static byte CheckedByte(long value, string name)
    {
        if (value < byte.MinValue || value > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy hero field exceeds byte range.");
        }

        return (byte)value;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < uint.MinValue || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy hero field exceeds UInt32 range.");
        }

        return (uint)value;
    }
}
