using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    [Fact]
    public Task MasteryUsesLowThirtyTwoBitsAndAnyOverlapWithoutOwnerOrPacketAccess() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.user = null!;
        scope.Ninja.UnlockMastery = 1;
        scope.UseNullBin();
        var mixed = (LegacyNativeNinja.UnlockMasterys)(0x8000000000000003UL);
        Assert.True(scope.Ninja.ContainsFlagMastery(mixed));
        scope.Ninja.AddFlagMastery(mixed);
        Assert.Equal(1u, scope.Ninja.UnlockMastery); // Missing bit2 is not added on any overlap.
        scope.Ninja.RemoveFlagMastery(mixed);
        Assert.Equal(0u, scope.Ninja.UnlockMastery);
        var highOnly = (LegacyNativeNinja.UnlockMasterys)(1UL << 40);
        Assert.False(scope.Ninja.ContainsFlagMastery(highOnly));
        scope.Ninja.AddFlagMastery(highOnly);
        scope.Ninja.RemoveFlagMastery(highOnly);
        Assert.Equal(0u, scope.Ninja.UnlockMastery);
        scope.RestoreOwnedBin();
        scope.NoSerialization();
    });

    [Fact]
    public Task MasteryZeroSignedCastBoundaryAndAllBitsRetainNativeUintNarrowing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.user = null!;
        scope.UseNullBin();
        var zero = (LegacyNativeNinja.UnlockMasterys)0;
        Assert.False(scope.Ninja.ContainsFlagMastery(zero));
        scope.Ninja.AddFlagMastery(zero);
        scope.Ninja.RemoveFlagMastery(zero);
        Assert.Equal(0u, scope.Ninja.UnlockMastery);
        var signedBoundary = (LegacyNativeNinja.UnlockMasterys)0x80000000UL;
        scope.Ninja.AddFlagMastery(signedBoundary);
        Assert.Equal(0x80000000u, scope.Ninja.UnlockMastery);
        Assert.True(scope.Ninja.ContainsFlagMastery(signedBoundary));
        scope.Ninja.RemoveFlagMastery(signedBoundary);
        Assert.Equal(0u, scope.Ninja.UnlockMastery);
        var all = (LegacyNativeNinja.UnlockMasterys)ulong.MaxValue;
        scope.Ninja.AddFlagMastery(all);
        Assert.Equal(uint.MaxValue, scope.Ninja.UnlockMastery);
        Assert.False(scope.Ninja.ContainsFlagMastery((LegacyNativeNinja.UnlockMasterys)(1UL << 63)));
        scope.Ninja.RemoveFlagMastery(all);
        Assert.Equal(0u, scope.Ninja.UnlockMastery);
        scope.RestoreOwnedBin();
        scope.NoSerialization();
    });

    [Fact]
    public Task PointsRejectOverflowAndKeepZeroAndFullWidthValidCredits() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.UID = uint.MaxValue;
        scope.Ninja.Exp = uint.MaxValue;
        scope.Ninja.Flag = ulong.MaxValue;
        scope.Ninja.Fire = scope.Ninja.WaterMastery = scope.Ninja.UnlockMastery = uint.MaxValue;
        scope.Ninja.Items = null!;
        Assert.Throws<OverflowException>(() => scope.Ninja.AddPoints(1));
        Assert.Equal(uint.MaxValue, scope.Ninja.Exp);
        scope.NoSerialization();
        scope.Ninja.AddPoints(0);
        Assert.Equal(uint.MaxValue, scope.Ninja.Exp);
        scope.Ninja.Exp = 0; // Independent valid zero/full-width credit controls.
        scope.Ninja.AddPoints(0);
        Assert.Equal(0U, scope.Ninja.Exp);
        scope.Ninja.AddPoints(uint.MaxValue);
        Assert.Equal(uint.MaxValue, scope.Ninja.Exp);
        Assert.Equal(3, scope.Queue.Count);
        Assert.Equal(1, scope.Available);
        scope.Sent(Frame(2, 0, 0, uint.MaxValue));
        scope.Sent(Frame(2), 1);
        scope.Sent(Frame(2, 0, 0, uint.MaxValue), 2);
        Assert.Empty(scope.Log);
    });

    [Fact]
    public Task RecycleFailurePrecedesPointAdditionEvenForZeroAmount() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Exp = 71;
        scope.UseNullBin();
        Assert.Throws<NullReferenceException>(() => scope.Ninja.AddPoints(9));
        Assert.Throws<NullReferenceException>(() => scope.Ninja.AddPoints(0));
        Assert.Equal(71u, scope.Ninja.Exp);
        scope.RestoreOwnedBin();
        scope.NoSerialization();
    });

    [Fact]
    public Task NullOwnerRefusesBeforePointMutationOrPacketLoan() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Exp = 71;
        scope.Ninja.user = null!;
        var failure = Record.Exception(() => scope.Ninja.AddPoints(3));
        Assert.Equal(71U, scope.Ninja.Exp);
        Assert.IsType<InvalidOperationException>(failure);
        scope.NoSerialization();
        Assert.Empty(scope.Log);
    });
}
