using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal struct LegacyNativeMsgTransformFairy
{
    public enum Action : uint
    {
        Active = 1u,
        Dezactive
    }

    public Action Mode;

    public uint UnKnow;

    public uint FairyType;

    public uint UID;

    public static LegacyNativeMsgTransformFairy Create()
    {
        return default(LegacyNativeMsgTransformFairy);
    }

    [LegacyNativePacketAttribute(2336)]
    public static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        stream.GetTransformFairy(out var action, out var fairy, out var uid);
        switch (action)
        {
        case Action.Active:
            if (user.Player.OnFairy)
            {
                user.Player.FairySpawn.Mode = Action.Dezactive;
                user.Player.View!.SendView(stream.TransformFairyCreate(Action.Dezactive, user.Player.FairySpawn.FairyType, user.Player.FairySpawn.UID), me: true);
                user.Player.OnFairy = false;
                user.Player.FairySpawn = default(LegacyNativeMsgTransformFairy);
            }
            user.Player.OnFairy = true;
            uid = user.Player.UID;
            user.Player.View!.SendView(stream.TransformFairyCreate(Action.Active, fairy, uid), me: true);
            user.Player.FairySpawn = Create();
            user.Player.FairySpawn.Mode = action;
            user.Player.FairySpawn.FairyType = fairy;
            user.Player.FairySpawn.UID = uid;
            break;
        case Action.Dezactive:
            user.Player.OnFairy = false;
            uid = user.Player.UID;
            user.Player.View!.SendView(stream.TransformFairyCreate(Action.Dezactive, fairy, uid), me: true);
            user.Player.FairySpawn = default(LegacyNativeMsgTransformFairy);
            break;
        }
    }
}



internal static class LegacyNativeTransformFairyPacket
{
    public static void GetTransformFairy(this LegacyNativePacket stream,
        out LegacyNativeMsgTransformFairy.Action action, out uint fairy, out uint uid)
    {
        action = (LegacyNativeMsgTransformFairy.Action)stream.ReadUInt32();
        uint num = stream.ReadUInt32();
        fairy = stream.ReadUInt32();
        uid = stream.ReadUInt32();
    }

    public static LegacyNativePacket TransformFairyCreate(this LegacyNativePacket stream,
        LegacyNativeMsgTransformFairy.Action action, uint fairy, uint uid)
    {
        stream.InitWriter();
        stream.Write((uint)action);
        stream.Write(0u);
        stream.Write(fairy);
        stream.Write(uid);
        stream.Finalize(2336);
        return stream;
    }
}
