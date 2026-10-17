using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyInstanceAction : uint
{
    Info = 10,
    Quit = 11,
}

public sealed record LegacyInstancePacket(uint Type, uint Weather = 0, uint UserId = 0)
{
    private const int TypeField = 1;
    private const int WeatherField = 3;
    private const int UserIdField = 4;

    public const ushort Opcode = WorldPacketOpcodes.LegacyInstance;

    public LegacyInstancePacket(LegacyInstanceAction action, uint weather = 0, uint userId = 0)
        : this((uint)action, weather, userId)
    {
    }

    public LegacyInstanceAction? Action => TryGetAction(out var action) ? action : null;

    public uint InstanceId => Weather;

    public uint PlayerUid => UserId;

    public bool TryGetAction(out LegacyInstanceAction action)
    {
        if (Type is (uint)LegacyInstanceAction.Info or (uint)LegacyInstanceAction.Quit)
        {
            action = (LegacyInstanceAction)Type;
            return true;
        }

        action = default;
        return false;
    }

    public static bool TryParse(PacketFrame frame, out LegacyInstancePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var type = 0U;
        var weather = 0U;
        var userId = 0U;
        var hasType = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (!TryReadUInt32(ref reader, wireType, out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;
                case WeatherField:
                    if (!TryReadUInt32(ref reader, wireType, out weather))
                    {
                        return false;
                    }

                    break;
                case UserIdField:
                    if (!TryReadUInt32(ref reader, wireType, out userId))
                    {
                        return false;
                    }

                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (!hasType)
        {
            return false;
        }

        packet = new LegacyInstancePacket(type, weather, userId);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, Type);
        LegacyProtoWire.WriteVarintField(payload, WeatherField, Weather);
        LegacyProtoWire.WriteVarintField(payload, UserIdField, UserId);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryReadUInt32(ref LegacyProtoWire.Reader reader, int wireType, out uint value)
    {
        value = 0;
        return wireType == LegacyProtoWire.VarintWireType && reader.TryReadUInt32(out value);
    }
}
