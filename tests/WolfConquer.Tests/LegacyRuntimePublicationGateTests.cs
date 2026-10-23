namespace WolfConquer.Tests;

public sealed partial class LegacyRuntimePublicationGateTests
{
    [Fact]
    public Task PreCanceledWaitCannotReleaseAnActiveHolder() => RunAsync(async fixture =>
    {
        var holder = fixture.Start(fixture.First);
        await holder.GetLeaseAsync();
        var canceled = fixture.Start(fixture.First, preCanceled: true);
        await canceled.AssertCanceledAsync();

        var next = fixture.Start(fixture.First);
        await next.AssertPendingAsync();
        holder.Release();
        await next.GetLeaseAsync();
    });

    [Fact]
    public Task CancelingAnActuallyQueuedWaitCannotReleaseItsHolder() => RunAsync(async fixture =>
    {
        var holder = fixture.Start(fixture.First);
        await holder.GetLeaseAsync();
        var canceled = fixture.Start(fixture.First);
        await canceled.AssertPendingAsync();
        canceled.Budget.Cancel();
        await canceled.AssertCanceledAsync();

        var next = fixture.Start(fixture.First);
        await next.AssertPendingAsync();
        holder.Release();
        await next.GetLeaseAsync();
    });

    [Fact]
    public Task DisposingTheActualLeaseTwiceDoesNotOverRelease() => RunAsync(async fixture =>
    {
        var first = fixture.Start(fixture.First);
        var firstLease = await first.GetLeaseAsync();
        first.Release();
        firstLease.Dispose();

        var second = fixture.Start(fixture.First);
        await second.GetLeaseAsync();
        var third = fixture.Start(fixture.First);
        await third.AssertPendingAsync();
        second.Release();
        await third.GetLeaseAsync();
    });

    [Fact]
    public Task DistinctPhysicalConnectionsWithTheSameEndpointsHaveIndependentGates() => RunAsync(async fixture =>
    {
        Assert.NotSame(fixture.First, fixture.Second);
        Assert.Equal(fixture.First.LocalEndPoint, fixture.Second.LocalEndPoint);
        Assert.Equal(fixture.First.RemoteEndPoint, fixture.Second.RemoteEndPoint);

        var first = fixture.Start(fixture.First);
        await first.GetLeaseAsync();
        var second = fixture.Start(fixture.Second);
        await second.GetLeaseAsync();
        var firstWaiter = fixture.Start(fixture.First);
        await firstWaiter.AssertPendingAsync();

        second.Release();
        await firstWaiter.AssertPendingAsync();
        first.Release();
        await firstWaiter.GetLeaseAsync();
    });
}
