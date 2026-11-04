using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class ConquerTcpServerRetirementTests
{
    private sealed class RetirementScope : IDisposable
    {
        private readonly StringWriter output = new();
        private readonly StringWriter errors = new();
        private readonly TextWriter synchronizedOutput;
        private readonly TextWriter synchronizedErrors;
        private readonly object countGate = new();
        private readonly List<int> closes = [];
        internal readonly List<TcpClient> Peers = [];
        private readonly Dictionary<TcpClient, NetworkStream> peerStreams = new(ReferenceEqualityComparer.Instance);
        internal readonly List<Task> PendingOperations = [];
        internal readonly ConquerTcpServer Server;
        internal Func<int, int, Task> OnClose = (_, _) => Task.CompletedTask;
        internal Action? ReleaseOnCleanup;
        internal Exception? HandlerFailure;
        internal int[] CloseAttempts { get { lock (countGate) return closes.ToArray(); } }
        internal string Output { get { lock (synchronizedOutput) return output.ToString(); } }
        internal string Errors { get { lock (synchronizedErrors) return errors.ToString(); } }

        internal RetirementScope()
        {
            synchronizedOutput = TextWriter.Synchronized(output); synchronizedErrors = TextWriter.Synchronized(errors);
            Server = new(new ConquerTcpServerOptions(ConquerRuntimeDefaults.WorldServer, IPAddress.Loopback,
                ListenPort: 0, Log: synchronizedOutput, ErrorLog: synchronizedErrors), HandleAsync);
        }

        private async ValueTask HandleAsync(ConquerTcpConnection connection, PacketFrame frame, CancellationToken token)
        {
            int index;
            lock (countGate) { index = closes.Count; closes.Add(0); }
            // Failure-only callback on a connection born by the actual TCP
            // listener. It creates no authentication, SQL/native or Store owner.
            Assert.True(connection.TryRegisterTransportCloseCleanup(() =>
            {
                int attempt;
                lock (countGate) attempt = ++closes[index];
                return OnClose(index, attempt);
            }));
            if (HandlerFailure is { } primary) ExceptionDispatchInfo.Capture(primary).Throw();
            await connection.SendAsync(frame.Frame.ToArray(), token);
        }

        internal async Task<TcpClient> ConnectAsync(bool expectEcho = true)
        {
            var peer = new TcpClient(); Peers.Add(peer);
            using var deadline = new CancellationTokenSource(Deadline);
            await peer.ConnectAsync(Server.BoundEndPoint.Address, Server.BoundEndPoint.Port, deadline.Token);
            byte[] packet = new PacketWriter(65012, 0).ToArray();
            var stream = peer.GetStream();
            peerStreams.Add(peer, stream);
            await stream.WriteAsync(packet.AsMemory(), deadline.Token);
            if (expectEcho)
            {
                byte[] echoed = new byte[packet.Length];
                await stream.ReadExactlyAsync(echoed.AsMemory(), deadline.Token);
                Assert.Equal(packet, echoed);
            }
            return peer;
        }

        internal async Task EndPeerSendAndObserveEofAsync(TcpClient peer, int expectedPending)
        {
            // GetStream rechecks TcpClient connectivity after half-close. Keep
            // using the exact stream acquired before this actual peer shutdown.
            var stream = peerStreams[peer];
            peer.Client.Shutdown(SocketShutdown.Send);
            using var eofDeadline = new CancellationTokenSource(Deadline);
            int count = await stream.ReadAsync(new byte[1].AsMemory(), eofDeadline.Token);
            Assert.Equal(0, count);
            var deadline = Stopwatch.StartNew();
            while (Server.ReadClientRetirementCounts() != (expectedPending, 0))
            {
                if (deadline.Elapsed >= Deadline) throw new TimeoutException("Actual TCP retirement/task state did not settle.");
                // Poll the actual producer/registry predicate; elapsed delay is
                // never treated as evidence that cleanup or task removal occurred.
                await Task.Delay(10);
            }
            Assert.Equal((expectedPending, 0), Server.ReadClientRetirementCounts());
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            foreach (var peer in Peers)
                try { peer.Dispose(); } catch (Exception failure) { failures.Add(failure); }
            foreach (var resource in new IDisposable[] { synchronizedOutput, synchronizedErrors, output, errors })
                try { resource.Dispose(); } catch (Exception failure) { failures.Add(failure); }
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }
    }
}
