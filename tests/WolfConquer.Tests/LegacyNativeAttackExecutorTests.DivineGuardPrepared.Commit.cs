using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData(0u, false, true, null)] [InlineData(uint.MaxValue, false, true, "éééééééé")]
    [InlineData(0u, false, false, "TQServer")] [InlineData(uint.MaxValue, false, false, null)]
    [InlineData(0u, true, false, null)] [InlineData(uint.MaxValue, true, true, "TQServer")]
    public Task DivineGuardPreparedNativeGoldensKeepSelectedStampOnWinLossAndPresent(uint uid, bool existing, bool wins, string? seal)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; p.UID = uid; p.LastDivineGuard = new DateTime(2026, 1, 3);
        p.Stamina = 77; p.Mana = 37; p.XPBooster = 4; p.XPKiller = 5; Assert.True(p.TrySetXpCount(12));
        if (existing) Assert.True(p.BitVector.TryAdd((int)Flag.DivineGuard, 9, false, 2));
        var before = RefinementStateSnapshot.Capture(p);
        s.Client.Rune = null; s.Client.HeroRewards = null; s.Client.MySpells = null; s.Client.Inventory = null;
        s.Client.Status = null!; // Unrelated HP/mana stat authority is not a selected Guard dependency.
        int seed = SeedFor(5, wins), clocks = 0;
        var real = new LegacyNativeSafeRandom(seed, () => { clocks++; throw new InvalidOperationException("Guard Rate must not reseed."); });
        LegacyNativePoolRandom.GetRandom = real; var expected = new LegacyNativeSafeRandom(seed);
        if (!existing) expected.Next();
        var reset = LegacyNativePoolRandom.ResetRandom;
        LegacyNativeAttackBase.MyRandom = new(217); var baseExpected = new LegacyNativeSafeRandom(217);
        var oldSeal = RefinementSeal.GetValue(null);
        try
        {
            RefinementSeal.SetValue(null, seal);
            DateTime begin = DateTime.Now, beforeStamp = begin.AddMinutes(10);
            Assert.True(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
            DateTime end = DateTime.Now;
            Assert.InRange(p.LastDivineGuard, beforeStamp, end.AddMinutes(10));
            var frames = s.Queue.ToArray(); bool grant = !existing && wins;
            Assert.Equal(grant ? 3 : 1, frames.Length);
            Assert.Equal(SealedRefinementGolden(Refine(uid, uid, 11), seal), frames[0]);
            if (grant)
            {
                Assert.Equal(SealedRefinementGolden(NativeAddFlagTestFixture.Status(Flag.DivineGuard, uid), seal), frames[1]);
                Assert.Equal(SealedRefinementGolden(Guard(uid), seal), frames[2]);
                var row = Assert.Single(p.BitVector.ArrayFlags).Value;
                Assert.Equal((int)Flag.DivineGuard, row.Key); Assert.Equal(15, row.Secounds);
                Assert.Equal(0, row.InvokerSecouds); Assert.Equal(default(DateTime), row.TimerInvoker);
                Assert.True(row.RemoveOnDead); Assert.InRange(row.Timer, begin, end);
            }
            else before.Unchanged(p);
            Assert.Equal((ushort)37, p.Mana); Assert.Equal((ushort)77, p.Stamina);
            Assert.Equal((ushort)12, p.XPCount); Assert.Equal((byte)4, p.XPBooster); Assert.Equal((byte)5, p.XPKiller);
            Assert.Equal(expected.Next(), real.Next()); Assert.Equal(0, clocks);
            Assert.Equal(baseExpected.Next(), LegacyNativeAttackBase.MyRandom.Next()); Assert.Equal(reset, LegacyNativePoolRandom.ResetRandom);
        }
        finally { RefinementSeal.SetValue(null, oldSeal); }
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task DivineGuardPreparedRealPeerGetsRefineAndOptionalBitmapNeverAzureArchive(int mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var peerQueue = new Queue<byte[]>(); var peer = s.Peer(peerQueue);
        bool existing = mode == 2, wins = mode == 0;
        if (existing) Assert.True(s.Player.BitVector.TryAdd((int)Flag.DivineGuard, 9, false, 0));
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        s.Client.Map = map; s.Player.Map = 1002; peer.Map = map; peer.Player.Map = 1002;
        peer.Player.X = 33; peer.Player.Y = 42; Assert.True(map.View.EnterMap(peer.Player));
        LegacyNativePoolRandom.GetRandom = new(SeedFor(5, wins));
        Assert.True(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        var owner = s.Queue.ToArray(); var visible = peerQueue.ToArray();
        Assert.Equal(wins ? 3 : 1, owner.Length); Assert.Equal(wins ? 2 : 1, visible.Length);
        Assert.Equal(Refine(17, 17, 11), visible[0]); Assert.Equal(owner[0], visible[0]);
        if (wins) { Assert.Equal(owner[1], visible[1]); Assert.Equal(Guard(17), owner[2]); }
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task DivineGuardPreparedFinalReadsFinishBeforeRealDrawOrPresentShortcut(int mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        bool existing = mode == 2, wins = mode == 0; int key = (int)(existing ? Flag.DivineGuard : Flag.TeamLeader);
        Assert.True(v.TryAdd(key, 9, false, 2));
        using var pool = new RefinementPool(); for (int i = 0; i < (existing ? 1 : 3); i++) pool.Enqueue();
        var comparer = new DivineGuardFinalComparer(pool, key, existing ? 1 : 2);
        v.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(v.ArrayFlags, comparer); comparer.Armed = true;
        var rng = new LegacyNativeSafeRandom(SeedFor(5, wins)); LegacyNativePoolRandom.GetRandom = rng;
        var expected = new LegacyNativeSafeRandom(SeedFor(5, wins)); if (!existing) expected.Next();
        Assert.True(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        Assert.True(comparer.Fired); Assert.Equal(existing ? 1 : 2, comparer.ReadsAfterRent);
        Assert.Empty(pool.Queue); Assert.Equal(wins ? 3 : 1, s.Queue.Count);
        Assert.Equal(expected.Next(), rng.Next());
    });

    [Theory]
    [InlineData("null")] [InlineData("gate")]
    public Task DivineGuardPreparedChanceOwnerFailureCannotLeaveCooldownOrRefinementPrefix(string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; p.LastDivineGuard = new DateTime(2026, 1, 3);
        var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
        var real = new LegacyNativeSafeRandom(219); var expected = new LegacyNativeSafeRandom(219);
        object gate = real.SyncRoot; LegacyNativePoolRandom.GetRandom = fault == "null" ? null! : real;
        if (fault == "gate") real.SyncRoot = null!;
        try { Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(s.Client)); }
        finally { real.SyncRoot = gate; LegacyNativePoolRandom.GetRandom = real; }
        before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        Assert.Equal(expected.Next(), real.Next());
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task DivineGuardPreparedAcceptedWinLossOrPresentIsNotUndoneByRealQueueFailure(int mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; bool existing = mode == 2, wins = mode == 0;
        if (existing) Assert.True(p.BitVector.TryAdd((int)Flag.DivineGuard, 9, false, 2));
        var retained = existing ? p.BitVector.ArrayFlags[(int)Flag.DivineGuard] : null;
        LegacyNativePoolRandom.GetRandom = new(SeedFor(5, wins)); p.LastDivineGuard = new DateTime(2026, 1, 3);
        var socket = s.Client.Socket!; var queue = RefinementSendQueue.GetValue(socket);
        DateTime begin = DateTime.Now.AddMinutes(10);
        try
        {
            RefinementSendQueue.SetValue(socket, null);
            Assert.True(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        }
        finally { RefinementSendQueue.SetValue(socket, queue); }
        Assert.InRange(p.LastDivineGuard, begin, DateTime.Now.AddMinutes(10)); Assert.Empty(s.Queue);
        Assert.Equal(existing || wins, p.BitVector.ArrayFlags.ToArray().Any(pair => pair.Key == (int)Flag.DivineGuard));
        if (existing) { Assert.Same(retained, Assert.Single(p.BitVector.ArrayFlags).Value); Assert.Equal(9, retained!.Secounds); }
        if (wins) Assert.Equal(15, Assert.Single(p.BitVector.ArrayFlags).Value.Secounds);
    });
}
