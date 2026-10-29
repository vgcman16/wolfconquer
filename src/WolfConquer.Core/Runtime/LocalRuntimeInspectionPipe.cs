using System.IO.Pipes;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

public sealed partial class LocalRuntimeInspectionPipe : IAsyncDisposable
{
    private readonly WorldServerFrameHandler handler;
    private readonly LegacyOwnedAdmissionRuntime owner;
    private readonly Guid domain;
    private readonly NamedPipeServerStream pipe;
    private readonly CancellationTokenSource stop;
    private readonly Task worker;
    private readonly object disposalGate = new();
    private Task? disposal;
    public string PipeName { get; }
    public Task Completion => worker;
    public string LastTransportStatus { get; private set; } = "waiting";

    private LocalRuntimeInspectionPipe(WorldServerFrameHandler handler, LegacyOwnedAdmissionRuntime owner,
        Guid domain, CancellationToken cancellationToken)
    {
        this.handler = handler; this.owner = owner; this.domain = domain;
        PipeName = LocalRuntimeInspectionProtocol.NameFor(domain);
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.ShutdownToken);
        try
        {
            pipe = new(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance,
                LocalRuntimeInspectionProtocol.MaximumRequestBytes + 4, 64 * 1024);
        }
        catch { stop.Dispose(); throw; }
        worker = RunAsync();
    }
    public static LocalRuntimeInspectionPipe? StartIfEnabled(WorldServerFrameHandler handler,
        LegacyOwnedAdmissionRuntime owner, Guid domain, CancellationToken cancellationToken = default)
    {
        if (Environment.GetEnvironmentVariable("WOLFCONQUER_RUNTIME_INSPECTOR") != "1") return null;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Local runtime inspection requires Windows.");
        ArgumentNullException.ThrowIfNull(handler); ArgumentNullException.ThrowIfNull(owner);
        if (!handler.CanInspectOwnedRuntime(owner, domain))
            throw new InvalidOperationException("Runtime inspection requires the actual current bound owner/domain.");
        return new(handler, owner, domain, cancellationToken);
    }
    private async Task RunAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try { await pipe.WaitForConnectionAsync(stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            Exception? primary = null;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                try { await HandleAsync(deadline.Token).ConfigureAwait(false); }
                catch (IOException) { LastTransportStatus = "peerIoStoppedNoDiagnosticCompletion"; }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested)
                { LastTransportStatus = "deadlineOrShutdownNoDiagnosticCompletion"; }
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                // WaitForConnectionAsync succeeded: a broken peer must also be reset.
                try { pipe.Disconnect(); }
                catch (Exception cleanup) when (primary is not null)
                { throw new AggregateException("Inspector worker and disconnect failed.", primary, cleanup); }
            }
        }
    }
    private async Task HandleAsync(CancellationToken cancellationToken)
    {
        LegacyRuntimeInspectionRequest request;
        try { request = LocalRuntimeInspectionProtocol.ParseRequest(await ReadAsync(cancellationToken).ConfigureAwait(false)); }
        catch (JsonException)
        {
            await WriteAsync(new(1, Guid.Empty, LegacyRuntimeInspectionStatus.Rejected, "invalidInspectionRequest", null), cancellationToken).ConfigureAwait(false);
            LastTransportStatus = "invalidRequestRejected"; return;
        }
        var result = request.ExpectedDomain == domain
            ? await handler.InspectOwnedRuntimeAsync(owner, request, cancellationToken).ConfigureAwait(false)
            : new(1, request.QueryId, LegacyRuntimeInspectionStatus.Rejected, "inspectionPipeDomainMismatch", null);
        // All World/Store/actor gates have already been released; only detached values enter transport.
        await WriteAsync(result, cancellationToken).ConfigureAwait(false);
        LastTransportStatus = "diagnosticResponseAcknowledged";
    }
}
