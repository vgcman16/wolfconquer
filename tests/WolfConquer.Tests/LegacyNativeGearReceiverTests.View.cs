using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeGearReceiverTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(-1)]
    public Task NonViewTypesDeserializeButNeverReadNullPoolOrUser(int type) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        LegacyNativePoolClients.GamePoll = null!;
        s.Input(ViewRequest(type));
        Receive("ProcessMsgCombatGear", null, s.Native.Packet);
        s.UnchangedInput();
        Assert.Empty(s.Queue);
        Assert.Null(LegacyNativePoolClients.GamePoll);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ViewUsesCurrentPhysicalPoolAndTargetClassUidNotLookupKeyAndSendsOnlyToRequester(bool replacePool) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var targetQueue = new Queue<byte[]>();
        var target = s.Target(29, 201, targetQueue);
        var oldPool = LegacyNativePoolClients.GamePoll!;
        oldPool[77] = target;
        var priorTarget = target;
        if (replacePool)
        {
            target = s.Target(31, 202, targetQueue);
            LegacyNativePoolClients.GamePoll = new ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>();
            LegacyNativePoolClients.GamePoll[77] = target;
        }
        s.User.CombatGear = null!; // View must consume the target's genuine gear, not requester gear.
        s.Input(ViewRequest(), opcode: 7);

        Receive("ProcessMsgCombatGear", s.User, s.Native.Packet);

        uint uid = replacePool ? 31u : 29u, item = replacePool ? 202u : 201u;
        string row = "08" + V(item) + "103218172001281F3000380040004800500058006000";
        Assert.Equal(Framed(2420, "080110" + V(uid) + "1A" + V((uint)(row.Length / 2)) + row), Assert.Single(s.Queue));
        Assert.Empty(targetQueue);
        Assert.Same(priorTarget, oldPool[77]);
        Assert.Equal(uid, target.Player.UID);
        Assert.Equal(2001u, target.Player.Class);
        Assert.Equal(23u, Assert.Single(target.CombatGear.Items).Value.Exp);
        Assert.Null(s.User.CombatGear);
        Assert.All(Rank.Ranks.Values, rank => Assert.Empty(rank.RankingList));
        s.UnchangedInput();
    });

    [Fact]
    public Task MissingViewTargetReturnsBeforeDereferencingNullRequester() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Input(ViewRequest());
        Receive("ProcessMsgCombatGear", null, s.Native.Packet);
        Assert.Empty(s.Queue);
        Assert.Empty(LegacyNativePoolClients.GamePoll!);
        s.UnchangedInput();
    });

    [Theory]
    [InlineData("pool")]
    [InlineData("target")]
    [InlineData("gear")]
    public Task ViewNativeNullOwnerFaultsPropagateAfterParseWithoutRepair(string fault) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        if (fault == "pool") LegacyNativePoolClients.GamePoll = null!;
        else if (fault == "target") LegacyNativePoolClients.GamePoll![77] = null!;
        else
        {
            var target = s.Target(29, 201, new());
            target.CombatGear = null!;
            LegacyNativePoolClients.GamePoll![77] = target;
        }
        s.Input(ViewRequest());
        Assert.Throws<NullReferenceException>(() => Receive("ProcessMsgCombatGear", s.User, s.Native.Packet));
        Assert.Empty(s.Queue);
        Assert.All(Rank.Ranks.Values, rank => Assert.Empty(rank.RankingList));
        s.UnchangedInput();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FalseGateDoesNotGuardViewReceiverParseOrRepairNullGetItemsResult(bool view) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        LegacyNativePoolClients.GamePoll![77] = s.Target(29, 201, new());
        Table.m_Load = false;
        s.Input(ViewRequest(view ? 1 : 0));
        if (view) Assert.Throws<NullReferenceException>(() => Receive("ProcessMsgCombatGear", s.User, s.Native.Packet));
        else Receive("ProcessMsgCombatGear", s.User, s.Native.Packet);
        Assert.Empty(s.Queue);
        Assert.False(Table.m_Load);
        s.UnchangedInput();
    });

    [Theory]
    [InlineData("ProcessMsgCombatGear", false)]
    [InlineData("ProcessMsgCombatGear", true)]
    [InlineData("ProcessAirPowerUp", false)]
    [InlineData("ProcessAirPowerUp", true)]
    public Task MalformedOrNegativeDeclaredPayloadFaultBeforeNullUserAndAllGameplayMutation(string method, bool negative) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Gear.AirPowers.Points = 91;
        s.Input(negative ? "" : "08", declared: negative ? 3 : 5);
        if (negative) Assert.Throws<OverflowException>(() => Receive(method, null, s.Native.Packet));
        else Assert.Throws<EndOfStreamException>(() => Receive(method, null, s.Native.Packet));
        Assert.Equal(negative ? 4 : 5, s.Native.Packet.Position);
        Assert.Equal(91u, s.Gear.AirPowers.Points);
        Assert.Empty(s.Queue); Assert.Empty(s.Native.Storage.ClientSpells);
        Assert.All(Rank.Ranks.Values, rank => Assert.Empty(rank.RankingList));
    });
}
