using WolfConquer.Core.World;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Theory]
    [InlineData(600u, 0)] // nonkingdom, missing source
    [InlineData(600u, 1)] // nonkingdom, source exists but never accepted
    [InlineData(35024u, 0)] // kingdom, missing source
    [InlineData(35024u, 2)] // kingdom, finished source
    public Task ObjectiveAdmissionRetainsWholeEarlyReturnsBeforeTouchingNullPacket(uint uid, int setup) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            if (setup != 0) scope.Row(uid, uid, setup == 2 ? Status.Finished : Status.Available, 7);
            scope.Quests.SetKingDomQuestObjectives(null!, uid, 1);
            scope.Quests.SetQuestObjectives(null!, uid, 1);
            scope.Quests.IncreaseQuestObjectives(null!, uid, 1);
            if (setup != 0) Assert.Equal(7u, scope.Quests.src[uid].Intentions[0]);
            Assert.Empty(scope.Queue);
        });

    [Theory]
    [InlineData(600u)]
    [InlineData(35024u)]
    [InlineData(35007u)]
    public Task SetObjectivesGrowsPreservingOldTailAndPublishesExactNativeDataPacket(uint uid) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(uid, uid, Status.Accepted, 5, 6, 7);
            scope.Quests.AcceptedQuests[uid] = row;
            uint[] supplied = [10, 20];

            scope.Quests.SetQuestObjectives(scope.Packet, uid, supplied);

            Assert.Same(row, scope.Quests.src[uid]);
            Assert.Equal(new uint[] { 10, 20, 7 }, row.Intentions);
            Assert.NotSame(supplied, row.Intentions);
            Assert.Equal(DataFrame(uid, 10, 20, 7), Assert.Single(scope.Queue));
            scope.Queue.Clear();
            scope.Quests.SetQuestObjectives(scope.Packet, uid, 1, 2, 3, 4);
            Assert.Equal(new uint[] { 1, 2, 3, 4 }, row.Intentions);
            Assert.Equal(DataFrame(uid, 1, 2, 3, 4), Assert.Single(scope.Queue));
        });

    [Theory]
    [InlineData(35024u)]
    [InlineData(35007u)]
    public Task KingdomSetterAcceptsAvailableRowsWithoutAcceptedDictionaryMembership(uint uid) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(uid, uid, Status.Available, 7);
            scope.Quests.SetKingDomQuestObjectives(scope.Packet, uid, 11, 12);
            Assert.Equal(new uint[] { 11, 12 }, row.Intentions);
            Assert.Equal(Status.Available, row.Status);
            Assert.Empty(scope.Quests.AcceptedQuests);
            Assert.Equal(DataFrame(uid, 11, 12), Assert.Single(scope.Queue));
        });

    [Fact]
    public Task OrdinarySetterReadsSourceAliasButActiveAdmissionReadsSeparateAcceptedAlias() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var source = scope.Row(600, 600, Status.Finished, 3);
            var accepted = new LegacyNativeMsgQuestList.QuestListItem
                { UID = 999, Status = Status.Available, Intentions = [80] };
            scope.Quests.AcceptedQuests[600] = accepted;

            scope.Quests.SetQuestObjectives(scope.Packet, 600, 7);

            Assert.Equal(7u, source.Intentions[0]);
            Assert.Equal(80u, accepted.Intentions[0]);
            Assert.Equal(Status.Finished, source.Status);
            Assert.Same(accepted, scope.Quests.AcceptedQuests[600]);
            Assert.Equal(DataFrame(600, 7), Assert.Single(scope.Queue));
        });

    [Theory]
    [InlineData(600u)]
    [InlineData(35024u)]
    [InlineData(35007u)]
    public Task IncreaseObjectivesRetainsUnsignedWrapArrayGrowthAndWholeSourcePacket(uint uid) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(uid, uid, Status.Accepted, uint.MaxValue, 10);
            scope.Quests.AcceptedQuests[uid] = row;
            scope.Quests.IncreaseQuestObjectives(scope.Packet, uid, 1, 2, 3);
            Assert.Equal(new uint[] { 0, 12, 3 }, row.Intentions);
            Assert.Equal(DataFrame(uid, 0, 12, 3), Assert.Single(scope.Queue));
        });

    [Fact]
    public Task OrdinaryIncreaseUsesAcceptedAliasWithoutReplacingDistinctSourceAlias() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var source = scope.Row(600, 600, Status.Finished, 3);
            var accepted = new LegacyNativeMsgQuestList.QuestListItem
                { UID = 999, Status = Status.Available, Intentions = [80] };
            scope.Quests.AcceptedQuests[600] = accepted;
            scope.Quests.IncreaseQuestObjectives(scope.Packet, 600, 7);
            Assert.Equal(3u, source.Intentions[0]);
            Assert.Equal(87u, accepted.Intentions[0]);
            Assert.Equal(Status.Available, accepted.Status);
            Assert.Equal(DataFrame(600, 87), Assert.Single(scope.Queue));
        });

    [Fact]
    public Task KingdomIncreaseDoesNotConsultAcceptedAliasOrRequireAcceptedStatus() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var source = scope.Row(35024, 35024, Status.Available, 3);
            scope.Quests.IncreaseQuestObjectives(scope.Packet, 35024, 7);
            Assert.Equal(10u, source.Intentions[0]);
            Assert.Empty(scope.Quests.AcceptedQuests);
            Assert.Equal(DataFrame(35024, 10), Assert.Single(scope.Queue));
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullPacketFaultOccursAfterRealObjectiveMutationRatherThanRollingItBack(bool increase) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(600, 600, Status.Accepted, 3);
            scope.Quests.AcceptedQuests[600] = row;
            Assert.Throws<NullReferenceException>(() =>
            {
                if (increase) scope.Quests.IncreaseQuestObjectives(null!, 600, 7);
                else scope.Quests.SetQuestObjectives(null!, 600, 7);
            });
            Assert.Equal(increase ? 10u : 7u, row.Intentions[0]);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task EmptyObjectiveInputStillPublishesExistingArrayWithoutShrinkingIt() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(600, 600, Status.Accepted, 3, 4);
            scope.Quests.AcceptedQuests[600] = row;
            scope.Quests.IncreaseQuestObjectives(scope.Packet, 600);
            Assert.Equal(new uint[] { 3, 4 }, row.Intentions);
            Assert.Equal(DataFrame(600, 3, 4), Assert.Single(scope.Queue));
        });

    [Fact]
    public Task DirectAutoPatcherBuildsNativeAction162WithoutQuestMutationOrDialogCallback() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Quests.SendAutoPatcher(1002, 400, 500, 321);
            Assert.Equal("1900FE07081118EA07480050C10260A201680070900378F403",
                Convert.ToHexString(Assert.Single(scope.Queue)));
            Assert.Empty(scope.Quests.src);
            Assert.Empty(scope.Quests.AcceptedQuests);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
        });
}
