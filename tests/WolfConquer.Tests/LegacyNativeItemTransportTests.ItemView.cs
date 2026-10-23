using System.Buffers.Binary;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemTransportTests
{
    [Fact]
    public Task WholeItemViewWritesTheLiteralNativeLayoutCostActivationDuplicatePurificationAndRelics() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = ViewItem(); var relics = item.RelicAttributes;
        Assert.Same(scope.Packet, scope.Packet.ItemViewCreate(0x88776655, -1234567890123L, item, LegacyNativeMsgItemView.ActionMode.ViewEquip));
        Assert.Equal(ViewBytes(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(212, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position); Assert.Empty(scope.Queue);
        Assert.Same(relics, item.RelicAttributes); Assert.Equal((ushort)0x1234, item.Position); Assert.Equal(2u, item.Activate);
    });

    [Fact]
    public Task ActivePositiveRemainingTimeOmitsOnlyTheNativeConditionalActivateWord() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = ViewItem(); item.Activate = 1; item.EndDate = DateTime.Now.AddHours(1);
        DateTime before = DateTime.Now;
        scope.Packet.ItemViewCreate(0x88776655, -1234567890123L, item, LegacyNativeMsgItemView.ActionMode.ViewEquip);
        DateTime after = DateTime.Now;
        byte[] actual = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(208, actual.Length); Assert.Equal((ushort)208, BinaryPrimitives.ReadUInt16LittleEndian(actual));
        uint seconds = BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(58));
        // Independent positive finite time bounds; no claim about the existing
        // RemainingTime getter's negative/overflow conversion domain.
        uint minimum = (uint)Math.Floor((item.EndDate - after).TotalSeconds);
        uint maximum = (uint)Math.Floor((item.EndDate - before).TotalSeconds);
        Assert.InRange(seconds, minimum, maximum);
        byte[] inactive = ViewBytes(); byte[] expected = new byte[208];
        Array.Copy(inactive, 0, expected, 0, 58); Array.Copy(inactive, 62, expected, 58, inactive.Length - 62);
        U16(expected, 0, 208); U32(expected, 58, seconds);
        Assert.Equal(expected, actual); Assert.Equal(1u, item.Activate);
    });

    [Theory]
    [InlineData(0, 204)]
    [InlineData(2, 212)]
    [InlineData(5, 224)]
    public Task WholeItemViewUsesTheActualMutableRelicArrayLengthWithoutInventingAFiveEntryClamp(int count, int size) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = ViewItem(); item.RelicAttributes = new LegacyNativeRelicAttribute[count];
        scope.Packet.ItemViewCreate(1, 0, item, (LegacyNativeMsgItemView.ActionMode)ushort.MaxValue);
        Assert.Equal(size, scope.Packet.Size); Assert.Equal((ushort)size, BitConverter.ToUInt16(NativeSpellTestFixture.Bytes(scope.Packet), 0));
        Assert.Equal(ushort.MaxValue, BitConverter.ToUInt16(NativeSpellTestFixture.Bytes(scope.Packet), 28));
        Assert.Equal(count, item.RelicAttributes.Length);
    });

    [Fact]
    public Task WholeItemViewKeepsNativeFixedStringTruncationAndRealSeal() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = ViewItem(); item.OwnerName = "狼X"; item.Signature = new string('S', 70);
        LegacyNativePacket.SealString = "TQServer";
        scope.Packet.ItemViewCreate(0x88776655, -1234567890123L, item, LegacyNativeMsgItemView.ActionMode.ViewEquip);
        byte[] expected = ViewBytes().Concat(new byte[] { 84,81,83,101,114,118,101,114 }).ToArray();
        Array.Clear(expected, 88, 32); expected[88] = 0xE7; expected[89] = 0x8B;
        Array.Fill(expected, (byte)'S', 120, 64);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(220, scope.Packet.Size);
    });

    [Fact]
    public Task NullItemViewFaultOccursAfterInitWriterWithoutChangingSizeOrSending() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<NullReferenceException>(() => scope.Packet.ItemViewCreate(1, 0, null!, LegacyNativeMsgItemView.ActionMode.Gold));
        Assert.Equal(4, scope.Packet.Position); Assert.Equal(333, scope.Packet.Size); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(true, 88)]
    [InlineData(false, 120)]
    public Task NullFixedStringFaultKeepsTheWholeNativePrefixAndDoesNotFinalize(bool ownerName, int position) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = ViewItem();
        if (ownerName) item.OwnerName = null!; else item.Signature = null!;
        Assert.Throws<NullReferenceException>(() => scope.Packet.ItemViewCreate(1, 0, item, LegacyNativeMsgItemView.ActionMode.CPs));
        Assert.Equal(position, scope.Packet.Position); Assert.Equal(333, scope.Packet.Size); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullRelicArrayFaultComesAfterRuneExpWithoutWritingTailOrFinalizing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = ViewItem(); item.RelicAttributes = null!;
        Assert.Throws<NullReferenceException>(() => scope.Packet.ItemViewCreate(1, 0, item, LegacyNativeMsgItemView.ActionMode.Rune));
        Assert.Equal(188, scope.Packet.Position); Assert.Equal(333, scope.Packet.Size);
        Assert.Equal(1000u, BitConverter.ToUInt32(NativeSpellTestFixture.Bytes(scope.Packet), 184));
        Assert.Equal(0xA5A5A5A5u, BitConverter.ToUInt32(NativeSpellTestFixture.Bytes(scope.Packet), 188));
    });
}
