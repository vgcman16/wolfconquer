using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData("vector", false)] [InlineData("vector", true)]
    [InlineData("registry", false)] [InlineData("registry", true)]
    [InlineData("words", false)] [InlineData("words", true)]
    [InlineData("row", false)] [InlineData("row", true)]
    [InlineData("key", false)] [InlineData("key", true)]
    [InlineData("bitmap", false)] [InlineData("bitmap", true)]
    [InlineData("deadline", false)] [InlineData("deadline", true)]
    public Task DivineGuardPreparedMalformedStatusCannotResetCooldownSpendChanceOrPublish(string fault, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var v = p.BitVector;
        Assert.True(v.TryAdd((int)Flag.TeamLeader, 7, true, 2));
        if (existing) Assert.True(v.TryAdd((int)Flag.DivineGuard, 9, false, 0));
        p.LastDivineGuard = new DateTime(2026, 1, 3); p.XPBooster = 7; p.XPKiller = 8;
        Assert.True(p.TrySetXpCount(12));
        switch (fault)
        {
            case "vector": p.BitVector = null!; break;
            case "registry": v.ArrayFlags = null!; break;
            case "words": v.bits = null!; break;
            case "row": v.ArrayFlags[(int)Flag.TeamLeader] = null!; break;
            case "key": v.ArrayFlags[(int)Flag.TeamLeader].Key++; break;
            case "bitmap": v.bits[0] ^= 1u; break;
            case "deadline": v.ArrayFlags[(int)Flag.TeamLeader].Timer = DateTime.MaxValue; break;
        }
        var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
        using var pool = new RefinementPool(); var loan = pool.Enqueue();
        var bytes = RefinementStorage(loan); var rng = new LegacyNativeSafeRandom(201);
        LegacyNativePoolRandom.GetRandom = new(201); var reset = LegacyNativePoolRandom.ResetRandom;
        Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        Assert.Same(loan, Assert.Single(pool.Queue)); Assert.Equal(bytes, RefinementStorage(loan));
        Assert.Equal(7, loan.Size); Assert.Equal(2, loan.Position);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next()); Assert.Equal(reset, LegacyNativePoolRandom.ResetRandom);
    });

    [Theory]
    [InlineData("view", false)] [InlineData("view", true)]
    [InlineData("view-owner", false)] [InlineData("view-owner", true)]
    [InlineData("client-player", false)] [InlineData("client-player", true)]
    [InlineData("map-view", false)] [InlineData("map-view", true)]
    public Task DivineGuardPreparedForeignBindingRefusesBeforeBorrowOrCooldown(string fault, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; var foreignQueue = new Queue<byte[]>(); var foreign = s.Peer(foreignQueue);
        if (existing) Assert.True(p.BitVector.TryAdd((int)Flag.DivineGuard, 9, false, 0));
        p.LastDivineGuard = new DateTime(2026, 1, 3);
        switch (fault)
        {
            case "view": p.View = null!; break;
            case "view-owner": p.View.Owner = foreign; break;
            case "client-player": s.Client.Player = foreign.Player; break;
            case "map-view": s.Client.Map = new LegacyNativeGameMap(72, 72, 1002); break;
        }
        var before = RefinementStateSnapshot.Capture(p); var otherBefore = RefinementStateSnapshot.Capture(foreign.Player);
        var stamp = p.LastDivineGuard; var otherStamp = foreign.Player.LastDivineGuard;
        using var pool = new RefinementPool(); var loan = pool.Enqueue();
        var rng = new LegacyNativeSafeRandom(203); LegacyNativePoolRandom.GetRandom = new(203);
        Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        before.Unchanged(p); otherBefore.Unchanged(foreign.Player); Assert.Equal(stamp, p.LastDivineGuard);
        Assert.Equal(otherStamp, foreign.Player.LastDivineGuard);
        Assert.Empty(s.Queue); Assert.Empty(foreignQueue); Assert.Same(loan, Assert.Single(pool.Queue)); Assert.Equal(2, loan.Position);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData("pool", false)] [InlineData("pool", true)]
    [InlineData("seal", false)] [InlineData("seal", true)]
    public Task DivineGuardPreparedRealPreparationFailureDoesNotResetStampOrSpendChance(string fault, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player;
        if (existing) Assert.True(p.BitVector.TryAdd((int)Flag.DivineGuard, 9, false, 0));
        p.LastDivineGuard = new DateTime(2026, 1, 3);
        var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
        using var pool = new RefinementPool(); var loan = pool.Enqueue(); var bytes = RefinementStorage(loan);
        var seal = RefinementSeal.GetValue(null); var bin = RefinementBin.GetValue(null);
        var rng = new LegacyNativeSafeRandom(205); LegacyNativePoolRandom.GetRandom = new(205);
        try
        {
            if (fault == "pool") RefinementBin.SetValue(null, null);
            else RefinementSeal.SetValue(null, "bad");
            Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        }
        finally { RefinementSeal.SetValue(null, seal); RefinementBin.SetValue(null, bin); }
        before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        Assert.Same(loan, Assert.Single(pool.Queue)); Assert.Equal(bytes, RefinementStorage(loan)); Assert.Equal(2, loan.Position);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task DivineGuardPreparedOversizedRealStatusFrameRefusesBeforeRentalOrStamp()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Player; p.BitVector = new(16384);
        p.LastDivineGuard = new DateTime(2026, 1, 3);
        var before = RefinementStateSnapshot.Capture(p); var stamp = p.LastDivineGuard;
        using var pool = new RefinementPool(); var loan = pool.Enqueue();
        var rng = new LegacyNativeSafeRandom(207); LegacyNativePoolRandom.GetRandom = new(207);
        Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(s.Client));
        before.Unchanged(p); Assert.Equal(stamp, p.LastDivineGuard); Assert.Empty(s.Queue);
        Assert.Same(loan, Assert.Single(pool.Queue)); Assert.Equal(2, loan.Position);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public void DivineGuardPreparedNullClientIsARefusal() =>
        Assert.False(LegacyNativePerfectionEffects.TryDivineGuard(null));
}
