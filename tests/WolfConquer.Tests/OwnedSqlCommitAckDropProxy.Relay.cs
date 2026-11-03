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

    private static async Task<CommitAckDropObservation?> RelayAsync(TcpClient client, TcpClient server, int skipCommitCount, CommitAckGate? gate, CancellationToken lifetime)
    {
        using var pairStop = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        var state = new RelayState();
        var outbound = RelayClientAsync(client.GetStream(), server.GetStream(), state, skipCommitCount, gate, pairStop.Token);
        var inbound = RelayServerAsync(server.GetStream(), client.GetStream(), state, skipCommitCount, gate, pairStop.Token);
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
        if (gate is not null && gate.Forwarded.Task.IsCompletedSuccessfully && state.CommitQueries == skipCommitCount + 1) return null;
        if (state.CommitQueries != skipCommitCount + 1 || state.Dropped != 1)
            throw new InvalidDataException("Owned SQL proxy ended without exactly one actual COMMIT ACK drop.");
        return new(state.CommitQueries, state.Dropped, state.Sequence, state.PayloadBytes, state.Status, true);
    }

    private static async Task RelayClientAsync(NetworkStream client, NetworkStream server, RelayState state, int skipCommitCount, CommitAckGate? gate, CancellationToken token)
    {
        var budget = new PacketBudget(); bool first = true;
        try
        {
            while (true)
            {
                using var packet = await ReadPacketAsync(client, budget, token, gate);
                if (first) { RequirePlainProtocol41(packet); first = false; }
                if (IsExactCommitQuery(packet))
                {
                    if (Interlocked.Increment(ref state.CommitQueries) > skipCommitCount + 1 ||
                        Interlocked.CompareExchange(ref state.AwaitingCommitAck, 1, 0) != 0)
                        throw new InvalidDataException("Owned SQL proxy accepts only one COMMIT boundary.");
                    if (gate is { HoldRequest: true } && Volatile.Read(ref state.CommitQueries) == skipCommitCount + 1)
                    {
                        gate.RequestObserved.TrySetResult(new CommitRequestGateObservation(state.CommitQueries, packet.Sequence, packet.PayloadLength));
                        await gate.RequestRelease.Task.WaitAsync(token); // Hold the real request before forwarding it.
                    }
                }
                if (gate is not null && Volatile.Read(ref gate.Forwarding) != 0 && packet.Sequence == 0 && packet.PayloadLength == 1 && packet.Payload[0] == 0x01)
                    Volatile.Write(ref gate.QuitForwarded, 1);
                await server.WriteAsync(packet.Wire, token); // All other packets are byte-for-byte relayed.
            }
        }
        catch (GatedClientEofException) when (gate is not null && Volatile.Read(ref gate.Forwarding) != 0) { }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static async Task RelayServerAsync(NetworkStream server, NetworkStream client, RelayState state, int skipCommitCount, CommitAckGate? gate, CancellationToken token)
    {
        var budget = new PacketBudget();
        try
        {
            while (true)
            {
                using var packet = await ReadPacketAsync(server, budget, token, gate, serverDirection: true);
                if (Volatile.Read(ref state.AwaitingCommitAck) != 0)
                {
                    state.Status = RequireActualCommitOk(packet);
                    if (Volatile.Read(ref state.CommitQueries) <= skipCommitCount)
                    {
                        Volatile.Write(ref state.AwaitingCommitAck, 0);
                        await client.WriteAsync(packet.Wire, token);
                        continue;
                    }
                    if (gate is not null)
                    {
                        gate.Observed.TrySetResult(new CommitAckGateObservation(state.CommitQueries, packet.Sequence, packet.PayloadLength, state.Status));
                        if (!gate.HoldRequest) await gate.Release.Task.WaitAsync(token);
                        Volatile.Write(ref state.AwaitingCommitAck, 0);
                        Volatile.Write(ref gate.Forwarding, 1);
                        try
                        {
                            await client.WriteAsync(packet.Wire, token); // The actual validated OK, unchanged.
                            gate.Forwarded.TrySetResult(true);
                        }
                        catch (Exception error) { gate.Forwarded.TrySetException(error); throw; }
                        continue;
                    }
                    state.Sequence = packet.Sequence; state.PayloadBytes = packet.PayloadLength;
                    Volatile.Write(ref state.Dropped, 1);
                    return; // Real OK observed, deliberately not forwarded. No synthetic reply.
                }
                await client.WriteAsync(packet.Wire, token);
            }
        }
        catch (GatedClientEofException) when (gate is not null && Volatile.Read(ref gate.QuitForwarded) != 0) { }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
}
