using System.Buffers.Binary;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    private sealed class WindowsOperatorPipeTheoryAttribute : TheoryAttribute
    {
        public WindowsOperatorPipeTheoryAttribute()
        { if (!OperatingSystem.IsWindows()) Skip = "Actual CurrentUserOnly abrupt-peer pipe recovery requires Windows."; }
    }

    [WindowsOperatorPipeTheory]
    [InlineData("partial-header")]
    [InlineData("partial-body")]
    [InlineData("partial-receipt")]
    public Task ActualAbruptPeerEofLeavesInspectorAvailableForTheNextGenuineDiagnostic(string phase) =>
        WithPipe(async (f, endpoint) =>
        {
            var files = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater; var current = f.Current;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using (var peer = Peer(endpoint.PipeName))
            {
                await peer.ConnectAsync(deadline.Token);
                var query = Query(f);
                if (phase == "partial-header")
                {
                    // Fewer than the real four-byte envelope header.
                    await peer.WriteAsync(new byte[] { 0x40, 0x00 }, deadline.Token);
                }
                else if (phase == "partial-body")
                {
                    var body = LocalRuntimeInspectionProtocol.RequestBytes(query);
                    Assert.True(body.Length > 1);
                    var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
                    await peer.WriteAsync(header, deadline.Token);
                    await peer.WriteAsync(body.AsMemory(0, body.Length - 1), deadline.Token);
                }
                else if (phase == "partial-receipt")
                {
                    await Send(peer, query, deadline.Token);
                    var observed = await Receive(peer, deadline.Token); Diagnostic(observed);
                    Assert.Equal(query.QueryId, observed.QueryId);
                    await Status(endpoint, "diagnosticAwaitingReceipt", deadline.Token);
                    var receipt = LocalRuntimeInspectionProtocol.ResponseReceipt(query.QueryId);
                    await peer.WriteAsync(receipt.AsMemory(0, receipt.Length - 1), deadline.Token);
                }
                else throw new ArgumentOutOfRangeException(nameof(phase));
                await peer.FlushAsync(deadline.Token);
                // Dispose below is the genuine EOF. No complete header/body/
                // receipt is substituted and the endpoint is not cancelled.
            }
            await Status(endpoint, "peerIoStoppedNoDiagnosticCompletion", deadline.Token);
            Assert.False(endpoint.Completion.IsCompleted);
            Diagnostic(await Exchange(endpoint.PipeName, Query(f)));
            Assert.False(endpoint.Completion.IsCompleted);
            Assert.Same(current, f.Current); Assert.False(f.Native.OrdinarySpellBinding!.IsRetired);
            Assert.Equal(water, f.HighWater); Unchanged(f, files, sent);
        });
}
