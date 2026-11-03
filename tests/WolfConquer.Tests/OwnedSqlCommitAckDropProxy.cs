using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using MySqlConnector;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

// Test-only, single connection to the existing owned MariaDB fixture. No payload logging.
internal sealed partial class OwnedSqlCommitAckDropProxy : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly IPEndPoint upstream;
    private readonly string scoped;
    private readonly int skipCommitCount;
    private readonly CommitAckGate? gate;
    private readonly CancellationTokenSource stop = new(TimeSpan.FromSeconds(30));
    private readonly Task<CommitAckDropObservation?> run;
    private int disposed;

    private OwnedSqlCommitAckDropProxy(TcpListener listener, IPEndPoint upstream, string scoped, int skipCommitCount, bool holdCommitAck, bool holdCommitRequest)
    { this.listener = listener; this.upstream = upstream; this.scoped = scoped; this.skipCommitCount = skipCommitCount; gate = holdCommitAck || holdCommitRequest ? new CommitAckGate(holdCommitRequest) : null; run = RunAsync(); }

    internal static OwnedSqlCommitAckDropProxy Start(string ownedConnectionString, int skipCommitCount = 0, bool holdCommitAck = false, bool holdCommitRequest = false)
    {
        if (skipCommitCount is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(skipCommitCount));
        if ((holdCommitAck || holdCommitRequest) && skipCommitCount != 1) throw new ArgumentException("Held ACK requires exactly one acquisition COMMIT.", nameof(skipCommitCount));
        if (holdCommitAck && holdCommitRequest) throw new ArgumentException("Only one held COMMIT boundary is permitted.");
        var builder = new MySqlConnectionStringBuilder(ownedConnectionString);
        var server = builder.Server.Trim();
        var address = server.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? IPAddress.Loopback :
            IPAddress.TryParse(server, out var parsed) && IPAddress.IsLoopback(parsed) ? parsed :
            throw new InvalidOperationException("Owned SQL proxy requires one literal loopback endpoint.");
        if (builder.Port is < 1024 or > 65535 || builder.Port == 3306 ||
            !builder.Database.StartsWith("wolf_sql_auth_", StringComparison.Ordinal))
            throw new InvalidOperationException("Owned SQL proxy requires the separate scoped fixture endpoint.");
        var upstream = new IPEndPoint(address, checked((int)builder.Port));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start(1);
            builder.Server = "127.0.0.1"; builder.Port = checked((uint)((IPEndPoint)listener.LocalEndpoint).Port);
            builder.ConnectionProtocol = MySqlConnectionProtocol.Sockets;
            builder.SslMode = MySqlSslMode.None; builder.UseCompression = false;
            builder.Pooling = false; builder.Pipelining = false;
            builder.ConnectionTimeout = 10; builder.DefaultCommandTimeout = 10;
            return new(listener, upstream, builder.ConnectionString, skipCommitCount, holdCommitAck, holdCommitRequest);
        }
        catch (Exception primary)
        {
            try { listener.Stop(); }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
            throw;
        }
    }

    internal SqlWorldStateRepository CreateWorldStateRepository() => new(scoped);
    internal async Task<CommitAckDropObservation> WaitForCommitAckDropAsync()
    {
        if (gate is not null) throw new InvalidOperationException("Held ACK is not a dropped ACK.");
        return await run ?? throw new InvalidOperationException("Actual COMMIT ACK drop was not observed.");
    }
    internal Task<CommitAckGateObservation> WaitForHeldCommitAckAsync() => gate is { HoldRequest: false } ? gate.Observed.Task : throw new InvalidOperationException("Proxy is not in held ACK mode.");
    internal Task<CommitRequestGateObservation> WaitForHeldCommitRequestAsync() => gate is { HoldRequest: true } ? gate.RequestObserved.Task : throw new InvalidOperationException("Proxy is not in held request mode.");
    internal void ReleaseHeldCommitRequest() { if (gate is not { HoldRequest: true }) throw new InvalidOperationException("Proxy is not in held request mode."); gate.RequestRelease.TrySetResult(true); }
    internal async Task<CommitAckGateObservation> WaitForForwardedCommitAsync()
    {
        if (gate is not { HoldRequest: true }) throw new InvalidOperationException("Proxy is not in held request mode.");
        var observed = await gate.Observed.Task;
        await gate.Forwarded.Task;
        return observed;
    }
    internal void ReleaseHeldCommitAck() { if (gate is not { HoldRequest: false }) throw new InvalidOperationException("Proxy is not in held ACK mode."); gate.Release.TrySetResult(true); }

    private async Task<CommitAckDropObservation?> RunAsync()
    {
        TcpClient? client = null, server = null;
        CommitAckDropObservation? observation = null;
        var errors = new List<Exception>();
        try
        {
            try { client = await listener.AcceptTcpClientAsync(stop.Token); }
            catch (Exception error) when (Volatile.Read(ref disposed) != 0 && stop.IsCancellationRequested &&
                error is ObjectDisposedException or SocketException { SocketErrorCode: SocketError.OperationAborted or SocketError.Interrupted })
            { throw new OperationCanceledException("Owned SQL proxy stopped before accept.", error, stop.Token); }
            listener.Stop(); // Exactly one owned client; no persistent listening surface.
            client.NoDelay = true;
            server = new TcpClient(upstream.AddressFamily); // Owned before the connect await.
            server.NoDelay = true;
            await server.ConnectAsync(upstream.Address, upstream.Port, stop.Token);
            observation = await RelayAsync(client, server, skipCommitCount, gate, stop.Token);
        }
        catch (Exception error) { errors.Add(error); }
        try { client?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { server?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { listener.Stop(); } catch (Exception error) { errors.Add(error); }
        ThrowErrors(errors);
        if (gate is null && observation is null) throw new InvalidOperationException("Actual COMMIT acknowledgement was not observed.");
        return observation;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        var errors = new List<Exception>();
        try { await stop.CancelAsync(); } catch (Exception error) { errors.Add(error); }
        try { listener.Stop(); } catch (Exception error) { errors.Add(error); }
        try { await run; }
        catch (OperationCanceledException error) when (stop.IsCancellationRequested && error.CancellationToken == stop.Token)
        { /* Owned teardown cancellation is not successful drop evidence; awaiting run still refuses it. */ }
        catch (Exception error) { errors.Add(error); }
        try { stop.Dispose(); } catch (Exception error) { errors.Add(error); }
        ThrowErrors(errors);
    }

    private static void ThrowErrors(List<Exception> errors)
    {
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
}

internal sealed record CommitAckDropObservation(int ObservedCommitQueries, int DroppedServerOkPackets,
    byte AckSequence, int AckPayloadBytes, ushort StatusFlags, bool ClientSocketClosed);
