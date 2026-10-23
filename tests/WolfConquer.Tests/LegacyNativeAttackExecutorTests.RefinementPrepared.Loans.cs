using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData("mirror", "short")] [InlineData("killing", "short")] [InlineData("stamina", "short")]
    [InlineData("mirror", "null")] [InlineData("killing", "null")] [InlineData("stamina", "null")]
    [InlineData("mirror", "disposed")] [InlineData("killing", "disposed")] [InlineData("stamina", "disposed")]
    [InlineData("mirror", "alias")] [InlineData("killing", "alias")] [InlineData("stamina", "alias")]
    public Task RefinementAllLoansValidateBeforeTheFirstCopyOrChance(string kind, string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = 2;
        var before = RefinementStateSnapshot.Capture(s.Player);
        using var pool = new RefinementPool();
        int count = kind == "stamina" ? 2 : 3;
        var first = pool.Enqueue(); var firstBytes = RefinementStorage(first);
        for (int index = 1; index < count - 1; index++) pool.Enqueue();
        switch (fault)
        {
            case "short": pool.Enqueue(1); break;
            case "null": pool.Queue.Enqueue(null!); break;
            case "disposed": var disposed = pool.Enqueue(); disposed.Dispose(); break;
            case "alias": pool.Queue.Enqueue(first); break;
        }
        var rng = new LegacyNativeSafeRandom(123); LegacyNativePoolRandom.GetRandom = new(123);
        Assert.False(AdmitRefinement(s, kind));
        before.Unchanged(s.Player); Assert.Empty(s.Queue);
        Assert.Equal(firstBytes, RefinementStorage(first)); Assert.Equal(7, first.Size);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData("mirror", "size-large")] [InlineData("killing", "size-large")] [InlineData("stamina", "size-large")]
    [InlineData("mirror", "size-short")] [InlineData("killing", "size-short")] [InlineData("stamina", "size-short")]
    [InlineData("mirror", "cursor")] [InlineData("killing", "cursor")] [InlineData("stamina", "cursor")]
    [InlineData("mirror", "content")] [InlineData("killing", "content")] [InlineData("stamina", "content")]
    [InlineData("mirror", "dispose")] [InlineData("killing", "dispose")] [InlineData("stamina", "dispose")]
    public Task RefinementFinalComparerCannotCommitAfterPreparedLoanDrift(string kind, string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = 3;
        var vector = s.Player.BitVector; Assert.True(vector.TryAdd((int)Flag.TeamLeader, 7, false, 2));
        using var pool = new RefinementPool(); pool.Enqueue();
        if (kind != "stamina") pool.Enqueue();
        var last = pool.Enqueue();
        var comparer = new RefinementComparer(pool, kind == "stamina" ? 1 : 2);
        vector.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(vector.ArrayFlags, comparer);
        var before = RefinementStateSnapshot.Capture(s.Player);
        comparer.OnFinalRead = () =>
        {
            switch (fault)
            {
                case "size-large": last.Size = 1032; break;
                case "size-short": last.Size--; break;
                case "cursor": last.Seek(last.Capacity + 1); break;
                case "content": Marshal.WriteByte(RefinementAddress(last), 0, 0xAB); break;
                case "dispose": last.Dispose(); break;
            }
        };
        var rng = new LegacyNativeSafeRandom(125); LegacyNativePoolRandom.GetRandom = new(125); comparer.Armed = true;
        Assert.False(AdmitRefinement(s, kind));
        Assert.True(comparer.Fired); before.Unchanged(s.Player); Assert.Empty(s.Queue);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData("mirror", "player")] [InlineData("killing", "player")] [InlineData("stamina", "player")]
    [InlineData("mirror", "view")] [InlineData("killing", "view")] [InlineData("stamina", "view")]
    [InlineData("mirror", "uid")] [InlineData("killing", "uid")] [InlineData("stamina", "uid")]
    [InlineData("mirror", "vector")] [InlineData("killing", "vector")] [InlineData("stamina", "vector")]
    [InlineData("mirror", "row")] [InlineData("killing", "row")] [InlineData("stamina", "row")]
    [InlineData("mirror", "word")] [InlineData("killing", "word")] [InlineData("stamina", "word")]
    [InlineData("mirror", "map")] [InlineData("killing", "map")] [InlineData("stamina", "map")]
    [InlineData("mirror", "map-view")] [InlineData("killing", "map-view")] [InlineData("stamina", "map-view")]
    public Task RefinementLateComparerOwnerOrGraphDriftHasNoContributedGameplayOrPrefix(string kind, string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var player = s.Player; player.Stamina = 4;
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        s.Client.Map = map; player.Map = 1002;
        var foreign = s.Peer(new Queue<byte[]>());
        var vector = player.BitVector; Assert.True(vector.TryAdd((int)Flag.TeamLeader, 7, false, 2));
        var retained = vector.ArrayFlags[(int)Flag.TeamLeader]; var oldWords = vector.bits;
        var oldCache = RefinementCache.GetValue(vector);
        using var pool = new RefinementPool(); int count = kind == "stamina" ? 2 : 3;
        for (int index = 0; index < count; index++) pool.Enqueue();
        var comparer = new RefinementComparer(pool, kind == "stamina" ? 1 : 2);
        vector.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(vector.ArrayFlags, comparer);
        var registry = vector.ArrayFlags;
        comparer.OnFinalRead = () =>
        {
            switch (fault)
            {
                case "player": s.Client.Player = foreign.Player; break;
                case "view": player.View = new LegacyNativeRoleView(foreign); break;
                case "uid": player.UID++; break;
                case "vector": player.BitVector = new Vector(576); break;
                case "row": retained.Secounds++; break;
                case "word": oldWords[0] ^= 1u; break;
                case "map": s.Client.Map = null; break;
                case "map-view": map.View = new LegacyNativeMapView(72, 72); break;
            }
        };
        var rng = new LegacyNativeSafeRandom(127); LegacyNativePoolRandom.GetRandom = new(127); comparer.Armed = true;
        bool accepted = kind == "mirror" ? LegacyNativePerfectionEffects.TryMirrorOfSin(player) :
            kind == "killing" ? LegacyNativePerfectionEffects.TryKillingFlash(s.Client) :
            LegacyNativePerfectionEffects.TryLightOfStamina(s.Client);
        Assert.False(accepted); Assert.True(comparer.Fired); Assert.Empty(s.Queue);
        Assert.Same(registry, vector.ArrayFlags); Assert.Same(oldWords, vector.bits);
        Assert.Same(oldCache, RefinementCache.GetValue(vector)); Assert.Same(retained, Assert.Single(vector.ArrayFlags).Value);
        Assert.Equal((ushort)4, player.Stamina); Assert.Equal((ushort)0, player.XPCount);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    private static unsafe IntPtr RefinementAddress(LegacyNativePacket packet) => (IntPtr)packet.Memory;
}
