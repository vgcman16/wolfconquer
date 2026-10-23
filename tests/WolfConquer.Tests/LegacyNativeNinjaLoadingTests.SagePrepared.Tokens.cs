using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("client")] [InlineData("player")]
    public Task SageTypedCommitRequiresBothPhysicalAdmissionGatesWithoutConsumingUnattemptedToken(string missingGate)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
        using var publication = PrepareBoundSageToken(bindings, 60, out var token);
        var before = SagePreparedState.Capture(player);
        if (missingGate == "client")
        {
            lock (player.SpellAdmissionSyncRoot)
            lock (vector.SyncRoot) Assert.False(vector.TryCommitSage(token!, bindings, publication));
        }
        else
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (vector.SyncRoot) Assert.False(vector.TryCommitSage(token!, bindings, publication));
        }
        before.Unchanged(player); Assert.Empty(scope.Queue);
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot) Assert.True(vector.TryCommitSage(token!, bindings, publication));
        Assert.True(vector.ContainFlag((int)SageFlag.SageSpeedMode));
        Assert.Empty(scope.Queue); // Typed-commit helper control, not a whole publication claim.
    });

    [Fact]
    public Task SageTypedFinalOwnershipRefusalConsumesPrivateTokenAndCannotReplayAfterRestoration()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSageModeBindings(scope.Ninja, client, player, vector, player.View);
            Assert.True(vector.TryPrepareAdd((int)SageFlag.SageSpeedMode, 60, true, 0, false, out var token));
            using var publication = LegacyNativePreparedSagePublication.Create(bindings, 60, token);
            var before = SagePreparedState.Capture(player);
            player.UID = 99;
            Assert.False(vector.TryCommitSage(token!, bindings, publication));
            before.Unchanged(player); Assert.Equal(99u, player.UID);
            player.UID = 17;
            Assert.False(vector.TryCommitSage(token!, bindings, publication));
            Assert.False(vector.TryGetPreparedWords(token, out var unavailable)); Assert.Empty(unavailable);
            before.Unchanged(player); Assert.Empty(scope.Queue);
        }
    });

    private static LegacyNativePreparedSagePublication PrepareBoundSageToken(LegacyNativeSageModeBindings bindings,
        uint timer, out LegacyNativeStatusFlagsBigVector32.PreparedChange? token)
    {
        lock (bindings.Vector.SyncRoot)
        {
            Assert.True(bindings.Vector.TryPrepareAdd((int)SageFlag.SageSpeedMode, unchecked((int)timer), true, 0, false, out token));
            return LegacyNativePreparedSagePublication.Create(bindings, timer, token);
        }
    }
}
