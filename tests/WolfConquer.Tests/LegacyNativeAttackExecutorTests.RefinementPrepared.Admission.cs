using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData("mirror", "vector")] [InlineData("killing", "vector")] [InlineData("stamina", "vector")]
    [InlineData("mirror", "registry")] [InlineData("killing", "registry")] [InlineData("stamina", "registry")]
    [InlineData("mirror", "words")] [InlineData("killing", "words")] [InlineData("stamina", "words")]
    [InlineData("mirror", "row")] [InlineData("killing", "row")] [InlineData("stamina", "row")]
    [InlineData("mirror", "bitmap")] [InlineData("killing", "bitmap")] [InlineData("stamina", "bitmap")]
    [InlineData("mirror", "deadline")] [InlineData("killing", "deadline")] [InlineData("stamina", "deadline")]
    public Task RefinementMalformedGraphRefusesBeforePoolRngScalarOrRefinementPrefix(string kind, string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = 39;
        Assert.True(s.Player.TrySetXpCount(12)); s.Player.XPBooster = 4; s.Player.XPKiller = 5;
        Assert.True(s.Player.BitVector.TryAdd((int)Flag.TeamLeader, 33, false, 2));
        var vector = s.Player.BitVector;
        switch (fault)
        {
            case "vector": s.Player.BitVector = null!; break;
            case "registry": vector.ArrayFlags = null!; break;
            case "words": vector.bits = null!; break;
            case "row": vector.ArrayFlags[(int)Flag.TeamLeader] = null!; break;
            case "bitmap": vector.bits[0] ^= 1u; break;
            case "deadline": vector.ArrayFlags[(int)Flag.TeamLeader].Timer = DateTime.MaxValue; break;
        }
        var before = RefinementStateSnapshot.Capture(s.Player);
        using var pool = new RefinementPool(); var loan = pool.Enqueue();
        var bytes = RefinementStorage(loan); var rng = new LegacyNativeSafeRandom(117);
        LegacyNativePoolRandom.GetRandom = new(117); DateTime reset = LegacyNativePoolRandom.ResetRandom;
        Assert.False(AdmitRefinement(s, kind));
        before.Unchanged(s.Player); Assert.Empty(s.Queue); Assert.Same(loan, Assert.Single(pool.Queue));
        Assert.Equal(bytes, RefinementStorage(loan)); Assert.Equal(7, loan.Size); Assert.Equal(2, loan.Position);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next()); Assert.Equal(reset, LegacyNativePoolRandom.ResetRandom);
    });

    [Theory]
    [InlineData("mirror", "view")] [InlineData("killing", "view")] [InlineData("stamina", "view")]
    [InlineData("mirror", "view-owner")] [InlineData("killing", "view-owner")] [InlineData("stamina", "view-owner")]
    [InlineData("mirror", "client-player")] [InlineData("killing", "client-player")] [InlineData("stamina", "client-player")]
    [InlineData("mirror", "map-view")] [InlineData("killing", "map-view")] [InlineData("stamina", "map-view")]
    public Task RefinementForeignOrMissingPhysicalOwnersDoNotBorrowOrPublish(string kind, string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var player = s.Player; player.Stamina = 9;
        var foreign = s.Peer(new Queue<byte[]>());
        switch (fault)
        {
            case "view": player.View = null!; break;
            case "view-owner": player.View.Owner = foreign; break;
            case "client-player": s.Client.Player = foreign.Player; break;
            case "map-view": s.Client.Map = new LegacyNativeGameMap(72, 72, 1002); break;
        }
        using var pool = new RefinementPool(); var loan = pool.Enqueue();
        var before = RefinementStateSnapshot.Capture(player); var foreignBefore = RefinementStateSnapshot.Capture(foreign.Player);
        bool accepted = kind == "mirror" ? LegacyNativePerfectionEffects.TryMirrorOfSin(player) :
            kind == "killing" ? LegacyNativePerfectionEffects.TryKillingFlash(s.Client) :
            LegacyNativePerfectionEffects.TryLightOfStamina(s.Client);
        // A replaced client.Player whose genuine Owner is foreign is also rejected.
        Assert.False(accepted);
        before.Unchanged(player); foreignBefore.Unchanged(foreign.Player); Assert.Empty(s.Queue);
        Assert.Same(loan, Assert.Single(pool.Queue)); Assert.Equal(2, loan.Position);
    });

    [Theory]
    [InlineData("mirror")] [InlineData("killing")]
    public Task RefinementOversizedGenuineStatusFrameRefusesBeforeAnyRealPoolCopyOrChance(string kind)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.BitVector = new LegacyNativeStatusFlagsBigVector32(16384);
        var before = RefinementStateSnapshot.Capture(s.Player);
        using var pool = new RefinementPool(); var loan = pool.Enqueue();
        var bytes = RefinementStorage(loan); var rng = new LegacyNativeSafeRandom(135);
        LegacyNativePoolRandom.GetRandom = new(135);
        Assert.False(AdmitRefinement(s, kind));
        before.Unchanged(s.Player); Assert.Empty(s.Queue);
        Assert.Same(loan, Assert.Single(pool.Queue)); Assert.Equal(bytes, RefinementStorage(loan));
        Assert.Equal(7, loan.Size); Assert.Equal(2, loan.Position);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData("mirror")] [InlineData("killing")] [InlineData("stamina")]
    public Task RefinementRealNullPoolFailureIsPrecommitAndDoesNotConsumeChance(string kind)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = 11;
        var before = RefinementStateSnapshot.Capture(s.Player);
        var bin = RefinementBin.GetValue(null); var rng = new LegacyNativeSafeRandom(119);
        LegacyNativePoolRandom.GetRandom = new(119);
        try
        {
            RefinementBin.SetValue(null, null);
            Assert.False(AdmitRefinement(s, kind));
        }
        finally { RefinementBin.SetValue(null, bin); }
        before.Unchanged(s.Player); Assert.Empty(s.Queue);
        Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Theory]
    [InlineData("mirror")] [InlineData("killing")] [InlineData("stamina")]
    public Task RefinementInvalidSealSerializationRefusesBeforeBorrowOrCommit(string kind)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Player.Stamina = 11;
        using var pool = new RefinementPool(); var loan = pool.Enqueue();
        var before = RefinementStateSnapshot.Capture(s.Player); var seal = RefinementSeal.GetValue(null);
        var rng = new LegacyNativeSafeRandom(121); LegacyNativePoolRandom.GetRandom = new(121);
        try
        {
            RefinementSeal.SetValue(null, "bad");
            Assert.False(AdmitRefinement(s, kind));
        }
        finally { RefinementSeal.SetValue(null, seal); }
        before.Unchanged(s.Player); Assert.Empty(s.Queue); Assert.Same(loan, Assert.Single(pool.Queue));
        Assert.Equal(2, loan.Position); Assert.Equal(rng.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });
}
