using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTransportPrerequisiteTests
{
    [Theory]
    [InlineData(1, 1, 1, 10101)]
    [InlineData(2024, 2, 29, 20240229)]
    [InlineData(9999, 12, 31, 99991231)]
    public void NativeTimerRoundTripsDateComponents(int year, int month, int day, int encoded)
    {
        var date = new DateTime(year, month, day, 23, 59, 58, DateTimeKind.Utc);
        Assert.Equal(encoded, LegacyNativeRoleCore.CreateTimer(year, month, day));
        Assert.Equal(encoded, LegacyNativeRoleCore.CreateTimer(date));
        var result = LegacyNativeRoleCore.GetTimer(encoded);
        Assert.Equal(date.Date, result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(20230229)]
    [InlineData(20261301)]
    [InlineData(20261000)]
    [InlineData(int.MaxValue)]
    public void NativeTimerDecoderDoesNotRepairInvalidComponents(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNativeRoleCore.GetTimer(value));

    [Fact]
    public void TimerEncoderIsUncheckedArithmeticRatherThanDateValidation()
    {
        Assert.Equal(20261399, LegacyNativeRoleCore.CreateTimer(2026, 13, 99));
        int year = int.MaxValue;
        Assert.Equal(unchecked(year * 10000 + year * 100 + year),
            LegacyNativeRoleCore.CreateTimer(year, year, year));
    }

    [Theory]
    [InlineData((byte)0, (byte)0)]
    [InlineData((byte)1, (byte)255)]
    [InlineData((byte)2, (byte)7)]
    [InlineData((byte)255, (byte)129)]
    public Task ItemLockCodecPreservesLiteralFieldsUnknownTypesAndNativeCursor(byte type, byte mode) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new NativeSpellTestFixture();
            Assert.Same(scope.Packet, scope.Packet.ItemLockCreate(0x01020304,
                (LegacyNativeItemLockPacket.TypeLock)type, mode, 0xA1A2A3A4));
            byte[] expected = Convert.FromHexString("100013090403020100000000A4A3A2A1");
            expected[8] = type;
            expected[9] = mode;
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            scope.Packet.GetItemLock(out var uid, out var actualType, out var actualMode, out var parameter);
            Assert.Equal(0x01020304u, uid);
            Assert.Equal(type, (byte)actualType);
            Assert.Equal(mode, actualMode);
            Assert.Equal(0xA1A2A3A4u, parameter);
            Assert.Equal(16, scope.Packet.Position);
        });

    [Fact]
    public Task ItemLockReaderUsesCurrentCursorAndIgnoresReservedWordAndHeader() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new NativeSpellTestFixture();
            var bytes = Enumerable.Repeat((byte)0x5A, 128).ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(17), uint.MaxValue);
            bytes[21] = 254;
            bytes[22] = 253;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(23), 0xABCD);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(25), 0x10203040);
            unsafe { Marshal.Copy(bytes, 0, (IntPtr)scope.Packet.Memory, bytes.Length); }
            scope.Packet.Size = bytes.Length;
            scope.Packet.Seek(17);
            scope.Packet.GetItemLock(out var uid, out var type, out var mode, out var parameter);
            Assert.Equal(uint.MaxValue, uid);
            Assert.Equal((byte)254, (byte)type);
            Assert.Equal((byte)253, mode);
            Assert.Equal(0x10203040u, parameter);
            Assert.Equal(29, scope.Packet.Position);
            Assert.Equal(128, scope.Packet.Size);
        });
}
