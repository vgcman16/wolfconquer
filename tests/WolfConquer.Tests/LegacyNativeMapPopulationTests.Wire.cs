using System.Text;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapPopulationTests
{
    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }

    private static byte[] SobWire(uint uid, int hp, int max, ushort x, ushort y,
        ushort mesh, byte type, ushort sort, string? name)
    {
        var payload = new List<byte>();
        ulong[] values = [uid, 0, 0, unchecked((uint)max), unchecked((uint)hp), x, y, mesh, type, sort];
        for (int i = 0; i < values.Length; i++) { Varint(payload, (ulong)((i + 1) << 3)); Varint(payload, values[i]); }
        if (!string.IsNullOrEmpty(name))
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(name); Varint(payload, 98); Varint(payload, (ulong)utf8.Length); payload.AddRange(utf8);
        }
        return Frame(2166, payload);
    }

    private static byte[] Frame(ushort opcode, List<byte> payload)
    {
        var frame = new List<byte>(); frame.AddRange(BitConverter.GetBytes((ushort)(payload.Count + 4)));
        frame.AddRange(BitConverter.GetBytes(opcode)); frame.AddRange(payload); return frame.ToArray();
    }

    private static Dictionary<int, List<ulong>> Scalars(byte[] frame)
    {
        int offset = 4;
        ulong Read()
        {
            ulong result = 0; int shift = 0;
            while (true)
            {
                byte value = frame[offset++]; result |= (ulong)(value & 127) << shift;
                if ((value & 128) == 0) return result;
                shift += 7; Assert.True(shift <= 63);
            }
        }
        var fields = new Dictionary<int, List<ulong>>();
        while (offset < frame.Length)
        {
            ulong key = Read(); int tag = (int)(key >> 3), wire = (int)(key & 7);
            if (wire == 0)
            {
                if (!fields.TryGetValue(tag, out var values)) fields[tag] = values = [];
                values.Add(Read());
            }
            else
            {
                Assert.Equal(2, wire); int size = checked((int)Read()); offset += size;
                Assert.InRange(offset, 0, frame.Length);
            }
        }
        return fields;
    }
}
