using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityPoleEventTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ZeroHpScoreRunsCompleteDoubleRankingRoundGlobalMessageAndRealReset(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Map();
        var pole = scope.Active(kind, 0, 900);
        var old = Scores(kind);
        var outside = new Queue<byte[]>();
        scope.Peer(95, 1002, 64, 888, outside);
        Call(kind, "UpdateScore", scope.Packet, 77u, scope.Client.Player);
        var owner = Get(kind, "RoundOwner")!;
        Assert.Equal(scope.Client.Player.UID, RowField<uint>(owner, "UID"));
        Assert.Equal(77u, RowField<uint>(owner, "Score"));
        Assert.Equal("Wolf", RowField<string>(owner, "Name"));
        Assert.Same(old[scope.Client.Player.UID], owner);
        Assert.NotSame(old, Scores(kind));
        Assert.Single(old);
        Assert.Empty(Scores(kind));
        Assert.Equal("Wolf", pole.Name);
        Assert.Equal(900, pole.HitPoints);
        Assert.Equal(LegacyNativeProcesType.Alive, Get<LegacyNativeProcesType>(kind, "Proces"));
        Assert.Equal(new ushort[] { 2179, 2179, 2179, 2176, 2166 }, scope.Queue.Select(Opcode));
        var packets = scope.Queue.ToArray();
        Assert.Equal("No 1. Wolf (77)", Message(packets[0]));
        Assert.Equal("No 1. Wolf (77)", Message(packets[1]));
        string announcement = "The [ " + Rank(kind) + " ]" + (kind == 3 ? " " : "") + "Round Ownered by Wolf.";
        Assert.Equal(announcement, Message(packets[2]));
        Assert.Equal(new ushort[] { 2179, 2176, 2166 }, outside.Select(Opcode));
        Assert.Equal(announcement, Message(outside.First()));
        var spawn = Proto<LegacyNativeSobNpcRuntime.SobNpcSpawnProto>(packets[^1]);
        Assert.Equal((uint)(22340 + kind), spawn.UID);
        Assert.Equal(900u, spawn.Hitpoints);
        Assert.Equal("Wolf", spawn.Name);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task IncrementUsesRealInfoAliasAndUncheckedUintWrap(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Map();
        scope.Active(kind);
        var row = Row(kind, scope.Client.Player.UID, uint.MaxValue, "Alias");
        Scores(kind).Add(scope.Client.Player.UID, row);
        Call(kind, "UpdateScore", scope.Packet, 3u, scope.Client.Player);
        Assert.Same(row, Scores(kind)[scope.Client.Player.UID]);
        Assert.Equal(2u, RowField<uint>(row, "Score"));
        Assert.Null(Get(kind, "RoundOwner"));
        Assert.Equal("No 1. Alias (2)", Message(Assert.Single(scope.Queue)));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task DeadUpdateStillAddsScoreButSortHasSourceEarlyReturn(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = scope.Active(kind, 1);
        Set(kind, "Proces", LegacyNativeProcesType.Dead);
        Call(kind, "UpdateScore", scope.Packet, 19u, scope.Client.Player);
        Assert.Equal(19u, RowField<uint>(Scores(kind)[scope.Client.Player.UID]!, "Score"));
        Assert.Equal(1, pole.HitPoints);
        Assert.Null(Get(kind, "RoundOwner"));
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task DeadLethalUpdateFaultsAtNullRoundOwnerAfterScoreInsertWithoutReset(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = scope.Active(kind, 0, 900);
        Set(kind, "Proces", LegacyNativeProcesType.Dead);
        var old = Scores(kind);
        Assert.Throws<NullReferenceException>(() => Call(kind, "UpdateScore", scope.Packet, 12u, scope.Client.Player));
        Assert.Same(old, Scores(kind));
        Assert.Equal(12u, RowField<uint>(old[scope.Client.Player.UID]!, "Score"));
        Assert.Equal("Previous", pole.Name);
        Assert.Equal(0, pole.HitPoints);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public Task SingletonNullKeyFaultLeavesRoundOwnerAndRecyclerReferenceOrderUntouched(int kind, bool winner) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Set(kind, "Proces", LegacyNativeProcesType.Alive);
        var old = Row(kind, 8, 42);
        Set(kind, "RoundOwner", old);
        Scores(kind).Add(7u, null);
        var recycler = scope.RecycleBin.ToArray();
        Assert.Throws<NullReferenceException>(() => Call(kind, "SortScores", winner));
        Assert.Same(old, Get(kind, "RoundOwner"));
        Assert.Null(Scores(kind)[7u]);
        Assert.Equal(recycler, scope.RecycleBin.ToArray());
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task SortingPublishesFiveStableUintSnapshotRowsAndKeepsRealTopAlias(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Map();
        Set(kind, "Proces", LegacyNativeProcesType.Alive);
        for (uint uid = 1; uid <= 7; uid++)
            Scores(kind).Add(uid, Row(kind, uid, uid <= 3 ? uint.MaxValue : 0, "Player" + uid));
        var snapshot = Scores(kind).Values.Cast<object>().ToArray();
        var expected = snapshot.OrderByDescending(row => RowField<uint>(row, "Score")).Take(5).ToArray();
        Call(kind, "SortScores", true);
        Assert.Same(expected[0], Get(kind, "RoundOwner"));
        Assert.Equal(expected.Select((row, index) => "No " + (index + 1) + ". " +
            RowField<string>(row, "Name") + " (" + RowField<uint>(row, "Score") + ")"), scope.Queue.Select(Message));
        Assert.Equal(7, Scores(kind).Count);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task MissingPoleFinishFaultHappensAfterSortingAndRoundOwnerBeforeGlobalOrReset(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Map();
        Set(kind, "Proces", LegacyNativeProcesType.Alive);
        var row = Row(kind, 7, 91, "Best");
        Scores(kind).Add(7u, row);
        var scores = Scores(kind);
        Assert.Throws<KeyNotFoundException>(() => Call(kind, "FinishRound", scope.Packet));
        Assert.Same(row, Get(kind, "RoundOwner"));
        Assert.Same(scores, Scores(kind));
        Assert.Single(scores);
        Assert.Equal("No 1. Best (91)", Message(Assert.Single(scope.Queue)));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task MissingPoleDeadFinishFaultPrecedesNullRoundOwnerAccess(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Null(Get(kind, "RoundOwner"));
        Assert.Throws<KeyNotFoundException>(() => Call(kind, "FinishRound", scope.Packet));
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task EmptyAliveFinishWithStaleWinnerPreservesRoundAliasAndPublishesRealRound(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Map();
        var pole = scope.Active(kind, -1, 800);
        var previous = Row(kind, 99, 4, "OldWinner");
        Set(kind, "RoundOwner", previous);
        Call(kind, "FinishRound", scope.Packet);
        Assert.Same(previous, Get(kind, "RoundOwner"));
        Assert.Equal("OldWinner", pole.Name);
        Assert.Equal(800, pole.HitPoints);
        Assert.Equal(new ushort[] { 2179, 2176, 2166 }, scope.Queue.Select(Opcode));
    });
}
