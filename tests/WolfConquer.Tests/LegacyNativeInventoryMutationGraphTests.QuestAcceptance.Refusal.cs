using WolfConquer.Core.Data;
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
    public Task AcceptanceRequiresTheGenuineCurrentConstructorPlayerAndAssignedBook(int fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var player = scope.Player;
        var row = scope.Row(700, 700, Status.Accepted, 3);
        scope.Quests.AcceptedQuests.Add(700, row);
        var book = new ObjectiveBookSnapshot(scope.Quests);
        var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
        switch (fault)
        {
            case 0: scope.Quests.Player = new LegacyNativePlayerRuntime(scope.Client); break;
            case 1: scope.Client.Player = new LegacyNativePlayerRuntime(scope.Client); break;
            case 2: player.QuestGUI = new LegacyNativeQuestsRuntime(player); break;
            case 3: player.QuestGUI = null; break;
            case 4: player.UID = 0; break;
            case 5: scope.Quests.Player = null!; break;
        }
        var definition = AcceptanceDefinition(600, 3);
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(definition, 8));
        book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet);
        Assert.Equal(3u, definition.Intentions); Assert.Empty(scope.Queue);
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
    public Task AcceptanceRejectsTheWholeInvalidGraphRatherThanPublishingASplitPrefix(int fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(700, 700, Status.Accepted, 3);
        scope.Quests.AcceptedQuests.Add(700, row);
        var values = row.Intentions;
        switch (fault)
        {
            case 0: scope.Quests.src = null!; break;
            case 1: scope.Quests.AcceptedQuests = null!; break;
            case 2: scope.Quests.AcceptedQuests = scope.Quests.src; break;
            case 3: scope.Quests.src[777] = null!; break;
            case 4: scope.Quests.src[777] = new Row { UID = 778 }; break;
            case 5: scope.Quests.AcceptedQuests[700] = new Row { UID = 700, Status = Status.Accepted, Intentions = [3] }; break;
            case 6: scope.Quests.AcceptedQuests[777] = row; break;
            case 7: row.Status = Status.Available; break;
            case 8: scope.Row(777, 777, Status.Available).Intentions = null!; break;
            case 9: scope.Row(777, 777, Status.Available).Intentions = values; break;
        }
        var source = scope.Quests.src; var accepted = scope.Quests.AcceptedQuests;
        var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(AcceptanceDefinition(600, 3), 8));
        Assert.Same(source, scope.Quests.src); Assert.Same(accepted, scope.Quests.AcceptedQuests);
        Assert.Same(values, row.Intentions); Assert.Equal(new uint[] { 3 }, values);
        packet.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task MissingAcceptanceMetadataRejectsWithoutAnyBookOrPacketEffect() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var book = new ObjectiveBookSnapshot(scope.Quests); var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(null!, 1));
        book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("123456789")]
    public Task InvalidRealSealRefusesBeforeEitherIndexOrSharedOverrideMetadataChanges(string value) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var seal = new AcceptanceSealScope(value);
        var definition = AcceptanceDefinition(1469, 99);
        var book = new ObjectiveBookSnapshot(scope.Quests); var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(definition, 1));
        book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet);
        Assert.Equal(99u, definition.Intentions); Assert.Empty(scope.Queue); scope.AssertTail();
    });

    [Theory]
    [InlineData(uint.MaxValue)]
    [InlineData((uint)int.MaxValue)]
    [InlineData(16_777_212u)]
    public Task UnrepresentableNewIntentionCountRefusesBeforeArrayOrGraphPublication(uint count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var definition = AcceptanceDefinition(600, count);
        var book = new ObjectiveBookSnapshot(scope.Quests); var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(definition, 1));
        book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet);
        Assert.Equal(count, definition.Intentions); Assert.Empty(scope.Queue); scope.AssertTail();
    });

    [Fact]
    public Task CompleteProjectedImageIncludesExistingRowsBeforeAllocatingNewIntentions() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Row(700, 700, Status.Available, new uint[1000]);
        var book = new ObjectiveBookSnapshot(scope.Quests); var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
        // This count fits a lone row exactly, but not this actual projected book.
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(AcceptanceDefinition(600, 16_777_211), 1));
        book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CustomComparerCollisionCannotPublishEitherStagedIndex(bool acceptedCollision) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(610, 610, Status.Accepted, 3);
        scope.Quests.AcceptedQuests.Add(610, row);
        var comparer = new AcceptanceComparer(tens: true);
        if (acceptedCollision) scope.Quests.AcceptedQuests = new(comparer) { [610] = row };
        else scope.Quests.src = new(comparer) { [610] = row };
        var book = new ObjectiveBookSnapshot(scope.Quests); var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(AcceptanceDefinition(600, 3), 8));
        book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
        scope.AssertTail();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GenuineStagingComparerFailureCannotPublishAnAcceptedOnlyOrSourceOnlyRow(bool acceptedFailure) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var row = scope.Row(700, 700, Status.Accepted, 3);
            scope.Quests.AcceptedQuests.Add(700, row);
            var comparer = new AcceptanceComparer();
            if (acceptedFailure) scope.Quests.AcceptedQuests = new(comparer) { [700] = row };
            else scope.Quests.src = new(comparer) { [700] = row };
            comparer.ArmedKey = 600; comparer.Throw = true;
            var book = new ObjectiveBookSnapshot(scope.Quests); var packet = ObjectiveCallerSnapshot.Capture(scope.Packet);
            Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(AcceptanceDefinition(600, 3), 8));
            Assert.Null(comparer.ArmedKey);
            book.AssertUnchanged(scope.Quests); packet.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
            scope.AssertTail();
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PhysicalDuplicateUidInCustomDictionaryRejectsWithoutCallingItsComparer(bool acceptedDuplicate) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(700, 700, Status.Accepted, 3);
        var comparer = new ObjectiveChangingHash();
        var duplicate = new Dictionary<uint, Row>(comparer);
        duplicate.Add(700, row);
        duplicate.Add(700, acceptedDuplicate ? row : new Row { UID = 700, Status = Status.Accepted, Intentions = [3] });
        Assert.Equal(2, duplicate.Count);
        if (acceptedDuplicate) scope.Quests.AcceptedQuests = duplicate; else scope.Quests.src = duplicate;
        int calls = comparer.Calls;
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(AcceptanceDefinition(600, 3), 8));
        Assert.Equal(calls, comparer.Calls); Assert.Equal(2, duplicate.Count);
        Assert.Equal(new uint[] { 3 }, row.Intentions); Assert.Empty(scope.Queue);
    });

    [Fact]
    public void AcceptanceFormatBoundUsesTheExistingExactPersistenceImagePolicy()
    {
        Assert.Equal(4, LegacyNativeRoleQuests.GetProjectedQuestImageLength([], null));
        Assert.Equal(20, LegacyNativeRoleQuests.GetProjectedQuestImageLength([], 0));
        Assert.Equal(64 * 1024 * 1024, LegacyNativeRoleQuests.GetProjectedQuestImageLength([], 16_777_211));
        Assert.Throws<InvalidDataException>(() => LegacyNativeRoleQuests.GetProjectedQuestImageLength([], 16_777_212));
        Assert.Throws<InvalidDataException>(() => LegacyNativeRoleQuests.GetProjectedQuestImageLength([16_777_211], 0));
        Assert.Throws<InvalidDataException>(() => LegacyNativeRoleQuests.GetProjectedQuestImageLength([-1], null));
    }
}
