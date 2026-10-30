using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using Pool = WolfConquer.Core.Networking.LegacyNativePacketRecycle;
using Recycled = WolfConquer.Core.Networking.LegacyNativeRecycledPacket;

namespace WolfConquer.Tests;

// Only valid owned allocations and bounded cursors. No unsafe malformed-input claims.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe class LegacyNativePacketStorageTests
{
    [Fact]
    public Task BoundedByteArrayConstructorCopiesWithoutAliasingAndStartsCursorAtZero()
    {
        return NativePacketTestThread.Run(() =>
        {
            byte[] source = [1, 2, 3, 4, 5, 6, 7, 8]; // Safely below native1024 allocation.
            using var packet = new Packet(source);
            source[0] = 99;
            Assert.Equal(8, packet.Size);
            Assert.Equal(0, packet.Position);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, packet.ReadBytes(8));
            Assert.Equal(8, packet.Position);
        });
    }

    [Fact]
    public Task PrimitiveProtocolWriterRetainsItsUnusualTrailingNextFieldTag()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var packet = new Packet(Packet.MAX_SIZE);
            packet.CreateProtocolBuffer(127, 128);
            Assert.Equal(Convert.FromHexString("087F10800118"), Bytes(packet, 0, packet.Position));
            packet.Seek(0);
            packet.CreateProtocolBuffer();
            Assert.Equal(1, packet.Position);
            Assert.Equal((byte)8, packet.Memory[0]);
        });
    }

    [Fact]
    public Task OwnedAllocationKeepsIndependentSizeAndLiteralCursorOperations()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var packet = new Packet(Packet.MAX_SIZE);
            Assert.Equal(16384, Packet.MAX_SIZE);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)packet.Memory);
            Assert.Equal((IntPtr)packet.Memory, (IntPtr)packet.stream);
            Assert.Equal(0, packet.Position);
            Assert.Equal(0, packet.Size);
            packet.InitWriter();
            Assert.Equal(4, packet.Position);
            packet.SeekForward(5);
            Assert.Equal(9, packet.Position);
            packet.SeekBackwards(3);
            Assert.Equal(6, packet.Position);
            packet.Size = 99;
            packet.Seek(0);
            Assert.Equal(0, packet.Position);
            Assert.Equal(99, packet.Size);
        });
    }

    [Fact]
    public Task PrimitiveSignedAndUnsignedValuesUseExactLittleEndianStorageAndAdvanceCursors()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var packet = new Packet(Packet.MAX_SIZE);
            packet.Write(true);
            packet.Write(false);
            packet.Write((byte)0xA5);
            packet.Write((sbyte)-2);
            packet.Write((ushort)0x1234);
            packet.Write((short)-12345);
            packet.Write(0x89ABCDEFU);
            packet.Write(-123456789);
            packet.Write(0x0123456789ABCDEFUL);
            packet.Write(-2L);
            Assert.Equal(32, packet.Position);
            Assert.Equal(Convert.FromHexString("0100A5FE3412C7CFEFCDAB89EB32A4F8EFCDAB8967452301FEFFFFFFFFFFFFFF"),
                Bytes(packet, 0, 32));
            Assert.Equal(0, packet.Size);

            packet.Size = packet.Position; // Capture the complete initialized writer extent.
            packet.Seek(0);
            Assert.Equal((byte)1, packet.ReadUInt8());
            Assert.Equal((byte)0, packet.ReadUInt8());
            Assert.Equal((byte)0xA5, packet.ReadUInt8());
            Assert.Equal((sbyte)-2, packet.ReadInt8());
            Assert.Equal((ushort)0x1234, packet.ReadUInt16());
            Assert.Equal((short)-12345, packet.ReadInt16());
            Assert.Equal(0x89ABCDEFU, packet.ReadUInt32());
            Assert.Equal(-123456789, packet.ReadInt32());
            Assert.Equal(0x0123456789ABCDEFUL, packet.ReadUInt64());
            Assert.Equal(-2L, packet.ReadInt64());
            Assert.Equal(32, packet.Position);
        });
    }

    [Theory]
    [InlineData("éZ", 5, "C3A9000000", "é")]
    [InlineData("Aé", 4, "41C30000", "A\uFFFD")]
    public Task FixedStringWritesCharacterCountOfUtf8BytesThenPadsTheField(
        string value, int length, string hex, string decoded)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var packet = new Packet(Packet.MAX_SIZE);
            packet.Write(value, length);
            Assert.Equal(length, packet.Position);
            Assert.Equal(Convert.FromHexString(hex), Bytes(packet, 0, length));
            packet.Size = packet.Position;
            packet.Seek(0);
            Assert.Equal(decoded, packet.ReadCString(length));
            Assert.Equal(length, packet.Position);
        });
    }

    [Fact]
    public Task StringListUsesUtf8ByteLengthsUnlikeFixedStringAndReadsEmptyEntries()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var packet = new Packet(Packet.MAX_SIZE);
            packet.Write(new[] { "éZ", "", "中" });
            Assert.Equal(Convert.FromHexString("030300C3A95A00000300E4B8AD"), Bytes(packet, 0, 13));
            Assert.Equal(13, packet.Position);
            packet.Size = packet.Position;
            packet.Seek(0);
            Assert.Equal(new[] { "éZ", "", "中" }, packet.ReadStringList());
            Assert.Equal(13, packet.Position);
        });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public Task PrimitiveBoundaryWritesAndReadsTheExactInitializedAllocationEnd(int width)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var packet = new Packet(Packet.MAX_SIZE);
            byte[] initialized = Enumerable.Repeat((byte)0xCC, Packet.MAX_SIZE).ToArray();
            Marshal.Copy(initialized, 0, (IntPtr)packet.Memory, initialized.Length);
            packet.Size = initialized.Length;
            packet.Seek(Packet.MAX_SIZE - width);
            WriteWidth(packet, width);
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
            Assert.Equal(Enumerable.Repeat((byte)0xCC, 8 - width)
                    .Concat(Enumerable.Repeat((byte)0xA5, width)).ToArray(),
                Bytes(packet, Packet.MAX_SIZE - 8, 8));
            packet.Seek(Packet.MAX_SIZE - width);
            ulong expected = width == 8 ? 0xA5A5A5A5A5A5A5A5UL :
                0xA5A5A5A5A5A5A5A5UL & ((1UL << (width * 8)) - 1);
            Assert.Equal(expected, ReadWidth(packet, width));
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
            Assert.Throws<EndOfStreamException>(() => ReadWidth(packet, width));
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
        });
    }

    [Fact]
    public Task ExactBulkWritesReadsAndZeroFillUseTheirCompleteInitializedExtent()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var packet = new Packet(Packet.MAX_SIZE);
            byte[] initialized = Enumerable.Repeat((byte)0xCC, Packet.MAX_SIZE).ToArray();
            Marshal.Copy(initialized, 0, (IntPtr)packet.Memory, initialized.Length);
            packet.Size = initialized.Length;
            byte* source = stackalloc byte[4] { 65, 66, 0, 68 };
            byte* destination = stackalloc byte[4] { 0xEE, 0xEE, 0xEE, 0xEE };
            packet.Seek(Packet.MAX_SIZE - 4);
            packet.WriteUnsafe(source, 4);
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
            Assert.Equal(new byte[] { 65, 66, 0, 68 }, Bytes(packet, Packet.MAX_SIZE - 4, 4));
            packet.Seek(Packet.MAX_SIZE - 4);
            packet.ReadUnsafe(destination, 4);
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
            Assert.Equal(new byte[] { 65, 66, 0, 68 }, new ReadOnlySpan<byte>(destination, 4).ToArray());
            packet.Seek(Packet.MAX_SIZE - 4);
            Assert.Equal("AB", packet.ReadCString(4));
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
            packet.Seek(Packet.MAX_SIZE - 4);
            packet.ZeroFill(4);
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
            Assert.Equal(new byte[4], Bytes(packet, Packet.MAX_SIZE - 4, 4));

            packet.Seek(Packet.MAX_SIZE - 5);
            packet.WriteUnsafe(source, 4);
            Assert.Equal(Packet.MAX_SIZE - 1, packet.Position);
            packet.Seek(Packet.MAX_SIZE - 5);
            packet.ReadUnsafe(destination, 4);
            Assert.Equal(new byte[] { 65, 66, 0, 68 }, new ReadOnlySpan<byte>(destination, 4).ToArray());
            Assert.Equal(Packet.MAX_SIZE - 1, packet.Position);
            packet.Seek(Packet.MAX_SIZE - 5);
            Assert.Equal("AB", packet.ReadCString(4));
            Assert.Equal(Packet.MAX_SIZE - 1, packet.Position);
            packet.Seek(Packet.MAX_SIZE - 5);
            packet.ZeroFill(4);
            Assert.Equal(new byte[4], Bytes(packet, Packet.MAX_SIZE - 5, 4));
            Assert.Equal(Packet.MAX_SIZE - 1, packet.Position);
            Assert.Equal((byte)0, packet.Memory[Packet.MAX_SIZE - 1]);
        });
    }

    [Fact]
    public Task CStringAndBytesConsumeExactInputEndAndRefuseTruncationWithoutPrefixConsumption()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var packet = new Packet(Packet.MAX_SIZE);
            byte[] initialized = Enumerable.Repeat((byte)0xCC, Packet.MAX_SIZE).ToArray();
            initialized[^2] = 65; initialized[^1] = 66;
            Marshal.Copy(initialized, 0, (IntPtr)packet.Memory, initialized.Length);
            packet.Size = initialized.Length;
            packet.Seek(Packet.MAX_SIZE - 2);
            Assert.Equal("AB", packet.ReadCString(2));
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
            packet.Seek(Packet.MAX_SIZE - 2);
            Assert.Equal(new byte[] { 65, 66 }, packet.ReadBytes(2));
            Assert.Equal(Packet.MAX_SIZE, packet.Position);
            packet.Size = initialized.Length - 1;
            packet.Seek(Packet.MAX_SIZE - 2);
            Assert.Throws<EndOfStreamException>(() => packet.ReadCString(2));
            Assert.Equal(Packet.MAX_SIZE - 2, packet.Position);
            Assert.Throws<EndOfStreamException>(() => packet.ReadBytes(2));
            Assert.Equal(Packet.MAX_SIZE - 2, packet.Position);
            Assert.Equal(initialized.Length - 1, packet.Size);
            Assert.Equal(initialized, Bytes(packet, 0, initialized.Length));
            Assert.Equal((byte)66, packet.Memory[Packet.MAX_SIZE - 1]);
        });
    }

    [Theory]
    [InlineData(null, 6, "060034122211")]
    [InlineData("TQServer", 14, "0600341222115451536572766572")]
    public Task FinalizeWritesHeaderExcludingSealLengthAndLeavesCursorAtFour(
        string? seal, int size, string hex)
    {
        return NativePacketTestThread.Run(() =>
        {
            using var savedSeal = new SealScope(seal);
            using var packet = new Packet(Packet.MAX_SIZE);
            Assert.Equal(seal is null ? 0 : 8, Packet.SealSize);
            packet.InitWriter();
            packet.Write((ushort)0x1122);
            packet.Finalize(0x1234);
            Assert.Equal(size, packet.Size);
            Assert.Equal(4, packet.Position);
            Assert.Equal(Convert.FromHexString(hex), Bytes(packet, 0, size));
        });
    }

    [Fact]
    public Task SealSetterRequiresEightCharactersAndRejectsWithoutChangingPriorSeal()
    {
        return NativePacketTestThread.Run(() =>
        {
            using var savedSeal = new SealScope("TQServer");
            foreach (var rejected in new[] { "short", "123456789" })
            {
                var error = Assert.Throws<ArgumentOutOfRangeException>(() => Packet.SealString = rejected);
                Assert.Equal("value", error.ParamName);
                Assert.Equal("TQServer", Packet.SealString);
            }
            Packet.SealString = "é1234567"; // Eight chars, deliberately nine UTF-8 bytes.
            Assert.Equal("é1234567", Packet.SealString);
            Assert.Equal(8, Packet.SealSize);
        });
    }

    [Fact]
    public Task DisposeFreesOwnedMemoryOnceAndRepeatedDisposeKeepsMemoryNull()
    {
        return NativePacketTestThread.Run(() =>
        {
            var packet = new Packet(Packet.MAX_SIZE);
            Assert.NotEqual(IntPtr.Zero, (IntPtr)packet.Memory);
            packet.Dispose();
            Assert.Equal(IntPtr.Zero, (IntPtr)packet.Memory);
            packet.Dispose();
            Assert.Equal(IntPtr.Zero, (IntPtr)packet.Memory);
            // Never read Position, stream contents or any freed pointer after Dispose.
        });
    }

    [Fact]
    public Task ReuseOnlySeeksZeroAndTakeStillAllocatesAFreshWriterAtFour()
    {
        return NativePacketTestThread.Run(() =>
        {
            Assert.Equal(0, Pool.Count); // Native Reuse does not enqueue; no test pool injection.
            using var original = new Packet(Packet.MAX_SIZE);
            original.Seek(24);
            original.Size = 73;
            var address = (IntPtr)original.Memory;
            Pool.Reuse(original);
            Assert.Equal(0, original.Position);
            Assert.Equal(73, original.Size);
            Assert.Equal(address, (IntPtr)original.Memory);
            Assert.Equal(0, Pool.Count);
            using var taken = Pool.Take();
            Assert.NotSame(original, taken);
            Assert.NotEqual(address, (IntPtr)taken.Memory);
            Assert.Equal(4, taken.Position);
            Assert.Equal(0, taken.Size);
            Assert.Equal(0, Pool.Count);
        });
    }

    [Fact]
    public Task RecycledWrapperDisposeReusesButDoesNotFreeOrEnqueueTheRetainedPacket()
    {
        return NativePacketTestThread.Run(() =>
        {
            Assert.Equal(0, Pool.Count);
            var wrapper = new Recycled();
            var packet = wrapper.GetStream();
            try
            {
                Assert.Same(packet, wrapper.GetStream());
                Assert.Equal(4, packet.Position);
                packet.Write(0xAABBCCDDU);
                packet.Size = 88;
                wrapper.Dispose();
                Assert.Null(wrapper.GetStream());
                Assert.Equal(0, packet.Position);
                Assert.Equal(88, packet.Size);
                Assert.NotEqual(IntPtr.Zero, (IntPtr)packet.Memory);
                Assert.Equal(0, Pool.Count);
                wrapper.Dispose();
                Assert.Null(wrapper.GetStream());
                Assert.Equal(0, packet.Position);
            }
            finally
            {
                wrapper.Dispose();
                packet.Dispose(); // Native wrapper intentionally does not own this free.
            }
        });
    }

    private static byte[] Bytes(Packet packet, int offset, int count)
    {
        var bytes = new byte[count];
        Marshal.Copy((IntPtr)(packet.Memory + offset), bytes, 0, count);
        return bytes;
    }

    private static void WriteWidth(Packet packet, int width)
    {
        switch (width)
        {
            case 1: packet.Write((byte)0xA5); break;
            case 2: packet.Write((ushort)0xA5A5); break;
            case 4: packet.Write(0xA5A5A5A5U); break;
            case 8: packet.Write(0xA5A5A5A5A5A5A5A5UL); break;
        }
    }

    private static ulong ReadWidth(Packet packet, int width) => width switch
    {
        1 => packet.ReadUInt8(), 2 => packet.ReadUInt16(),
        4 => packet.ReadUInt32(), 8 => packet.ReadUInt64(),
        _ => throw new ArgumentOutOfRangeException(nameof(width)),
    };

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(Packet).GetField("seal",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;
        private readonly object? previous = Field.GetValue(null);

        internal SealScope(string? value)
        {
            if (value is null) Field.SetValue(null, null);
            else Packet.SealString = value;
        }

        public void Dispose() => Field.SetValue(null, previous); // Includes native initial null.
    }
}
