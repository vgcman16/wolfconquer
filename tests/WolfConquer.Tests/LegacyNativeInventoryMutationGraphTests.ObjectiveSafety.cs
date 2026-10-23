using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public Task ObjectiveMutationRequiresCurrentConstructorPlayerAndAssignedBook(int fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var original = scope.Player;
        var row = scope.Row(600, 600, Status.Accepted, 3, 4);
        scope.Quests.AcceptedQuests.Add(600, row);
        var graph = new ObjectiveBookSnapshot(scope.Quests);
        var caller = ObjectiveCallerSnapshot.Capture(scope.Packet);
        switch (fault)
        {
            case 0: scope.Quests.Player = new LegacyNativePlayerRuntime(scope.Client); break;
            case 1: scope.Client.Player = new LegacyNativePlayerRuntime(scope.Client); break;
            case 2: original.QuestGUI = new LegacyNativeQuestsRuntime(original); break;
            case 3: original.QuestGUI = null; break;
            case 4: original.UID = 0; break;
            case 5: scope.Quests.Player = null!; break;
        }
        Assert.Throws<InvalidDataException>(() => scope.Quests.IncreaseQuestObjectives(scope.Packet, 600, 1));
        graph.AssertUnchanged(scope.Quests); caller.AssertUnchanged(scope.Packet);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public Task ObjectiveMutationRejectsNoncanonicalOrUnavailableFullBook(int fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(600, 600, Status.Accepted, 3, 4);
        scope.Quests.AcceptedQuests.Add(600, row);
        var old = row.Intentions;
        var caller = ObjectiveCallerSnapshot.Capture(scope.Packet);
        switch (fault)
        {
            case 0: scope.Quests.src = null!; break;
            case 1: scope.Quests.AcceptedQuests = null!; break;
            case 2: scope.Quests.AcceptedQuests = scope.Quests.src; break;
            case 3: scope.Quests.src[777] = null!; break;
            case 4: scope.Quests.src[777] = new Row { UID = 778 }; break;
            case 5: scope.Quests.AcceptedQuests[600] = new Row { UID = 600, Status = Status.Accepted, Intentions = [90] }; break;
            case 6: scope.Quests.AcceptedQuests[777] = row; break;
            case 7: row.Status = Status.Available; break;
            case 8: scope.Row(777, 777, Status.Available).Intentions = null!; break;
            case 9: scope.Row(777, 777, Status.Available).Intentions = old; break;
        }
        var source = scope.Quests.src; var accepted = scope.Quests.AcceptedQuests;
        Assert.Throws<InvalidDataException>(() => scope.Quests.IncreaseQuestObjectives(scope.Packet, 600, 1));
        Assert.Same(source, scope.Quests.src); Assert.Same(accepted, scope.Quests.AcceptedQuests);
        Assert.Same(old, row.Intentions); Assert.Equal(new uint[] { 3, 4 }, row.Intentions);
        caller.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ObjectiveValidationRejectsChangingHashPhysicalUidDuplicatesWithoutCallingComparer(bool acceptedDuplicate) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(600, 600, Status.Accepted, 3);
            var comparer = new ObjectiveChangingHash();
            var duplicate = new Dictionary<uint, Row>(comparer);
            duplicate.Add(600, row);
            duplicate.Add(600, acceptedDuplicate ? row : new Row { UID = 600, Status = Status.Accepted, Intentions = [3] });
            Assert.Equal(2, duplicate.Count);
            if (acceptedDuplicate) scope.Quests.AcceptedQuests = duplicate;
            else scope.Quests.src = duplicate;
            var calls = comparer.Calls;
            var caller = ObjectiveCallerSnapshot.Capture(scope.Packet);
            Assert.Throws<InvalidDataException>(() => scope.Quests.IncreaseQuestObjectives(scope.Packet, 600, 1));
            Assert.Equal(calls, comparer.Calls);
            Assert.Equal(2, duplicate.Count);
            Assert.Equal(new uint[] { 3 }, row.Intentions);
            caller.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task InputAliasingLiveIntentionsUsesPrivateValuesAndNeverChangesRetainedArray(int operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(35024, 35024, Status.Accepted, 3, 4);
        scope.Quests.AcceptedQuests.Add(35024, row);
        var old = row.Intentions;
        ApplyObjectiveOperation(scope, operation, scope.Packet, 35024, old);
        Assert.Equal(new uint[] { 3, 4 }, old);
        Assert.NotSame(old, row.Intentions);
        var values = operation == 2 ? new uint[] { 6, 8 } : new uint[] { 3, 4 };
        Assert.Equal(values, row.Intentions);
        Assert.Same(row, scope.Quests.src[35024]); Assert.Same(row, scope.Quests.AcceptedQuests[35024]);
        Assert.Equal(DataFrame(35024, values), Assert.Single(scope.Queue));
        scope.AssertTail();
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task SharedEmptyIntentionArraysRemainValidIndependentRows(int operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var empty = Array.Empty<uint>();
        var row = scope.Row(35024, 35024, Status.Accepted, empty);
        var other = scope.Row(777, 777, Status.Available, empty);
        scope.Quests.AcceptedQuests.Add(35024, row);
        Assert.Same(row.Intentions, other.Intentions);
        ApplyObjectiveOperation(scope, operation, scope.Packet, 35024, 7);
        Assert.Equal(new uint[] { 7 }, row.Intentions);
        Assert.Same(empty, other.Intentions); Assert.Empty(other.Intentions);
        Assert.Equal(DataFrame(35024, 7), Assert.Single(scope.Queue));
        Assert.Same(row, scope.Quests.AcceptedQuests[35024]);
        scope.AssertTail();
    });

    [Theory]
    [InlineData(600u)]
    [InlineData(35024u)]
    [InlineData(35007u)]
    public Task LaterCounterOverflowCannotLeaveAnEarlierCounterIncremented(uint uid) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(uid, uid, Status.Accepted, 5, uint.MaxValue);
        scope.Quests.AcceptedQuests.Add(uid, row);
        var book = new ObjectiveBookSnapshot(scope.Quests);
        var caller = ObjectiveCallerSnapshot.Capture(scope.Packet);
        Assert.Throws<InvalidDataException>(() => scope.Quests.IncreaseQuestObjectives(scope.Packet, uid, 1, 1));
        book.AssertUnchanged(scope.Quests); caller.AssertUnchanged(scope.Packet);
        Assert.Empty(scope.Queue); scope.AssertTail();
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task DisposedCallerRefusesAllWholeObjectiveOperationsBeforeMutation(int operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(35024, 35024, Status.Accepted, 3);
        scope.Quests.AcceptedQuests.Add(35024, row);
        var book = new ObjectiveBookSnapshot(scope.Quests);
        using var caller = new LegacyNativePacket(64);
        caller.Dispose();
        Assert.Throws<InvalidDataException>(() => ApplyObjectiveOperation(scope, operation, caller, 35024, 1));
        book.AssertUnchanged(scope.Quests); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public Task InvalidCallerMetadataOrNullInputCannotWriteQuestOrCallerPrefix(int fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(35024, 35024, Status.Accepted, 3);
        scope.Quests.AcceptedQuests.Add(35024, row);
        switch (fault)
        {
            case 0: scope.Packet.Size = -1; break;
            case 1: scope.Packet.Size = scope.Packet.Capacity + 1; break;
            case 2: scope.Packet.Seek(-1); break;
            case 3: scope.Packet.Seek(scope.Packet.Capacity + 1); break;
        }
        var book = new ObjectiveBookSnapshot(scope.Quests);
        var caller = ObjectiveCallerSnapshot.Capture(scope.Packet);
        Assert.Throws<InvalidDataException>(() => scope.Quests.SetKingDomQuestObjectives(
            fault == 4 ? null! : scope.Packet, 35024, fault == 5 ? null! : [1]));
        book.AssertUnchanged(scope.Quests); caller.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
    });
}
