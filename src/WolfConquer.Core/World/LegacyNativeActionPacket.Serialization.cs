#nullable disable
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeActionPacket
{
    // Projection of ActionQuery's complete attributed contract only, not generic protobuf.
    private static unsafe void SerializeTypedActionQueryIntoPacket(LegacyNativePacket stream, LegacyNativeActionQuery value)
    {
        if (stream is null)
            throw new NullReferenceException();
        using MemoryStream output = new MemoryStream();
        Serialize(output, value);
        byte[] array = output.ToArray();
        fixed (byte* src = array)
            stream.memcpy(stream.Memory + stream.Position, src, array.Length);
        stream.SeekForward(array.Length);
    }

    private static void Serialize(Stream output, LegacyNativeActionQuery value)
    {
        if (value is null)
            return;
        Field(output, 1, value.ObjId);
        if (value.TargetUID != 0) Field(output, 2, value.TargetUID);
        if (value.dwParam != 0) Field(output, 3, value.dwParam);
        if (value.dwParam2 != 0) Field(output, 4, unchecked((ulong)(long)value.dwParam2));
        if (value.dwParam3 != 0) Field(output, 5, unchecked((ulong)value.dwParam3));
        if (value.SucDone) Field(output, 6, 1);
        if (value.TargetPositionX != 0) Field(output, 7, value.TargetPositionX);
        if (value.TargetPositionY != 0) Field(output, 8, value.TargetPositionY);
        Field(output, 9, value.Timestamp);
        if (value.NpcID != 0) Field(output, 10, unchecked((ulong)(long)value.NpcID));
        if (value.CheatLogData1 != 0) Field(output, 11, value.CheatLogData1);
        Field(output, 12, value.Type);
        Field(output, 13, value.Fascing);
        Field(output, 14, value.PositionX);
        Field(output, 15, value.PositionY);
        if (value.CheatLogData2 != 0) Field(output, 16, value.CheatLogData2);
        if (value.MapID != 0) Field(output, 17, value.MapID);
        if (value.CheatLogData3 != 0) Field(output, 18, value.CheatLogData3);
        if (value.ScheduleTime != 0) Field(output, 19, unchecked((ulong)(long)value.ScheduleTime));
        if (value.LinkIndex != 0) Field(output, 20, unchecked((ulong)(long)value.LinkIndex));
        if (value.CheatLogData4 != 0) Field(output, 21, value.CheatLogData4);
        if (value.RemainTime != 0) Field(output, 22, unchecked((ulong)(long)value.RemainTime));
        if (value.MountCharge) Field(output, 23, 1);
        if (value.Strings is not null)
        {
            foreach (string text in value.Strings)
            {
                if (text is null)
                    continue;
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                Varint(output, (24u << 3) | 2u);
                Varint(output, (ulong)bytes.Length);
                output.Write(bytes, 0, bytes.Length);
            }
        }
    }

    private static void Field(Stream output, uint field, ulong value)
    {
        Varint(output, (ulong)field << 3);
        Varint(output, value);
    }

    private static void Varint(Stream output, ulong value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        output.WriteByte((byte)value);
    }
}
