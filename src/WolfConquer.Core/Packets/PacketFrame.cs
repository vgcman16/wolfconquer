using System.Buffers;

namespace WolfConquer.Core.Packets;

public readonly record struct PacketFrame(PacketHeader Header, ReadOnlySequence<byte> Frame, ReadOnlySequence<byte> Payload);
