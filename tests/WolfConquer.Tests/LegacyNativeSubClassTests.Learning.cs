using Sort = WolfConquer.Core.Data.LegacyNativeDbLevExp.Sort;
using SubClass = WolfConquer.Core.World.LegacyNativeSubClassRuntime;
using Row = WolfConquer.Core.World.LegacyNativeMsgSubClass.SubClases;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubClassTests
{
    [Fact]
    public Task SendLearnInsertsActualObjectAndSendsItsPhysicalIdAndLevelNotPhrase() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = new Row { ID = Sort.Warlock, Level = 3, Phrase = 7 };
        Assert.True(scope.Runtime.SendLearn(scope.Client, row, scope.Packet));
        Assert.Same(row, Assert.Single(scope.Runtime.src).Value);
        RowIs(row, Sort.Warlock, 3, 7);
        scope.Frames(Single(4, 2, 3));
        scope.CallerIs(Single(4, 2, 3));
    });

    [Fact]
    public Task DuplicateSendLearnReturnsFalseBeforeClientAndStreamUseAndKeepsExistingRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var old = scope.State.Row(Sort.Warlock, Sort.Wrangler, 7, 5);
        var incoming = new Row { ID = Sort.Warlock, Level = 255, Phrase = 254 };
        Assert.False(scope.Runtime.SendLearn(null!, incoming, null!));
        Assert.Same(old, Assert.Single(scope.Runtime.src).Value);
        RowIs(old, Sort.Wrangler, 7, 5);
        RowIs(incoming, Sort.Warlock, 255, 254);
        scope.Unwritten();
    });

    [Fact]
    public Task NullStreamFaultLeavesLearnedObjectPublishedBeforeAnySend() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = new Row { ID = Sort.Warlock, Level = 3, Phrase = 7 };
        Assert.Throws<NullReferenceException>(() => scope.Runtime.SendLearn(scope.Client, row, null!));
        Assert.Same(row, Assert.Single(scope.Runtime.src).Value);
        scope.Unwritten();
    });

    [Fact]
    public Task NullClientFaultOccursAfterLearnPublicationAndRealPacketSerialization() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = new Row { ID = Sort.Warlock, Level = 3, Phrase = 7 };
        Assert.Throws<NullReferenceException>(() => scope.Runtime.SendLearn(null!, row, scope.Packet));
        Assert.Same(row, Assert.Single(scope.Runtime.src).Value);
        Assert.Empty(scope.Queue);
        scope.CallerIs(Single(4, 2, 3));
    });

    [Fact]
    public Task NullItemFaultPrecedesDictionaryMutationOrPacketUse() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<NullReferenceException>(() => scope.Runtime.SendLearn(scope.Client, null!, scope.Packet));
        Assert.Empty(scope.Runtime.src);
        scope.Unwritten();
    });

    [Fact]
    public Task LearnNewSubClassDelegatesToActualPlayerMemberRatherThanReceiverStorage() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var receiver = new SubClass();
        scope.Client.Player.SubClass = scope.Runtime;
        Assert.True(receiver.LearnNewSubClass(Sort.Warlock, scope.Client, scope.Packet));
        Assert.Empty(receiver.src);
        RowIs(Assert.Single(scope.Runtime.src).Value, Sort.Warlock, 9, 9);
        scope.Frames(Single(4, 2, 9));
        scope.Client.Player.SubClass = null;
        Assert.Throws<NullReferenceException>(() => receiver.LearnNewSubClass(Sort.Sage, scope.Client, scope.Packet));
        scope.Frames(Single(4, 2, 9));
        Assert.Empty(receiver.src);
    });

    [Fact]
    public Task SendPromotedSendsProvidedPhysicalPhraseWithoutRequiringOrAddingStoredRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = new Row { ID = (Sort)255, Level = 3, Phrase = 254 };
        scope.Runtime.SendPromoted(scope.Client, row, scope.Packet);
        Assert.Empty(scope.Runtime.src);
        RowIs(row, (Sort)255, 3, 254);
        scope.Frames(Single(5, 255, 254));
        scope.CallerIs(Single(5, 255, 254));
    });

    [Fact]
    public Task ShowGuiSendsEmptyThenActualPhysicalRowsWithPhraseBeforeLevelAndCurrentStudy() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Runtime.StudyPoints = 17;
        scope.Runtime.ShowGui(scope.Client, scope.Packet);
        scope.State.Row(Sort.MartialArtist, Sort.Wrangler, 3, 7);
        scope.State.Row(Sort.Warlock, (Sort)255, 255, 254);
        scope.Runtime.StudyPoints = 65535;
        var rows = scope.Runtime.src.Values.ToArray();
        var expectedRows = rows.Select(row => ((byte)row.ID, row.Phrase, row.Level)).ToArray();
        scope.Runtime.ShowGui(scope.Client, scope.Packet);
        scope.Frames(ListFrame(7, 17), ListFrame(7, 65535, expectedRows));
        scope.CallerIs(ListFrame(7, 65535, expectedRows));
        Assert.Equal(2, scope.Runtime.src.Count);
        Assert.Equal(ushort.MaxValue, scope.Runtime.StudyPoints);
    });
}
