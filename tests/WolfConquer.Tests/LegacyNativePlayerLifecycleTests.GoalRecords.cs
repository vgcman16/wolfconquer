using System.Globalization;
using WolfConquer.Core.World;
using GoalOwner = WolfConquer.Core.World.LegacyNativeHeroRewardsRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    [Theory]
    [InlineData(null)] [InlineData("garbage")] [InlineData("-1/101/1/")] [InlineData("+1/101/1/")]
    [InlineData("1/")] [InlineData("2/103/1/104/")] [InlineData("2/103/1/invalid/2/")]
    [InlineData("1/0/1/")] [InlineData("1/-1/1/")] [InlineData("1/4294967296/1/")]
    [InlineData("1/103/256/")] [InlineData("1/103/-1/")] [InlineData("1/103/invalid/")]
    [InlineData("1/103//")] [InlineData("1/103/1/extra")] [InlineData("1/103/1//")]
    [InlineData("0/103/1/")] [InlineData("2147483647/")] [InlineData("65536/")]
    public Task GoalLoadRejectsWholeMalformedRecordWithoutPublishingItsValidPrefix(string? record)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope);
        var goals = scope.Client.HeroRewards!; goals.AddGoal(101); goals.ArrayGoals[101].Claim = 7;
        var snapshot = new GoalAdmissionSnapshot(scope);
        Assert.False(goals.TryLoad(record)); snapshot.AssertUnchanged(scope);
        Assert.Throws<InvalidDataException>(() => goals.Load(record!)); snapshot.AssertUnchanged(scope);
    });

    [Theory]
    [InlineData("", "1/101/7/")] [InlineData("0", "1/101/7/")] [InlineData("0/", "1/101/7/")]
    [InlineData("1/103/255", "2/101/7/103/255/")]
    [InlineData("3/101/9/103/4/103/8/", "2/101/7/103/4/")]
    [InlineData("1/4294967295/255/", "2/101/7/4294967295/255/")]
    public Task GoalLoadKeepsCountedGrammarFirstIncomingAndExistingPhysicalClaimAliases(string record, string expected)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var goals = scope.Client.HeroRewards!;
        goals.AddGoal(101); var prior = goals.ArrayGoals[101]; prior.Claim = 7; prior.UnKnow = 77;
        Assert.True(goals.TryLoad(record)); Assert.Equal(expected, goals.ToString());
        Assert.Same(prior, goals.ArrayGoals[101]); Assert.Equal((byte)7, prior.Claim); Assert.Equal(77u, prior.UnKnow);
        var reloadedClient = new LegacyNativeGameClientRuntime(); var reloaded = new GoalOwner(reloadedClient);
        reloaded.Load(goals.ToString()); Assert.Equal(expected, reloaded.ToString());
        Assert.Empty(scope.Queue); Assert.Equal(0, scope.Player.XPCount);
    });

    [Fact]
    public Task GoalRecordInputLengthBoundRefusesBeforeParsingOrBookMutation()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope); var snapshot = new GoalAdmissionSnapshot(scope);
        Assert.False(scope.Client.HeroRewards!.TryLoad(new string('/', 1024 * 1024 + 1))); snapshot.AssertUnchanged(scope);
    });

    [Theory]
    [InlineData("tr-TR")] [InlineData("ar-SA")]
    public Task CanonicalGoalRecordAndMaximumByteClaimRemainCultureIndependent(string culture)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            scope.Client.HeroRewards!.Load("1/4294967295/255/");
            Assert.Equal("1/4294967295/255/", scope.Client.HeroRewards.ToString());
        }
        finally { CultureInfo.CurrentCulture = prior; }
    });

    [Fact]
    public Task RequiredGoalOwnerCanBeExplicitlyPreparedBeforeSlotAssignmentButNeverAdoptsItself()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = new LegacyNativeGameClientRuntime(); var goals = new GoalOwner(client);
        Assert.Null(client.HeroRewards);
        goals.AddGoal(101); goals.Load("1/102/3/");
        Assert.Null(client.HeroRewards); Assert.Equal("2/101/0/102/3/", goals.ToString());
        Assert.False(client.Player.TrySetXpCount(20)); Assert.Equal(0, client.Player.XPCount);
        client.HeroRewards = goals;
        Assert.True(client.Player.TrySetXpCount(20)); Assert.True(goals.ContainGoal(103));
    });

    [Fact]
    public Task MalformedPhysicalGoalGraphCannotAuthorizeMembershipOrStageQueries()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var goals = scope.Client.HeroRewards!;
        goals.ArrayGoals[101] = new() { UID = 999, Claim = 1 };
        var snapshot = new GoalAdmissionSnapshot(scope);
        Assert.Throws<InvalidDataException>(() => goals.ContainGoal(101));
        Assert.Throws<InvalidDataException>(() => goals.FinishStage(1));
        Assert.Throws<InvalidDataException>(() => goals.ComplateFullStage(1));
        Assert.Throws<InvalidDataException>(() => goals.ToString()); snapshot.AssertUnchanged(scope);
    });

    [Fact]
    public Task HeroStageCompletionDoesNotWrapMaximumRequiredGoalCountIntoZero()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var tables = new NativeSaveRecordTables();
        LegacyNativePoolSaveRecords.TableHeroRewards[1] = new() { UID = 1, CountGoals = uint.MaxValue };
        Assert.False(scope.Client.HeroRewards!.ComplateFullStage(1)); Assert.False(scope.Client.HeroRewards.FinishStage(1));
        Assert.Empty(scope.Queue); Assert.Equal(0, scope.Player.XPCount);
    });
}
