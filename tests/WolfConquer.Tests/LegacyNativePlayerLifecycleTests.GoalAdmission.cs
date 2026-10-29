using WolfConquer.Core.World;
using GoalOwner = WolfConquer.Core.World.LegacyNativeHeroRewardsRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    [Theory]
    [InlineData("missing")] [InlineData("foreign")] [InlineData("book")]
    [InlineData("row")]
    [InlineData("key")] [InlineData("zero")] [InlineData("alias")]
    public Task XpTwentyRequiresItsCurrentWellFormedGoalOwnerBeforeAnyMutation(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope);
        CorruptGoalOwner(scope, corruption); var snapshot = new GoalAdmissionSnapshot(scope);
        Assert.False(scope.Player.TrySetXpCount(20)); snapshot.AssertUnchanged(scope);
        Assert.Throws<InvalidDataException>(() => scope.Player.XPCount = 20); snapshot.AssertUnchanged(scope);
    });

    [Theory]
    [InlineData(0)] [InlineData(19)] [InlineData(21)] [InlineData(65535)]
    public Task OtherXpValuesDoNotReadEvenAnUnavailableGoalDictionary(ushort value)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope);
        var comparer = InstallGoalComparer(scope, "throw", 1);
        var snapshot = new GoalAdmissionSnapshot(scope); comparer.Armed = true;
        Assert.True(scope.Player.TrySetXpCount(value)); snapshot.AssertUnchanged(scope, value);
        Assert.False(comparer.Fired); Assert.Equal(0, comparer.Reads);
        scope.Client.HeroRewards = null;
        scope.Player.XPCount = value; Assert.Equal(value, scope.Player.XPCount);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task XpTwentyStagesRequiredGoalAndPreservesExistingRowsClaimsAndOpaqueValues(bool duplicate)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope);
        var goals = scope.Client.HeroRewards!;
        goals.AddGoal(101); var prior = goals.ArrayGoals[101]; prior.Claim = 7; prior.UnKnow = uint.MaxValue;
        GoalOwner.Item? existing = null;
        if (duplicate)
        {
            goals.AddGoal(103); existing = goals.ArrayGoals[103]; existing.Claim = 255; existing.UnKnow = 91;
        }
        var oldBook = goals.ArrayGoals;
        Assert.True(scope.Player.TrySetXpCount(20)); Assert.Equal(20, scope.Player.XPCount);
        Assert.Same(prior, goals.ArrayGoals[101]); Assert.Equal((byte)7, prior.Claim); Assert.Equal(uint.MaxValue, prior.UnKnow);
        var goal = goals.ArrayGoals[103]; Assert.Equal(103u, goal.UID);
        if (duplicate)
        { Assert.Same(existing, goal); Assert.Same(oldBook, goals.ArrayGoals); Assert.Equal((byte)255, goal.Claim); Assert.Equal(91u, goal.UnKnow); }
        else
        { Assert.NotSame(oldBook, goals.ArrayGoals); Assert.Equal((byte)0, goal.Claim); Assert.Equal(0u, goal.UnKnow); }
        Assert.Equal(new uint[] { 101, 103 }, goals.ArrayGoals.Keys);
        Assert.Equal(1, scope.Queue.Count); Assert.Equal((byte)67, scope.Player.XPBooster); Assert.Equal((byte)43, scope.Player.XPKiller);
    });

    [Theory]
    [InlineData(0u)] [InlineData(17u)] [InlineData(uint.MaxValue)]
    public Task XpGoalTransactionUsesExactCurrentOwnerNotANonzeroUidHeuristic(uint uid)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.UID = uid;
        scope.Player.XPCount = 20;
        Assert.Equal(20, scope.Player.XPCount); Assert.True(scope.Client.HeroRewards!.ContainGoal(103)); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(19)] [InlineData(20)]
    public Task DetachedPlayerCannotMutateItsCounterOrTheCurrentCharactersGoalBook(ushort value)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope); var oldPlayer = scope.Player;
        scope.Client.Player = new LegacyNativePlayerRuntime(scope.Client) { UID = oldPlayer.UID };
        var snapshot = new GoalAdmissionSnapshot(scope, oldPlayer);
        Assert.False(oldPlayer.TrySetXpCount(value)); snapshot.AssertUnchanged(scope);
    });

    [Fact]
    public Task GoalOwnerConstructionDoesNotInventAPlayerBeforeTheNativeBirthPhase()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = new LegacyNativeGameClientRuntime(); client.Player = null!;
        var goals = new GoalOwner(client); Assert.Null(client.Player); Assert.Empty(goals.ArrayGoals);
        client.HeroRewards = goals; client.Player = new LegacyNativePlayerRuntime(client);
        client.Player.XPCount = 20;
        Assert.Equal(20, client.Player.XPCount); Assert.True(goals.ContainGoal(103));
        Assert.Throws<ArgumentNullException>(() => new GoalOwner(null!));
    });
}
