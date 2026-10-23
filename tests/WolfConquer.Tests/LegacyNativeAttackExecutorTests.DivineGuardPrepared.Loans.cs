using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData("short", false)] [InlineData("null", false)] [InlineData("disposed", false)] [InlineData("alias", false)]
    [InlineData("short", true)] [InlineData("null", true)] [InlineData("disposed", true)]
    public Task DivineGuardPreparedValidatesEveryLoanBeforeFirstCopyOrCooldown(string fault, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player;
        if (existing) Assert.True(p.BitVector.TryAdd((int)Flag.DivineGuard, 9, false, 0));
        p.LastDivineGuard = new DateTime(2026, 1, 3);
        var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
        using var pool = new RefinementPool();
        var first = existing ? null : pool.Enqueue(); var bytes = first is null ? null : RefinementStorage(first);
        if (!existing) pool.Enqueue();
        switch (fault)
        {
            case "short": pool.Enqueue(1); break;
            case "null": pool.Queue.Enqueue(null!); break;
            case "disposed": var disposed = pool.Enqueue(); disposed.Dispose(); break;
            case "alias": pool.Queue.Enqueue(first!); break;
        }
        var rng = new LegacyNativeSafeRandom(209); LegacyNativePoolRandom.GetRandom = new(209);
        Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        if (first is not null) { Assert.Equal(bytes, RefinementStorage(first)); Assert.Equal(7, first.Size); }
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData("size-large", 0, false)] [InlineData("size-large", 1, false)] [InlineData("size-large", 2, false)] [InlineData("size-large", 0, true)]
    [InlineData("size-short", 0, false)] [InlineData("size-short", 1, false)] [InlineData("size-short", 2, false)] [InlineData("size-short", 0, true)]
    [InlineData("cursor", 0, false)] [InlineData("cursor", 1, false)] [InlineData("cursor", 2, false)] [InlineData("cursor", 0, true)]
    [InlineData("content", 0, false)] [InlineData("content", 1, false)] [InlineData("content", 2, false)] [InlineData("content", 0, true)]
    [InlineData("dispose", 0, false)] [InlineData("dispose", 1, false)] [InlineData("dispose", 2, false)] [InlineData("dispose", 0, true)]
    public Task DivineGuardPreparedFinalComparerRejectsEveryLoanMetadataContentOrLifetimeDrift(string fault, int index, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        int key = (int)(existing ? Flag.DivineGuard : Flag.TeamLeader);
        Assert.True(v.TryAdd(key, 9, false, 2)); p.LastDivineGuard = new DateTime(2026, 1, 3);
        using var pool = new RefinementPool();
        var loans = Enumerable.Range(0, existing ? 1 : 3).Select(_ => pool.Enqueue()).ToArray();
        var comparer = new DivineGuardFinalComparer(pool, key, existing ? 1 : 2);
        v.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(v.ArrayFlags, comparer);
        var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
        comparer.OnFinalRead = () =>
        {
            var changed = loans[index];
            switch (fault)
            {
                case "size-large": changed.Size = 1032; break;
                case "size-short": changed.Size--; break;
                case "cursor": changed.Seek(changed.Capacity + 1); break;
                case "content": Marshal.WriteByte(RefinementAddress(changed), 0, 0xAC); break;
                case "dispose": changed.Dispose(); break;
            }
        };
        var rng = new LegacyNativeSafeRandom(211); LegacyNativePoolRandom.GetRandom = new(211); comparer.Armed = true;
        Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        Assert.True(comparer.Fired); before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData("player", false)] [InlineData("player", true)]
    [InlineData("vector", false)] [InlineData("vector", true)]
    [InlineData("view", false)] [InlineData("view", true)]
    [InlineData("view-owner", false)] [InlineData("view-owner", true)]
    [InlineData("uid", false)] [InlineData("uid", true)]
    [InlineData("map", false)] [InlineData("map", true)]
    [InlineData("map-view", false)] [InlineData("map-view", true)]
    [InlineData("stamp", false)] [InlineData("stamp", true)]
    [InlineData("row", false)] [InlineData("row", true)]
    [InlineData("word", false)] [InlineData("word", true)]
    public Task DivineGuardPreparedFinalComparerCannotTurnOwnerStampOrGraphDriftIntoAcceptance(string fault, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        var foreignQueue = new Queue<byte[]>(); var foreign = s.Peer(foreignQueue);
        int key = (int)(existing ? Flag.DivineGuard : Flag.TeamLeader);
        Assert.True(v.TryAdd(key, 9, false, 2));
        var retained = v.ArrayFlags[key]; var words = v.bits; var cache = RefinementCache.GetValue(v);
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        s.Client.Map = map; p.Map = 1002; p.LastDivineGuard = new DateTime(2026, 1, 3);
        var stamp = p.LastDivineGuard; var otherStamp = foreign.Player.LastDivineGuard;
        using var pool = new RefinementPool(); for (int i = 0; i < (existing ? 1 : 3); i++) pool.Enqueue();
        var comparer = new DivineGuardFinalComparer(pool, key, existing ? 1 : 2);
        v.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(v.ArrayFlags, comparer);
        var registrations = v.ArrayFlags;
        comparer.OnFinalRead = () =>
        {
            switch (fault)
            {
                case "player": s.Client.Player = foreign.Player; break;
                case "vector": p.BitVector = new(576); break;
                case "view": p.View = new LegacyNativeRoleView(s.Client); break;
                case "view-owner": p.View.Owner = foreign; break;
                case "uid": p.UID++; break;
                case "map": s.Client.Map = null; break;
                case "map-view": map.View = new LegacyNativeMapView(72, 72); break;
                case "stamp": p.LastDivineGuard = stamp.AddDays(1); break;
                case "row": retained.Secounds++; break;
                case "word": words[0] ^= 1u; break;
            }
        };
        var rng = new LegacyNativeSafeRandom(213); LegacyNativePoolRandom.GetRandom = new(213); comparer.Armed = true;
        Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(s.Client)); Assert.True(comparer.Fired);
        Assert.Equal(fault == "stamp" ? stamp.AddDays(1) : stamp, p.LastDivineGuard);
        Assert.Same(registrations, v.ArrayFlags); Assert.Same(words, v.bits); Assert.Same(cache, RefinementCache.GetValue(v));
        Assert.Same(retained, Assert.Single(v.ArrayFlags).Value); Assert.Empty(s.Queue); Assert.Empty(foreignQueue);
        Assert.Equal(otherStamp, foreign.Player.LastDivineGuard);
        Assert.Equal((ushort)100, p.Stamina); Assert.Equal((ushort)0, p.XPCount);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });
}
