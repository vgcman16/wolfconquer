using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    [Fact]
    public void RealProcessLeaseExcludesSecondOwnerAndRestartNeverReissuesReservedRange()
    {
        using var fixture = new Fixture();
        using (var first = fixture.Bootstrap())
        {
            Assert.Equal(new uint[] { 1001, 1002, 1003 }, Fixture.Reserve(first, 3));
            Assert.False(LegacyItemUidRegistry.TryOpen(fixture.Options(), out var second, out _));
            Assert.Null(second);
        }
        using var reopened = fixture.Open();
        Assert.Equal(new uint[] { 1004, 1005 }, Fixture.Reserve(reopened, 2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(int.MaxValue)]
    public void InvalidCountsDoNotWriteAnyReservation(int count)
    {
        using var fixture = new Fixture();
        using var owner = fixture.Bootstrap();
        var before = File.ReadAllBytes(fixture.State);
        Assert.False(owner.TryReserve(count, out var reservation, out _));
        Assert.Null(reservation);
        Assert.Equal(before, File.ReadAllBytes(fixture.State));
        Assert.False(File.Exists(fixture.Pending));
        Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(owner, 1));
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(uint.MaxValue - 2)]
    public void ExactBoundaryRangesDoNotWrap(uint floor)
    {
        using var fixture = new Fixture();
        using var owner = fixture.Bootstrap(floor);
        var amount = floor == 0 ? 1 : 2;
        var ids = Fixture.Reserve(owner, amount);
        Assert.Equal(floor + 1, ids[0]);
        Assert.Equal(floor + (uint)amount, ids[^1]);
        if (floor != 0)
        {
            var bytes = File.ReadAllBytes(fixture.State);
            Assert.False(owner.TryReserve(1, out var exhausted, out var reason));
            Assert.Null(exhausted);
            Assert.Equal("itemUidNamespaceExhausted", reason);
            Assert.Equal(bytes, File.ReadAllBytes(fixture.State));
        }
    }

    [Fact]
    public async Task ConcurrentRealReservationsAreDisjointAndOneTimeConsumable()
    {
        using var fixture = new Fixture();
        using var owner = fixture.Bootstrap();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        { await start.Task; return Fixture.Reserve(owner, 3); })).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
        var ids = results.SelectMany(result => result).Order().ToArray();
        Assert.Equal(48, ids.Length);
        Assert.Equal(48, ids.Distinct().Count());
        Assert.Equal(Enumerable.Range(1001, 48).Select(value => (uint)value), ids);
    }

    [Fact]
    public void DisposedOwnerCannotAllocateEvenWhileAnotherOwnerHoldsTheDomain()
    {
        using var fixture = new Fixture();
        var first = fixture.Bootstrap();
        first.Dispose();
        using var second = fixture.Open();
        Assert.False(first.TryReserve(1, out var range, out var reason));
        Assert.Null(range);
        Assert.Equal("itemUidRegistryDisposed", reason);
        Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(second, 1));
    }

    [Fact]
    public void RangeCannotBePreparedOutsideTheRealIssuerGate()
    {
        using var fixture = new Fixture();
        using var owner = fixture.Bootstrap();
        var before = File.ReadAllBytes(fixture.State);
        Assert.Throws<InvalidOperationException>(() => LegacyItemUidRegistry.Reservation.Prepare(owner, 1));
        Assert.Equal(before, File.ReadAllBytes(fixture.State));
        Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(owner, 1));
    }

    [Theory]
    [InlineData((int)LegacyItemUidInterruption.AfterPendingFlush)]
    [InlineData((int)LegacyItemUidInterruption.AfterStateReplace)]
    public void ReservationCrashPrefixesBurnWholeRangeBeforeFreshOwnerCanContinue(int interruptionValue)
    {
        var interruption = (LegacyItemUidInterruption)interruptionValue;
        using var fixture = new Fixture();
        using (var owner = fixture.Bootstrap())
        {
            Assert.False(owner.TryReserve(3, interruption, out var lost, out var reason));
            Assert.Null(lost);
            Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
            Assert.False(owner.TryReserve(1, out var retry, out _));
            Assert.Null(retry);
        }
        using var restarted = fixture.Open();
        Assert.Equal(new uint[] { 1004 }, Fixture.Reserve(restarted, 1));
        Assert.False(File.Exists(fixture.Pending));
    }

    [Theory]
    [InlineData((int)LegacyItemUidInterruption.AfterPendingFlush)]
    [InlineData((int)LegacyItemUidInterruption.AfterStateReplace)]
    public void BootstrapCrashPrefixesRecoverGenuinePreparedStateWithoutReset(int interruptionValue)
    {
        var interruption = (LegacyItemUidInterruption)interruptionValue;
        using var fixture = new Fixture();
        Assert.False(LegacyItemUidRegistry.TryBootstrap(fixture.Options(), interruption, out var interrupted, out _));
        Assert.Null(interrupted);
        Assert.False(LegacyItemUidRegistry.TryBootstrap(fixture.Options(), out _, out _));
        using var restarted = fixture.Open();
        Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(restarted, 1));
    }
}
