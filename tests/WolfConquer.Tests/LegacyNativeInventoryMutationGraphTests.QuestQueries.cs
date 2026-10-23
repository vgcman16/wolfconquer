using WolfConquer.Core.World;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Fact]
    public void QuestsConstructorRetainsSuppliedNullOwnerAndCreatesDistinctNativeDictionaries()
    {
        var first = new LegacyNativeQuestsRuntime(null!);
        var second = new LegacyNativeQuestsRuntime(null!);
        Assert.Null(first.Player);
        Assert.Empty(first.src);
        Assert.Empty(first.AcceptedQuests);
        Assert.NotSame(first.src, first.AcceptedQuests);
        Assert.NotSame(first.src, second.src);
        Assert.NotSame(first.AcceptedQuests, second.AcceptedQuests);
    }

    [Fact]
    public Task ObjectivesMissingRowReturnsFreshOneZeroAndExistingRowReturnsDeepArrayCopy() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Quests.GetQuestObjectives(777, out uint[] first);
            scope.Quests.GetQuestObjectives(777, out uint[] second);
            Assert.Equal(new uint[] { 0 }, first);
            Assert.NotSame(first, second);
            var row = scope.Row(600, 600, Status.Accepted, 2, 4);
            scope.Quests.GetQuestObjectives(600, out uint[] copy);
            Assert.Equal(new uint[] { 2, 4 }, copy);
            Assert.NotSame(row.Intentions, copy);
            copy[0] = 99;
            Assert.Equal(2u, row.Intentions[0]);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task ObjectiveCopyDoesNotNormalizeNullExistingIntentions() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Row(600, 600, Status.Accepted).Intentions = null!;
        uint[] before = [777];
        Assert.Throws<NullReferenceException>(() => scope.Quests.GetQuestObjectives(600, out before));
        Assert.Equal(new uint[] { 777 }, before);
        Assert.Null(scope.Quests.src[600].Intentions);
    });

    [Fact]
    public Task NonKingdomAcceptedQueryUsesOnlyAcceptedKeyNotRowUidOrStatus() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(500, 999, Status.Finished, 1);
        scope.Quests.AcceptedQuests[600] = row;
        Assert.True(scope.Quests.CheckQuest(600, Status.Accepted));
        Assert.True(scope.Quests.IsActiveQuest(600));
        Assert.False(scope.Quests.CheckQuest(999, Status.Accepted));
        Assert.False(scope.Quests.CheckQuest(600, Status.Finished));
    });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public Task NonAcceptedQueryCountsMatchingSourceValuesRatherThanDictionaryKeys(int statusValue) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var status = (Status)statusValue;
            scope.Row(500, 600, status, 1);
            Assert.True(scope.Quests.CheckQuest(600, status));
            scope.Row(501, 600, status, 1);
            Assert.False(scope.Quests.CheckQuest(600, status));
            scope.Quests.src.Remove(500);
            Assert.True(scope.Quests.CheckQuest(600, status));
        });

    [Fact]
    public Task KingdomAcceptedQueryUsesSourceValuesAndLiveMutableKingdomCatalog() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(500, 35024, Status.Accepted, 1);
            Assert.True(scope.Quests.CheckQuest(35024, Status.Accepted));
            Assert.Empty(scope.Quests.AcceptedQuests);
            scope.Quests.AcceptedQuests[777] = scope.Quests.src[500];
            Assert.True(scope.Quests.CheckQuest(777, Status.Accepted));
            WolfConquer.Core.Data.LegacyNativeQuestInfo.KingDomMissions.Add(777);
            Assert.False(scope.Quests.CheckQuest(777, Status.Accepted));
            scope.Row(501, 35024, Status.Accepted, 1);
            Assert.False(scope.Quests.IsActiveQuest(35024));
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ObjectiveThresholdQueryUsesExactChosenDictionaryAndDoesNotRequireActiveStatus(bool kingdom) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(600, 999, Status.Finished, 10, 20);
            if (!kingdom)
            {
                scope.Quests.src.Clear();
                scope.Quests.AcceptedQuests[600] = row;
            }
            bool Query(params uint[] values) => kingdom
                ? scope.Quests.CheckKingDomQuest(600, values)
                : scope.Quests.CheckObjectives(600, values);
            Assert.True(Query());
            Assert.True(Query(10, 20));
            Assert.True(Query(9, 0));
            Assert.False(Query(11, 20));
            Assert.False(Query(10, 21));
            Assert.Equal(new uint[] { 10, 20 }, row.Intentions);
            Assert.Empty(scope.Queue);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingObjectiveKeyReturnsFalseEvenForEmptyThresholds(bool kingdom) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.False(kingdom ? scope.Quests.CheckKingDomQuest(600) : scope.Quests.CheckObjectives(600));
        });

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    public Task AcceptCountUsesSourceAcceptedRowsIncludingAliasedDuplicatesAndNativeLimit(
        int count, bool allowed) => NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(700, 999, Status.Finished);
            scope.Quests.src.Clear();
            for (uint index = 0; index < count; index++)
            {
                row.Status = Status.Accepted;
                scope.Quests.src[index + 1] = row;
            }
            scope.Quests.AcceptedQuests[777] = new();
            Assert.Equal(count, scope.Quests.AcceptQuestsCount());
            Assert.Equal(allowed, scope.Quests.AllowAccept());
            Assert.Single(scope.Quests.AcceptedQuests);
        });

    [Fact]
    public Task NullSourceRowCountFaultDoesNotRepairSourceOrAcceptedDictionary() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Quests.src[600] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Quests.AcceptQuestsCount());
        Assert.Throws<NullReferenceException>(() => scope.Quests.AllowAccept());
        Assert.Null(scope.Quests.src[600]);
        Assert.Empty(scope.Quests.AcceptedQuests);
    });
}
