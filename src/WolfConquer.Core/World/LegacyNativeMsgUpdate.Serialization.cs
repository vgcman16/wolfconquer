using System.IO;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgUpdate
{
    // Projection of the exact UpdateProto/UpdateQuery contract only, not a generic
    // protobuf serializer. Keep native stream mutation after complete serialization.
    private static unsafe void SerializeTypedUpdateProtoIntoPacket(LegacyNativePacket stream, UpdateProto value)
    {
        // The native instance ProtoBufferSerialize call faults on a null receiver
        // before it serializes, but after GetArray publishes Info.Updates.
        if (stream is null)
        {
            throw new NullReferenceException();
        }
        using MemoryStream memoryStream = new MemoryStream();
        SerializeTypedUpdateProto(memoryStream, value);
        byte[] array = memoryStream.ToArray();
        fixed (byte* src = array)
        {
            stream.memcpy(stream.Memory + stream.Position, src, array.Length);
        }
        stream.SeekForward(array.Length);
    }

    private static void SerializeTypedUpdateProto(MemoryStream output, UpdateProto value)
    {
        // UID is required, including zero. Repeated Updates have no wrapper field.
        WriteUpdateVarintField(output, 1, value.UID);
        foreach (UpdateQuery update in value.Updates!)
        {
            // The pinned original protobuf-net2.0.0.668 serializer omits null
            // repeated-message elements without removing them from the owner array.
            if (update is null)
            {
                continue;
            }
            using MemoryStream nested = new MemoryStream();
            if (update.Type != 0)
            {
                // Native [Flags] enum uses protobuf-net's Int32 enum writer:
                // high-bit UInt32 values are sign-extended, not five-byte uints.
                WriteUpdateVarintField(nested, 1, unchecked((ulong)(long)(int)(uint)update.Type));
            }
            if (update.Value != 0)
            {
                WriteUpdateVarintField(nested, 2, update.Value);
            }
            if (update.Values is not null)
            {
                foreach (ulong element in update.Values)
                {
                    // Unpacked repeated values retain zero elements.
                    WriteUpdateVarintField(nested, 3, element);
                }
            }
            if (update.Flag != 0)
            {
                WriteUpdateVarintField(nested, 4, update.Flag);
            }
            if (update.Time != 0)
            {
                WriteUpdateVarintField(nested, 5, update.Time);
            }
            if (update.Damage != 0)
            {
                WriteUpdateVarintField(nested, 6, update.Damage);
            }
            if (update.SpellLevel != 0)
            {
                WriteUpdateVarintField(nested, 7, update.SpellLevel);
            }
            byte[] nestedBytes = nested.ToArray();
            WriteUpdateVarint(output, (2u << 3) | 2u);
            WriteUpdateVarint(output, (ulong)nestedBytes.Length);
            output.Write(nestedBytes, 0, nestedBytes.Length);
        }
    }

    private static void WriteUpdateVarintField(MemoryStream output, uint fieldNumber, ulong value)
    {
        WriteUpdateVarint(output, (ulong)fieldNumber << 3);
        WriteUpdateVarint(output, value);
    }

    private static void WriteUpdateVarint(MemoryStream output, ulong value)
    {
        do
        {
            byte next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }
            output.WriteByte(next);
        }
        while (value != 0);
    }
}
