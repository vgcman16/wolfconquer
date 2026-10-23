using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly ConditionalWeakTable<ConquerTcpConnection, SemaphoreSlim> _legacyRuntimePublicationGates = new();

    private async ValueTask<LegacyRuntimePublicationLease> EnterLegacyRuntimePublicationAsync(
        ConquerTcpConnection connection,
        CancellationToken cancellationToken)
    {
        var gate = _legacyRuntimePublicationGates.GetValue(connection, static _ => new SemaphoreSlim(1, 1));
        var lease = new LegacyRuntimePublicationLease(gate);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return lease;
    }

    private sealed class LegacyRuntimePublicationLease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;

        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }
}
