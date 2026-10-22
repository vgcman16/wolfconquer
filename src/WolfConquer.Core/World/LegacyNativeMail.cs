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
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2183);
        return stream;
    }

    public static void GetMailboxMode(this LegacyNativePacket stream, out LegacyNativeCMsgMailboxMode.Type Type,
        out uint ID, out uint count, out ulong[] items)
    {
        Type = unchecked((LegacyNativeCMsgMailboxMode.Type)stream.ReadUInt16());
        ID = stream.ReadUInt32();
        count = stream.ReadUInt32();
        items = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = stream.ReadUInt64();
        }
    }

    public static LegacyNativePacket CreateMailboxMode(this LegacyNativePacket stream, LegacyNativeCMsgMailboxMode.Type Type,
        uint ID, uint count, ulong[] items)
    {
        stream.InitWriter();
        stream.Write((ushort)Type);
        stream.Write(ID);
        stream.Write(count);
        for (int i = 0; i < count; i++)
        {
            stream.Write(items[i]);
        }
        stream.Finalize(2264);
        return stream;
    }
}
