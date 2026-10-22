using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Query = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaInfo;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeNinjaInfoBehaviorTests
{
    private sealed class ConsoleScope : IDisposable
    {
        private readonly TextWriter oldConsole = Console.Out;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        internal StringWriter Writer { get; } = new(CultureInfo.InvariantCulture) { NewLine = "<NOT-LF>" };
        internal ConsoleScope(string culture = "tr-TR")
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Console.SetOut(Writer);
        }
        public void Dispose()
        {
            Console.SetOut(oldConsole);
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
            Writer.Dispose();
        }
    }

    private sealed class PacketScope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly ConsoleScope console = new();
        private readonly NativeSpellTestFixture native = new();
        private readonly LegacyNativePacket sentinel = new(LegacyNativePacket.MAX_SIZE);
        private readonly ConcurrentQueue<LegacyNativePacket> ownedBin = new();
        internal LegacyNativePacket Packet => native.Packet;
        internal Queue<byte[]> Queue => native.Queue;
        internal PacketScope()
        {
            Fill(Packet); Fill(sentinel);
            ownedBin.Enqueue(sentinel);
            Bin.SetValue(null, ownedBin);
        }
        internal unsafe void NoRecycleOrSend()
        {
            Assert.Same(ownedBin, Bin.GetValue(null)); Assert.Equal(1, LegacyNativePacketRecycle.Count);
            Assert.Same(sentinel, Assert.Single(ownedBin));
            Assert.Equal(321, sentinel.Size); Assert.Equal(237, sentinel.Position);
            Assert.Equal((byte)0xA5, sentinel.Memory[0]);
            Assert.Empty(Queue); Assert.Empty(console.Writer.ToString());
        }
        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            sentinel.Dispose(); native.Dispose(); // Native fixture restores exact previous seal.
            console.Dispose();
        }
    }

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
            0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        packet.Size = 321; packet.Seek(237);
    }

    private static unsafe void Tail(LegacyNativePacket packet) => Assert.Equal((byte)0xA5, packet.Memory[packet.Size]);

    // Expected bytes only, not a replacement parser/provider. Signed native Flags
    // use sign-extended Int32 varints; all four fields are required even at zero.
    private static byte[] Expected(Query value)
    {
        var payload = new List<byte>();
        ExpectedField(payload, 1, unchecked((ulong)(long)(int)value.Type));
        ExpectedField(payload, 2, value.ID); ExpectedField(payload, 3, value.Points); ExpectedField(payload, 4, value.Position);
        return Frame(payload.ToArray());
    }

    private static void ExpectedField(List<byte> output, uint field, ulong value)
    {
        output.Add((byte)(field << 3));
        while (value > 127) { output.Add((byte)((value & 127) | 128)); value >>= 7; }
        output.Add((byte)value);
    }

    private static byte[] Frame(byte[] payload)
    {
        var bytes = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2394);
        payload.CopyTo(bytes, 4);
        return bytes;
    }

    private static unsafe void Copy(LegacyNativePacket packet, byte[] bytes)
    {
        Assert.InRange(bytes.Length, 4, LegacyNativePacket.MAX_SIZE);
        Marshal.Copy(bytes, 0, (IntPtr)packet.Memory, bytes.Length);
        packet.Size = bytes.Length;
    }

    private static void AssertValues(Query wanted, Query actual)
    {
        Assert.Equal(wanted.Type, actual.Type); Assert.Equal(wanted.ID, actual.ID);
        Assert.Equal(wanted.Points, actual.Points); Assert.Equal(wanted.Position, actual.Position);
    }
}
