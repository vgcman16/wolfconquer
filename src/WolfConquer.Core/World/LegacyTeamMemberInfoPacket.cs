using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyTeamMemberInfoAction : byte
{
    AddMember = 0,
    DropAddMember = 1,
}

public sealed record LegacyTeamMemberInfoEntry(
    string Name,
    uint Uid,
    uint Mesh,
    uint MaxHitpoints,
    uint MinHitpoints);

public sealed record LegacyTeamMemberInfoPacket(
    LegacyTeamMemberInfoAction Action,
    byte Count,
    ushort Unknown,
    IReadOnlyList<LegacyTeamMemberInfoEntry> Entries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int ActionOffset = 0;
    private const int CountOffset = ActionOffset + sizeof(byte);
    private const int UnknownOffset = CountOffset + sizeof(byte);
    private const int SlotsOffset = UnknownOffset + sizeof(ushort);
    private const int SlotNameOffset = 0;
    private const int SlotUidOffset = SlotNameOffset + FixedNameLength;
    private const int SlotMeshOffset = SlotUidOffset + sizeof(uint);
    private const int SlotMaxHitpointsOffset = SlotMeshOffset + sizeof(uint);
    private const int SlotMinHitpointsOffset = SlotMaxHitpointsOffset + sizeof(uint);
    private const int FixedNameLength = 32;

    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamMemberInfo;
    public const int SlotCount = 5;
    public const int SlotLength = 48;
    public const int PayloadLength = 4 + (SlotCount * SlotLength);

    public static bool TryParse(PacketFrame frame, out LegacyTeamMemberInfoPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var action = reader.Payload[ActionOffset];
        var count = reader.Payload[CountOffset];
        if (!Enum.IsDefined(typeof(LegacyTeamMemberInfoAction), action) ||
            count > SlotCount)
        {
            return false;
        }

        var entries = new List<LegacyTeamMemberInfoEntry>(count);
        for (var index = 0; index < count; index++)
        {
            var offset = SlotsOffset + (index * SlotLength);
            entries.Add(new LegacyTeamMemberInfoEntry(
                reader.ReadCString(offset + SlotNameOffset, FixedNameLength, NameEncoding),
                reader.ReadUInt32(offset + SlotUidOffset),
                reader.ReadUInt32(offset + SlotMeshOffset),
                reader.ReadUInt32(offset + SlotMaxHitpointsOffset),
                reader.ReadUInt32(offset + SlotMinHitpointsOffset)));
        }

        packet = new LegacyTeamMemberInfoPacket(
            (LegacyTeamMemberInfoAction)action,
            count,
            reader.ReadUInt16(UnknownOffset),
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);
        if (Entries.Count > SlotCount)
        {
            throw new InvalidOperationException("Legacy team member-info packets cannot contain more than five entries.");
        }

        if (Count != Entries.Count)
        {
            throw new InvalidOperationException("Legacy team member-info count must match the number of entries.");
        }

        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.Payload[ActionOffset] = (byte)Action;
        writer.Payload[CountOffset] = Count;
        writer.WriteUInt16(UnknownOffset, Unknown);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = SlotsOffset + (index * SlotLength);
            WriteFixedString(writer.Payload.Slice(offset + SlotNameOffset, FixedNameLength), entry.Name);
            writer.WriteUInt32(offset + SlotUidOffset, entry.Uid);
            writer.WriteUInt32(offset + SlotMeshOffset, entry.Mesh);
            writer.WriteUInt32(offset + SlotMaxHitpointsOffset, entry.MaxHitpoints);
            writer.WriteUInt32(offset + SlotMinHitpointsOffset, entry.MinHitpoints);
        }

        return writer.ToArray();
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var encoder = NameEncoding.GetEncoder();
        encoder.Convert(value.AsSpan(), destination, flush: true, out _, out _, out _);
    }
}
