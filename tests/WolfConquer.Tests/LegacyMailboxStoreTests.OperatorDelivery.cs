using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxStoreTests
{
    [Fact]
    public void OperatorProjectionPreservesRawRowsAndDoesNotWriteOrPublish()
    {
        const uint uid = 8111;
        var store = CreateStore();
        store.Add(uid, CreateEntry(1, itemId: 301));
        store.Save(uid);
        var path = store.GetMailboxPath(uid);
        var before = File.ReadAllBytes(path);
        lock (store.SyncRoot)
        {
            Assert.True(store.TryCaptureOperatorDelivery(uid, before, out var capture, out _));
            Assert.True(capture.TryAllocateMailIds(2, out var ids));
            Assert.Equal(new uint[] { 2, 3 }, ids);
            Assert.True(capture.TryPrepare(new[] { CreateEntry(ids[0], itemId: 302), CreateEntry(ids[1], itemId: 303) }, out var projection, out _));
            var after = projection.CopyAfterImage();
            Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(after));
            Assert.Equal(before.AsSpan(4).ToArray(), after.AsSpan(4, before.Length - 4).ToArray());
            Assert.Equal(4 + 3 * 740, after.Length);
            after[4] ^= 1;
            Assert.NotEqual(after[4], projection.CopyAfterImage()[4]);
            Assert.True(capture.MatchesBeforeImage(before));
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.True(capture.IsCurrent());
        }
        Assert.Equal(1U, Assert.Single(store.List(uid)).Id);
    }

    [Fact]
    public void OperatorCaptureDistinguishesAbsentFromEmptyAndDoesNotCreateFile()
    {
        const uint uid = 8112;
        var store = CreateStore();
        var path = store.GetMailboxPath(uid);
        lock (store.SyncRoot)
        {
            Assert.True(store.TryCaptureOperatorDelivery(uid, null, out var capture, out _));
            Assert.Null(capture.CopyBeforeImage());
            Assert.True(capture.MatchesBeforeImage(null));
            Assert.False(capture.MatchesBeforeImage(new byte[4]));
            Assert.True(capture.TryPrepare(new[] { CreateEntry(1, itemId: 1) }, out var projection, out _));
            Assert.Equal(744, projection.CopyAfterImage().Length);
            Assert.False(File.Exists(path));
            Assert.True(capture.IsCurrent());
        }
    }

    [Fact]
    public void OperatorProjectionRejectsDuplicateIdsAttachmentsAndInvalidCounts()
    {
        const uint uid = 8113;
        var store = CreateStore();
        store.Add(uid, CreateEntry(4, itemId: 900));
        store.Save(uid);
        var bytes = File.ReadAllBytes(store.GetMailboxPath(uid));
        lock (store.SyncRoot)
        {
            Assert.True(store.TryCaptureOperatorDelivery(uid, bytes, out var capture, out _));
            Assert.False(capture.TryAllocateMailIds(0, out _));
            Assert.False(capture.TryAllocateMailIds(33, out _));
            Assert.False(capture.TryPrepare(Array.Empty<LegacyMailboxEntry>(), out _, out _));
            Assert.False(capture.TryPrepare(Enumerable.Range(0, 33).Select(i => CreateEntry((uint)(100 + i), itemId: (uint)(2000 + i))).ToArray(), out _, out _));
            Assert.False(capture.TryPrepare(new[] { CreateEntry(4, itemId: 901) }, out _, out _));
            Assert.False(capture.TryPrepare(new[] { CreateEntry(5, itemId: 900) }, out _, out _));
            Assert.False(capture.TryPrepare(new[] { CreateEntry(5, itemId: 901), CreateEntry(6, itemId: 901) }, out _, out _));
            Assert.False(capture.TryPrepare(new[] { CreateEntry(0, itemId: 901) }, out _, out _));
            Assert.True(capture.TryPrepare(new[] { CreateEntry(uint.MaxValue, itemId: 901) }, out _, out _));
            Assert.True(capture.IsCurrent());
        }
        Assert.Equal(bytes, File.ReadAllBytes(store.GetMailboxPath(uid)));
    }

    [Theory]
    [InlineData("nonascii")]
    [InlineData("nul")]
    [InlineData("long")]
    [InlineData("surrogate")]
    public void OperatorProjectionRejectsLossyOrTruncatedText(string kind)
    {
        var store = CreateStore();
        var title = kind switch { "nonascii" => "狼", "nul" => "A\0B", "long" => new string('x', 65), _ => "\ud800" };
        lock (store.SyncRoot)
        {
            Assert.True(store.TryCaptureOperatorDelivery(8114, null, out var capture, out _));
            Assert.False(capture.TryPrepare(new[] { CreateEntry(1, title: title, itemId: 4) }, out _, out _));
            Assert.True(capture.IsCurrent());
        }
        Assert.False(File.Exists(store.GetMailboxPath(8114)));
    }

    [Fact]
    public void OperatorProjectionRejectsStaleCacheAndMalformedImage()
    {
        const uint uid = 8115;
        var store = CreateStore();
        store.Add(uid, CreateEntry(1, itemId: 99));
        store.Save(uid);
        var image = File.ReadAllBytes(store.GetMailboxPath(uid));
        lock (store.SyncRoot)
        {
            Assert.False(store.TryCaptureOperatorDelivery(uid, image[..^1], out _, out _));
            Assert.True(store.TryCaptureOperatorDelivery(uid, image, out var capture, out _));
            store.Add(uid, CreateEntry(2, itemId: 100));
            Assert.False(capture.IsCurrent());
            Assert.False(capture.TryPrepare(new[] { CreateEntry(3, itemId: 101) }, out _, out _));
            Assert.False(store.TryCaptureOperatorDelivery(uid, image, out _, out _));
        }
        Assert.Equal(image, File.ReadAllBytes(store.GetMailboxPath(uid)));
    }
    [Fact]
    public void OperatorProjectionAcceptsBoundedBatchAndRejectsExpiredEntry()
    {
        var store = CreateStore();
        lock (store.SyncRoot)
        {
            Assert.True(store.TryCaptureOperatorDelivery(8116, new byte[4], out var capture, out _));
            Assert.True(capture.TryAllocateMailIds(32, out var ids));
            var rows = ids.Select(id => CreateEntry(id, itemId: id + 100)).ToArray();
            Assert.True(capture.TryPrepare(rows, out var projection, out _));
            Assert.Equal(4 + 32 * 740, projection.CopyAfterImage().Length);
            Assert.False(capture.TryPrepare(new[] { CreateEntry(50, itemId: 500, timer: now.AddSeconds(-1).ToBinary()) }, out _, out _));
        }
    }
}
