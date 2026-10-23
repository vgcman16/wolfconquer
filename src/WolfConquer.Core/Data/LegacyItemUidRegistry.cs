namespace WolfConquer.Core.Data;

// Only a trusted, quiescent startup/migration owner may provide these roots and
// historical floor. A nonempty evidence label is NOT itself proof of authority.
internal sealed record LegacyItemUidRegistryOptions(string UsersRoot, string ItemsRoot,
    string MailboxRoot, string RedeemFile, string ClaimFile,
    uint? HistoricalHighWater, string HistoricalEvidence);

internal enum LegacyItemUidInterruption { None, AfterPendingFlush, AfterStateReplace }

// Reservations are durable BEFORE return. Gaps are intentional and never refunded.
// This phase has no live-host/producer binding and cannot enable Hero grants alone.
internal sealed partial class LegacyItemUidRegistry : IDisposable
{
    internal const string RecoveryRequired = "itemUidRecoveryRequired";
    internal const int MaximumReservation = ushort.MaxValue;
    private readonly object gate = new();
    private readonly RegistryPaths paths;
    private readonly FileStream processLease;
    private RegistryState state;
    private string? blocked;
    private bool disposed;

    private LegacyItemUidRegistry(RegistryPaths paths, FileStream lease, RegistryState state)
    { this.paths = paths; processLease = lease; this.state = state; }

    internal sealed class Reservation
    {
        private readonly uint first;
        private readonly int count;
        private int consumed;
        private Reservation(uint first, int count) { this.first = first; this.count = count; }
        internal static Reservation Prepare(LegacyItemUidRegistry issuer, int count)
        {
            if (!Monitor.IsEntered(issuer.gate) || issuer.disposed || issuer.blocked is not null ||
                count is < 1 or > MaximumReservation || (ulong)issuer.state.HighWater + (uint)count > uint.MaxValue)
                throw new InvalidOperationException("A UID reservation can be prepared only by its live issuer gate.");
            return new(issuer.state.HighWater + 1, count);
        }
        // Single-use materialization, not permission to bypass a reward transaction.
        // A failed allocation burns this reservation too; no UID is returned twice.
        internal bool TryTake(out uint[] ids)
        {
            ids = [];
            if (Interlocked.Exchange(ref consumed, 1) != 0) return false;
            var result = new uint[count];
            for (var index = 0; index < count; index++) result[index] = first + (uint)index;
            ids = result;
            return true;
        }
    }

    internal bool TryReserve(int count, out Reservation? reservation, out string reason) =>
        TryReserve(count, LegacyItemUidInterruption.None, out reservation, out reason);

    // Typed owned-test crash boundary only: it can STOP the genuine writer, never
    // supply a successful reservation/save callback or choose replacement bytes.
    internal bool TryReserve(int count, LegacyItemUidInterruption interruption,
        out Reservation? reservation, out string reason)
    {
        lock (gate)
        {
            reservation = null;
            reason = "itemUidReservationInvalid";
            if (disposed) { reason = "itemUidRegistryDisposed"; return false; }
            if (blocked is not null) { reason = blocked; return false; }
            if (count is < 1 or > MaximumReservation || !Enum.IsDefined(interruption)) return false;
            if ((ulong)state.HighWater + (uint)count > uint.MaxValue || state.Generation == ulong.MaxValue)
            { reason = "itemUidNamespaceExhausted"; return false; }
            var next = state with { Generation = state.Generation + 1, HighWater = state.HighWater + (uint)count };
            var prepared = Reservation.Prepare(this, count);
            if (!TryAdvance(paths, state, next, interruption, out reason))
            {
                if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
                return false;
            }
            state = next;
            reservation = prepared;
            return true;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            processLease.Dispose();
        }
    }
}
