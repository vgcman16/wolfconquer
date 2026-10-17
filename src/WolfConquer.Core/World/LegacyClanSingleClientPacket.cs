using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

/// <summary>
/// Mirrors <c>MsgClan.ClanAppendSingleClientCreate(...)</c> from the decompiled <c>MsgClan.Info.Join</c>
/// and <c>MsgClan.Info.MyClan</c> flows. The source writes a single-client clan append/request frame with
/// mode, uid, zero dwparam, and one UTF-8 name field.
/// </summary>
public sealed record LegacyClanSingleClientPacket(uint Mode, uint Uid, uint Parameter, string Name)
{
    private const int ModeOffset = 0;
    private const int UidOffset = ModeOffset + sizeof(uint);
    private const int ParameterOffset = UidOffset + sizeof(uint);
    private const int StringCountOffset = ParameterOffset + sizeof(uint);
    private const int NameLengthOffset = StringCountOffset + sizeof(byte);
    private const int NameOffset = NameLengthOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyClan;

    public static LegacyClanSingleClientPacket Create(uint mode, uint uid, string name)
    {
        // Source-shape coverage for MsgClan.ClanAppendSingleClientCreate(...).
        ArgumentNullException.ThrowIfNull(name);
        return new LegacyClanSingleClientPacket(mode, uid, Parameter: 0, name);
    }

    public static bool TryParse(PacketFrame frame, out LegacyClanSingleClientPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode || frame.Payload.Length < NameOffset)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        if (payload[StringCountOffset] != 1)
        {
            return false;
        }

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(NameLengthOffset, sizeof(ushort)));
        if (payload.Length != NameOffset + nameLength)
        {
            return false;
        }

        packet = new LegacyClanSingleClientPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ModeOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ParameterOffset, sizeof(uint))),
            Encoding.UTF8.GetString(payload.AsSpan(NameOffset, nameLength)));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var nameBytes = Encoding.UTF8.GetBytes(Name ?? string.Empty);
        if (nameBytes.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Legacy clan single-client name cannot exceed 65,535 bytes.");
        }

        var writer = new PacketWriter(Opcode, NameOffset + nameBytes.Length);
        writer.WriteUInt32(ModeOffset, Mode);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(ParameterOffset, Parameter);
        writer.Payload[StringCountOffset] = 1;
        writer.WriteUInt16(NameLengthOffset, (ushort)nameBytes.Length);
        nameBytes.CopyTo(writer.Payload[NameOffset..]);
        return writer.ToArray();
    }
}
