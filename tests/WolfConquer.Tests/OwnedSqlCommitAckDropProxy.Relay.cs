using System.Net.Sockets;

namespace WolfConquer.Tests;

internal sealed partial class OwnedSqlCommitAckDropProxy
{
    private sealed class RelayState
    {
        internal int AwaitingCommitAck, CommitQueries, Dropped;
        internal byte Sequence;
        internal int PayloadBytes;
        internal ushort Status;
    }

    private static async Task<CommitAckDropObservation> RelayAsync(TcpClient client, TcpClient server, CancellationToken lifetime)
    {
        using var pairStop = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        var state = new RelayState();
        var outbound = RelayClientAsync(client.GetStream(), server.GetStream(), state, pairStop.Token);
        var inbound = RelayServerAsync(server.GetStream(), client.GetStream(), state, pairStop.Token);
        await Task.WhenAny(outbound, inbound);
        var errors = new List<Exception>();
        try { await pairStop.CancelAsync(); } catch (Exception error) { errors.Add(error); }
        // Server OK is fully read before its relay returns. It is never written to
        // the client. Closing that socket now creates the genuine lost-ACK boundary.
        try { client.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { server.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { await outbound; } catch (Exception error) { errors.Add(error); }
        try { await inbound; } catch (Exception error) { errors.Add(error); }
        ThrowErrors(errors);
        lifetime.ThrowIfCancellationRequested(); // Deadline/teardown is never drop success.
        if (state.CommitQueries != 1 || state.Dropped != 1)
            throw new InvalidDataException("Owned SQL proxy ended without exactly one actual COMMIT ACK drop.");
        return new(state.CommitQueries, state.Dropped, state.Sequence, state.PayloadBytes, state.Status, true);
    }

    private static async Task RelayClientAsync(NetworkStream client, NetworkStream server, RelayState state, CancellationToken token)
    {
        var budget = new PacketBudget(); bool first = true;
        try
        {
            while (true)
            {
                using var packet = await ReadPacketAsync(client, budget, token);
                if (first) { RequirePlainProtocol41(packet); first = false; }
                if (IsExactCommitQuery(packet))
                {
                    if (Interlocked.Increment(ref state.CommitQueries) != 1 ||
                        Interlocked.CompareExchange(ref state.AwaitingCommitAck, 1, 0) != 0)
                        throw new InvalidDataException("Owned SQL proxy accepts only one COMMIT boundary.");
                }
                await server.WriteAsync(packet.Wire, token); // All other packets are byte-for-byte relayed.
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static async Task RelayServerAsync(NetworkStream server, NetworkStream client, RelayState state, CancellationToken token)
    {
        var budget = new PacketBudget();
        try
        {
            while (true)
            {
                using var packet = await ReadPacketAsync(server, budget, token);
                if (Volatile.Read(ref state.AwaitingCommitAck) != 0)
                {
                    state.Status = RequireActualCommitOk(packet);
                    state.Sequence = packet.Sequence; state.PayloadBytes = packet.PayloadLength;
                    Volatile.Write(ref state.Dropped, 1);
                    return; // Real OK observed, deliberately not forwarded. No synthetic reply.
                }
                await client.WriteAsync(packet.Wire, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
