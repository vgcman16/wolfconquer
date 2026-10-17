using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record VisibleDx8D13TrainingVitalityScorePacket(
    byte PowerType,
    uint AttributeScore,
    string CharacterName)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int PowerTypeOffset = 0;
    private const int AttributeScoreOffset = PowerTypeOffset + sizeof(byte);
    private const int CharacterNameOffset = AttributeScoreOffset + sizeof(uint);

    public const ushort Opcode = 2276;
    public const int CharacterNameLength = 32;
    public const int PayloadLength = CharacterNameOffset + CharacterNameLength;
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static bool TryParse(
        PacketFrame frame,
        out VisibleDx8D13TrainingVitalityScorePacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode ||
            frame.Header.Length != FrameLength ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var powerType = payload[PowerTypeOffset];
        if (powerType is < (byte)LegacyChiPowerType.Dragon or > (byte)LegacyChiPowerType.Turtle)
        {
            return false;
        }

        var nameBytes = payload.AsSpan(CharacterNameOffset, CharacterNameLength);
        var terminator = nameBytes.IndexOf((byte)0);
        if (terminator >= 0)
        {
            nameBytes = nameBytes[..terminator];
        }

        packet = new VisibleDx8D13TrainingVitalityScorePacket(
            powerType,
            BinaryPrimitives.ReadUInt32LittleEndian(
                payload.AsSpan(AttributeScoreOffset, sizeof(uint))),
            NameEncoding.GetString(nameBytes));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(CharacterName);
        if (PowerType is < (byte)LegacyChiPowerType.Dragon or > (byte)LegacyChiPowerType.Turtle)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PowerType),
                PowerType,
                "D13 Training Vitality score power type must be between 1 and 4.");
        }

        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.Payload[PowerTypeOffset] = PowerType;
        writer.WriteUInt32(AttributeScoreOffset, AttributeScore);
        var destination = writer.Payload.Slice(CharacterNameOffset, CharacterNameLength);
        var encoder = NameEncoding.GetEncoder();
        encoder.Convert(
            CharacterName.AsSpan(),
            destination[..^1],
            flush: true,
            out _,
            out _,
            out _);
        return writer.ToArray();
    }
}
