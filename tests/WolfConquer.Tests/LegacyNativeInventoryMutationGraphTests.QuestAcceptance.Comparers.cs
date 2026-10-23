using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Fact]
    public Task KingdomAcceptanceRetainsAndNeverInvokesTheUnchangedAcceptedIndexComparer() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(700, 700, Status.Accepted, 3);
        var comparer = new AcceptanceComparer();
        scope.Quests.AcceptedQuests = new(comparer) { [700] = row };
        comparer.ArmedKey = 700; comparer.Throw = true;
        int calls = comparer.Calls;
        var accepted = scope.Quests.AcceptedQuests;
        Assert.True(scope.Quests.AcceptKingDomMission(AcceptanceDefinition(35024, 2), 8, out var found));
        Assert.Equal(calls, comparer.Calls); Assert.Equal(700u, comparer.ArmedKey);
        Assert.Same(accepted, scope.Quests.AcceptedQuests); Assert.Same(row, Assert.Single(accepted).Value);
        Assert.Same(found, scope.Quests.src[35024]); Assert.Equal(AcceptanceFrame(35024, 8), Assert.Single(scope.Queue));
        scope.AssertTail();
    });

    [Fact]
    public Task LegitimateCustomComparersAndExistingPhysicalAliasesSurviveCoherentAcceptance() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(700, 700, Status.Accepted, 3);
        var sourceComparer = new AcceptanceComparer(); var acceptedComparer = new AcceptanceComparer();
        scope.Quests.src = new(sourceComparer) { [700] = row };
        scope.Quests.AcceptedQuests = new(acceptedComparer) { [700] = row };
        var source = scope.Quests.src; var accepted = scope.Quests.AcceptedQuests;
        Assert.True(scope.Quests.Accept(AcceptanceDefinition(600, 3), 8));
        Assert.Same(sourceComparer, scope.Quests.src.Comparer); Assert.Same(acceptedComparer, scope.Quests.AcceptedQuests.Comparer);
        Assert.Same(row, scope.Quests.src[700]); Assert.Same(row, scope.Quests.AcceptedQuests[700]);
        Assert.Same(row, Assert.Single(source).Value); Assert.Same(row, Assert.Single(accepted).Value);
        Assert.Same(scope.Quests.src[600], scope.Quests.AcceptedQuests[600]);
        Assert.Equal(AcceptanceFrame(600, 8), Assert.Single(scope.Queue)); scope.AssertTail();
    });

    [Fact]
    public Task ExistingNoOpDoesNotInvokeAUserComparerOrRebuildItsIndex() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(600, 600, Status.Available, 3);
        var comparer = new AcceptanceComparer();
        scope.Quests.src = new(comparer) { [600] = row };
        comparer.ArmedKey = 600; comparer.Throw = true;
        int calls = comparer.Calls;
        var book = new ObjectiveBookSnapshot(scope.Quests);
        Assert.False(scope.Quests.Accept(AcceptanceDefinition(600, uint.MaxValue), 8));
        Assert.Equal(calls, comparer.Calls); Assert.Equal(600u, comparer.ArmedKey);
        book.AssertUnchanged(scope.Quests); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public Task FinalPureRecaptureRejectsLateComparerOwnerGraphMetadataOrPolicyDrift(int fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Row(700, 700, Status.Accepted, 3);
        var comparer = new AcceptanceComparer();
        scope.Quests.AcceptedQuests = new(comparer) { [700] = row };
        var definition = AcceptanceDefinition(600, 3);
        var caller = ObjectiveCallerSnapshot.Capture(scope.Packet);
        var originalPlayer = scope.Player;
        var replacement = fault == 1 ? new LegacyNativePlayerRuntime(scope.Client) : null;
        comparer.ArmedKey = 600;
        comparer.OnHash = () =>
        {
            switch (fault)
            {
                case 0: originalPlayer.UID++; break;
                case 1: scope.Client.Player = replacement!; break;
                case 2: row.Intentions[0] = 99; break;
                case 3: definition.Intentions = 4; break;
                case 4: definition.MissionId = 601; break;
                case 5: LegacyNativeQuestInfo.KingDomMissions.Add(999); break;
                case 6: scope.Quests.src.Add(701, new Row { UID = 701, Status = Status.Available }); break;
            }
        };
        Assert.Throws<InvalidDataException>(() => scope.Quests.Accept(definition, 8));
        Assert.Null(comparer.ArmedKey); AssertNoPhysicalAcceptedUid(scope, 600); caller.AssertUnchanged(scope.Packet);
        // Arbitrary comparer effects are external raw writes, not magically
        // rolled back by admission. The owned new row/maps are never published.
        if (fault == 2) Assert.Equal(99u, row.Intentions[0]);
        if (fault == 3) Assert.Equal(4u, definition.Intentions);
        if (fault == 4) Assert.Equal((ushort)601, definition.MissionId);
    });

    [Fact]
    public Task LateSealChangeCannotResizeOrRewriteTheAlreadyPreparedOwnedPacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var seal = new AcceptanceSealScope("TQServer");
        var row = scope.Row(700, 700, Status.Accepted, 3);
        var comparer = new AcceptanceComparer();
        scope.Quests.AcceptedQuests = new(comparer) { [700] = row };
        comparer.ArmedKey = 600;
        comparer.OnHash = () => typeof(WolfConquer.Core.Networking.LegacyNativePacket)
            .GetField("seal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, "bad");
        Assert.True(scope.Quests.Accept(AcceptanceDefinition(600, 3), 8));
        Assert.Null(comparer.ArmedKey);
        Assert.Equal(AcceptanceFrame(600, 8, "TQServer"), Assert.Single(scope.Queue));
        Assert.Same(scope.Quests.src[600], scope.Quests.AcceptedQuests[600]); scope.AssertTail();
    });
}
