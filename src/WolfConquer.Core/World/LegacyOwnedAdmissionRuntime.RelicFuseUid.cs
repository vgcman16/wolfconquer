using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyRelicFuseNamespaceInterruption { None, AfterReservation }

public sealed partial class LegacyOwnedAdmissionRuntime
{
    private LegacyRelicFuseOwnedEvidence? activeRelicFuseAllocation;
    private RelicFuseStartupAuthority? activeRelicFuseStartup;
    private bool relicFuseStartupComplete;
    internal LegacyRelicFuseNamespaceInterruption RelicFuseNamespaceInterruption { get; set; }
    internal LegacyItemUidInterruption RelicFuseNamespaceRetirementInterruption { get; set; }

    internal sealed class RelicFuseStartupAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime owner;
        private readonly int thread;
        internal RelicFuseStartupAuthority(LegacyOwnedAdmissionRuntime owner)
        { this.owner = owner; thread = Environment.CurrentManagedThreadId; }
        internal LegacyOwnedAdmissionOptions Configuration => owner.configured;
        internal bool IsCurrentPlain(LegacyItemUidRegistry registry) => Monitor.IsEntered(owner.gate) &&
            thread == Environment.CurrentManagedThreadId && !owner.closed && !owner.stopping && owner.frame is null &&
            !owner.relicFuseStartupComplete && ReferenceEquals(owner.activeRelicFuseStartup, this) &&
            ReferenceEquals(owner.registry, registry) && owner.runeDecomposeStartupComplete && owner.runeBagStartupComplete;
    }

    private bool TryCompleteRelicFuseNamespaceStartup(out string reason)
    {
        lock (gate)
        {
            reason = "relicFuseStartupOwnerUnavailable";
            if (closed || stopping || frame is not null || !runeDecomposeStartupComplete || !runeBagStartupComplete ||
                relicFuseStartupComplete || activeRelicFuseStartup is not null || !Ordinary.IsCurrent(configured.Roots) ||
                !Ordinary.IsReconciliationComplete || !Recovery.IsReadyForOrdinaryAdmission(configured.Roots)) return false;
            activeRelicFuseStartup = new(this);
            try
            {
                if (!registry.TryRetireStartupRelicFuseNamespaces(activeRelicFuseStartup, out reason))
                { Recovery.Block(reason); return false; }
                relicFuseStartupComplete = true; reason = string.Empty; return true;
            }
            finally { activeRelicFuseStartup = null; }
        }
    }
    internal sealed class RelicFuseUidAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime owner;
        private readonly WorldServerFrameHandler frame;
        private readonly LegacyRelicFuseOwnedEvidence evidence;
        private readonly LegacyItemUidReservationRequest request;
        private readonly int thread;
        internal RelicFuseUidAuthority(LegacyOwnedAdmissionRuntime owner, WorldServerFrameHandler frame,
            LegacyRelicFuseOwnedEvidence evidence, LegacyItemUidReservationRequest request)
        { this.owner = owner; this.frame = frame; this.evidence = evidence; this.request = request; thread = Environment.CurrentManagedThreadId; }
        internal bool TryPrepareNextUid(LegacyItemUidRegistry registry, LegacyItemUidReservationRequest actual,
            uint nextUid, out string reason)
        {
            reason = "relicFuseUidOwnerUnavailable";
            return IsCurrentPlain(registry, actual) && evidence.TryPrepareNextUid(nextUid, out reason);
        }
        internal Guid Domain => owner.configured.ExpectedDomain;
        internal LegacyOwnedAdmissionOptions Configuration => owner.configured;
        internal bool IsCurrentPlain(LegacyItemUidRegistry registry, LegacyItemUidReservationRequest actual) =>
            Monitor.IsEntered(owner.gate) && thread == Environment.CurrentManagedThreadId && !owner.closed && !owner.stopping &&
            owner.runeDecomposeStartupComplete && owner.runeBagStartupComplete && owner.relicFuseStartupComplete && ReferenceEquals(owner.frame, frame) &&
            ReferenceEquals(owner.registry, registry) && ReferenceEquals(owner.activeRelicFuseAllocation, evidence) &&
            ReferenceEquals(request, actual);
    }
    // The selected real Frame and shared registry are checked before any
    // entropy or range reservation. This routine creates no authority.
    internal Guid RequireRelicFuseCapacity(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!RelicFuseFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRelicFuseCapacity(configured);
            return configured.ExpectedDomain;
        }
    }

    internal void RequireRelicFuseObservedMaximum(WorldServerFrameHandler actualFrame,
        LegacyRelicFuseOwnedEvidence evidence, uint observed)
    {
        lock (gate)
        {
            if (!RelicFuseFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRelicFuseObservedMaximum(evidence, observed);
        }
    }
    internal LegacyItemUidReservationOutcome ReserveRelicFuseUid(WorldServerFrameHandler actualFrame,
        LegacyRelicFuseOwnedEvidence evidence, LegacyItemUidReservationRequest request,
        out LegacyItemUidRegistry.Reservation? reservation, out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        ArgumentNullException.ThrowIfNull(evidence); ArgumentNullException.ThrowIfNull(request);
        evidence.RequireReservationReady(this, actualFrame, request);
        lock (gate)
        {
            reservation = null; receipt = null; reason = "relicFuseUidOwnerUnavailable";
            if (activeRelicFuseAllocation is not null || request.Writer != LegacyItemUidRegistry.RelicFuseWriter ||
                request.Count != 1 || !Enum.IsDefined(RelicFuseNamespaceInterruption) ||
                !Enum.IsDefined(RelicFuseNamespaceRetirementInterruption) || !RelicFuseFrameIsCurrent(actualFrame, out reason)) return LegacyItemUidReservationOutcome.Rejected;
            registry.RequireRelicFuseCapacity(configured);
            activeRelicFuseAllocation = evidence;
            try
            {
                var authority = new RelicFuseUidAuthority(this, actualFrame, evidence, request);
                var outcome = registry.ReserveRelicFuseNamespace(authority, request, out reservation, out receipt, out reason);
                if (outcome == LegacyItemUidReservationOutcome.Reserved &&
                    RelicFuseNamespaceInterruption == LegacyRelicFuseNamespaceInterruption.AfterReservation)
                {
                    registry.BlockRelicFuseAllocation();
                    reason = LegacyItemUidRegistry.RecoveryRequired + ":relicFuseAfterNamespaceReservation";
                    Recovery.Block(reason); reservation = null; receipt = null;
                    return LegacyItemUidReservationOutcome.Rejected;
                }
                if (reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal)) Recovery.Block(reason);
                return outcome;
            }
            catch
            {
                registry.BlockRelicFuseAllocation();
                Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":relicFuseReservationOutcomeUnknown");
                throw;
            }
            finally { activeRelicFuseAllocation = null; }
        }
    }

    internal bool IsRelicFuseAllocationCurrent(WorldServerFrameHandler actualFrame,
        LegacyRelicFuseOwnedEvidence evidence, LegacyItemUidReservationRequest request,
        LegacyItemUidReservationReceipt expected)
    {
        if (!evidence.IsIssuedAllocationCurrent(this, actualFrame, request, expected)) return false;
        lock (gate)
        {
            if (!RelicFuseFrameIsCurrent(actualFrame, out _)) return false;
            try { registry.RequireRelicFuseAllocation(request, expected); return true; }
            catch (InvalidDataException error)
            { Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":relicFuseAllocationRead:" + error.GetType().Name); return false; }
        }
    }
    internal bool RetireRelicFuseUid(WorldServerFrameHandler actualFrame, LegacyPreparedSpellFileWrite writer,
        LegacyRelicFuseOwnedEvidence evidence, LegacyItemUidReservationReceipt receipt, out string reason)
    {
        lock (gate)
        {
            reason = "relicFuseRetirementRuntimeUnavailable";
            if (!RelicFuseFrameIsCurrent(actualFrame, out reason)) { Recovery.Block(reason); return false; }
            try
            {
                if (registry.TryRetireRelicFuseNamespace(writer, evidence, receipt, RelicFuseNamespaceRetirementInterruption, out reason)) return true;
                Recovery.Block(reason); return false;
            }
            catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
            {
                Recovery.Block("relicFuseRetirementUnproven:" + error.GetType().Name);
                throw;
            }
        }
    }
    internal void FenceRelicFuseUid(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!ReferenceEquals(frame, actualFrame)) throw new InvalidOperationException("relicFuseForeignFrame");
            registry.BlockRelicFuseAllocation();
            Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":relicFuseAllocationUnproven");
        }
    }

    private bool RelicFuseFrameIsCurrent(WorldServerFrameHandler actualFrame, out string reason)
    {
        reason = "relicFuseOwnedRuntimeUnavailable";
        return Monitor.IsEntered(gate) && !closed && !stopping && runeDecomposeStartupComplete && runeBagStartupComplete && relicFuseStartupComplete &&
            ReferenceEquals(frame, actualFrame) && actualFrame is not null && Ordinary.IsCurrent(configured.Roots) &&
            Ordinary.IsReconciliationComplete && Recovery.IsReadyForOrdinaryAdmission(configured.Roots) &&
            registry.TryValidateProducerBinding(configured.ExpectedDomain, configured.UsersRoot,
                configured.ItemsRoot, configured.MailboxRoot, out reason);
    }
}
