using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native enum and complete primitive codec only, without handler/host routing.
internal static class LegacyNativeItemLockPacket
{
    public enum TypeLock : byte
    {
        RequestLock,
        RequestUnlock,
        UnlockDate
    }

    public static void GetItemLock(this LegacyNativePacket stream, out uint UID,
        out TypeLock Typ, out byte LockedMode, out uint dwPram)
    {
        UID = stream.ReadUInt32();
        Typ = (TypeLock)stream.ReadUInt8();
        LockedMode = stream.ReadUInt8();
        uint num = stream.ReadUInt16();
        dwPram = stream.ReadUInt32();
    }

    public static LegacyNativePacket ItemLockCreate(this LegacyNativePacket stream,
        uint UID, TypeLock Typ, byte LockedMode, uint dwPram)
    {
        stream.InitWriter();
        stream.Write(UID);
        stream.Write((byte)Typ);
        stream.Write(LockedMode);
        stream.Write((ushort)0);
        stream.Write(dwPram);
        stream.Finalize(2323);
        return stream;
    }
}
