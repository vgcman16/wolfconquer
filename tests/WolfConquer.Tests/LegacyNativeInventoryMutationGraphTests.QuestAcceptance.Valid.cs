using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Quest = WolfConquer.Core.Data.LegacyNativeQuestInfo;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Theory]
    [InlineData(0, 1u, 1)]
    [InlineData(600, 0u, 0)]
    [InlineData(600, 3u, 3)]
    [InlineData(1469, 0u, 4)]
    [InlineData(1469, uint.MaxValue, 4)]
    [InlineData(409, 7u, 3)]
    [InlineData(409, uint.MaxValue, 3)]
    [InlineData(35024, 2u, 2)]
    public Task OrdinaryAcceptKeepsExactFrameCanonicalAliasesAndLocalCountOverrides(int uid, uint catalogCount, int expectedCount) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var definition = AcceptanceDefinition(checked((ushort)uid), catalogCount);
            var oldSource = scope.Quests.src; var oldAccepted = scope.Quests.AcceptedQuests;
            var caller = ObjectiveCallerSnapshot.Capture(scope.Packet);
            Assert.True(scope.Quests.Accept(definition, uint.MaxValue));
            Assert.NotSame(oldSource, scope.Quests.src); Assert.NotSame(oldAccepted, scope.Quests.AcceptedQuests);
            Assert.Empty(oldSource); Assert.Empty(oldAccepted);
            var row = Assert.Single(scope.Quests.src).Value;
            Assert.Same(row, Assert.Single(scope.Quests.AcceptedQuests).Value);
            AssertAcceptedRow(row, (uint)uid, uint.MaxValue, expectedCount);
            Assert.Equal(catalogCount, definition.Intentions); Assert.Equal((ushort)uid, definition.MissionId);
            Assert.Equal(AcceptanceFrame((uint)uid, uint.MaxValue), Assert.Single(scope.Queue));
            caller.AssertUnchanged(scope.Packet); scope.AssertTail();
        });

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(99, false)]
    public Task ExistingOrdinaryQuestIsFalseWithoutPacketOrCatalogCountEffects(int status, bool indexed) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var seal = new AcceptanceSealScope("bad");
        var row = scope.Row(600, 600, (Status)status, 7);
        if (indexed) scope.Quests.AcceptedQuests.Add(600, row);
        var book = new ObjectiveBookSnapshot(scope.Quests);
        var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
        var definition = AcceptanceDefinition(600, uint.MaxValue);
        Assert.False(scope.Quests.Accept(definition, 99));
        book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet);
        Assert.Equal(uint.MaxValue, definition.Intentions); Assert.Empty(scope.Queue); scope.AssertTail();
    });

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(99, false)]
    public Task ExistingKingdomQuestReturnsCanonicalOutAndFinishedPredicateWithoutReaccepting(int status, bool indexed) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); using var seal = new AcceptanceSealScope("bad");
            var row = scope.Row(35024, 35024, (Status)status, 7);
            if (indexed) scope.Quests.AcceptedQuests.Add(35024, row);
            var book = new ObjectiveBookSnapshot(scope.Quests);
            var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
            Assert.Equal(status != (int)Status.Finished,
                scope.Quests.AcceptKingDomMission(AcceptanceDefinition(35024, uint.MaxValue), 99, out var found));
            Assert.Same(row, found); book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet);
            Assert.Empty(scope.Queue); scope.AssertTail();
        });

    [Fact]
    public Task NonKingdomRequestReturnsFalseNullOutWithoutAllocatingAPacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var seal = new AcceptanceSealScope("bad");
        Assert.False(scope.Quests.AcceptKingDomMission(AcceptanceDefinition(600, uint.MaxValue), 8, out var found));
        Assert.Null(found); Assert.Empty(scope.Quests.src); Assert.Empty(scope.Quests.AcceptedQuests);
        Assert.Empty(scope.Queue); scope.AssertTail();
    });

    [Theory]
    [InlineData(35024)]
    [InlineData(35007)]
    public Task NewKingdomAcceptancePublishesOnlySourceAndReturnsTheSameRow(int uid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var definition = AcceptanceDefinition((ushort)uid, 3);
        var oldAccepted = scope.Quests.AcceptedQuests;
        Assert.True(scope.Quests.AcceptKingDomMission(definition, 0x12345678, out var found));
        Assert.Same(found, Assert.Single(scope.Quests.src).Value); Assert.Empty(scope.Quests.AcceptedQuests);
        Assert.Same(oldAccepted, scope.Quests.AcceptedQuests);
        AssertAcceptedRow(found, (uint)uid, 0x12345678, 3);
        Assert.Equal(AcceptanceFrame((uint)uid, 0x12345678), Assert.Single(scope.Queue));
        Assert.Equal(3u, definition.Intentions); scope.AssertTail();
    });

    [Fact]
    public Task KingdomAcceptanceDoesNotApplyOrdinaryCountOverrides() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Quest.KingDomMissions.Add(1469);
        var definition = AcceptanceDefinition(1469, 7);
        Assert.True(scope.Quests.AcceptKingDomMission(definition, 1, out var found));
        AssertAcceptedRow(found, 1469, 1, 7); Assert.Equal(7u, definition.Intentions);
        Assert.Empty(scope.Quests.AcceptedQuests); Assert.Equal(AcceptanceFrame(1469, 1), Assert.Single(scope.Queue));
    });

    [Fact]
    public Task AcceptanceKeepsExistingRowsOrderAcceptedSubsetAndHarmlessSharedEmptyArrays() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var empty = Array.Empty<uint>();
        var first = scope.Row(700, 700, Status.Accepted, empty);
        var second = scope.Row(701, 701, Status.Available, empty);
        Assert.True(scope.Quests.Accept(AcceptanceDefinition(600, 2), 3));
        Assert.Equal(new uint[] { 700, 701, 600 }, scope.Quests.src.Keys);
        Assert.Same(first, scope.Quests.src[700]); Assert.Same(second, scope.Quests.src[701]);
        Assert.Same(empty, first.Intentions); Assert.Same(empty, second.Intentions);
        Assert.Equal(600u, Assert.Single(scope.Quests.AcceptedQuests).Key);
        Assert.Equal(AcceptanceFrame(600, 3), Assert.Single(scope.Queue));
    });

    [Fact]
    public Task AcceptanceDoesNotInventFiveQuestOrUInt16IntentionGameplayLimits() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint uid = 700; uid < 707; uid++)
        {
            var row = scope.Row(uid, uid, Status.Accepted, uid);
            scope.Quests.AcceptedQuests.Add(uid, row);
        }
        Assert.True(scope.Quests.Accept(AcceptanceDefinition(600, 65536), 5));
        Assert.Equal(8, scope.Quests.src.Count); Assert.Equal(8, scope.Quests.AcceptedQuests.Count);
        AssertAcceptedRow(scope.Quests.src[600], 600, 5, 65536);
        Assert.Equal(new uint[] { 706 }, scope.Quests.src[706].Intentions);
        Assert.Equal(AcceptanceFrame(600, 5), Assert.Single(scope.Queue)); scope.AssertTail();
    });

    [Theory]
    [InlineData(1469)]
    [InlineData(409)]
    public Task SharedCatalogDefinitionRemainsUntouchedAcrossTwoRealBookOwners(int uid) => NativePacketTestThread.Run(() =>
    {
        using var first = new Scope(); using var second = new Scope();
        var old = Quest.AllQuests;
        var definition = AcceptanceDefinition((ushort)uid, 17);
        try
        {
            Quest.AllQuests = new() { [(uint)uid] = definition };
            Assert.True(first.Quests.Accept(Quest.AllQuests[(uint)uid], 1));
            Assert.True(second.Quests.Accept(Quest.AllQuests[(uint)uid], 2));
            Assert.Same(definition, Quest.AllQuests[(uint)uid]); Assert.Equal(17u, definition.Intentions);
            Assert.NotSame(first.Quests.src[(uint)uid], second.Quests.src[(uint)uid]);
            Assert.NotSame(first.Quests.src[(uint)uid].Intentions, second.Quests.src[(uint)uid].Intentions);
            Assert.Equal(AcceptanceFrame((uint)uid, 1), Assert.Single(first.Queue));
            Assert.Equal(AcceptanceFrame((uint)uid, 2), Assert.Single(second.Queue));
        }
        finally { Quest.AllQuests = old; }
    });

    [Theory]
    [InlineData(null)]
    [InlineData("TQServer")]
    [InlineData("éxxxxxxx")]
    [InlineData("狼xxxxxxx")]
    public Task AcceptancePacketMatchesWholeGenuineCodecWithOneStableSeal(string? value) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var seal = new AcceptanceSealScope(value);
        using var control = new WolfConquer.Core.Networking.LegacyNativePacket(64);
        var row = new LegacyNativeMsgQuestList.QuestListItem { UID = 600, Status = Status.Accepted, Time = 19 };
        control.QuestListCreate(LegacyNativeMsgQuestList.QuestMode.AcceptQuest, 1).AddItemQuestList(row).QuestListFinalize();
        Assert.Equal(AcceptanceFrame(600, 19, value), NativeSpellTestFixture.Bytes(control));
        Assert.True(scope.Quests.Accept(AcceptanceDefinition(600, 3), 19));
        Assert.Equal(NativeSpellTestFixture.Bytes(control), Assert.Single(scope.Queue)); scope.AssertTail();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DroppedNativeDeliveryDoesNotReportACommittedAcceptanceAsRejected(bool fake) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (fake) scope.Client.Fake = true; else scope.Client.Socket!.Alive = false;
        Assert.True(scope.Quests.Accept(AcceptanceDefinition(600, 2), 1));
        Assert.Same(scope.Quests.src[600], scope.Quests.AcceptedQuests[600]);
        AssertAcceptedRow(scope.Quests.src[600], 600, 1, 2); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task AcceptedCanonicalBookRoundTripsThroughTheActualOwnedFilePersistence() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles();
        Assert.True(scope.Quests.Accept(AcceptanceDefinition(1469, 99), 23));
        Assert.Equal(LegacyNativeRoleQuests.Outcome.Committed, LegacyNativeRoleQuests.TrySave(scope.Client).Status);
        Assert.Equal(QuestFile((1469, 0, 23, new uint[4])), File.ReadAllBytes(scope.QuestPath));
        scope.Quests.src.Clear(); scope.Quests.AcceptedQuests.Clear();
        Assert.Equal(LegacyNativeRoleQuests.Outcome.Committed, LegacyNativeRoleQuests.TryLoad(scope.Client).Status);
        Assert.Same(scope.Quests.src[1469], scope.Quests.AcceptedQuests[1469]);
        AssertAcceptedRow(scope.Quests.src[1469], 1469, 23, 4);
        Assert.Equal(AcceptanceFrame(1469, 23), Assert.Single(scope.Queue));
    });
}
