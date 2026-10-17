using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyWeatherType : uint
{
    Nothing = 1,
    Rain = 2,
    Snow = 3,
    RainWind = 4,
    CherryBlossomPetals = 7,
    CherryBlossomPetalsWind = 8,
    BlowingCotten = 9,
    Atoms = 10,
}

public sealed record LegacyWeatherPacket(
    LegacyWeatherType Type,
    uint Intensity,
    uint Direction,
    uint Color,
    uint SpeedSeconds)
{
    private const int PayloadLength = sizeof(uint) * 5;
    private const uint StartupValue = 250;

    public const ushort Opcode = WorldPacketOpcodes.LegacyWeather;

    public static bool TryCreateStartup(CharacterSnapshot snapshot, out LegacyWeatherPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        packet = default!;

        if (!TryReadWeatherType(snapshot, out var type))
        {
            return false;
        }

        packet = new LegacyWeatherPacket(type, StartupValue, StartupValue, StartupValue, StartupValue);
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyWeatherPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new LegacyWeatherPacket(
            (LegacyWeatherType)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(12, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(16, sizeof(uint))));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(0, (uint)Type);
        writer.WriteUInt32(4, Intensity);
        writer.WriteUInt32(8, Direction);
        writer.WriteUInt32(12, Color);
        writer.WriteUInt32(16, SpeedSeconds);
        return writer.ToArray();
    }

    private static bool TryReadWeatherType(CharacterSnapshot snapshot, out LegacyWeatherType type)
    {
        type = default;
        foreach (var key in new[] { "weatherType", "WeatherType" })
        {
            if (!snapshot.Attributes.TryGetValue(key, out var raw) ||
                !uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
                value == 0)
            {
                continue;
            }

            type = (LegacyWeatherType)value;
            return type is LegacyWeatherType.Nothing or
                LegacyWeatherType.Rain or
                LegacyWeatherType.Snow or
                LegacyWeatherType.RainWind or
                LegacyWeatherType.CherryBlossomPetals or
                LegacyWeatherType.CherryBlossomPetalsWind or
                LegacyWeatherType.BlowingCotten or
                LegacyWeatherType.Atoms;
        }

        return false;
    }
}
