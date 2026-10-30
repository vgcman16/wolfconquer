#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native ElTabakh.Mail constants and primitive codecs, using genuine Packet owners.
internal static class LegacyNativeMail
{
    public const ushort CMsgMailList = 2183;
    public const ushort CMsgMailNotify = 2066;
    public const ushort CMsgMailboxMode = 2264;
    public const ushort CMsgMailContent = 2058;

    public static LegacyNativePacket CreateMailList(this LegacyNativePacket stream, LegacyNativeCMsgMailList obj)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(obj);
        int type = obj.Type, count = obj.Count;
        var input = obj.MailList;
        LegacyNativeCMsgMailList.Mail[] rows = null;
        if (input != null)
        {
            var source = (LegacyNativeCMsgMailList.Mail[])input.Clone();
            rows = new LegacyNativeCMsgMailList.Mail[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (source[index] is null)
                    throw new ArgumentException("Mail list contains a missing row.", nameof(obj));
                rows[index] = new LegacyNativeCMsgMailList.Mail(source[index]);
            }
        }
        var snapshot = new LegacyNativeCMsgMailList { Type = type, Count = count, MailList = rows };
        stream.PrepareFrame(CMsgMailList, prepared => prepared.ProtoBufferSerialize(snapshot));
        return stream;
    }

    public static void GetMailboxMode(this LegacyNativePacket stream, out LegacyNativeCMsgMailboxMode.Type Type,
        out uint ID, out uint count, out ulong[] items)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var parsed = stream.ReadPrepared(prepared =>
        {
            var type = unchecked((LegacyNativeCMsgMailboxMode.Type)prepared.ReadUInt16());
            uint id = prepared.ReadUInt32();
            uint length = prepared.ReadUInt32();
            int remaining = prepared.Size - prepared.Position;
            if (length > (uint)(remaining / sizeof(ulong)))
                throw new EndOfStreamException("Mailbox item count exceeds its supplied input window.");
            var values = new ulong[(int)length];
            for (int index = 0; index < values.Length; index++) values[index] = prepared.ReadUInt64();
            return (Type: type, ID: id, Count: length, Items: values);
        });
        Type = parsed.Type;
        ID = parsed.ID;
        count = parsed.Count;
        items = parsed.Items;
    }

    public static LegacyNativePacket CreateMailboxMode(this LegacyNativePacket stream, LegacyNativeCMsgMailboxMode.Type Type,
        uint ID, uint count, ulong[] items)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (count > 0)
        {
            ArgumentNullException.ThrowIfNull(items);
            if (count > (uint)items.Length)
                throw new ArgumentOutOfRangeException(nameof(count), "Mailbox item count exceeds its supplied array.");
        }
        long declaredLength = 14L + sizeof(ulong) * (long)count;
        if (declaredLength > stream.Capacity || declaredLength > ushort.MaxValue)
            throw new InvalidOperationException("Mailbox mode frame exceeds its physical or UInt16 capacity.");
        ulong[] selected = count == 0 ? Array.Empty<ulong>() : items.AsSpan(0, (int)count).ToArray();
        stream.PrepareFrame(CMsgMailboxMode, prepared =>
        {
            prepared.Write((ushort)Type);
            prepared.Write(ID);
            prepared.Write(count);
            foreach (ulong item in selected) prepared.Write(item);
        });
        return stream;
    }
}
