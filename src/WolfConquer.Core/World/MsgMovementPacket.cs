using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum MsgMovementMode : uint
{
    Walk = 0,
    Run = 1,
    Steed = 9,
}

public sealed record MsgMovementPacket
{
    private const int DirectionField = 1;
    private const int UidField = 2;
    private const int RunningField = 3;
    private const int TimestampField = 4;
    private const int MapIdField = 5;
    private const int VarintWireType = 0;
    private const int Fixed64WireType = 1;
    private const int LengthDelimitedWireType = 2;
    private const int Fixed32WireType = 5;

    public const ushort Opcode = WorldPacketOpcodes.MsgMovement;

    public uint Direction { get; init; }

    public uint Uid { get; init; }

    public MsgMovementMode Running { get; init; }

    public uint Timestamp { get; init; }

    public uint MapId { get; init; }

    public IReadOnlyList<uint> UnknownFields { get; init; } = [];

    public static bool TryParse(PacketFrame frame, out MsgMovementPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var result = new MsgMovementPacket();
        var hasDirection = false;
        var hasUid = false;
        var hasRunning = false;
        var hasTimestamp = false;
        var unknownFields = new List<uint>();

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            if ((tagValue >> 3) > int.MaxValue)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            switch (fieldNumber)
            {
                case DirectionField:
                    if (!TryReadUInt32Varint(ref reader, wireType, out var direction))
                    {
                        return false;
                    }

                    result = result with { Direction = direction };
                    hasDirection = true;
                    break;
                case UidField:
                    if (!TryReadUInt32Varint(ref reader, wireType, out var uid))
                    {
                        return false;
                    }

                    result = result with { Uid = uid };
                    hasUid = true;
                    break;
                case RunningField:
                    if (!TryReadUInt32Varint(ref reader, wireType, out var running))
                    {
                        return false;
                    }

                    result = result with { Running = (MsgMovementMode)running };
                    hasRunning = true;
                    break;
                case TimestampField:
                    if (!TryReadUInt32Varint(ref reader, wireType, out var timestamp))
                    {
                        return false;
                    }

                    result = result with { Timestamp = timestamp };
                    hasTimestamp = true;
                    break;
                case MapIdField:
                    if (!TryReadUInt32Varint(ref reader, wireType, out var mapId))
                    {
                        return false;
                    }

                    result = result with { MapId = mapId };
                    break;
                default:
                    if (!reader.TrySkip(wireType))
                    {
                        return false;
                    }

                    unknownFields.Add((uint)fieldNumber);
                    break;
            }
        }

        if (!hasDirection || !hasUid || !hasRunning || !hasTimestamp)
        {
            return false;
        }

        packet = result with
        {
            UnknownFields = unknownFields.Count == 0 ? [] : unknownFields.ToArray(),
        };
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, DirectionField, Direction);
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, RunningField, (uint)Running);
        WriteVarintField(payload, TimestampField, Timestamp);
        if (MapId != 0)
        {
            WriteVarintField(payload, MapIdField, MapId);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
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

    private static bool TryReadUInt32Varint(ref ProtoReader reader, int wireType, out uint value)
    {
        value = 0;
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var raw) ||
            raw > uint.MaxValue)
        {
            return false;
        }

        value = (uint)raw;
        return true;
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

        public bool TrySkip(int wireType)
        {
            return wireType switch
            {
                VarintWireType => TryReadVarint(out _),
                Fixed64WireType => TryAdvance(sizeof(ulong)),
                LengthDelimitedWireType => TrySkipLengthDelimited(),
                Fixed32WireType => TryAdvance(sizeof(uint)),
                _ => false,
            };
        }

        private bool TrySkipLengthDelimited()
        {
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue)
            {
                return false;
            }

            return TryAdvance((int)length);
        }

        private bool TryAdvance(int count)
        {
            if (count < 0 ||
                _offset > _payload.Length - count)
            {
                return false;
            }

            _offset += count;
            return true;
        }
    }
}
