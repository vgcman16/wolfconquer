using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task PlayerBoundFlagCommitRequiresBothAdmissionGatesWithoutConsumingTheToken(bool missingPlayer)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var vector = s.Player.BitVector;
        LegacyNativeStatusFlagsBigVector32.PreparedChange? token;
        lock (vector.SyncRoot)
            Assert.True(vector.TryPrepareAdd((int)Flag.TeamLeader, 19, true, 0, false, out token));
        var registry = vector.ArrayFlags; var words = vector.bits;
        if (missingPlayer)
        {
            lock (s.Client.SpellAdmissionSyncRoot)
            lock (vector.SyncRoot)
                Assert.False(token!.TryCommit(vector, s.Player, s.Client, s.Player.View, s.Player.UID));
        }
        else
        {
            lock (s.Player.SpellAdmissionSyncRoot)
            lock (vector.SyncRoot)
                Assert.False(token!.TryCommit(vector, s.Player, s.Client, s.Player.View, s.Player.UID));
        }
        Assert.Same(registry, vector.ArrayFlags); Assert.Empty(registry);
        Assert.Same(words, vector.bits); Assert.All(words, word => Assert.Equal(0u, word));
        lock (s.Client.SpellAdmissionSyncRoot)
        lock (s.Player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
            Assert.True(token!.TryCommit(vector, s.Player, s.Client, s.Player.View, s.Player.UID));
        Assert.True(vector.ContainFlag((int)Flag.TeamLeader)); Assert.Empty(s.Queue);
        // Component-only commitment proof, not an emitted frame or full action.
    });
}
