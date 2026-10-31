using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneBagEvidence
{
    internal void RequireNamespaceObservationReady(LegacyOwnedAdmissionRuntime actual, WorldServerFrameHandler actualFrame)
    {
        if (!ReferenceEquals(actual, runtime) || !ReferenceEquals(actualFrame, frame) || !DependenciesAreCurrent()) throw Invalid();
        before.RequireRuneBagOwner(context, stable);
    }

    internal void RequireRawScanRoots(string items, string mailbox, string redeem, string claim)
    {
        RequireNamespaceObservationReady(runtime, frame);
        var actual = runtime.RequireRuneBagConfiguration(frame);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(items, actual.ItemsRoot, comparison) || !string.Equals(mailbox, actual.MailboxRoot, comparison) ||
            !string.Equals(redeem, actual.RedeemFile, comparison) || !string.Equals(claim, actual.ClaimFile, comparison)) throw Invalid();
    }

    // Called by the raw scanner's closed actual-Evidence branch. There is no
    // per-row I/O, Runtime acquisition, ignored path or caller permission.
    internal void RequireUnoccupiedPhysicalBirth(uint uid)
    {
        if (issuedAllocation is not { } allocation) throw Invalid();
        if (uid == allocation.First) throw new InvalidDataException("A physical input already owns the actual RuneBag reserved birth.");
    }

    private void RequireRawNamespace()
    {
        RequireNamespaceObservationReady(runtime, frame);
        var actual = runtime.RequireRuneBagConfiguration(frame);
        LegacyItemUidRawCatalog.Observation observed; string reason;
        bool valid = issuedAllocation is null
            ? LegacyItemUidRawCatalog.TryScan(actual.ItemsRoot, actual.MailboxRoot, actual.RedeemFile, actual.ClaimFile, out observed, out reason)
            : LegacyItemUidRawCatalog.TryScanRuneBag(actual.ItemsRoot, actual.MailboxRoot, actual.RedeemFile, actual.ClaimFile, this, out observed, out reason);
        if (!valid) throw new InvalidDataException("RuneBag physical namespace is unproven: " + reason);
        runtime.RequireRuneBagObservedMaximum(frame, this, observed.Maximum);
    }
}
