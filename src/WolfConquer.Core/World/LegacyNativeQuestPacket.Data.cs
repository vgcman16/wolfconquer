using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeQuestPacket
{
    public static void GetQuestData(this LegacyNativePacket stream, out uint UnKnow, out uint UID, out uint[] intents)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = stream.ReadPrepared(static packet =>
        {
            uint unknown = packet.ReadUInt32();
            uint uid = packet.ReadUInt32();
            uint[] values = [packet.ReadUInt32()];
            return (Unknown: unknown, Uid: uid, Intents: values);
        });
        UnKnow = prepared.Unknown;
        UID = prepared.Uid;
        intents = prepared.Intents;
    }

    public static LegacyNativePacket MsgQuestDataCreate(this LegacyNativePacket stream, uint UnKnow, uint UID, params uint[] intents)
    {
        ArgumentNullException.ThrowIfNull(stream);
        uint[] values = CaptureQuestIntents(intents);
        stream.PrepareFrame(2136, prepared =>
        {
            prepared.Write((ushort)0);
            prepared.Write((ushort)values.Length);
            prepared.Write(UID);
            foreach (uint value in values) prepared.Write(value);
            if (UID == 35024)
            {
                foreach (uint value in values) prepared.Write(value);
            }
            prepared.Write(0);
            if (UID == 35007)
            {
                prepared.ZeroFill(12);
                foreach (uint value in values) prepared.Write(value);
            }
        });
        return stream;
    }

    public static LegacyNativePacket MsgQuestDataCreate(this LegacyNativePacket stream, uint UnKnow, uint UID,
        uint OwnerUID, params uint[] intents)
    {
        ArgumentNullException.ThrowIfNull(stream);
        uint[] values = CaptureQuestIntents(intents);
        stream.PrepareFrame(2136, prepared =>
        {
            prepared.Write((ushort)0);
            prepared.Write((ushort)values.Length);
            prepared.Write(UID);
            foreach (uint value in values) prepared.Write(value);
        });
        return stream;
    }

    private static uint[] CaptureQuestIntents(uint[] intents)
    {
        ArgumentNullException.ThrowIfNull(intents);
        if (intents.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(intents), "Quest intent count exceeds its UInt16 representation.");
        return (uint[])intents.Clone();
    }
}
