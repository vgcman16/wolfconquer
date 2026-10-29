using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using GoalOwner = WolfConquer.Core.World.LegacyNativeHeroRewardsRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RetiredGoalOwnerCannotWriteLoadOrAnswerPermissionAfterItsSlotIsReplaced(bool load)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope); var goals = scope.Client.HeroRewards!;
        goals.AddGoal(101); var replacement = new GoalOwner(scope.Client);
        scope.Client.HeroRewards = replacement; var snapshot = new GoalAdmissionSnapshot(scope, capturedGoals: goals);
        if (load)
        {
            Assert.False(goals.TryLoad("1/103/4/")); Assert.Throws<InvalidDataException>(() => goals.Load("1/103/4/"));
        }
        else
        {
            Assert.False(goals.TryAddGoal(103)); Assert.Throws<InvalidDataException>(() => goals.AddGoal(103));
        }
        Assert.Throws<InvalidDataException>(() => goals.ContainGoal(103));
        snapshot.AssertUnchanged(scope); Assert.Same(replacement, scope.Client.HeroRewards); Assert.Empty(replacement.ArrayGoals);
    });

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public Task GoalWritersRecheckTheExactCapturedAssignedOrUnassignedSlotAfterComparerWork(bool load, bool unassigned)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope); var goals = scope.Client.HeroRewards!;
        var comparer = InstallGoalComparer(scope, "goals");
        if (unassigned) scope.Client.HeroRewards = null;
        var snapshot = new GoalAdmissionSnapshot(scope, capturedGoals: goals); comparer.Armed = true;
        Assert.False(load ? goals.TryLoad("1/103/4/") : goals.TryAddGoal(103));
        Assert.True(comparer.Fired); Assert.Equal(2, comparer.Reads); snapshot.AssertUnchanged(scope);
        Assert.Same(comparer.ReplacementGoals, scope.Client.HeroRewards);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task GoalTotalBookBoundRejectsNewPublicationButPreservesExistingGoalAliases(bool requiredPresent)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope); var goals = scope.Client.HeroRewards!;
        var book = new LegacyNativeDbSafeDictionary<uint, GoalOwner.Item>(65535);
        for (uint index = 0; index < 65535; index++)
        {
            uint uid = requiredPresent ? index + 1 : index + 1000;
            ((IDictionary<uint, GoalOwner.Item>)book).Add(uid, new() { UID = uid });
        }
        goals.ArrayGoals = book;
        var snapshot = new GoalAdmissionSnapshot(scope);
        Assert.Equal(requiredPresent, scope.Player.TrySetXpCount(20));
        snapshot.AssertUnchanged(scope, requiredPresent ? (ushort)20 : (ushort)19);
        Assert.True(goals.TryAddGoal(requiredPresent ? 1u : 1000u));
        Assert.False(goals.TryAddGoal(uint.MaxValue));
        Assert.False(goals.TryLoad("1/4294967295/0/"));
        snapshot.AssertUnchanged(scope, requiredPresent ? (ushort)20 : (ushort)19);
    });

    [Fact]
    public Task EscapedGoalSnapshotsCannotCorruptXpAdmissionOrReplaceItsStableGate()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope);
        var goals = scope.Client.HeroRewards!;
        goals.AddGoal(101);
        var book = goals.ArrayGoals; var row = book[101];
        object gate = book.SyncRoot; long revision = book.Revision;
        var values = book.GetValues(); var cached = book.MyArray;
        values[0] = null!; cached[0] = new() { UID = 999 };
        Assert.Same(gate, book.SyncRoot); Assert.Equal(revision, book.Revision);
        Assert.Same(row, Assert.Single(book.GetValues())); Assert.Same(row, book[101]);
        var before = new GoalAdmissionSnapshot(scope);

        Assert.True(scope.Player.TrySetXpCount(20));

        before.AssertUnchanged(scope, 20, requireBookBinding: false);
        Assert.Same(gate, book.SyncRoot); Assert.Equal(revision, book.Revision);
        Assert.NotSame(book, goals.ArrayGoals); Assert.Same(row, goals.ArrayGoals[101]);
        Assert.Equal(103u, goals.ArrayGoals[103].UID);
        Assert.Null(Assert.Single(values)); Assert.Equal(999u, Assert.Single(cached).UID);
    });

    [Fact]
    public Task GoalWriterDoesNotSilentlyRepairMalformedBookOrAdmitZeroGoal()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); SeedGoalPreimage(scope); var goals = scope.Client.HeroRewards!;
        goals.ArrayGoals[500] = null!; var snapshot = new GoalAdmissionSnapshot(scope);
        Assert.False(goals.TryAddGoal(103)); Assert.False(goals.TryAddGoal(0)); Assert.False(goals.TryLoad("1/103/1/"));
        Assert.Throws<InvalidDataException>(() => goals.AddGoal(103)); snapshot.AssertUnchanged(scope);
    });
}
