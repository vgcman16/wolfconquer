#nullable disable
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using ProtoBuf;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    public const ushort MsgNinjaItem_ID = 2393;
    // Required outer Type/UID each need a tag+one value byte. Each nonnull
    // Items row has five required uint fields (10 minimum bytes), plus the
    // repeated field3 tag and its minimum one-byte length prefix.
    private const int MinimumDeclaredItemFrame = 4 + 2 + 2;
    private const int MinimumRepeatedItemBytes = 1 + 1 + (5 * 2);
    private const int MaximumMinimumSizeRows = (ushort.MaxValue - MinimumDeclaredItemFrame) / MinimumRepeatedItemBytes;

    public static LegacyNativePacket CreateNinjaItem(this LegacyNativePacket stream, MsgNinjaItem obj) =>
        WritePreparedNinjaItem(stream, PrepareNinjaItemFrame(obj));

    internal static byte[] PrepareNinjaItemFrame(MsgNinjaItem obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        // The same serializer invocation/model as ProtoBufferSerialize, entirely
        // managed until the complete payload/header/seal length is established.
        var snapshot = SnapshotNinjaItemMessage(obj);
        var body = SerializeNinjaItemPayload(snapshot);
        var sealSize = LegacyNativePacket.SealSize;
        var seal = LegacyNativePacket.SealString;
        if ((seal is null) != (sealSize == 0) || seal is not null && seal.Length != sealSize)
            throw new InvalidOperationException("Ninja item packet seal is unavailable or changed.");
        var declaredLength = checked(4 + body.Length);
        if (declaredLength > ushort.MaxValue) throw new InvalidOperationException("Ninja item frame exceeds its UInt16 length.");
        var frame = new byte[checked(declaredLength + sealSize)];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)declaredLength);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), MsgNinjaItem_ID);
        body.CopyTo(frame, 4);
        // Native WriteSeal copies the first SealSize UTF8 bytes of its fixed
        // character string. Capture once; never reread a mutable seal while writing.
        if (seal is not null) Encoding.UTF8.GetBytes(seal).AsSpan(0, sealSize).CopyTo(frame.AsSpan(declaredLength));
        return frame;
    }

    private static MsgNinjaItem SnapshotNinjaItemMessage(MsgNinjaItem obj)
    {
        var type = obj.Type;
        var uid = obj.UID;
        var source = obj.Items;
        var rows = new List<MsgNinjaItem.Item>();
        if (source is not null)
        {
            foreach (var row in source)
            {
                if (row is null) continue; // Actual protobuf repeated-entry omission.
                if (rows.Count >= MaximumMinimumSizeRows)
                    throw new InvalidOperationException("Ninja item rows cannot fit their minimum UInt16 frame size.");
                rows.Add(new() { ID = row.ID, Level = row.Level, Position = row.Position, Points = row.Points, UK = row.UK });
            }
        }
        return new() { Type = type, UID = uid, Items = source is null ? null : rows.ToArray() };
    }

    private static byte[] SerializeNinjaItemPayload(MsgNinjaItem snapshot)
    {
        using var payload = new MemoryStream();
        Serializer.Serialize((Stream)payload, (object)snapshot);
        return payload.ToArray();
    }

    internal static unsafe LegacyNativePacket WritePreparedNinjaItem(LegacyNativePacket stream, byte[] frame)
    {
        if (stream is null || stream.Memory == null || frame is null || frame.Length < 4 ||
            stream.Capacity < frame.Length) throw new InvalidOperationException("Ninja item frame exceeds its real packet storage.");
        Marshal.Copy(frame, 0, (nint)stream.Memory, frame.Length);
        stream.Size = frame.Length;
        stream.Seek(4); // The genuine Finalize leaves its cursor after the header.
        return stream;
    }

    public static void GetNinjaItem(this LegacyNativePacket stream, out MsgNinjaItem pQuery)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var decoded = stream.ProtoBufferDeserialize(new MsgNinjaItem());
        pQuery = decoded;
    }
}
