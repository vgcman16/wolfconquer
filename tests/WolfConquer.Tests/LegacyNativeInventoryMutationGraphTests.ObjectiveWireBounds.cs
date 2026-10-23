using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Theory]
    [InlineData(600u, false, false)]
    [InlineData(600u, false, true)]
    [InlineData(600u, true, false)]
    [InlineData(600u, true, true)]
    [InlineData(35024u, false, false)]
    [InlineData(35024u, false, true)]
    [InlineData(35024u, true, false)]
    [InlineData(35024u, true, true)]
    [InlineData(35007u, false, false)]
    [InlineData(35007u, false, true)]
    [InlineData(35007u, true, false)]
    [InlineData(35007u, true, true)]
    public Task ObjectiveFrameUsesExactLiveCallerAllocationOrRejectsWithoutAnyWrites(
        uint uid, bool sealedFrame, bool exactCapacity) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var seal = new ObjectiveSealScope(sealedFrame);
        var row = scope.Row(uid, uid, Status.Accepted, 3, 4);
        scope.Quests.AcceptedQuests.Add(uid, row);
        var expected = ObjectiveFrame(uid, sealedFrame, 10, 12, 9);
        using var caller = new LegacyNativePacket(expected.Length - (exactCapacity ? 0 : 1));
        InitializeObjectiveCaller(caller);
        var before = ObjectiveCallerSnapshot.Capture(caller);
        var graph = new ObjectiveBookSnapshot(scope.Quests);
        if (exactCapacity)
        {
            scope.Quests.IncreaseQuestObjectives(caller, uid, 7, 8, 9);
            Assert.Equal(new uint[] { 10, 12, 9 }, row.Intentions);
            Assert.Same(row, scope.Quests.src[uid]); Assert.Same(row, scope.Quests.AcceptedQuests[uid]);
            Assert.Equal(expected, Assert.Single(scope.Queue));
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(caller));
            Assert.Equal(4, caller.Position); Assert.Equal(expected.Length, caller.Size);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => scope.Quests.IncreaseQuestObjectives(caller, uid, 7, 8, 9));
            graph.AssertUnchanged(scope.Quests); before.AssertUnchanged(caller);
            Assert.Empty(scope.Queue);
        }
        scope.AssertTail();
    });

    [Theory]
    [InlineData(600u, false, false)]
    [InlineData(600u, false, true)]
    [InlineData(600u, true, false)]
    [InlineData(600u, true, true)]
    [InlineData(35024u, false, false)]
    [InlineData(35024u, false, true)]
    [InlineData(35024u, true, false)]
    [InlineData(35024u, true, true)]
    [InlineData(35007u, false, false)]
    [InlineData(35007u, false, true)]
    [InlineData(35007u, true, false)]
    [InlineData(35007u, true, true)]
    public Task WholeObjectiveFrameRespectsActualNativeSendLimitIncludingSealAndRepeatedTails(
        uint uid, bool sealedFrame, bool overBound) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var seal = new ObjectiveSealScope(sealedFrame);
        int fixedLength = uid == 35007 ? 28 : 16;
        int wordBytes = uid is 35007 or 35024 ? 8 : 4;
        int maximum = (1032 - fixedLength - (sealedFrame ? 8 : 0)) / wordBytes;
        var values = Enumerable.Range(1, maximum + (overBound ? 1 : 0)).Select(value => (uint)value).ToArray();
        var row = scope.Row(uid, uid, Status.Accepted, 3);
        scope.Quests.AcceptedQuests.Add(uid, row);
        var caller = ObjectiveCallerSnapshot.Capture(scope.Packet);
        var graph = new ObjectiveBookSnapshot(scope.Quests);
        if (overBound)
        {
            Assert.Throws<InvalidDataException>(() => scope.Quests.SetQuestObjectives(scope.Packet, uid, values));
            graph.AssertUnchanged(scope.Quests); caller.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
        }
        else
        {
            scope.Quests.SetQuestObjectives(scope.Packet, uid, values);
            var expected = ObjectiveFrame(uid, sealedFrame, values);
            Assert.Equal(expected, Assert.Single(scope.Queue));
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(values, row.Intentions); Assert.NotSame(values, row.Intentions);
            Assert.InRange(expected.Length, 1, 1032); Assert.Equal(4, scope.Packet.Position);
        }
        scope.AssertTail();
    });

    [Theory]
    [InlineData(600u, "TQServer")]
    [InlineData(35024u, "TQServer")]
    [InlineData(35007u, "TQServer")]
    [InlineData(600u, "éxxxxxxx")]
    [InlineData(35024u, "éxxxxxxx")]
    [InlineData(35007u, "éxxxxxxx")]
    [InlineData(600u, "狼xxxxxxx")]
    [InlineData(35024u, "狼xxxxxxx")]
    [InlineData(35007u, "狼xxxxxxx")]
    public Task PreparedObjectiveCodecRetainsCompleteGenuineStableSealEncoding(uint uid, string seal) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var sealField = typeof(LegacyNativePacket).GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
        sealField.SetValue(null, seal);
        uint[] values = [7, uint.MaxValue, 9];
        using var control = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
        control.MsgQuestDataCreate(0, uid, values);
        Assert.Equal(NativeSpellTestFixture.Bytes(control),
            LegacyNativeQuestPacket.PrepareQuestObjectiveFrame(uid, values, seal));
        scope.AssertTail();
    });

    [Fact]
    public Task ObjectiveMutationDoesNotInventFiveQuestCatalogOrUniqueIntentionCountPolicy() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint uid = 600; uid < 607; uid++)
        {
            var row = scope.Row(uid, uid, Status.Accepted, uid);
            scope.Quests.AcceptedQuests.Add(uid, row);
        }
        scope.Quests.SetQuestObjectives(scope.Packet, 600, 1, 2, 3, 4, 5, 6, 7);
        Assert.Equal(7, scope.Quests.src.Count); Assert.Equal(7, scope.Quests.AcceptedQuests.Count);
        Assert.Equal(DataFrame(600, 1, 2, 3, 4, 5, 6, 7), Assert.Single(scope.Queue));
        Assert.Equal(new uint[] { 601 }, scope.Quests.src[601].Intentions);
        scope.AssertTail();
    });
}
