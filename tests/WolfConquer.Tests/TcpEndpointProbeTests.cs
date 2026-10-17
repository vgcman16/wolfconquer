using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class TcpEndpointProbeTests
{
    [Fact]
    public async Task PassiveProbeReadsServerFirstBytesWithoutSendingPayload()
    {
        var listener = StartLoopbackListener();
        var endpoint = ToConquerEndpoint("ProbeFixture", listener);
        var clientBytesReceived = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acceptTask = AcceptAndSendAsync(listener, [0x10, 0x20, 0x30], clientBytesReceived);

        try
        {
            var result = await TcpEndpointProbe.ProbeAsync(endpoint, TimeSpan.FromSeconds(1));

            Assert.True(result.Connected);
            Assert.False(result.TimedOut);
            Assert.False(result.RemoteClosed);
            Assert.Null(result.Error);
            Assert.Equal(3, result.BytesRead);
            Assert.Equal("ECAw", result.PayloadBase64);
            Assert.Equal(ToSha256Hex([0x10, 0x20, 0x30]), result.PayloadSha256);
            Assert.Equal(0, await clientBytesReceived.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            listener.Stop();
            await IgnoreSocketShutdownAsync(acceptTask);
        }
    }

    [Fact]
    public async Task PassiveProbeReportsReadTimeoutWhenServerSendsNothing()
    {
        var listener = StartLoopbackListener();
        var endpoint = ToConquerEndpoint("QuietFixture", listener);
        var releaseServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acceptTask = AcceptWithoutSendingAsync(listener, releaseServer.Task);

        try
        {
            var result = await TcpEndpointProbe.ProbeAsync(endpoint, TimeSpan.FromMilliseconds(100));

            Assert.True(result.Connected);
            Assert.True(result.TimedOut);
            Assert.False(result.RemoteClosed);
            Assert.Null(result.Error);
            Assert.Equal(0, result.BytesRead);
            Assert.Equal(string.Empty, result.PayloadBase64);
            Assert.Equal(string.Empty, result.PayloadSha256);
        }
        finally
        {
            releaseServer.TrySetResult();
            listener.Stop();
            await IgnoreSocketShutdownAsync(acceptTask);
        }
    }

    [Fact]
    public async Task PassiveProbeSamplesEndpointRepeatedly()
    {
        var listener = StartLoopbackListener();
        var endpoint = ToConquerEndpoint("RepeatFixture", listener);
        var acceptTask = AcceptAndSendSequenceAsync(listener, [[0x01], [0x02]]);

        try
        {
            var results = await TcpEndpointProbe.ProbeManyAsync(
                endpoint,
                samples: 2,
                timeout: TimeSpan.FromSeconds(1),
                sampleDelay: TimeSpan.Zero);

            Assert.Collection(
                results,
                first =>
                {
                    Assert.Equal("AQ==", first.PayloadBase64);
                    Assert.Equal(ToSha256Hex([0x01]), first.PayloadSha256);
                },
                second =>
                {
                    Assert.Equal("Ag==", second.PayloadBase64);
                    Assert.Equal(ToSha256Hex([0x02]), second.PayloadSha256);
                });
        }
        finally
        {
            listener.Stop();
            await IgnoreSocketShutdownAsync(acceptTask);
        }
    }

    private static TcpListener StartLoopbackListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static ConquerEndpoint ToConquerEndpoint(string role, TcpListener listener)
    {
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        return new ConquerEndpoint(role, "127.0.0.1", endpoint.Port);
    }

    private static async Task AcceptAndSendAsync(
        TcpListener listener,
        byte[] payload,
        TaskCompletionSource<int> clientBytesReceived)
    {
        using var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();
        await stream.WriteAsync(payload);
        await stream.FlushAsync();

        var buffer = new byte[1];
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        try
        {
            clientBytesReceived.TrySetResult(await stream.ReadAsync(buffer, timeout.Token));
        }
        catch (OperationCanceledException)
        {
            clientBytesReceived.TrySetResult(0);
        }
    }

    private static async Task AcceptWithoutSendingAsync(TcpListener listener, Task releaseServer)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await releaseServer;
    }

    private static async Task AcceptAndSendSequenceAsync(TcpListener listener, IReadOnlyList<byte[]> payloads)
    {
        foreach (var payload in payloads)
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            await stream.WriteAsync(payload);
            await stream.FlushAsync();
        }
    }

    private static string ToSha256Hex(byte[] payload)
    {
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static async Task IgnoreSocketShutdownAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException)
        {
        }
        catch (TimeoutException)
        {
        }
    }
}
