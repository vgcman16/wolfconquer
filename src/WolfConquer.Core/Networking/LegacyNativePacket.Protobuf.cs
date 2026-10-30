using System.Buffers.Binary;
using ProtoBuf;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    public unsafe void ProtoBufferSerialize(object obj)
    {
        int position = ValidateProtobufStorage();
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        int sealSize = SealSize;
        using MemoryStream memoryStream = new MemoryStream();
        Serializer.Serialize((Stream)memoryStream, obj);
        byte[] array = memoryStream.ToArray();
        RequireProtobufState(allocation, cursor, size);
        if (SealSize != sealSize || sealSize > Capacity - position ||
            array.Length > Capacity - position - sealSize)
            throw new InvalidOperationException("Protobuf payload and current seal exceed the packet allocation.");

        // Production callers subsequently Finalize. Reserve the current seal,
        // but retain the append API's header/Size ownership and fresh Size0 writers.
        array.AsSpan().CopyTo(new Span<byte>(stream, array.Length));
        stream += array.Length;
    }

    public unsafe T ProtoBufferDeserialize<T>(T obj)
    {
        ValidateProtobufStorage();
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        if (size < 4)
            throw new InvalidDataException("Protobuf frame is missing its complete header.");

        int declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(allocation, 4));
        if (declaredLength < 4 || declaredLength > size)
            throw new InvalidDataException("Protobuf frame length exceeds its actual input or omits the header.");

        // Only the bounded managed copy reaches protobuf-net. Failure never
        // publishes a partially consumed native cursor or merges the supplied object.
        byte[] array = new ReadOnlySpan<byte>(allocation + 4, declaredLength - 4).ToArray();
        using MemoryStream source = new MemoryStream(array);
        obj = Serializer.Deserialize<T>(source);
        RequireProtobufState(allocation, cursor, size);
        stream = allocation + declaredLength;
        return obj;
    }

    private unsafe int ValidateProtobufStorage()
    {
        ObjectDisposedException.ThrowIf(IsDisposed || Memory == null, this);
        nuint allocation = (nuint)Memory;
        nuint cursor = (nuint)stream;
        if (cursor < allocation || cursor - allocation > (nuint)Capacity)
            throw new InvalidOperationException("Protobuf cursor is outside the packet allocation.");
        if (Size < 0 || Size > Capacity)
            throw new InvalidOperationException("Protobuf Size is outside the packet allocation.");
        return (int)(cursor - allocation);
    }

    private unsafe void RequireProtobufState(byte* allocation, byte* cursor, int size)
    {
        ValidateProtobufStorage();
        if (Memory != allocation || stream != cursor || Size != size)
            throw new InvalidOperationException("Packet storage changed while preparing protobuf data.");
    }

    public T ProtoBufferDeserialize<T>(T obj, byte[] packet)
    {
        using (MemoryStream source = new MemoryStream(packet))
        {
            obj = Serializer.Deserialize<T>(source);
        }
        return obj;
    }
}
