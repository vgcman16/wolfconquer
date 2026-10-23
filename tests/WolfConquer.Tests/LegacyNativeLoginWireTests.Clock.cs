using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLoginWireTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WholeServerTimerSamplesRealLocalTimeAndWritesAllEightRequiredProtobufFields(bool sealedFrame)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new Scope(sealedFrame);
        DateTime before = DateTime.Now;
        Assert.Same(f.Packet, f.Packet.ServerTimerCreate());
        DateTime after = DateTime.Now;
        int length = BinaryPrimitives.ReadUInt16LittleEndian(f.Bytes(0, 2));
        byte[] payload = f.Bytes(4, length - 4); int cursor = 0;
        uint ReadVarint()
        {
            uint result = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                Assert.InRange(cursor, 0, payload.Length - 1); byte part = payload[cursor++];
                result |= (uint)(part & 127) << shift;
                if ((part & 128) == 0) return result;
            }
            throw new InvalidOperationException("Unbounded native timer varint.");
        }
        uint[] values = new uint[8];
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal((uint)((i + 1) << 3), ReadVarint()); values[i] = ReadVarint();
        }
        Assert.Equal(payload.Length, cursor); Assert.Equal(0u, values[0]);
        DateTime sample = new((int)values[1] + 1900, (int)values[2] + 1, (int)values[4],
            (int)values[5], (int)values[6], (int)values[7]);
        Assert.Equal(sample.DayOfYear, (int)values[3]);
        DateTime first = new(before.Ticks - before.Ticks % TimeSpan.TicksPerSecond);
        DateTime last = new(after.Ticks - after.Ticks % TimeSpan.TicksPerSecond);
        Assert.InRange(sample, first <= last ? first : last, first <= last ? last : first);
        var expectedPayload = new List<byte>();
        void Varint(uint value)
        {
            while (value >= 128) { expectedPayload.Add((byte)((value & 127) | 128)); value >>= 7; }
            expectedPayload.Add((byte)value);
        }
        for (int i = 0; i < values.Length; i++) { Varint((uint)((i + 1) << 3)); Varint(values[i]); }
        Assert.Equal(expectedPayload, payload);
        f.AssertFrame(Frame(2224, expectedPayload.ToArray(), sealedFrame));
    });
}
