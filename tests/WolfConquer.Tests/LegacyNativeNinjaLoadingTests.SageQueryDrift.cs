using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("client-slot")] [InlineData("vector")] [InlineData("view")]
    [InlineData("uid")] [InlineData("user")] [InlineData("map-view")]
    public Task SageDuplicateFinalMembershipComparerCannotPublishThroughRetiredBindings(string drift)
        => NativePacketTestThread.Run(() =>
    {
        using var random = new SageQueryOwnedRandom();
        using var scope = new Scope();
        var client = scope.Client; var player = client.Player; var vector = player.BitVector;
        var originalView = player.View;
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        client.Map = map;
        var originalMapView = map.View;
        var foreignFrames = new Queue<byte[]>();
        var foreign = NativeSpellTestFixture.CreateOwner(foreignFrames);
        foreign.Player.UID = 999;
        var replacementVector = new Vector(576);
        Assert.True(vector.TryAdd((int)SageFlag.SageSpeedMode, 77, false, 7));
        var row = vector.ArrayFlags[(int)SageFlag.SageSpeedMode];
        var seconds = row.Secounds; var invoker = row.InvokerSecouds;
        var timer = row.Timer; var invokeTimer = row.TimerInvoker;
        var comparer = new SageFinalMembershipDriftComparer();
        vector.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(vector.ArrayFlags, comparer);
        var registrations = vector.ArrayFlags; var entries = registrations.ToArray();
        var words = vector.bits; var bytes = words.ToArray();
        var cache = SageQueryDriftCache.GetValue(vector);
        SageAdmissionSnapshot? afterDrift = null;
        comparer.OnFinalMembership = () =>
        {
            comparer.AllGatesHeld = Monitor.IsEntered(client.SpellAdmissionSyncRoot) &&
                Monitor.IsEntered(player.SpellAdmissionSyncRoot) && Monitor.IsEntered(vector.SyncRoot);
            switch (drift)
            {
                case "client-slot": client.Player = foreign.Player; break;
                case "vector": player.BitVector = replacementVector; break;
                case "view": player.View = foreign.Player.View; break;
                case "uid": player.UID = 999; break;
                case "user": scope.Ninja.user = foreign; break;
                case "map-view": map.View = null!; break;
                default: throw new ArgumentOutOfRangeException(nameof(drift));
            }
            // The comparer-induced mutation is the test stimulus, not an effect to undo.
            // Capture after it, while the genuine membership query is still on stack.
            afterDrift = SageAdmissionSnapshot.Capture(scope, player);
        };
        comparer.Armed = true;

        scope.Ninja.SageMode(999);

        Assert.True(comparer.ReturnedAfterDrift);
        Assert.True(comparer.AllGatesHeld);
        Assert.Equal(new[] { (int)SageFlag.SageSpeedMode, (int)SageFlag.SageSpeedMode }, comparer.HashQueries);
        Assert.NotNull(afterDrift); afterDrift.Unchanged(scope, player);
        Assert.Same(registrations, vector.ArrayFlags); Assert.Equal(entries, vector.ArrayFlags.ToArray());
        Assert.Same(words, vector.bits); Assert.Equal(bytes, vector.bits);
        Assert.Same(cache, SageQueryDriftCache.GetValue(vector));
        Assert.Same(row, Assert.Single(vector.ArrayFlags).Value);
        Assert.Equal((int)SageFlag.SageSpeedMode, row.Key); Assert.Equal(seconds, row.Secounds);
        Assert.Equal(invoker, row.InvokerSecouds); Assert.Equal(timer, row.Timer);
        Assert.Equal(invokeTimer, row.TimerInvoker); Assert.False(row.RemoveOnDead);
        Assert.Same(drift == "client-slot" ? foreign.Player : player, client.Player);
        Assert.Same(drift == "vector" ? replacementVector : vector, player.BitVector);
        Assert.Same(drift == "view" ? foreign.Player.View : originalView, player.View);
        Assert.Equal(drift == "uid" ? 999u : 17u, player.UID);
        Assert.Same(drift == "user" ? foreign : client, scope.Ninja.user);
        Assert.Same(map, client.Map);
        if (drift == "map-view") Assert.Null(map.View); else Assert.Same(originalMapView, map.View);
        Assert.Empty(replacementVector.ArrayFlags); Assert.Equal(new uint[18], replacementVector.bits);
        Assert.Empty(foreignFrames);
    });

    private static readonly FieldInfo SageQueryDriftCache = typeof(Vector).GetField("Array",
        BindingFlags.NonPublic | BindingFlags.Instance)!;

    private sealed class SageFinalMembershipDriftComparer : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool AllGatesHeld;
        internal bool ReturnedAfterDrift;
        internal Action? OnFinalMembership;
        internal List<int> HashQueries { get; } = [];
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value)
        {
            if (Armed)
            {
                HashQueries.Add(value);
                // One genuine Sage row: IsCurrent.TryGetValue is read1, then the
                // final present=true ContainsKey is read2. No exception is injected.
                if (HashQueries.Count == 2)
                {
                    OnFinalMembership!();
                    ReturnedAfterDrift = true;
                }
            }
            return value.GetHashCode();
        }
    }

    private sealed class SageQueryOwnedRandom : IDisposable
    {
        private readonly LegacyNativeSafeRandom prior = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime priorReset = LegacyNativePoolRandom.ResetRandom;
        internal SageQueryOwnedRandom() => LegacyNativePoolRandom.GetRandom = new(4191);
        public void Dispose()
        {
            LegacyNativePoolRandom.GetRandom = prior;
            LegacyNativePoolRandom.ResetRandom = priorReset;
        }
    }
}
