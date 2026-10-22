using ProtoBuf;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    public unsafe void ProtoBufferSerialize(object obj)
    {
        using MemoryStream memoryStream = new MemoryStream();
        Serializer.Serialize((Stream)memoryStream, obj);
        byte[] array = memoryStream.ToArray();
        fixed (byte* src = array)
        {
            memcpy(stream, src, array.Length);
        }
        stream += array.Length;
    }

    public T ProtoBufferDeserialize<T>(T obj)
    {
        Seek(0);
        ushort num = ReadUInt16();
        ReadUInt16();
        byte[] array = new byte[num - 4];
        array = ReadBytes(num - 4);
        using (MemoryStream source = new MemoryStream(array))
        {
            obj = Serializer.Deserialize<T>(source);
        }
        return obj;
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
