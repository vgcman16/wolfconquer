using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Mail = WolfConquer.Core.World.LegacyNativeMailList;
using ListMessage = WolfConquer.Core.World.LegacyNativeCMsgMailList;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMailTests
{
    [Fact]
    public Task DefaultMailAndExplicitFieldInitializerSlotHaveOnlyNativeState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = new Mail();
        Assert.Null(row.title); Assert.Null(row.Sender_name); Assert.Null(row.content);
        Assert.Equal(0u, row.id); Assert.Equal(0u, row.Expiration_Date); Assert.Equal(0ul, row.Money);
        Assert.Equal(0u, row.EMoney); Assert.Equal(0u, row.itemid); Assert.Equal(0u, row.Emoney_Record_Type);
        Assert.Equal(0u, row.Flag); Assert.Equal(default, row.TimeStamp);
        var queue = new Queue<byte[]>();
        var client = NativeSpellTestFixture.CreateOwner(queue);
        Assert.Null(client.MailList);
        LegacyNativeMailListMemberPhases.AssignMailListAtNativeFieldInitializerSlot(client);
        var old = client.MailList;
        old.Add(1, row);
        LegacyNativeMailListMemberPhases.AssignMailListAtNativeFieldInitializerSlot(client);
        Assert.NotSame(old, client.MailList); Assert.Empty(client.MailList);
        Assert.Same(row, old[1]); Assert.Empty(queue);
    });

    [Fact]
    public Task MailProjectionCopiesAllPhysicalFieldsButNotContentOrFutureMutations() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = Row(uint.MaxValue); row.Money = ulong.MaxValue; row.EMoney = uint.MaxValue;
        row.Expiration_Date = 71; row.itemid = 72; row.Emoney_Record_Type = 73; row.Flag = 74;
        var message = new ListMessage.Mail(row);
        row.id = 99; row.title = "Changed"; row.Sender_name = "Changed"; row.Money = 0;
        Assert.Equal((ulong)uint.MaxValue, message.Id); Assert.Equal(ulong.MaxValue, message.Money);
        Assert.Equal((ulong)uint.MaxValue, message.EMoney); Assert.Equal(71ul, message.Expiration_Date);
        Assert.Equal(72ul, message.Itemid); Assert.Equal(73ul, message.Emoney_Record_Type); Assert.Equal(74ul, message.Flag);
        Assert.Equal("T", message.Title); Assert.Equal("S", message.Sender_Name);
        Assert.Throws<NullReferenceException>(() => new ListMessage.Mail(null!));
    });

    [Fact]
    public Task ConstructorSelectsFirstPhysicalKeyHoleThenSendsListStoresAndNotifies() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var one = Row(99); var three = Row(98);
        scope.Client.MailList.Add(1, one); scope.Client.MailList.Add(3, three);
        var cached = scope.Client.MailList.GetValues();
        var before = DateTime.Now;
        var row = new Mail(scope.Client, "A", "B", "Hidden content", 120, ulong.MaxValue, 7, 8, 9, 10);
        var after = DateTime.Now;
        Assert.Equal(2u, row.id); Assert.Same(row, scope.Client.MailList[2]);
        Assert.Same(one, scope.Client.MailList[1]); Assert.Same(three, scope.Client.MailList[3]);
        Assert.Equal("Hidden content", row.content); Assert.Equal(120u, row.Expiration_Date);
        Assert.InRange(row.TimeStamp, before.AddSeconds(120), after.AddSeconds(120));
        Assert.True(scope.Client.MailList.Update); Assert.Equal(2, cached.Length);
        Assert.Equal(2, scope.Queue.Count);
        Assert.Equal(MailFrame(2, 0, row), scope.Queue.Dequeue());
        Assert.Equal(Convert.FromHexString("0500120803"), scope.Queue.Dequeue());
        Assert.Equal(10, scope.Available);
        Assert.Equal(0, scope.Recycled(0).Position); Assert.Equal(0, scope.Recycled(1).Position);
    });

    [Fact]
    public Task SuppressedTransportDoesNotSuppressConstructorPublicationOrNotificationCreation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.Fake = true;
        var before = DateTime.Now;
        var row = new Mail(scope.Client, null!, null!, null!, 0, 0, 0, 0, 0, 0);
        Assert.Same(row, scope.Client.MailList[1]); Assert.Empty(scope.Queue);
        Assert.Null(row.title); Assert.Null(row.Sender_name); Assert.Null(row.content);
        Assert.InRange(row.TimeStamp, before, DateTime.Now);
        Assert.Equal(10, scope.Available);
    });

    [Fact]
    public Task ConstructorPublishesThroughTheStableExternallyHeldDictionaryGate() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var book = scope.Client.MailList;
        object gate = book.SyncRoot;
        long revision = book.Revision;
        Mail row;
        lock (gate)
        {
            row = new Mail(scope.Client, "T", "S", "C", 60, 2, 3, 4, 6, 5);
            Assert.True(Monitor.IsEntered(book.SyncRoot));
        }
        Assert.Same(gate, book.SyncRoot);
        Assert.Same(row, Assert.Single(book).Value); Assert.True(book.Update);
        Assert.Equal(revision + 1, book.Revision);
        Assert.Equal(MailFrame(2, 0, row), scope.Queue.Dequeue());
        Assert.Equal(Convert.FromHexString("0500120803"), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue);
        Assert.Equal(10, scope.Available);
        Assert.Equal(0, scope.Recycled(0).Position); Assert.Equal(0, scope.Recycled(1).Position);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ConstructorTakesRecycledOwnerBeforeNullClientOrMailDictionaryFault(bool nullClient) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.MailList = null!;
        Assert.Throws<NullReferenceException>(() => new Mail(nullClient ? null! : scope.Client, "T", "S", "C", 60, 0, 0, 0, 0, 0));
        Assert.Empty(scope.Queue); Assert.Equal(11, scope.Available);
        Assert.Equal(0, scope.Recycled(0).Position); Assert.Equal(321, scope.Recycled(0).Size);
    });

    [Fact]
    public Task MissingActualRecycleQueueFailsBeforeAnyConstructorPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.NullBin();
        Assert.Throws<NullReferenceException>(() => new Mail(scope.Client, "T", "S", "C", 60, 0, 0, 0, 0, 0));
        Assert.Empty(scope.Queue); Assert.Empty(scope.Client.MailList);
        scope.RestoreBin(); Assert.Equal(12, scope.Available);
    });
}
