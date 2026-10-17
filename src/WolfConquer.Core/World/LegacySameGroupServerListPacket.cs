using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacySameGroupServerListPacket(IReadOnlyList<LegacySameGroupServer> Servers)
{
    private const int ServerField = 1;
    private const int ServerIdField = 1;
    private const int TypeField = 2;
    private const int NameField = 3;
    private const int MapIdField = 4;
    private const int XField = 5;
    private const int YField = 6;
    private const int UnknownField = 7;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacySameGroupServerList;
    public const uint StartupServerType = 1;

    public static bool TryCreateFromClientConfigs(
        IReadOnlyList<ClientConfigRecord>? clientConfigs,
        out LegacySameGroupServerListPacket packet)
    {
        packet = default!;

        if (clientConfigs is null || clientConfigs.Count == 0)
        {
            return false;
        }

        var servers = new List<LegacySameGroupServer>(clientConfigs.Count);
        foreach (var config in clientConfigs)
        {
            servers.Add(new LegacySameGroupServer(
                CheckedUInt32(config.ServerId, nameof(config.ServerId)),
                StartupServerType,
                config.ServerName,
                CheckedUInt32(config.ClientBuild, nameof(config.ClientBuild)),
                CheckedUInt32(config.LoginX, nameof(config.LoginX)),
                CheckedUInt32(config.LoginY, nameof(config.LoginY)),
                Unknown: 0));
        }

        packet = new LegacySameGroupServerListPacket(servers);
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacySameGroupServerListPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var servers = new List<LegacySameGroupServer>();

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (fieldNumber != ServerField ||
                wireType != LengthDelimitedWireType ||
                !reader.TryReadBytes(out var serverPayload) ||
                !TryParseServer(serverPayload, out var server))
            {
                return false;
            }

            servers.Add(server);
        }

        packet = new LegacySameGroupServerListPacket(servers);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        foreach (var server in Servers)
        {
            var serverPayload = BuildServerPayload(server);
            WriteVarint(payload, ((ulong)ServerField << 3) | LengthDelimitedWireType);
            WriteVarint(payload, (ulong)serverPayload.Count);
            payload.AddRange(serverPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static List<byte> BuildServerPayload(LegacySameGroupServer server)
    {
        var payload = new List<byte>();
        WriteVarintField(payload, ServerIdField, server.ServerId);
        WriteVarintField(payload, TypeField, server.Type);
        WriteString(payload, NameField, server.Name);
        if (server.MapId != 0)
        {
            WriteVarintField(payload, MapIdField, server.MapId);
        }

        if (server.X != 0)
        {
            WriteVarintField(payload, XField, server.X);
        }

        if (server.Y != 0)
        {
            WriteVarintField(payload, YField, server.Y);
        }

        if (server.Unknown != 0)
        {
            WriteVarintField(payload, UnknownField, server.Unknown);
        }

        return payload;
    }

    private static bool TryParseServer(ReadOnlySpan<byte> payload, out LegacySameGroupServer server)
    {
        server = default;
        var reader = new ProtoReader(payload);
        uint serverId = 0;
        uint type = 0;
        string? name = null;
        uint mapId = 0;
        uint x = 0;
        uint y = 0;
        uint unknown = 0;
        var hasServerId = false;
        var hasType = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);

            if (fieldNumber == NameField)
            {
                if (wireType != LengthDelimitedWireType ||
                    !reader.TryReadString(out name))
                {
                    return false;
                }

                continue;
            }

            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var value) ||
                value > uint.MaxValue)
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ServerIdField:
                    serverId = (uint)value;
                    hasServerId = true;
                    break;
                case TypeField:
                    type = (uint)value;
                    hasType = true;
                    break;
                case MapIdField:
                    mapId = (uint)value;
                    break;
                case XField:
                    x = (uint)value;
                    break;
                case YField:
                    y = (uint)value;
                    break;
                case UnknownField:
                    unknown = (uint)value;
                    break;
                default:
                    return false;
            }
        }

        if (!hasServerId || !hasType || name is null)
        {
            return false;
        }

        server = new LegacySameGroupServer(serverId, type, name, mapId, x, y, unknown);
        return true;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteString(List<byte> payload, int fieldNumber, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)bytes.Length);
        payload.AddRange(bytes);
    }

    private static void WriteVarint(List<byte> payload, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload.Add(next);
        }
        while (value != 0);
    }

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy group-server field cannot be negative.");
        }

        return (uint)value;
    }

    private ref struct ProtoReader(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> _payload = payload;
        private int _offset;

        public readonly bool End => _offset >= _payload.Length;

        public bool TryReadVarint(out ulong value)
        {
            value = 0;

            for (var shift = 0; shift < 64; shift += 7)
            {
                if (_offset >= _payload.Length)
                {
                    return false;
                }

                var current = _payload[_offset++];
                value |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryReadBytes(out ReadOnlySpan<byte> value)
        {
            value = default;
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset + (int)length > _payload.Length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length);
            _offset += (int)length;
            return true;
        }

        public bool TryReadString(out string value)
        {
            value = string.Empty;
            if (!TryReadBytes(out var bytes))
            {
                return false;
            }

            value = Encoding.UTF8.GetString(bytes);
            return true;
        }
    }
}

public readonly record struct LegacySameGroupServer(
    uint ServerId,
    uint Type,
    string Name,
    uint MapId,
    uint X,
    uint Y,
    uint Unknown);
