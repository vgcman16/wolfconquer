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
    private readonly CancellationTokenSource stop = new(TimeSpan.FromSeconds(30));
    private readonly Task<CommitAckDropObservation> run;
    private int disposed;

    private OwnedSqlCommitAckDropProxy(TcpListener listener, IPEndPoint upstream, string scoped)
    { this.listener = listener; this.upstream = upstream; this.scoped = scoped; run = RunAsync(); }

    internal static OwnedSqlCommitAckDropProxy Start(string ownedConnectionString)
    {
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
            return new(listener, upstream, builder.ConnectionString);
        }
        catch (Exception primary)
        {
            try { listener.Stop(); }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
            throw;
        }
    }

    internal SqlWorldStateRepository CreateWorldStateRepository() => new(scoped);
    internal Task<CommitAckDropObservation> WaitForCommitAckDropAsync() => run;

    private async Task<CommitAckDropObservation> RunAsync()
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
            observation = await RelayAsync(client, server, stop.Token);
        }
        catch (Exception error) { errors.Add(error); }
        try { client?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { server?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { listener.Stop(); } catch (Exception error) { errors.Add(error); }
        ThrowErrors(errors);
        return observation ?? throw new InvalidOperationException("Actual COMMIT acknowledgement was not observed.");
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
