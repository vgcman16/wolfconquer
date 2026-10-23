using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    private sealed class DurationLookupComparer(LegacyNativePlayerRuntime player, string mode) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal int Reads;
        internal LegacyNativePlayerRuntime? ReplacementPlayer;
        internal Flags? ReplacementVector;
        internal int FinalKey;
        internal Flags.Flag? EarlierRow;
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value)
        {
            if (!Armed || (mode == "earlier-row" && value != FinalKey)) return value.GetHashCode();
            Armed = false; Reads++;
            switch (mode)
            {
                case "throw": throw new InvalidOperationException("Owned duration lookup failure.");
                case "uid": player.UID++; break;
                case "player": player.Owner.Player = ReplacementPlayer!; break;
                case "vector": player.BitVector = ReplacementVector!; break;
                case "earlier-row": EarlierRow!.Secounds = 99; break;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
            return value.GetHashCode();
        }
    }

    [Theory]
    [InlineData("throw")] [InlineData("uid")] [InlineData("player")] [InlineData("vector")]
    public Task DurationCannotCommitAfterRealComparerFailureOrExternallyChangedBinding(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player; scope.Flag(Flag.Cyclone);
        var vector = player.BitVector; var oldUid = player.UID;
        var replacement = new LegacyNativePlayerRuntime(scope.Client) { UID = oldUid };
        var comparer = new DurationLookupComparer(player, mode)
        {
            ReplacementPlayer = replacement, ReplacementVector = replacement.BitVector
        };
        vector.ArrayFlags = new ConcurrentDictionary<int, Flags.Flag>(vector.ArrayFlags, comparer);
        var row = vector.ArrayFlags[(int)Flag.Cyclone]; var timer = row.Timer;
        var registry = vector.ArrayFlags; var words = vector.bits; var wordValues = words.ToArray();
        var snapshot = DurationState.Capture(scope, player); comparer.Armed = true;
        Assert.False(player.TryIncreaseXPDuration(5)); Assert.Equal(1, comparer.Reads);
        Assert.Equal(17, row.Secounds); Assert.Equal(timer, row.Timer);
        Assert.Same(registry, vector.ArrayFlags); Assert.Same(words, vector.bits); Assert.Equal(wordValues, vector.bits);
        Assert.Same(row, vector.ArrayFlags[(int)Flag.Cyclone]); Assert.Empty(scope.Queue);
        if (mode != "vector") snapshot.Unchanged(scope, player);
        if (mode == "uid") Assert.Equal(oldUid + 1, player.UID);
        else Assert.Equal(oldUid, player.UID);
        if (mode == "player") Assert.Same(replacement, scope.Client.Player);
        else Assert.Same(player, scope.Client.Player);
        if (mode == "vector")
        {
            Assert.Same(replacement.BitVector, player.BitVector);
            Assert.Empty(replacement.BitVector.ArrayFlags);
        }
        else Assert.Same(vector, player.BitVector);
        // Fixture raw writes are not rolled back. The invariant is that the
        // rejected operation contributes no duration delta or packet effects.
    });

    [Fact]
    public Task FinalPhysicalRecaptureFindsEarlierRowChangedByLastComparerLookup()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Flag(Flag.Cyclone); scope.Flag(Flag.TeamLeader, 13);
        var vector = scope.Player.BitVector;
        var comparer = new DurationLookupComparer(scope.Player, "earlier-row");
        vector.ArrayFlags = new ConcurrentDictionary<int, Flags.Flag>(vector.ArrayFlags, comparer);
        var entries = vector.ArrayFlags.ToArray();
        Assert.Equal(2, entries.Length); comparer.FinalKey = entries[^1].Key; comparer.EarlierRow = entries[0].Value;
        var cyclone = vector.ArrayFlags[(int)Flag.Cyclone]; var timer = cyclone.Timer;
        comparer.Armed = true;
        Assert.False(scope.Player.TryIncreaseXPDuration(5)); Assert.Equal(1, comparer.Reads);
        Assert.Equal(99, comparer.EarlierRow.Secounds); // external raw writer, not an operation commit
        Assert.Equal(ReferenceEquals(cyclone, comparer.EarlierRow) ? 99 : 17, cyclone.Secounds);
        Assert.Equal(timer, cyclone.Timer); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task ComponentCannotBypassRealClientPlayerAndVectorGateOwnership()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Flag(Flag.Cyclone);
        var vector = scope.Player.BitVector; var snapshot = DurationState.Capture(scope);
        Assert.False(vector.TryAdjustFirstDuration([(int)Flag.Cyclone], (int)Flag.Normal, 5,
            scope.Player, scope.Client, scope.Player.UID)); snapshot.Unchanged(scope);
        lock (vector.SyncRoot)
        {
            Assert.False(vector.TryAdjustFirstDuration([(int)Flag.Cyclone], (int)Flag.Normal, 5,
                scope.Player, scope.Client, scope.Player.UID)); snapshot.Unchanged(scope);
        }
    });

    [Fact]
    public Task CommittedDurationInvalidatesAnOlderGenuinePreparedFlagChange()
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Flag(Flag.Cyclone);
        var vector = scope.Player.BitVector;
        lock (scope.Client.SpellAdmissionSyncRoot)
        lock (scope.Player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            Assert.True(vector.TryPrepareAdd((int)Flag.TeamLeader, 30, false, 0, false, out var prepared));
            Assert.True(scope.Player.TryIncreaseXPDuration(5));
            Assert.False(prepared!.TryCommit(vector));
        }
        Assert.Equal(22, vector.ArrayFlags[(int)Flag.Cyclone].Secounds);
        Assert.False(vector.ContainFlag((int)Flag.TeamLeader)); Assert.Empty(scope.Queue);
    });
}
