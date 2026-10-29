using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [Fact]
    public void EndpointIsDefaultOffBeforeAnyOwnerOrPipeWork()
    {
        var previous = Environment.GetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR");
        try
        {
            Environment.SetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR", null);
            Assert.Null(LocalRuntimeInspectionPipe.StartIfEnabled(null!, null!, Guid.Empty));
        }
        finally { Environment.SetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR", previous); }
    }

    [WindowsOperatorPipeFact]
    public Task ActualCurrentUserPipeRepeatsDiagnosticsAndReleasesWorldBeforeAwaitingReceipt() => WithPipe(async (f, endpoint) =>
    {
        var before = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var peer = Peer(endpoint.PipeName); await peer.ConnectAsync(deadline.Token);
        var query = Query(f); await Send(peer, query, deadline.Token);
        var result = await Receive(peer, deadline.Token); Diagnostic(result); Assert.Equal(query.QueryId, result.QueryId);
        var operations = (WorldOperationBarrier)typeof(WorldServerFrameHandler).GetField("_worldOperations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Handler)!;
        using (await operations.EnterExclusiveAsync(deadline.Token)) { } // Must acquire while the real pipe still awaits its receipt.
        await peer.WriteAsync(LocalRuntimeInspectionProtocol.ResponseReceipt(result.QueryId), deadline.Token); await peer.FlushAsync(deadline.Token);
        await Status(endpoint, "diagnosticResponseAcknowledged", deadline.Token);
        peer.Dispose();
        var second = await Exchange(endpoint.PipeName, Query(f)); Diagnostic(second);
        var foreign = await Exchange(endpoint.PipeName, Query(f) with { ExpectedDomain = Guid.NewGuid() }); Rejected(foreign);
        Assert.Equal(water, f.HighWater); Unchanged(f, before, sent);
    });

    [WindowsOperatorPipeFact]
    public Task PartialRequestCancellationJoinsActualWorkerAndWrongMailReceiptNeverCompletesInspectorExchange() => WithPipe(async (f, endpoint) =>
    {
        var before = Files(f); var sent = f.SentSnapshot();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using (var peer = Peer(endpoint.PipeName))
        {
            await peer.ConnectAsync(deadline.Token); var query = Query(f); await Send(peer, query, deadline.Token);
            var result = await Receive(peer, deadline.Token); Diagnostic(result);
            await peer.WriteAsync(LocalOperatorMailPipe.CreateResponseReceipt(query.QueryId), deadline.Token); await peer.FlushAsync(deadline.Token);
            await Status(endpoint, "peerIoStoppedNoDiagnosticCompletion", deadline.Token);
        }
        using var partial = Peer(endpoint.PipeName); await partial.ConnectAsync(deadline.Token);
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, 100);
        await partial.WriteAsync(header, deadline.Token); await partial.WriteAsync("{}"u8.ToArray(), deadline.Token); await partial.FlushAsync(deadline.Token);
        await endpoint.DisposeAsync();
        Assert.True(endpoint.Completion.IsCompleted); await endpoint.Completion;
        Unchanged(f, before, sent);
    });

    private static Task WithPipe(Func<OperatorGearMailFixture, LocalRuntimeInspectionPipe, Task> body) =>
        LegacyOperatorGearMailOwnerTests.WithOwnerAsync(async f =>
        {
            var previous = Environment.GetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR");
            LocalRuntimeInspectionPipe? endpoint = null; Exception? primary = null;
            try
            {
                Environment.SetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR", "1");
                endpoint = Assert.IsType<LocalRuntimeInspectionPipe>(LocalRuntimeInspectionPipe.StartIfEnabled(f.Handler, f.Runtime, f.Domain));
                await body(f, endpoint);
            }
            catch (Exception error) { primary = error; }
            Exception? cleanup = null;
            try { if (endpoint is not null) await endpoint.DisposeAsync(); } catch (Exception error) { cleanup = error; }
            finally { Environment.SetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR", previous); }
            if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
            if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
            if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
            Assert.True(endpoint!.Completion.IsCompleted);
        });
    private static NamedPipeClientStream Peer(string name) => new(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    private static async Task Send(NamedPipeClientStream peer, LegacyRuntimeInspectionRequest query, CancellationToken token)
    {
        var body = LocalRuntimeInspectionProtocol.RequestBytes(query); var frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length); body.CopyTo(frame, 4);
        await peer.WriteAsync(frame, token); await peer.FlushAsync(token);
    }
    private static async Task<LegacyRuntimeInspectionResult> Receive(NamedPipeClientStream peer, CancellationToken token)
    {
        var header = new byte[4]; await peer.ReadExactlyAsync(header, token); var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        Assert.InRange(length, 1, LocalRuntimeInspectionProtocol.MaximumResponseBytes);
        var body = new byte[length]; await peer.ReadExactlyAsync(body, token); return LocalRuntimeInspectionProtocol.ParseResult(body);
    }
    private static async Task<LegacyRuntimeInspectionResult> Exchange(string name, LegacyRuntimeInspectionRequest query)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)); using var peer = Peer(name);
        await peer.ConnectAsync(deadline.Token); await Send(peer, query, deadline.Token);
        var result = await Receive(peer, deadline.Token); Assert.Equal(query.QueryId, result.QueryId);
        await peer.WriteAsync(LocalRuntimeInspectionProtocol.ResponseReceipt(result.QueryId), deadline.Token); await peer.FlushAsync(deadline.Token);
        return result;
    }
    private static async Task Status(LocalRuntimeInspectionPipe endpoint, string expected, CancellationToken token)
    {
        while (endpoint.LastTransportStatus != expected) { token.ThrowIfCancellationRequested(); await Task.Delay(10, token); }
    }
}
