using System.Net;
using System.Net.Sockets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldPublicHeartbeatTests
{
    [Fact]
    public async Task RealListenerLifecycleCannotPublishOnlineBeforeListenAndStopsOnlyAfterActualDispose() => await Run(async f =>
    {
        var publisher = f.Publish(); Assert.True(await publisher.PublishOnceAsync());
        var before = f.Read(); Assert.Equal("starting", before.State); Assert.Null(before.StartedAtUtc); Assert.Equal(0, before.UptimeSeconds);
        f.Server.Start(); Assert.True(f.Server.IsRunning);
        Assert.True(await publisher.PublishOnceAsync());
        var listening = f.Read(); Assert.NotNull(listening.StartedAtUtc); Assert.True(listening.UptimeSeconds >= 0);
        Assert.Equal("unavailable", listening.State); // Actual Frame has no configured snapshot owner.
        await publisher.StopPollingAsync();
        Assert.True(f.Server.IsRunning); // Exporter cancellation is not TCP shutdown.
        await f.StopServer(); Assert.False(f.Server.IsRunning);
        await publisher.DisposeAsync();
        var stopped = f.Read(); Assert.Equal("offline", stopped.State);
        Assert.Equal(listening.StartedAtUtc, stopped.StartedAtUtc); Assert.Equal(f.Instance, stopped.InstanceId);
    });

    [Fact]
    public async Task ForeignFramePairRefusesBeforeCreatingAnyStatusFile() => await Run(async f =>
    {
        var foreign = new WorldServerFrameHandler(null, TextWriter.Null);
        Assert.Throws<InvalidOperationException>(() => WorldPublicHeartbeatPublisher.StartIfEnabled(f.Server, foreign, f.Options));
        Assert.False(Directory.Exists(f.Root)); await Task.CompletedTask;
    });

    [Fact]
    public async Task ActualBindFailureIsUnavailableWithNoInventedStartTime()
    {
        var occupied = new TcpListener(IPAddress.Loopback, 0); occupied.Start();
        try
        {
            int port = ((IPEndPoint)occupied.LocalEndpoint).Port;
            await Run(async f =>
            {
                Assert.ThrowsAny<SocketException>(() => f.Server.Start());
                Assert.True(await f.Publish().PublishOnceAsync());
                var status = f.Read(); Assert.Equal("unavailable", status.State);
                Assert.Null(status.StartedAtUtc); Assert.Equal(0, status.UptimeSeconds); Assert.False(f.Server.IsRunning);
            }, port: port);
        }
        finally { occupied.Stop(); }
    }

    [Fact]
    public async Task RealAdmittedFrameHolderIsCountedAndActualTransportCloseRemovesIt()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async actual =>
        {
            await Run(async f =>
            {
                f.Server.Start(); var publisher = f.Publish();
                var beforeRecovery = actual.Recovery.ReadPublicHeartbeatRecovery();
                Assert.True(await publisher.PublishOnceAsync());
                Assert.Equal(beforeRecovery, actual.Recovery.ReadPublicHeartbeatRecovery()); // No recovery initialization by capture.
                Assert.Equal(0, f.Read().OnlinePlayers);
                await actual.Login();
                Assert.True(await publisher.PublishOnceAsync());
                var admitted = f.Read(); Assert.Equal("online", admitted.State); Assert.Equal(1, admitted.OnlinePlayers);
                Assert.Equal("VisibleDx8D13", admitted.Profile);
                await actual.Connection.CompleteTransportCloseAsync();
                Assert.True(await publisher.PublishOnceAsync()); Assert.Equal(0, f.Read().OnlinePlayers);
            }, actual.Handler);
        });
    }

    [Fact]
    public async Task RealRecoveryFenceIsReportedWithoutHidingCurrentAdmittedHolderOrLeakingTheReason()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async actual =>
        {
            await actual.Login();
            await Run(async f =>
            {
                f.Server.Start(); var publisher = f.Publish(); Assert.True(await publisher.PublishOnceAsync());
                Assert.Equal("online", f.Read().State);
                actual.Recovery.Block("owned-heartbeat-private-reason");
                Assert.True(await publisher.PublishOnceAsync());
                Assert.Equal("recovery_required", f.Read().State); Assert.Equal(1, f.Read().OnlinePlayers);
                Assert.DoesNotContain("owned-heartbeat-private-reason", File.ReadAllText(f.FilePath), StringComparison.Ordinal);
            }, actual.Handler);
        });
    }

    [Fact]
    public async Task ChangedActualPlayerOwnerMakesCountUnavailableWithoutChangingGameState()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async actual =>
        {
            await actual.Login(); var current = actual.Current; var player = actual.Native.Player;
            var files = actual.Files();
            await Run(async f =>
            {
                f.Server.Start(); var publisher = f.Publish(); Assert.True(await publisher.PublishOnceAsync());
                actual.Native.Player = new LegacyNativePlayerRuntime(actual.Native);
                try
                {
                    Assert.True(await publisher.PublishOnceAsync());
                    Assert.Equal("unavailable", f.Read().State); Assert.Null(f.Read().OnlinePlayers);
                    Assert.Same(current, actual.Current); Assert.False(actual.Recovery.IsBlocked); actual.CheckFiles(files);
                }
                finally { actual.Native.Player = player; }
            }, actual.Handler);
        });
    }
}
