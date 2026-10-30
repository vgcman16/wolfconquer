using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
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
        byte[] before = MailCodecBytes(scope.Packet);
        int size = scope.Packet.Size, position = scope.Packet.Position;
        Assert.Throws<ArgumentNullException>(() => scope.Packet.CreateMailList(null!));
        Assert.Equal(before, MailCodecBytes(scope.Packet));
        Assert.Equal(size, scope.Packet.Size); Assert.Equal(position, scope.Packet.Position);
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
    public Task ModeReaderNarrowsSixteenBitsToNativeByteEnumWithinTheActualCopiedInput() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] bytes = Convert.FromHexString("0000FFFF070178563412010000000807060504030201");
        scope.Input(bytes);
        scope.Packet.GetMailboxMode(out var mode, out var id, out var count, out var items);
        Assert.Equal(Mode.ClaimAll, mode); Assert.Equal(0x12345678u, id); Assert.Equal(1u, count);
        Assert.Equal(new[] { 0x0102030405060708ul }, items); Assert.Equal(22, scope.Packet.Position);
        Assert.Equal(bytes.Length, scope.Packet.Size);
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
    [InlineData(false)]
    [InlineData(true)]
    public Task ShortOrRequiredNullItemArrayRefusesBeforeAnyTargetMutation(bool nullItems) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] before = MailCodecBytes(scope.Packet);
        int position = scope.Packet.Position, size = scope.Packet.Size;
        ulong[] items = nullItems ? null! : [9ul];
        if (nullItems)
            Assert.Throws<ArgumentNullException>(() => scope.Packet.CreateMailboxMode(Mode.Delete, 7, 2, items));
        else
            Assert.Throws<ArgumentOutOfRangeException>(() => scope.Packet.CreateMailboxMode(Mode.Delete, 7, 2, items));
        Assert.Equal(position, scope.Packet.Position); Assert.Equal(size, scope.Packet.Size);
        Assert.Equal(before, MailCodecBytes(scope.Packet));
        if (!nullItems) Assert.Equal(new[] { 9ul }, items);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ReaderBoundaryAndNullCodecsPreserveOriginalOutputsAndTargetState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.Seek(16383);
        byte[] before = MailCodecBytes(scope.Packet);
        Mode type = (Mode)77; uint id = 88, count = 99; ulong[] previous = [111]; var items = previous;
        Assert.Throws<EndOfStreamException>(() => scope.Packet.GetMailboxMode(out type, out id, out count, out items));
        Assert.Equal((Mode)77, type); Assert.Equal(88u, id); Assert.Equal(99u, count); Assert.Same(previous, items);
        Assert.Equal(16383, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(before, MailCodecBytes(scope.Packet));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeMail.CreateMailList(null!, null!));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeMail.CreateMailboxMode(null!, Mode.Delete, 0, 0, null!));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeMail.GetMailboxMode(null!, out type, out id, out count, out items));
        Assert.Equal((Mode)77, type); Assert.Equal(88u, id); Assert.Equal(99u, count); Assert.Same(previous, items);
        Assert.Empty(scope.Queue);
    });

    private static unsafe byte[] MailCodecBytes(LegacyNativePacket packet)
    {
        var result = new byte[packet.Capacity];
        Marshal.Copy((IntPtr)packet.Memory, result, 0, result.Length);
        return result;
    }
}
