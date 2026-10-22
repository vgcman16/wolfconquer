using TitleType = WolfConquer.Core.World.LegacyNativeMsgTitleStorage.TitleType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTitleTests
{
    [Fact]
    public Task ScoreIncludesDuplicateKnownKeysSkipsUnknownsWrapsUintAndDoesNotSetPhysicalScore() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 77, score: uint.MaxValue);
            scope.Row(7, 88, score: 5);
            scope.Player.SpecialTitles.AddRange([(TitleType)1, (TitleType)1, (TitleType)7, (TitleType)999]);
            scope.Player.SpecialTitleScore = 777;
            Assert.Equal(3u, scope.Player.GetTitlesScore());
            Assert.Equal(777u, scope.Player.SpecialTitleScore);
            ListIs(scope, 1, 1, 7, 999);
            Untouched(scope);
        });

    [Fact]
    public Task AddUsesMetadataAliasAndPreAddScoreThenAppendsOnlyAfterBothRealSends() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(2, 2, score: 10);
            scope.Row(7, 1);
            scope.Player.SpecialTitles.Add((TitleType)2);
            scope.Player.SpecialTitleID = 888;
            scope.Player.SpecialTitleScore = 777;
            scope.Player.AddSpecialTitle((TitleType)7, scope.Packet);
            Frames(scope, Frame(0, 10, 1, 2), Frame(6, 100, 1, 2));
            ListIs(scope, 2, 7);
            Assert.Equal(19u, scope.Player.GetTitlesScore());
            Assert.Equal(888u, scope.Player.SpecialTitleID);
            Assert.Equal(777u, scope.Player.SpecialTitleScore);
        });

    [Fact]
    public Task AddMissingMetadataAndAlreadyOwnedTitleSkipStreamUseWithoutDuplicates() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            scope.Player.SpecialTitles.Add((TitleType)1);
            scope.Player.AddSpecialTitle((TitleType)999, null!);
            scope.Player.AddSpecialTitle((TitleType)1, null!);
            ListIs(scope, 1);
            Untouched(scope);
        });

    [Fact]
    public Task AddNullStreamDoesNotAppendBeforeItsFirstRequiredSerializationCompletes() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            Assert.Throws<NullReferenceException>(() => scope.Player.AddSpecialTitle((TitleType)1, null!));
            ListIs(scope);
            Untouched(scope);
        });

    [Fact]
    public Task SendSpecialTitlePreservesListDuplicatesAndUsesCurrentTotalScoreForEachKnownRow() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1, score: 7);
            scope.Player.SpecialTitles.AddRange([(TitleType)1, (TitleType)999, (TitleType)1]);
            scope.Player.SendSpecialTitle(scope.Packet);
            Frames(scope, Frame(0, 14, 1, 2), Frame(6, 100, 1, 2),
                Frame(0, 14, 1, 2), Frame(6, 100, 1, 2));
            ListIs(scope, 1, 999, 1);
            Assert.Equal(0u, scope.Player.SpecialTitleScore);
        });

    [Fact]
    public Task ActiveSendsTitleThenWingFromMetadataEvenWithoutOwnedListMembership() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1, 2);
            scope.Row(7, 4001, 3);
            scope.Player.SpecialTitleID = 19999;
            scope.Player.SpecialWingID = 70111;
            scope.Player.SpecialTitleScore = 777;
            scope.Player.ActiveSpecialTitles(scope.Packet);
            Frames(scope, Frame(0, 0, 1, 2, titleValue: 100), Frame(0, 0, 4001, 3, titleValue: 100));
            ListIs(scope);
            Assert.Equal(19999u, scope.Player.SpecialTitleID);
            Assert.Equal(70111u, scope.Player.SpecialWingID);
            Assert.Equal(777u, scope.Player.SpecialTitleScore);
        });

    [Fact]
    public Task ActiveZeroIdsStillPerformBothPhysicalMetadataKeyZeroLookups() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(0, 9, 8);
            scope.Player.ActiveSpecialTitles(scope.Packet);
            Frames(scope, Frame(0, 0, 9, 8, titleValue: 100), Frame(0, 0, 9, 8, titleValue: 100));
            Assert.Equal(0u, scope.Player.SpecialTitleID);
            Assert.Equal(0u, scope.Player.SpecialWingID);
        });

    [Fact]
    public Task ActiveMissingTitleMetadataStillAttemptsKnownWingAndRetainsFieldsOnNullStreamFault() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(7, 7);
            scope.Player.SpecialTitleID = 19999;
            scope.Player.SpecialWingID = 70002;
            Assert.Throws<NullReferenceException>(() => scope.Player.ActiveSpecialTitles(null!));
            Assert.Equal(19999u, scope.Player.SpecialTitleID);
            Assert.Equal(70002u, scope.Player.SpecialWingID);
            Untouched(scope);
        });

    [Fact]
    public Task RemoveDropsOnlyFirstDuplicateSendsFourFramesThenClearsTitleButNotSameTypeWing() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            scope.Row(7, 7, score: 10);
            scope.Player.SpecialTitles.AddRange([(TitleType)1, (TitleType)1, (TitleType)7]);
            scope.Player.SpecialTitleID = 10002;
            scope.Player.SpecialWingID = 10003;
            scope.Player.SpecialTitleScore = 777;
            scope.Player.RemoveSpecialTitle((TitleType)1, scope.Packet);
            Frames(scope, Frame(5, 0, 1, 2, title: false), Frame(5, 19, 1, 2),
                Frame(0, 19, 1, 2), Frame(3, 19, 1, 2));
            ListIs(scope, 1, 7);
            Assert.Equal(0u, scope.Player.SpecialTitleID);
            Assert.Equal(0u, scope.Player.SpecialTitleScore);
            Assert.Equal(10003u, scope.Player.SpecialWingID);
        });

    [Fact]
    public Task RemoveMetadataWithoutMembershipStillSendsFourFramesAndCanClearWingOnly() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            scope.Row(7, 7, score: 10);
            scope.Player.SpecialTitles.Add((TitleType)7);
            scope.Player.SpecialWingID = 10003;
            scope.Player.SpecialTitleScore = 777;
            scope.Player.RemoveSpecialTitle((TitleType)1, scope.Packet);
            Frames(scope, Frame(5, 0, 1, 2, title: false), Frame(5, 10, 1, 2),
                Frame(0, 10, 1, 2), Frame(3, 10, 1, 2));
            ListIs(scope, 7);
            Assert.Equal(0u, scope.Player.SpecialWingID);
            Assert.Equal(777u, scope.Player.SpecialTitleScore);
        });

    [Fact]
    public Task RemoveMissingMetadataDoesNotRemoveOwnedTitleOrClearItsEquippedFields() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Player.SpecialTitles.Add((TitleType)1);
            scope.Player.SpecialTitleID = 10002;
            scope.Player.SpecialTitleScore = 777;
            scope.Player.RemoveSpecialTitle((TitleType)1, null!);
            ListIs(scope, 1);
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Assert.Equal(777u, scope.Player.SpecialTitleScore);
            Untouched(scope);
        });

    [Fact]
    public Task RemoveNullStreamKeepsPriorListRemovalButDoesNotReachEquippedFieldClearing() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            scope.Player.SpecialTitles.Add((TitleType)1);
            scope.Player.SpecialTitleID = 10002;
            scope.Player.SpecialTitleScore = 777;
            Assert.Throws<NullReferenceException>(() => scope.Player.RemoveSpecialTitle((TitleType)1, null!));
            ListIs(scope);
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Assert.Equal(777u, scope.Player.SpecialTitleScore);
            Untouched(scope);
        });

    [Fact]
    public Task RemoveNullMetadataRowFaultsAfterListRemovalBeforeAnyFrameOrEquippedChange() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Titles[1] = null!;
            scope.Player.SpecialTitles.Add((TitleType)1);
            scope.Player.SpecialTitleID = 10002;
            Assert.Throws<NullReferenceException>(() => scope.Player.RemoveSpecialTitle((TitleType)1, scope.Packet));
            ListIs(scope);
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Untouched(scope);
        });

    [Fact]
    public Task RemoveLateScoreFaultRetainsExactlyFirstQueuedUnEquipAndLeavesEquippedIdsUncleared() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            scope.Titles[7] = null!;
            scope.Player.SpecialTitles.AddRange([(TitleType)1, (TitleType)7]);
            scope.Player.SpecialTitleID = 10002;
            scope.Player.SpecialTitleScore = 777;
            Assert.Throws<NullReferenceException>(() => scope.Player.RemoveSpecialTitle((TitleType)1, scope.Packet));
            ListIs(scope, 7);
            Frames(scope, Frame(5, 0, 1, 2, title: false));
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Assert.Equal(777u, scope.Player.SpecialTitleScore);
            Assert.Equal(Frame(5, 0, 1, 2, title: false), NativeSpellTestFixture.Bytes(scope.Packet));
        });

    [Fact]
    public Task RemoveUsesMetadataIdForFramesButRequestedListTypeForFinalEquippedClearing() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(7, 1);
            scope.Player.SpecialTitles.Add((TitleType)7);
            scope.Player.SpecialTitleID = 10002;
            scope.Player.SpecialWingID = 70003;
            scope.Player.SpecialTitleScore = 777;
            scope.Player.RemoveSpecialTitle((TitleType)7, scope.Packet);
            Frames(scope, Frame(5, 0, 1, 2, title: false), Frame(5, 0, 1, 2),
                Frame(0, 0, 1, 2), Frame(3, 0, 1, 2));
            ListIs(scope);
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Assert.Equal(777u, scope.Player.SpecialTitleScore);
            Assert.Equal(0u, scope.Player.SpecialWingID);
        });
}
