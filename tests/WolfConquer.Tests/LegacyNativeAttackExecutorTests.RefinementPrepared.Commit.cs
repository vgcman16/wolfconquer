using System.Collections.Concurrent;
using System.Text;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData("mirror", 0u, null)] [InlineData("mirror", uint.MaxValue, "TQServer")]
    [InlineData("killing", 0u, "éééééééé")] [InlineData("killing", uint.MaxValue, null)]
    [InlineData("stamina", 0u, null)] [InlineData("stamina", uint.MaxValue, "TQServer")]
    public Task RefinementPreparedNativeGoldensKeepRequiredUidSealAndRefineStatusScalarOrder(string kind, uint uid, string? seal)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.UID = uid; s.Player.Stamina = 7;
        s.Player.XPBooster = 7; s.Player.XPKiller = 8; Assert.True(s.Player.TrySetXpCount(12));
        s.Client.Rune = null; s.Client.HeroRewards = null; s.Client.MySpells = null; s.Client.Inventory = null;
        var previous = RefinementSeal.GetValue(null);
        int seed = SeedFor(10, true); LegacyNativePoolRandom.GetRandom = new(seed);
        var expectedRandom = new LegacyNativeSafeRandom(seed); if (kind == "killing") expectedRandom.Next();
        try
        {
            RefinementSeal.SetValue(null, seal);
            DateTime beforeGrant = DateTime.Now;
            Assert.True(AdmitRefinement(s, kind));
            DateTime afterGrant = DateTime.Now;
            var frames = s.Queue.ToArray();
            Assert.Equal(kind == "stamina" ? 2 : 3, frames.Length);
            Assert.Equal(SealedRefinementGolden(Refine(uid, uid, kind == "mirror" ? (byte)10 : kind == "killing" ? (byte)9 : (byte)7), seal), frames[0]);
            if (kind != "stamina")
            {
                Assert.Equal(SealedRefinementGolden(NativeAddFlagTestFixture.Status(Flag.XPList, uid), seal), frames[1]);
                var row = Assert.Single(s.Player.BitVector.ArrayFlags).Value;
                Assert.Equal((int)Flag.XPList, row.Key); Assert.Equal(20, row.Secounds);
                Assert.True(row.RemoveOnDead); Assert.Equal(0, row.InvokerSecouds);
                Assert.Equal(default(DateTime), row.TimerInvoker); Assert.InRange(row.Timer, beforeGrant, afterGrant);
            }
            Assert.Equal(SealedRefinementGolden(Scalar(uid,
                kind == "stamina" ? LegacyNativeMsgUpdate.DataType.Stamina : LegacyNativeMsgUpdate.DataType.XPList,
                kind == "stamina" ? 100u : 1u), seal), frames[^1]);
            Assert.Equal(kind == "stamina" ? (ushort)100 : (ushort)7, s.Player.Stamina);
            Assert.Equal((ushort)12, s.Player.XPCount); Assert.Equal((byte)7, s.Player.XPBooster); Assert.Equal((byte)8, s.Player.XPKiller);
            Assert.Equal(expectedRandom.Next(), LegacyNativePoolRandom.GetRandom.Next());
        }
        finally { RefinementSeal.SetValue(null, previous); }
    });

    [Theory]
    [InlineData("mirror")] [InlineData("killing")]
    public Task RefinementKeepsFullPredicatePriorityAndNoComparerAfterTheRealChance(string kind)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); using var pool = new RefinementPool();
        for (int index = 0; index < 3; index++) pool.Enqueue();
        var comparer = new RefinementComparer(pool, 2);
        s.Player.BitVector.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(comparer);
        comparer.Armed = true;
        int seed = SeedFor(10, false); LegacyNativePoolRandom.GetRandom = new(seed); var expected = new LegacyNativeSafeRandom(seed);
        if (kind == "killing") expected.Next();
        Assert.True(AdmitRefinement(s, kind));
        int[] xp = [(int)Flag.Cyclone, (int)Flag.Superman, (int)Flag.Oblivion, (int)Flag.FatalStrike,
            (int)Flag.ShurikenVortex, (int)Flag.ChaintBolt, (int)Flag.BlackbeardsRage, (int)Flag.CannonBarrage,
            (int)Flag.BladeFlurry, (int)Flag.SuperCyclone, (int)Flag.DragonCyclone, (int)Flag.Omnipotence,
            (int)Flag.ManiacDance, (int)Flag.ThunderRampage];
        int[] prefix = kind == "mirror" ? [.. xp, (int)Flag.XPList] : [(int)Flag.XPList, .. xp];
        Assert.Equal(prefix, comparer.Trace.Take(prefix.Length));
        Assert.Equal(expected.Next(), LegacyNativePoolRandom.GetRandom.Next());
        Assert.Equal(kind == "mirror" ? 3 : 1, s.Queue.Count);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task EligibleKillingWinAndLossUseTwoFinalRegistryReadsThenOneRealDrawWithoutReset(bool wins)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var vector = s.Player.BitVector;
        Assert.True(vector.TryAdd((int)Flag.TeamLeader, 7, false, 2));
        using var pool = new RefinementPool(); for (int index = 0; index < 3; index++) pool.Enqueue();
        var comparer = new RefinementComparer(pool, 2);
        vector.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(vector.ArrayFlags, comparer);
        int seed = SeedFor(10, wins), clocks = 0;
        var real = new LegacyNativeSafeRandom(seed, () => { clocks++; throw new InvalidOperationException("Rate must not reseed."); });
        var expected = new LegacyNativeSafeRandom(seed); expected.Next();
        LegacyNativePoolRandom.GetRandom = real; var reset = LegacyNativePoolRandom.ResetRandom; comparer.Armed = true;
        Assert.True(LegacyNativePerfectionEffects.TryKillingFlash(s.Client));
        Assert.True(comparer.Fired); Assert.Equal(2, comparer.ReadsAfterRent); Assert.Equal(0, clocks);
        Assert.Empty(pool.Queue); Assert.Equal(wins ? 3 : 1, s.Queue.Count);
        Assert.Equal(wins ? 2 : 1, vector.ArrayFlags.ToArray().Length);
        Assert.Equal(expected.Next(), real.Next()); Assert.Equal(reset, LegacyNativePoolRandom.ResetRandom);
    });

    [Theory]
    [InlineData("mirror")] [InlineData("stamina")]
    public Task RefinementAcceptedStateSurvivesRealQueueDeliveryFailureAndRetryDoesNotRepeatGrant(string kind)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = 8;
        var socket = s.Client.Socket!; var queue = RefinementSendQueue.GetValue(socket);
        try
        {
            RefinementSendQueue.SetValue(socket, null);
            Assert.True(AdmitRefinement(s, kind));
        }
        finally { RefinementSendQueue.SetValue(socket, queue); }
        Assert.Empty(s.Queue);
        var row = kind == "mirror" ? s.Player.BitVector.ArrayFlags[(int)Flag.XPList] : null;
        Assert.Equal(kind == "stamina" ? (ushort)100 : (ushort)8, s.Player.Stamina);
        Assert.True(AdmitRefinement(s, kind));
        if (kind == "mirror") { Assert.Same(row, s.Player.BitVector.ArrayFlags[(int)Flag.XPList]); Assert.Equal(Refine(17, 17, 10), Assert.Single(s.Queue)); }
        else Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData("null")] [InlineData("gate")]
    public Task EligibleKillingRandomOwnerFailureRefusesWithoutGameplayOrRefinementPrefix(string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var before = RefinementStateSnapshot.Capture(s.Player);
        var real = new LegacyNativeSafeRandom(131); var expected = new LegacyNativeSafeRandom(131);
        LegacyNativePoolRandom.GetRandom = fault == "null" ? null! : real;
        object gate = real.SyncRoot; if (fault == "gate") real.SyncRoot = null!;
        try { Assert.False(LegacyNativePerfectionEffects.TryKillingFlash(s.Client)); }
        finally { real.SyncRoot = gate; LegacyNativePoolRandom.GetRandom = real; }
        before.Unchanged(s.Player); Assert.Empty(s.Queue); Assert.Equal(expected.Next(), real.Next());
    });

    [Theory]
    [InlineData("mirror")] [InlineData("killing")] [InlineData("stamina")]
    public Task RefinementNullInputIsAnExplicitUncommittedRefusal(string kind)
        => NativePacketTestThread.Run(() =>
    {
        Assert.False(kind == "mirror" ? LegacyNativePerfectionEffects.TryMirrorOfSin(null) :
            kind == "killing" ? LegacyNativePerfectionEffects.TryKillingFlash(null) :
            LegacyNativePerfectionEffects.TryLightOfStamina(null));
    });

    [Theory]
    [InlineData((ushort)100)] [InlineData(ushort.MaxValue)]
    public Task LightValidAtOrAboveHundredIsAcceptedWithoutAnyRentalResetOrScalar(ushort stamina)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = stamina;
        var before = RefinementStateSnapshot.Capture(s.Player);
        var bin = RefinementBin.GetValue(null); var rng = new LegacyNativeSafeRandom(133);
        LegacyNativePoolRandom.GetRandom = new(133);
        try
        {
            RefinementBin.SetValue(null, null);
            Assert.True(LegacyNativePerfectionEffects.TryLightOfStamina(s.Client));
        }
        finally { RefinementBin.SetValue(null, bin); }
        before.Unchanged(s.Player); Assert.Empty(s.Queue);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData("mirror")] [InlineData("killing")] [InlineData("stamina")]
    public Task RefinementRealVisiblePeerReceivesRefinementThenBitmapButNotPrivateScalar(string kind)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = 6;
        var peerQueue = new Queue<byte[]>(); var peer = s.Peer(peerQueue);
        var map = new LegacyNativeGameMap(72, 72, 1002) { View = new LegacyNativeMapView(72, 72) };
        s.Client.Map = map; s.Player.Map = 1002;
        peer.Map = map; peer.Player.Map = 1002; peer.Player.X = 33; peer.Player.Y = 42;
        Assert.True(map.View.EnterMap(peer.Player));
        LegacyNativePoolRandom.GetRandom = new(SeedFor(10, true));
        Assert.True(AdmitRefinement(s, kind));
        byte effect = kind == "mirror" ? (byte)10 : kind == "killing" ? (byte)9 : (byte)7;
        var ownerFrames = s.Queue.ToArray(); var peerFrames = peerQueue.ToArray();
        Assert.Equal(kind == "stamina" ? 2 : 3, ownerFrames.Length);
        Assert.Equal(kind == "stamina" ? 1 : 2, peerFrames.Length);
        Assert.Equal(Refine(17, 17, effect), peerFrames[0]); Assert.Equal(ownerFrames[0], peerFrames[0]);
        if (kind != "stamina") Assert.Equal(ownerFrames[1], peerFrames[1]);
        Assert.Equal((ushort)2176, Opcode(ownerFrames[^1]));
    });

    private static byte[] SealedRefinementGolden(byte[] frame, string? seal) => seal is null ? frame :
        [.. frame, .. Encoding.UTF8.GetBytes(seal).Take(8)];
}
