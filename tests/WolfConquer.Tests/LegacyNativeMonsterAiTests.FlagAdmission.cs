using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAiTests
{
    [Theory]
    [InlineData("vector")] [InlineData("words")] [InlineData("dictionary")]
    [InlineData("bitmap")] [InlineData("key")] [InlineData("deadline")] [InlineData("alias")]
    public Task SearchUnavailableFlightInvalidatesStaleAttackBeforeMovementOrRandom(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = scope.Monster(); var peerFrames = new Queue<byte[]>();
        var peer = scope.Peer(31, 41, 40, peerFrames);
        role.Target = peer.Player; role.State = LegacyNativeMobStatus.Attacking;
        var flags = CorruptTargetStatus(peer.Player, corruption);
        var before = TargetMotionSnapshot.Capture(scope, role);
        scope.Ai.SearchTarget(scope.Player, role);
        Assert.Null(role.Target); Assert.Equal(LegacyNativeMobStatus.Idle, role.State);
        flags.Unchanged(peer.Player); before.Unchanged(scope, role);
        Assert.Empty(peerFrames);
    });

    [Theory]
    [InlineData("vector")] [InlineData("words")] [InlineData("dictionary")]
    [InlineData("bitmap")] [InlineData("key")] [InlineData("deadline")] [InlineData("alias")]
    public Task CheckUnavailableTriggerCannotAuthorizeTargetSelection(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = scope.Monster();
        role.State = LegacyNativeMobStatus.Attacking;
        var flags = CorruptTargetStatus(scope.Player, corruption);
        var before = TargetMotionSnapshot.Capture(scope, role);
        scope.Ai.CheckTarget(scope.Player, role);
        Assert.Null(role.Target); Assert.Equal(LegacyNativeMobStatus.Idle, role.State);
        flags.Unchanged(scope.Player); before.Unchanged(scope, role);
    });

    [Theory]
    [InlineData("vector")] [InlineData("words")] [InlineData("dictionary")]
    [InlineData("bitmap")] [InlineData("key")] [InlineData("deadline")] [InlineData("alias")]
    public Task CheckUnavailableHighestIndexCandidateIsNeverPublished(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = scope.Monster(); var peerFrames = new Queue<byte[]>();
        var peer = scope.Peer(31, 41, 40, peerFrames);
        Assert.Same(peer.Player, role.View.GetTarget(scope.Map, LegacyNativeMapObjectType.Player));
        role.State = LegacyNativeMobStatus.Attacking;
        var flags = CorruptTargetStatus(peer.Player, corruption);
        var before = TargetMotionSnapshot.Capture(scope, role);
        scope.Ai.CheckTarget(scope.Player, role);
        Assert.Null(role.Target); Assert.Equal(LegacyNativeMobStatus.Idle, role.State);
        flags.Unchanged(peer.Player); before.Unchanged(scope, role);
        Assert.Empty(peerFrames);
    });

    [Theory]
    [InlineData("vector")] [InlineData("words")] [InlineData("dictionary")]
    [InlineData("bitmap")] [InlineData("key")] [InlineData("deadline")] [InlineData("alias")]
    public Task CheckUnavailableExistingTargetCannotRetainAttackingState(string corruption)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = scope.Monster(); var peerFrames = new Queue<byte[]>();
        var peer = scope.Peer(31, 41, 40, peerFrames);
        role.Target = peer.Player; role.State = LegacyNativeMobStatus.Attacking;
        var flags = CorruptTargetStatus(peer.Player, corruption);
        var before = TargetMotionSnapshot.Capture(scope, role);
        scope.Ai.CheckTarget(scope.Player, role);
        Assert.Null(role.Target); Assert.Equal(LegacyNativeMobStatus.Idle, role.State);
        flags.Unchanged(peer.Player); before.Unchanged(scope, role);
        Assert.Empty(peerFrames);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MalformedFallbackTailCannotPublishEarlierValidCandidateOrPartialDecision(bool queryFailure)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var role = scope.Monster();
        var corruptFrames = new Queue<byte[]>(); var flyingFrames = new Queue<byte[]>();
        var corrupt = scope.Peer(31, 41, 40, corruptFrames);
        var flying = scope.Peer(32, 42, 40, flyingFrames);
        Assert.True(flying.Player.BitVector.TryAdd((int)Flag.Fly, 30, false, 0));
        Assert.Same(flying.Player, role.View.GetTarget(scope.Map, LegacyNativeMapObjectType.Player));
        Assert.Equal(new[] { scope.Player, corrupt.Player, flying.Player },
            role.View.Roles(scope.Map, LegacyNativeMapObjectType.Player).Cast<LegacyNativePlayerRuntime>());
        role.State = LegacyNativeMobStatus.Attacking;
        TargetStatusQueryProbe? probe = null;
        if (queryFailure)
        {
            Assert.True(corrupt.Player.BitVector.TryAdd((int)Flag.TeamLeader, 19, true, 3));
            probe = new(role);
            corrupt.Player.BitVector.ArrayFlags = new(corrupt.Player.BitVector.ArrayFlags, probe);
        }
        else CorruptTargetStatus(corrupt.Player, "words");
        var flags = TargetStatusSnapshot.Capture(corrupt.Player);
        var before = TargetMotionSnapshot.Capture(scope, role);
        if (probe is not null) probe.Armed = true;
        scope.Ai.CheckTarget(scope.Player, role);
        Assert.Null(role.Target); Assert.Equal(LegacyNativeMobStatus.Idle, role.State);
        if (probe is not null)
        {
            Assert.Equal(1, probe.Reads);
            Assert.Null(probe.ObservedTarget);
            Assert.Equal(LegacyNativeMobStatus.Attacking, probe.ObservedState);
        }
        flags.Unchanged(corrupt.Player); before.Unchanged(scope, role);
        Assert.Empty(corruptFrames); Assert.Empty(flyingFrames);
    });
}
