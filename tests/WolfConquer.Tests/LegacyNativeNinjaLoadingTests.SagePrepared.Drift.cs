using System.Collections.Concurrent;
using WolfConquer.Core.World;
using SageFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("client-slot", false)] [InlineData("client-slot", true)]
    [InlineData("vector", false)] [InlineData("vector", true)]
    [InlineData("view", false)] [InlineData("view", true)]
    [InlineData("uid", false)] [InlineData("uid", true)]
    [InlineData("user", false)] [InlineData("user", true)]
    [InlineData("map", false)] [InlineData("map", true)]
    [InlineData("map-view", false)] [InlineData("map-view", true)]
    [InlineData("replacement-map-view", false)] [InlineData("replacement-map-view", true)]
    [InlineData("archive-disposed", false)] [InlineData("archive-disposed", true)]
    public Task SagePreparedFinalRealComparerCannotCommitOrPublishThroughRetiredBindings(string drift, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var random = new SageQueryOwnedRandom();
        using var scope = new Scope(); var client = scope.Client; var player = client.Player;
        var vector = player.BitVector; var originalView = player.View;
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        client.Map = map; var originalMapView = map.View;
        var foreignFrames = new Queue<byte[]>(); var foreign = NativeSpellTestFixture.CreateOwner(foreignFrames);
        foreign.Player.UID = 999;
        var replacementVector = new Vector(576);
        int key = (int)(existing ? SageFlag.SageSpeedMode : SageFlag.TeamLeader);
        Assert.True(vector.TryAdd(key, 77, false, 7));
        using var pool = new SagePreparedPool();
        if (!existing) pool.Enqueue(pool.Packet(256));
        var archive = pool.Packet(256); pool.Enqueue(archive);
        var comparer = new SagePreparedFinalComparer(pool, existing ? 2 : 1);
        vector.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(vector.ArrayFlags, comparer);
        var before = SagePreparedState.Capture(player);
        comparer.OnFinalRead = () =>
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
                case "map": client.Map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) }; break;
                case "map-view": map.View = null!; break;
                case "replacement-map-view": map.View = new LegacyNativeMapView(72, 72); break;
                case "archive-disposed": archive.Dispose(); break;
                default: throw new ArgumentOutOfRangeException(nameof(drift));
            }
        };
        comparer.Armed = true;

        Assert.False(scope.Ninja.TryAdmitSageMode(900));

        Assert.True(comparer.ReturnedAfterDrift); Assert.True(comparer.AllGatesHeld);
        Assert.Equal(existing ? new[] { key, key } : new[] { key }, comparer.FinalReads);
        before.Unchanged(player);
        Assert.Equal(existing, vector.ArrayFlags.ContainsKey((int)SageFlag.SageSpeedMode));
        Assert.Empty(scope.Queue); Assert.Empty(foreignFrames); Assert.Empty(pool.Queue);
        Assert.Same(drift == "client-slot" ? foreign.Player : player, client.Player);
        Assert.Same(drift == "vector" ? replacementVector : vector, player.BitVector);
        Assert.Same(drift == "view" ? foreign.Player.View : originalView, player.View);
        Assert.Equal(drift == "uid" ? 999u : 17u, player.UID);
        Assert.Same(drift == "user" ? foreign : client, scope.Ninja.user);
        if (drift == "map") Assert.NotSame(map, client.Map); else Assert.Same(map, client.Map);
        if (drift == "map-view") Assert.Null(map.View);
        else if (drift == "replacement-map-view") Assert.NotSame(originalMapView, map.View);
        else Assert.Same(originalMapView, map.View);
        Assert.Empty(replacementVector.ArrayFlags); Assert.Equal(new uint[18], replacementVector.bits);
        for (int index = 0; index < 12; index++) scope.Untaken(index);
    });

    private sealed class SagePreparedFinalComparer(SagePreparedPool pool, int finalRead) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal bool AllGatesHeld;
        internal bool ReturnedAfterDrift;
        internal Action? OnFinalRead;
        internal List<int> FinalReads { get; } = [];
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value)
        {
            if (Armed && pool.Queue.IsEmpty)
            {
                FinalReads.Add(value);
                if (FinalReads.Count == finalRead)
                {
                    Armed = false;
                    OnFinalRead!();
                    ReturnedAfterDrift = true;
                }
            }
            return value.GetHashCode();
        }
    }
}
