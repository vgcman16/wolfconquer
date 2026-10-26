namespace WolfConquer.Core.World;

internal sealed partial class WorldOperationBarrier
{
    internal bool OwnsActiveLease(IDisposable? lease) => lease is Lease owned && owned.IsOwnedBy(this);

    // Packet handlers opt into their invocation admission. Cleanup and runtime
    // owners use ordinary leases even when ExecutionContext flowed from a send.
    internal ValueTask<IDisposable> EnterPacketSharedAsync(CancellationToken cancellationToken = default) =>
        EnterAdmittedAsync(exclusive: false, cancellationToken);

    internal ValueTask<IDisposable> EnterPacketExclusiveAsync(CancellationToken cancellationToken = default) =>
        EnterAdmittedAsync(exclusive: true, cancellationToken);

    private async ValueTask<IDisposable> EnterAdmittedAsync(bool exclusive, CancellationToken cancellationToken)
    {
        var lease = await EnterAsync(exclusive, cancellationToken).ConfigureAwait(false);
        try
        {
            LegacyMailboxPacketInvocation.RequireAfterLease(this);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}
