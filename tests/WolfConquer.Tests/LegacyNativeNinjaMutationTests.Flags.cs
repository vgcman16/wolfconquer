using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    [Fact]
    public void ContainsFlagUsesAnyOverlapAndRetainsAllSixtyFourBits()
    {
        var ninja = new LegacyNativeNinja { Flag = 0x8000000000000001UL };
        Assert.True(ninja.ContainsFlag(3));
        Assert.True(ninja.ContainsFlag(1UL << 63));
        Assert.True(ninja.ContainsFlag(ulong.MaxValue));
        Assert.False(ninja.ContainsFlag(0));
        Assert.False(ninja.ContainsFlag(2));
        Assert.False(ninja.ContainsFlag(1UL << 32));
        Assert.Equal(0x8000000000000001UL, ninja.Flag);
    }

    [Fact]
    public Task ExistingCompositeAddAndAbsentRemoveDoNotTouchRecycleOrOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Flag = 1;
        scope.Ninja.user = null!;
        scope.UseNullBin();
        scope.Ninja.AddFlag(3); // Any existing overlap prevents adding even the missing bit2.
        scope.Ninja.RemoveFlag(2);
        scope.Ninja.RemoveFlag(0);
        Assert.Equal(1UL, scope.Ninja.Flag);
        scope.RestoreOwnedBin();
        scope.NoSerialization();
    });

    [Fact]
    public Task AddZeroStillAllocatesAndSendsFlagActionWithUidAndUnrelatedFieldsZero() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.UID = uint.MaxValue;
        scope.Ninja.Flag = 0x8000000000000001UL;
        scope.Ninja.Exp = scope.Ninja.Fire = scope.Ninja.UnlockMastery = uint.MaxValue;
        scope.Ninja.Items = null!; // The Flag update must not evaluate Score.
        scope.Ninja.AddFlag(0);
        Assert.Equal(0x8000000000000001UL, scope.Ninja.Flag);
        Assert.Single(scope.Queue);
        Assert.Equal(3, scope.Available);
        scope.Sent(Frame(1, 0, 0x8000000000000001UL));
        Assert.Empty(scope.Log);
    });

    [Fact]
    public Task FullWidthAddAndCompositeRemoveSendRetainedResultInNativeOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.AddFlag(ulong.MaxValue);
        scope.Ninja.RemoveFlag(0x8000000100000001UL);
        Assert.Equal(0x7FFFFFFEFFFFFFFEUL, scope.Ninja.Flag);
        Assert.Equal(2, scope.Queue.Count);
        Assert.Equal(2, scope.Available);
        scope.Sent(Frame(1, 0, ulong.MaxValue));
        scope.Sent(Frame(1, 0, 0x7FFFFFFEFFFFFFFEUL), 1);
        Assert.Empty(scope.Log);
    });

    [Fact]
    public Task RemovingCompositeWithOnlyOneOverlapClearsThatBitAndSendsZeroResult() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Flag = 1;
        scope.Ninja.RemoveFlag(3);
        Assert.Equal(0UL, scope.Ninja.Flag);
        scope.Sent(Frame(1));
        Assert.Single(scope.Queue);
        Assert.Equal(3, scope.Available);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RecycleFailureHappensBeforeEitherFlagMutation(bool remove) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Flag = remove ? 1UL : 0UL;
        scope.UseNullBin();
        if (remove) Assert.Throws<InvalidOperationException>(() => scope.Ninja.RemoveFlag(1));
        else Assert.Throws<InvalidOperationException>(() => scope.Ninja.AddFlag(1));
        Assert.Equal(remove ? 1UL : 0UL, scope.Ninja.Flag);
        scope.RestoreOwnedBin();
        scope.NoSerialization();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullOwnerRefusesBeforeFlagMutationOrPacketLoan(bool remove) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Ninja.Flag = remove ? ulong.MaxValue : 0;
        scope.Ninja.user = null!;
        if (remove) Assert.Throws<InvalidOperationException>(() => scope.Ninja.RemoveFlag(ulong.MaxValue));
        else Assert.Throws<InvalidOperationException>(() => scope.Ninja.AddFlag(ulong.MaxValue));
        Assert.Equal(remove ? ulong.MaxValue : 0UL, scope.Ninja.Flag);
        scope.NoSerialization();
        Assert.Empty(scope.Log);
    });
}
