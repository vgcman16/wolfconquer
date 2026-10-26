using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false, "uid")]
    [InlineData(true, "uid")]
    [InlineData(false, "ninja-exp")]
    [InlineData(true, "ninja-exp")]
    [InlineData(false, "ninja-row")]
    [InlineData(true, "ninja-row")]
    [InlineData(false, "retire")]
    [InlineData(true, "retire")]
    [InlineData(false, "pin")]
    [InlineData(true, "pin")]
    [InlineData(false, "none")]
    [InlineData(true, "none")]
    public Task RegisteredD13PackageDeliveryFencesSameValueCatalogCallbacksAfterActualJointCommit(bool claimAll, string mutation) =>
        RunDeliveryProbeAsync(claimAll, mutation);

    private static async Task RunDeliveryProbeAsync(bool claimAll, string mutation)
    {
        var fixture = new Fixture();
        var probe = new DeliveryCatalogProbe();
        PackageJoin? join = null;
        var errors = new List<Exception>();
        try
        {
            fixture.SeedMailboxPackageJoin(job: 5051);
            fixture.SeedPackageJoinNinjaLine();
            join = new PackageJoin(fixture, DeliveryProbeCatalog(probe), bindProducer: true, nativeComponentJob: 5051);
            await join.StartAsync(bootstrap: true);
            await VerifyDeliveryProbeAsync(fixture, join, probe, claimAll, mutation);
        }
        catch (Exception error) { errors.Add(error); }
        if (join is not null)
        {
            probe.Armed = false;
            probe.Mutation = null;
            join.SendObserver = null;
            try { await join.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        }
        if (join is null || join.IsClosed)
        {
            try { await fixture.CleanupAsync(errors); }
            catch (Exception error) { errors.Add(error); Unfinished.Add(fixture); }
        }
        else
        {
            Unfinished.Add(fixture);
            errors.Add(new InvalidOperationException("Delivery fixture retained because actual owner closure is unproven."));
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Delivery callback operation and cleanup failed.", errors);
    }

    private static async Task VerifyDeliveryProbeAsync(Fixture fixture, PackageJoin join,
        DeliveryCatalogProbe probe, bool claimAll, string mutation)
    {
        var before = CapturePackageJoin(fixture, join);
        Dictionary<string, byte[]>? committedFiles = null;
        var firstFrame = fixture.Sent.Count;
        var deliveries = 0;
        var errors = new List<Exception>();
        var client = join.NativeClient;
        var ninja = Assert.IsType<LegacyNativeNinja>(client.MyNinja);
        Assert.True(ninja.Items.TryGetValue(100, out var physicalRow));
        var rowLevel = physicalRow.Level;
        join.SendObserver = _ =>
        {
            deliveries++;
            if (deliveries != 1) return;
            AssertDeliveryCommittedState(fixture, join, before, claimAll);
            committedFiles = fixture.PackageFiles();
            probe.Mutation = MutateDeliveryNativeOwner(mutation, fixture, join, join.Current);
            probe.Armed = true;
        };
        var started = DateTime.Now;
        try
        {
            await join.FrameAsync(PackageJoinRequest(claimAll));
            join.SendObserver = null;
            Assert.True(probe.Fired);
            Assert.True(probe.AfterCommitReads > 0);
            Assert.NotNull(committedFiles);
            if (mutation == "none")
            {
                Assert.True(deliveries > 1);
                AssertPackageJoinFrames(fixture, join, before, claimAll, started, DateTime.Now, firstFrame, job: 5051);
            }
            else
            {
                Assert.Equal(1, deliveries);
                Assert.Single(fixture.Sent.Skip(firstFrame));
                Assert.Contains("mailbox claim delivery stopped", fixture.Log.ToString(), StringComparison.Ordinal);
            }
            AssertPackageJoinFiles(fixture, committedFiles!);
            var allocation = AssertPackageJoinAllocation(fixture, join);
            Assert.Equal(6, allocation.Allocation!.Count);
        }
        catch (Exception error) { errors.Add(error); }
        try
        {
            probe.Armed = false;
            join.SendObserver = null;
            client.Player.UID = Fixture.Uid;
            ninja.Exp = 75;
            physicalRow.Level = rowLevel;
            if (join.SessionStore.TryGet(join.Connection!, out var current) && current is not null)
            {
                Assert.True(LegacySecondaryPasswordState.TryRead(current.Character.Attributes, out var pin, out var reason), reason);
                Assert.True(LegacySecondaryPasswordRuntime.TryInitialize(join.Connection!, current, pin));
            }
        }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Delivery probe and native restoration failed.", errors);
        var files = fixture.PackageFiles();
        await join.RestartAsync();
        AssertPackageJoinFiles(fixture, files);
        Assert.Equal(1031U, join.NativeClient.Player.ClassExperience);
        Assert.Equal(75U, join.NativeClient.MyNinja!.Exp);
        var replayStart = fixture.Sent.Count;
        await join.FrameAsync(PackageJoinRequest(claimAll));
        AssertPackageJoinReplay(fixture, claimAll, replayStart);
        AssertPackageJoinFiles(fixture, files);
    }
}
