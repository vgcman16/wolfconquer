using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using ListMessage = WolfConquer.Core.World.LegacyNativeCMsgMailList;
using Mode = WolfConquer.Core.World.LegacyNativeCMsgMailboxMode.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMailTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ModeFrameFitsExactRealCapacityAndOneByteLessRefusesWithoutWrites(bool sealedFrame) => NativePacketTestThread.Run(() =>
    {
        using var seal = new NativeStringPacketTestScope(sealedFrame);
        byte[] expected = Convert.FromHexString("1E00D808070078563412020000000807060504030201FFFFFFFFFFFFFFFF" +
            (sealedFrame ? "5451536572766572" : ""));
        ulong[] items = [0x0102030405060708ul, ulong.MaxValue, 123];
        using var exact = new LegacyNativePacket(expected.Length); InitializeMailTarget(exact);
        Assert.Same(exact, exact.CreateMailboxMode(Mode.ClaimAll, 0x12345678, 2, items));
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(exact));
        Assert.Equal(expected.Length, exact.Size); Assert.Equal(4, exact.Position);
        Assert.Equal(new[] { 0x0102030405060708ul, ulong.MaxValue, 123ul }, items);
        using var shortPacket = new LegacyNativePacket(expected.Length - 1); InitializeMailTarget(shortPacket);
        AssertMailWriteRefuses<InvalidOperationException>(shortPacket,
            () => shortPacket.CreateMailboxMode(Mode.ClaimAll, 0x12345678, 2, items));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ListMissingRepeatedArrayAndEmptyArrayRetainRequiredScalarWireFields(bool missingArray) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        ListMessage.Mail[] rows = missingArray ? null! : [];
        var obj = new ListMessage { Type = -1, Count = 17, MailList = rows };
        scope.Packet.CreateMailList(obj);
        Assert.Equal(MailFrame(-1, 17), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Same(rows, obj.MailList); Assert.Equal(-1, obj.Type); Assert.Equal(17, obj.Count);
        Assert.Equal(4, scope.Packet.Position); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ListNullLaterRowAndOversizedTextPreserveTheExistingWholeTarget() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = new ListMessage.Mail(Row()) { Title = "before", Money = ulong.MaxValue };
        var rows = new[] { row, null! }; var obj = new ListMessage { Type = 2, Count = 99, MailList = rows };
        AssertMailWriteRefuses<ArgumentException>(scope.Packet, () => scope.Packet.CreateMailList(obj));
        Assert.Same(rows, obj.MailList); Assert.Same(row, rows[0]); Assert.Null(rows[1]);
        Assert.Equal("before", row.Title); Assert.Equal(ulong.MaxValue, row.Money);
        using var small = new LegacyNativePacket(128); InitializeMailTarget(small);
        row.Title = new string('雪', 1000); obj.MailList = [row];
        AssertMailWriteRefuses<InvalidOperationException>(small, () => small.CreateMailList(obj));
        Assert.Equal(1000, row.Title.Length); Assert.Equal(99, obj.Count); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task MailDtoCopyRetainsAllFullWidthFieldsAndSeparatesLaterMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var original = new ListMessage.Mail(Row())
        {
            Id = ulong.MaxValue, Money = ulong.MaxValue - 1, EMoney = ulong.MaxValue - 2,
            Expiration_Date = ulong.MaxValue - 3, Itemid = ulong.MaxValue - 4,
            Emoney_Record_Type = ulong.MaxValue - 5, Flag = ulong.MaxValue - 6,
            Title = "雪", Sender_Name = "sender"
        };
        var copy = new ListMessage.Mail(original);
        original.Id = 1; original.Money = 2; original.Title = "changed"; original.Sender_Name = "other";
        Assert.NotSame(original, copy); Assert.Equal(ulong.MaxValue, copy.Id); Assert.Equal(ulong.MaxValue - 1, copy.Money);
        Assert.Equal(ulong.MaxValue - 2, copy.EMoney); Assert.Equal(ulong.MaxValue - 3, copy.Expiration_Date);
        Assert.Equal(ulong.MaxValue - 4, copy.Itemid); Assert.Equal(ulong.MaxValue - 5, copy.Emoney_Record_Type);
        Assert.Equal(ulong.MaxValue - 6, copy.Flag); Assert.Equal("雪", copy.Title); Assert.Equal("sender", copy.Sender_Name);
        Assert.Throws<ArgumentNullException>(() => new ListMessage.Mail((ListMessage.Mail)null!)); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task DisposedMailBuildersRejectBeforeAnyFramePublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var obj = new ListMessage { Type = 2, Count = 0, MailList = [] };
        scope.Packet.Dispose();
        Assert.Throws<ObjectDisposedException>(() => scope.Packet.CreateMailList(obj));
        Assert.Throws<ObjectDisposedException>(() => scope.Packet.CreateMailboxMode(Mode.Delete, 7, 0, null!));
        Assert.Equal(321, scope.Packet.Size); Assert.Empty(scope.Queue);
    });

    private static unsafe void InitializeMailTarget(LegacyNativePacket packet)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray();
        Marshal.Copy(bytes, 0, (nint)packet.Memory, bytes.Length);
        packet.Size = 3; packet.Seek(2);
    }

    private static void AssertMailWriteRefuses<T>(LegacyNativePacket packet, System.Action action) where T : Exception
    {
        byte[] before = MailCodecBytes(packet); int position = packet.Position, size = packet.Size;
        Assert.Throws<T>(action);
        Assert.Equal(before, MailCodecBytes(packet)); Assert.Equal(position, packet.Position); Assert.Equal(size, packet.Size);
    }
}
