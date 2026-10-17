using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyStringId : byte
{
    Fireworks = 1,
    GuildName = 3,
    Spouse = 6,
    LocationEffect = 9,
    Effect = 10,
    GuildList = 11,
    Unknown = 13,
    ViewEquipSpouse = 16,
    StartGamble = 17,
    EndGamble = 18,
    Sound = 20,
    GuildAllies = 21,
    GuildEnemies = 22,
    WhisperDetails = 26,
    ServerName = 61,
}

public sealed record LegacyStringPacket(uint Uid, LegacyStringId Id, IReadOnlyList<string> Strings)
{
    private const int UidOffset = 0;
    private const int IdOffset = UidOffset + sizeof(uint);
    private const int StringCountOffset = IdOffset + sizeof(byte);
    private const int StringsOffset = StringCountOffset + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyString;

    public static LegacyStringPacket PlayerEffect(uint uid, string effectName)
    {
        ArgumentNullException.ThrowIfNull(effectName);
        return new LegacyStringPacket(uid, LegacyStringId.Effect, [effectName]);
    }

    public static LegacyStringPacket LocationEffect(ushort x, ushort y, string effectName)
    {
        ArgumentNullException.ThrowIfNull(effectName);
        var packedLocation = (uint)x | ((uint)y << 16);
        return new LegacyStringPacket(packedLocation, LegacyStringId.LocationEffect, [effectName]);
    }

    public static bool TryParse(PacketFrame frame, out LegacyStringPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < StringsOffset)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        if (!Enum.IsDefined(typeof(LegacyStringId), payload[IdOffset]))
        {
            return false;
        }

        var count = payload[StringCountOffset];
        var offset = StringsOffset;
        var strings = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            if (offset > payload.Length - sizeof(ushort))
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (offset > payload.Length - length)
            {
                return false;
            }

            strings.Add(Encoding.UTF8.GetString(payload.AsSpan(offset, length)));
            offset += length;
        }

        if (offset != payload.Length)
        {
            return false;
        }

        packet = new LegacyStringPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            (LegacyStringId)payload[IdOffset],
            strings);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        if (Strings.Count > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Strings), "Legacy string packets cannot contain more than 255 strings.");
        }

        var encoded = new byte[Strings.Count][];
        var payloadLength = StringsOffset;
        for (var index = 0; index < Strings.Count; index++)
        {
            encoded[index] = Encoding.UTF8.GetBytes(Strings[index]);
            if (encoded[index].Length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(Strings), "Legacy string packet entries cannot exceed 65,535 bytes.");
            }

            payloadLength += sizeof(ushort) + encoded[index].Length;
        }

        var writer = new PacketWriter(Opcode, payloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.Payload[IdOffset] = (byte)Id;
        writer.Payload[StringCountOffset] = (byte)encoded.Length;

        var offset = StringsOffset;
        foreach (var entry in encoded)
        {
            writer.WriteUInt16(offset, (ushort)entry.Length);
            offset += sizeof(ushort);
            entry.CopyTo(writer.Payload[offset..]);
            offset += entry.Length;
        }

        return writer.ToArray();
    }
}
