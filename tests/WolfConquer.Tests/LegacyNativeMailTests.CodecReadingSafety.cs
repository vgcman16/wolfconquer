using System.Buffers.Binary;
using WolfConquer.Core.World;
using Mode = WolfConquer.Core.World.LegacyNativeCMsgMailboxMode.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMailTests
{
    // Corrected-only malformed cases; never run against the old unmanaged codec.
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(5)] [InlineData(6)] [InlineData(9)]
    public Task ModeTruncatedPrefixPreservesEveryOriginalOutputAndTheWholeCursor(int length) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] complete = ModeInput(0, []);
        scope.Input(complete[..(4 + length)]);
        AssertMailReadRefuses(scope);
    });

    [Theory]
    [InlineData(1u, 7)] [InlineData(2u, 8)] [InlineData(uint.MaxValue, 0)]
    public Task ModeCountMustFitTheCompleteCapturedItemWindowBeforeAllocation(uint count, int suppliedItemBytes) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] frame = ModeInput(count, Enumerable.Repeat((byte)0xA5, suppliedItemBytes).ToArray());
        scope.Input(frame); AssertMailReadRefuses(scope);
    });

    [Fact]
    public Task ModeReadsOnlyRequestedItemsAndLeavesCapturedTrailingBytesAvailable() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        byte[] body = Convert.FromHexString("08070605040302011817161514131211");
        byte[] frame = ModeInput(1, body); scope.Input(frame);
        scope.Packet.GetMailboxMode(out var type, out var id, out var count, out var items);
        Assert.Equal(Mode.ClaimAll, type); Assert.Equal(0x12345678u, id); Assert.Equal(1u, count);
        Assert.Equal(new[] { 0x0102030405060708ul }, items); Assert.Equal(22, scope.Packet.Position);
        Assert.Equal(0x1112131415161718ul, scope.Packet.ReadUInt64());
        Assert.Equal(frame.Length, scope.Packet.Position); Assert.Equal(frame.Length, scope.Packet.Size);
        Assert.Equal(frame, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task DisposedMailReaderPreservesCallerOutputsWithoutTouchingFreedStorage() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Input(ModeInput(0, [])); scope.Packet.Dispose();
        Mode mode = (Mode)77; uint id = 88, count = 99; ulong[] old = [111]; var items = old;
        Assert.Throws<ObjectDisposedException>(() => scope.Packet.GetMailboxMode(out mode, out id, out count, out items));
        Assert.Equal((Mode)77, mode); Assert.Equal(88u, id); Assert.Equal(99u, count); Assert.Same(old, items);
        Assert.Equal(new[] { 111ul }, items); Assert.Equal(14, scope.Packet.Size); Assert.Empty(scope.Queue);
    });

    private static byte[] ModeInput(uint count, byte[] itemBytes)
    {
        var input = new byte[14 + itemBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(input, (ushort)input.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(2), 2264);
        BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(4), 7);
        BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan(6), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan(10), count);
        itemBytes.CopyTo(input, 14);
        return input;
    }

    private static void AssertMailReadRefuses(Scope scope)
    {
        byte[] before = MailCodecBytes(scope.Packet);
        int position = scope.Packet.Position, size = scope.Packet.Size;
        Mode type = (Mode)77; uint id = 88, count = 99; ulong[] old = [111]; var items = old;
        Assert.Throws<EndOfStreamException>(() => scope.Packet.GetMailboxMode(out type, out id, out count, out items));
        Assert.Equal((Mode)77, type); Assert.Equal(88u, id); Assert.Equal(99u, count); Assert.Same(old, items);
        Assert.Equal(new[] { 111ul }, items); Assert.Equal(position, scope.Packet.Position); Assert.Equal(size, scope.Packet.Size);
        Assert.Equal(before, MailCodecBytes(scope.Packet)); Assert.Empty(scope.Queue);
    }
}
