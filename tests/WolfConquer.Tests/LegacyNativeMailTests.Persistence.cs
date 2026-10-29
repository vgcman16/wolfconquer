using System.Buffers.Binary;
using WolfConquer.Core.World;
using Mail = WolfConquer.Core.World.LegacyNativeMailList;
using Record = WolfConquer.Core.World.LegacyNativeTMailList;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMailTests
{
    [Fact]
    public Task MissingInputIsNoOpAndSaveDoesNotCreateMissingPlayersDirectory() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); string root = scope.OwnFiles(mailDirectory: false);
        var retained = Row(7); scope.Client.MailList.Add(7, retained);
        Mail.LoadMailList(scope.Client);
        Mail.SaveMailList(scope.Client);
        Assert.Same(retained, scope.Client.MailList[7]);
        Assert.False(Directory.Exists(Path.Combine(root, "PlayersMailList")));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task EmptySaveTruncatesExistingOwnedFileToNativeSignedCountAndLoadDoesNotClear() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); string path = scope.FileName;
        File.WriteAllBytes(path, Enumerable.Repeat((byte)0xCC, 1200).ToArray());
        Mail.SaveMailList(scope.Client);
        Assert.Equal(new byte[4], File.ReadAllBytes(path));
        var retained = Row(7); scope.Client.MailList.Add(7, retained);
        Mail.LoadMailList(scope.Client);
        Assert.Same(retained, scope.Client.MailList[7]); Assert.Empty(scope.Queue);
        using var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(4, reopened.Length); // Both complete native methods closed their local handle.
    });

    [Fact]
    public Task SaveUsesLivePhysicalRowsAndOneReusedRecordSoShortAsciiTailsPersistOnDisk() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = Row(41);
        first.title = "TITLE"; first.Sender_name = "SENDER"; first.content = "CONTENT";
        var second = Row(42); second.title = "x"; second.Sender_name = "y"; second.content = "z";
        scope.Client.MailList.Add(900, first); scope.Client.MailList.Add(901, second);
        var stale = scope.Client.MailList.GetValues();
        var replacement = Row(43); replacement.title = "A"; replacement.Sender_name = "B"; replacement.content = "D";
        scope.Client.MailList[901] = replacement;
        Assert.Same(second, stale[1]); Assert.True(scope.Client.MailList.Update);
        long revision = scope.Client.MailList.Revision;
        string path = scope.FileName;
        Mail.SaveMailList(scope.Client);
        Record record = default;
        byte[] one = RecordBytes(record.GetMailList(first));
        byte[] two = RecordBytes(record.GetMailList(replacement));
        Assert.Equal(FileBytes(2, one, two), File.ReadAllBytes(path));
        Assert.Equal("AITLE", record.title); Assert.Equal("BENDER", record.Sender_name); Assert.Equal("DONTENT", record.content);
        Assert.Equal(43u, BinaryPrimitives.ReadUInt32LittleEndian(two.AsSpan(704)));
        Assert.Equal(revision, scope.Client.MailList.Revision); Assert.True(scope.Client.MailList.Update);
        Assert.Equal(new[] { first, replacement }, scope.Client.MailList.GetValues()); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task RealSaveThenFreshLoadRekeysByPhysicalIdOverwritesDuplicatesRetainsOtherRowsAndCanRepeat() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = Row(9); first.Money = 77;
        var second = Row(9); second.Money = 88;
        var expired = Row(10); expired.TimeStamp = DateTime.Now.AddDays(-1);
        scope.Client.MailList.Add(100, first); scope.Client.MailList.Add(101, second); scope.Client.MailList.Add(102, expired);
        scope.OwnFiles(); Mail.SaveMailList(scope.Client);
        // Fail before loading if a runtime FileStream/handle mismatch corrupted the count/header.
        Record record = default;
        byte[] one = RecordBytes(record.GetMailList(first));
        byte[] two = RecordBytes(record.GetMailList(second));
        byte[] three = RecordBytes(record.GetMailList(expired));
        Assert.Equal(FileBytes(3, one, two, three), File.ReadAllBytes(scope.FileName));
        var queue = new Queue<byte[]>(); var fresh = scope.Other(queue); var retained = Row(77);
        fresh.MailList.Add(77, retained);
        Mail.LoadMailList(fresh);
        Assert.Equal(2, fresh.MailList.Count); Assert.Same(retained, fresh.MailList[77]);
        Assert.False(fresh.MailList.ContainsKey(100)); Assert.False(fresh.MailList.ContainsKey(10));
        Assert.Equal(88ul, fresh.MailList[9].Money); Assert.NotSame(second, fresh.MailList[9]);
        Assert.Equal(second.TimeStamp.Ticks, fresh.MailList[9].TimeStamp.Ticks);
        var loaded = fresh.MailList[9]; Mail.LoadMailList(fresh);
        Assert.NotSame(loaded, fresh.MailList[9]); Assert.Equal(88ul, fresh.MailList[9].Money);
        Assert.Equal(2, fresh.MailList.Count); Assert.Empty(queue); Assert.Empty(scope.Queue);
        Assert.True(fresh.MailList.Update);
        using var reopened = new FileStream(scope.FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(4 + 3 * 744, reopened.Length);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public Task EmptyPartialOrNegativeCountFilesFinishAndCloseWithoutMutatingMail(int length) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); string path = scope.FileName;
        // Short zero bytes leave the native zero-initialized count zero; signed -1 skips the loop.
        File.WriteAllBytes(path, length == 4 ? Convert.FromHexString("FFFFFFFF") : new byte[length]);
        var retained = Row(8); scope.Client.MailList.Add(8, retained);
        Mail.LoadMailList(scope.Client); Assert.Same(retained, scope.Client.MailList[8]);
        Assert.Single(scope.Client.MailList); Assert.Empty(scope.Queue);
        using var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(length, reopened.Length);
    });

    [Fact]
    public Task ShortRecordWithZeroTimerIsSafelySkippedAndNativeMethodClosesFile() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); string path = scope.FileName;
        File.WriteAllBytes(path, FileBytes(1, new byte[704]));
        Mail.LoadMailList(scope.Client); Assert.Empty(scope.Client.MailList); Assert.Empty(scope.Queue);
        using var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(708, reopened.Length);
    });

    [Fact]
    public Task ShortSecondRecordReusesFirstRecordTailIncludingFutureTimestampAndOverwritesSameId() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); string path = scope.FileName; var first = Row(9);
        first.title = "TITLE"; first.Sender_name = "SENDER"; first.content = "CONTENT"; first.Money = 77;
        byte[] whole = RecordBytes(Stored(first));
        File.WriteAllBytes(path, FileBytes(2, whole, new[] { (byte)'x' }));
        Mail.LoadMailList(scope.Client);
        var loaded = Assert.Single(scope.Client.MailList).Value;
        Assert.Equal(9u, loaded.id); Assert.Equal("xITLE", loaded.title); Assert.Equal("SENDER", loaded.Sender_name);
        Assert.Equal("CONTENT", loaded.content); Assert.Equal(77ul, loaded.Money);
        Assert.Equal(first.TimeStamp.Ticks, loaded.TimeStamp.Ticks); Assert.Empty(scope.Queue);
        using var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(749, reopened.Length);
    });
}
