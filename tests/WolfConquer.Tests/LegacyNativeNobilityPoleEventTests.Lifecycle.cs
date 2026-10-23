using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityPoleEventTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task CompleteNumberedStartReplacesListAndOwnerWithoutResettingFurnitureOrSending(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = scope.Active(kind, 2, 900);
        var old = Scores(kind);
        var row = Row(kind, 7, 88);
        old.Add(7u, row);
        Set(kind, "RoundOwner", row);
        Set(kind, "Proces", LegacyNativeProcesType.Idle);
        Call(kind, "Start");
        Assert.Null(Get(kind, "RoundOwner"));
        Assert.NotSame(old, Scores(kind));
        Assert.Empty(Scores(kind));
        Assert.Same(row, old[7u]);
        Assert.Equal(LegacyNativeProcesType.Alive, Get<LegacyNativeProcesType>(kind, "Proces"));
        Assert.Equal(2, pole.HitPoints);
        Assert.Equal("Previous", pole.Name);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task CompleteEndRetainsRoundOwnerAndFurnitureButClearsOriginalListInPlace(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pole = scope.Active(kind, 2, 900);
        var row = Row(kind, 7, 88);
        var old = Scores(kind);
        old.Add(7u, row);
        Set(kind, "RoundOwner", row);
        Call(kind, "End");
        Assert.Equal(LegacyNativeProcesType.Dead, Get<LegacyNativeProcesType>(kind, "Proces"));
        Assert.Same(old, Scores(kind));
        Assert.Empty(old);
        Assert.Same(row, Get(kind, "RoundOwner"));
        Assert.Equal(2, pole.HitPoints);
        Assert.Equal("Previous", pole.Name);
        if (kind == 0)
            Assert.Equal("NobilityPoleWar  has ended , Go Claim ur Prize <3.", Message(Assert.Single(scope.Queue)));
        else
            Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0, 2)]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 2)]
    [InlineData(3, 3)]
    public Task EndGuardRetainsDeadAndIdleScoresWithoutAttemptingAnyPoolAccess(int kind, int state) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Set(kind, "Proces", (LegacyNativeProcesType)state);
        var row = Row(kind, 7, 88);
        Scores(kind).Add(7u, row);
        LegacyNativePoolClients.GamePoll = null;
        Call(kind, "End");
        Assert.Equal((LegacyNativeProcesType)state, Get<LegacyNativeProcesType>(kind, "Proces"));
        Assert.Same(row, Scores(kind)[7u]);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task KingEndWithoutRoundOwnerUsesRealNoWinnerAnnouncement() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Active(0);
        Scores(0).Add(7u, Row(0, 7, 88));
        Assert.Null(Get(0, "RoundOwner"));
        Call(0, "End");
        Assert.Empty(Scores(0));
        Assert.Equal("NobilityPoleWar has ended and there is no winner", Message(Assert.Single(scope.Queue)));
    });

    [Fact]
    public Task KingEndGlobalFaultCommitsDeadButDoesNotClearScoresOrRoundAlias() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Active(0);
        var row = Row(0, 7, 88);
        Scores(0).Add(7u, row);
        Set(0, "RoundOwner", row);
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => Call(0, "End"));
        Assert.Equal(LegacyNativeProcesType.Dead, Get<LegacyNativeProcesType>(0, "Proces"));
        Assert.Same(row, Scores(0)[7u]);
        Assert.Same(row, Get(0, "RoundOwner"));
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task WholeNumberedCheckUpIdleGuardHasNoClockDependentSideEffects(int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Set(kind, "Proces", LegacyNativeProcesType.Idle);
        var row = Row(kind, 7, 88);
        Scores(kind).Add(7u, row);
        LegacyNativePoolClients.GamePoll = null;
        Call(kind, "CheckUP");
        Assert.Equal(LegacyNativeProcesType.Idle, Get<LegacyNativeProcesType>(kind, "Proces"));
        Assert.Same(row, Scores(kind)[7u]);
        Assert.Empty(scope.Queue);
        // This is the actual whole-method idle guard, NOT proof of the8AM timer arms.
    });
}
