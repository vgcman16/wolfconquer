using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    [Theory]
    [InlineData("throw")] [InlineData("player")] [InlineData("uid")]
    [InlineData("goals")] [InlineData("retire")] [InlineData("book")]
    public Task LateRealGoalComparerFailureOrOwnerDriftCannotCommitCounterOrNewGoal(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope); var player = scope.Player;
        var goals = scope.Client.HeroRewards!; uint uid = player.UID;
        var comparer = InstallGoalComparer(scope, mode); var snapshot = new GoalAdmissionSnapshot(scope, player, goals);
        comparer.Armed = true;
        Assert.False(player.TrySetXpCount(20));
        Assert.True(comparer.Fired); Assert.Equal(2, comparer.Reads);
        Assert.True(comparer.HeldClient); Assert.True(comparer.HeldPlayer); Assert.True(comparer.HeldBook);
        snapshot.AssertUnchanged(scope, requireBookBinding: mode != "book");
        Assert.Same(mode == "player" ? comparer.ReplacementPlayer : player, scope.Client.Player);
        Assert.Equal(mode == "uid" ? uid + 1 : uid, player.UID);
        if (mode == "goals") Assert.Same(comparer.ReplacementGoals, scope.Client.HeroRewards);
        else if (mode == "retire") Assert.Null(scope.Client.HeroRewards);
        else Assert.Same(goals, scope.Client.HeroRewards);
        if (mode == "book") Assert.Same(comparer.ReplacementBook, goals.ArrayGoals);
        // Intentional comparer raw changes remain visible; they are not undone
        // or certified safe. The refused transaction adds no counter/goal effect.
    });

    [Fact]
    public Task RealNewGoalMaterializationFailureLeavesPriorCounterAndBookIntact()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope);
        var comparer = InstallGoalComparer(scope, "throw", 1); comparer.TriggerKey = 103;
        var snapshot = new GoalAdmissionSnapshot(scope); comparer.Armed = true;
        Assert.False(scope.Player.TrySetXpCount(20)); Assert.True(comparer.Fired); Assert.Equal(1, comparer.Reads);
        snapshot.AssertUnchanged(scope);
    });

    [Fact]
    public Task FinalGoalLookupCannotHideAnEarlierPhysicalClaimMutation()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope);
        var comparer = InstallGoalComparer(scope, "earlier-row"); var goals = scope.Client.HeroRewards!;
        goals.ArrayGoals.Add(102, new() { UID = 102, Claim = 3 });
        comparer.TriggerKey = 102; comparer.EarlierRow = goals.ArrayGoals[101];
        comparer.Armed = true;
        Assert.False(scope.Player.TrySetXpCount(20)); Assert.True(comparer.Fired); Assert.Equal(2, comparer.Reads);
        Assert.Equal(19, scope.Player.XPCount); Assert.Equal(2, goals.ArrayGoals.Count);
        Assert.Equal((byte)8, goals.ArrayGoals[101].Claim); Assert.False(goals.ArrayGoals.ContainsKey(103));
        Assert.Equal(1, scope.Queue.Count);
    });

    [Fact]
    public Task RealObservingComparerRetainsPositiveGoalAndCounterPublication()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var comparer = InstallGoalComparer(scope, "observe");
        var prior = scope.Client.HeroRewards!.ArrayGoals[101]; comparer.Armed = true;
        Assert.True(scope.Player.TrySetXpCount(20)); Assert.True(comparer.Fired); Assert.Equal(2, comparer.Reads);
        Assert.Equal(20, scope.Player.XPCount); Assert.Same(prior, scope.Client.HeroRewards.ArrayGoals[101]);
        Assert.True(scope.Client.HeroRewards.ContainGoal(103)); Assert.Empty(scope.Queue);
    });
}
