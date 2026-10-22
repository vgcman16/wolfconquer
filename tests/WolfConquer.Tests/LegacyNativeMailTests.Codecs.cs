using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using ListMessage = WolfConquer.Core.World.LegacyNativeCMsgMailList;
using Mode = WolfConquer.Core.World.LegacyNativeCMsgMailboxMode.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMailTests
{
    [Fact]
    public Task ListCodecResetsWriterUsesAllRequiredFieldsAndReturnsSamePacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row();
        var obj = new ListMessage { Type = 2, Count = 0, MailList = new[] { new ListMessage.Mail(row) } };
        Assert.Same(scope.Packet, scope.Packet.CreateMailList(obj));
        Assert.Equal(MailFrame(2, 0, row), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.Same(row.title, obj.MailList[0].Title); Assert.Equal(1ul, obj.MailList[0].Id);
        Assert.Same(scope.Packet, scope.Packet.CreateMailList(null!));
        Assert.Equal(Convert.FromHexString("04008708"), NativeSpellTestFixture.Bytes(scope.Packet));
    });

    [Fact]
    public Task MailboxModeFullPrimitiveFrameRoundTripsWidthsCurrentCursorAndOnlyRequestedItems() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); ulong[] items = { 0x0102030405060708, ulong.MaxValue, 123 };
        Assert.Same(scope.Packet, scope.Packet.CreateMailboxMode(Mode.ClaimAll, 0x12345678, 2, items));
        byte[] literal = Convert.FromHexString("1E00D808070078563412020000000807060504030201FFFFFFFFFFFFFFFF");
        Assert.Equal(literal, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(4, scope.Packet.Position);
        scope.Packet.GetMailboxMode(out var mode, out var id, out var count, out var decoded);
        Assert.Equal(Mode.ClaimAll, mode); Assert.Equal(0x12345678u, id); Assert.Equal(2u, count);
        Assert.Equal(items.Take(2), decoded); Assert.Equal(30, scope.Packet.Position);
        Assert.Equal(items, new ulong[] { 0x0102030405060708, ulong.MaxValue, 123 });
    });

    [Fact]
    public Task ModeReaderNarrowsSixteenBitsToNativeByteEnumAndReadsOwnedBytesBeyondLogicalSize() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] bytes = Convert.FromHexString("0000FFFF070178563412010000000807060504030201");
        scope.Input(bytes); scope.Packet.Size = 5; // Buffer remains fully owned/initialized.
        scope.Packet.GetMailboxMode(out var mode, out var id, out var count, out var items);
        Assert.Equal(Mode.ClaimAll, mode); Assert.Equal(0x12345678u, id); Assert.Equal(1u, count);
        Assert.Equal(new[] { 0x0102030405060708ul }, items); Assert.Equal(22, scope.Packet.Position);
        Assert.Equal(5, scope.Packet.Size);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ZeroCountDoesNotDereferenceNullOrExtraItems(bool nullItems) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Packet.CreateMailboxMode((Mode)255, uint.MaxValue, 0, nullItems ? null! : new[] { ulong.MaxValue });
        Assert.Equal(Convert.FromHexString("0E00D808FF00FFFFFFFF00000000"), NativeSpellTestFixture.Bytes(scope.Packet));
        scope.Packet.GetMailboxMode(out var type, out var id, out var count, out var items);
        Assert.Equal((Mode)255, type); Assert.Equal(uint.MaxValue, id); Assert.Equal(0u, count); Assert.Empty(items);
        Assert.Equal(14, scope.Packet.Position);
    });

    [Theory]
    [InlineData(false, 22)]
    [InlineData(true, 14)]
    public Task ShortOrNullItemArrayFaultLeavesWrittenPrefixAndOldSizeWithoutFinalize(bool nullItems, int position) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (nullItems)
            Assert.Throws<NullReferenceException>(() => scope.Packet.CreateMailboxMode(Mode.Delete, 7, 2, null!));
        else
            Assert.Throws<IndexOutOfRangeException>(() => scope.Packet.CreateMailboxMode(Mode.Delete, 7, 2, new[] { 9ul }));
        Assert.Equal(position, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        byte[] prefix = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.Equal(Convert.FromHexString("A5A5A5A502000700000002000000"), prefix.Take(14));
        if (!nullItems) Assert.Equal(9ul, BinaryPrimitives.ReadUInt64LittleEndian(prefix.AsSpan(14)));
        Assert.Equal((byte)0xA5, prefix[position]); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ReaderBoundaryGuardReturnsZerosWithoutAdvancingAndNullCodecHasNoFallback() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.Seek(16383);
        scope.Packet.GetMailboxMode(out var type, out var id, out var count, out var items);
        Assert.Equal((Mode)0, type); Assert.Equal(0u, id); Assert.Equal(0u, count); Assert.Empty(items);
        Assert.Equal(16383, scope.Packet.Position);
        Assert.Throws<NullReferenceException>(() => LegacyNativeMail.CreateMailList(null!, null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeMail.CreateMailboxMode(null!, Mode.Delete, 0, 0, null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeMail.GetMailboxMode(null!, out _, out _, out _, out _));
    });
}
