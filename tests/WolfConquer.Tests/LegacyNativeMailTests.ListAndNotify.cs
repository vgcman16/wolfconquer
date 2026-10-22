using WolfConquer.Core.World;
using Mail = WolfConquer.Core.World.LegacyNativeMailList;
using ListMessage = WolfConquer.Core.World.LegacyNativeCMsgMailList;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMailTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(20)]
    public Task LoadingUsesReversedLiveValueSnapshotAndNativeTenRowPageCount(int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint i = 1; i <= count; i++) scope.Client.MailList.Add(i * 100, Row(i));
        var live = scope.Client.MailList.Values.Reverse().ToArray();
        ListMessage.Loading(scope.Client);
        int expected = count == 0 ? 0 : count / 10 + 1;
        Assert.Equal(expected, scope.Queue.Count);
        for (int page = 0; page < expected; page++)
            Assert.Equal(MailFrame(0, 1, live.Skip(page * 10).Take(10).ToArray()), scope.Queue.Dequeue());
        Assert.Equal(11, scope.Available); Assert.Equal(0, scope.Recycled(0).Position);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(11)]
    public Task RequestIgnoresSuppliedPacketAndSendsAscendingPhysicalIdsWithSamePageQuirk(int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint i = 1; i <= count; i++) scope.Client.MailList.Add(i, Row((uint)count + 1 - i));
        var expectedRows = scope.Client.MailList.Values.OrderBy(p => p.id).ToArray();
        Process(scope.Client, scope.Packet);
        int expected = count == 0 ? 0 : count / 10 + 1;
        Assert.Equal(expected, scope.Queue.Count);
        for (int page = 0; page < expected; page++)
            Assert.Equal(MailFrame(0, 1, expectedRows.Skip(page * 10).Take(10).ToArray()), scope.Queue.Dequeue());
        Assert.Equal(12, scope.Available);
        Assert.Equal(count == 0 ? 237 : 4, scope.Packet.Position);
        Assert.Equal(count == 0 ? 321 : MailFrame(0, 1, expectedRows.Skip((expected - 1) * 10).Take(10).ToArray()).Length,
            scope.Packet.Size);
    });

    [Fact]
    public Task ProducersObserveLiveDictionaryRatherThanExposedStaleSafeDictionaryCache() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var old = Row(4); scope.Client.MailList.Add(7, old);
        var cached = scope.Client.MailList.GetValues();
        var replacement = Row(9); replacement.title = "Replacement";
        scope.Client.MailList[7] = replacement; // Actual native indexer does not dirty MyArray.
        Assert.False(scope.Client.MailList.Update); Assert.Same(cached, scope.Client.MailList.GetValues());
        Assert.Same(old, Assert.Single(cached));
        ListMessage.Loading(scope.Client);
        Assert.Equal(MailFrame(0, 1, replacement), scope.Queue.Dequeue());
        Process(scope.Client, scope.Packet);
        Assert.Equal(MailFrame(0, 1, replacement), scope.Queue.Dequeue());
        Assert.Same(cached, scope.Client.MailList.GetValues());
    });

    [Fact]
    public Task LoadingCanSendFirstTenBeforeNullLastPageFaultButRequestSortFaultsBeforeWriting() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.MailList.Add(1, null!);
        for (uint i = 2; i <= 11; i++) scope.Client.MailList.Add(i, Row(i));
        var snapshot = scope.Client.MailList.Values.ToArray();
        Assert.Null(snapshot[0]); // Fixture's actual, observed enumeration precondition.
        Assert.Throws<NullReferenceException>(() => ListMessage.Loading(scope.Client));
        Assert.Equal(MailFrame(0, 1, snapshot.Skip(1).Reverse().ToArray()), Assert.Single(scope.Queue));
        Assert.Equal(0, scope.Recycled(0).Position);
        scope.Queue.Clear();
        Assert.Throws<NullReferenceException>(() => Process(scope.Client, scope.Packet));
        Assert.Empty(scope.Queue); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
    });

    [Fact]
    public Task RequestWithNullPacketReturnsForEmptyButFaultsOnlyWhenFirstPageMustSerialize() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); Process(scope.Client, null!); Assert.Empty(scope.Queue);
        scope.Client.MailList.Add(1, Row());
        Assert.Throws<NullReferenceException>(() => Process(scope.Client, null!));
        Assert.Single(scope.Client.MailList); Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task EmptyLoadingStillTakesPacketBeforeInspectingMailWhereasRequestNeverTakesOne() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.NullBin();
        Assert.Throws<NullReferenceException>(() => ListMessage.Loading(scope.Client));
        Process(scope.Client, scope.Packet); Assert.Empty(scope.Queue);
        scope.RestoreBin(); Assert.Equal(12, scope.Available);
    });

    [Theory]
    [InlineData(false, "0500120802")]
    [InlineData(true, "0500120803")]
    public Task NotifyContainsOnlyTheNativeOneByteCountPredicate(bool nonempty, string hex) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (nonempty) scope.Client.MailList.Add(999, null!); // Values are never inspected.
        LegacyNativeCMsgMailNotify.Loading(scope.Client);
        Assert.Equal(Convert.FromHexString(hex), Assert.Single(scope.Queue));
        Assert.Equal(11, scope.Available); Assert.Equal(0, scope.Recycled(0).Position);
        Assert.Equal(5, scope.Recycled(0).Size);
    });

    [Fact]
    public Task NotifyResetsWriterBeforeNullDictionaryFaultAndFinallyReleasesActualPacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Client.MailList = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeCMsgMailNotify.Loading(scope.Client));
        Assert.Empty(scope.Queue); Assert.Equal(11, scope.Available);
        Assert.Equal(0, scope.Recycled(0).Position); Assert.Equal(321, scope.Recycled(0).Size);
    });
}
