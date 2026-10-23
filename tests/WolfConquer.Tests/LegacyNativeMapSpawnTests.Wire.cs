namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapSpawnTests
{
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
