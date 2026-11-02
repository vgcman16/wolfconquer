using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyRelicDecomposeNamespaceInterruption { None, AfterReservation }

public sealed partial class LegacyOwnedAdmissionRuntime
{
    private LegacyRelicDecomposeOwnedEvidence? activeRelicDecomposeAllocation;
    private RelicDecomposeStartupAuthority? activeRelicDecomposeStartup;
    private bool relicDecomposeStartupComplete;
    internal LegacyRelicDecomposeNamespaceInterruption RelicDecomposeNamespaceInterruption { get; set; }
    internal LegacyItemUidInterruption RelicDecomposeNamespaceRetirementInterruption { get; set; }

    internal sealed class RelicDecomposeStartupAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime owner;
        private readonly int thread;
        internal RelicDecomposeStartupAuthority(LegacyOwnedAdmissionRuntime owner)
        { this.owner = owner; thread = Environment.CurrentManagedThreadId; }
        internal LegacyOwnedAdmissionOptions Configuration => owner.configured;
        internal bool IsCurrentPlain(LegacyItemUidRegistry registry) => Monitor.IsEntered(owner.gate) &&
            thread == Environment.CurrentManagedThreadId && !owner.closed && !owner.stopping && owner.frame is null &&
            !owner.relicDecomposeStartupComplete && ReferenceEquals(owner.activeRelicDecomposeStartup, this) &&
            ReferenceEquals(owner.registry, registry) && owner.runeDecomposeStartupComplete && owner.runeBagStartupComplete && owner.relicFuseStartupComplete;
    }

    private bool TryCompleteRelicDecomposeNamespaceStartup(out string reason)
    {
        lock (gate)
        {
            reason = "relicDecomposeStartupOwnerUnavailable";
            if (closed || stopping || frame is not null || !runeDecomposeStartupComplete || !runeBagStartupComplete || !relicFuseStartupComplete ||
                relicDecomposeStartupComplete || activeRelicDecomposeStartup is not null || !Ordinary.IsCurrent(configured.Roots) ||
                !Ordinary.IsReconciliationComplete || !Recovery.IsReadyForOrdinaryAdmission(configured.Roots)) return false;
            activeRelicDecomposeStartup = new(this);
            try
            {
                if (!registry.TryRetireStartupRelicDecomposeNamespaces(activeRelicDecomposeStartup, out reason))
                { Recovery.Block(reason); return false; }
                relicDecomposeStartupComplete = true; reason = string.Empty; return true;
            }
            finally { activeRelicDecomposeStartup = null; }
        }
    }
    internal sealed class RelicDecomposeUidAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime owner;
        private readonly WorldServerFrameHandler frame;
        private readonly LegacyRelicDecomposeOwnedEvidence evidence;
        private readonly LegacyItemUidReservationRequest request;
        private readonly int thread;
        internal RelicDecomposeUidAuthority(LegacyOwnedAdmissionRuntime owner, WorldServerFrameHandler frame,
            LegacyRelicDecomposeOwnedEvidence evidence, LegacyItemUidReservationRequest request)
        { this.owner = owner; this.frame = frame; this.evidence = evidence; this.request = request; thread = Environment.CurrentManagedThreadId; }
        internal bool TryPrepareNextUids(LegacyItemUidRegistry registry, LegacyItemUidReservationRequest actual,
            uint nextUid, int count, out string reason)
        {
            reason = "relicDecomposeUidOwnerUnavailable";
            return IsCurrentPlain(registry, actual) && evidence.TryPrepareNextUids(nextUid, count, out reason);
        }
        internal Guid Domain => owner.configured.ExpectedDomain;
        internal LegacyOwnedAdmissionOptions Configuration => owner.configured;
        internal bool IsCurrentPlain(LegacyItemUidRegistry registry, LegacyItemUidReservationRequest actual) =>
            Monitor.IsEntered(owner.gate) && thread == Environment.CurrentManagedThreadId && !owner.closed && !owner.stopping &&
            owner.runeDecomposeStartupComplete && owner.runeBagStartupComplete && owner.relicFuseStartupComplete && owner.relicDecomposeStartupComplete && ReferenceEquals(owner.frame, frame) &&
            ReferenceEquals(owner.registry, registry) && ReferenceEquals(owner.activeRelicDecomposeAllocation, evidence) &&
            ReferenceEquals(request, actual);
    }
    // The selected real Frame and shared registry are checked before any
    // entropy or range reservation. This routine creates no authority.
    internal Guid RequireRelicDecomposeCapacity(WorldServerFrameHandler actualFrame, int count)
    {
        lock (gate)
        {
            if (!RelicDecomposeFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRelicDecomposeCapacity(configured, count);
            return configured.ExpectedDomain;
        }
    }

    internal void RequireRelicDecomposeObservedMaximum(WorldServerFrameHandler actualFrame,
        LegacyRelicDecomposeOwnedEvidence evidence, uint observed)
    {
        lock (gate)
        {
            if (!RelicDecomposeFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRelicDecomposeObservedMaximum(evidence, observed);
        }
    }
    internal LegacyItemUidReservationOutcome ReserveRelicDecomposeUids(WorldServerFrameHandler actualFrame,
        LegacyRelicDecomposeOwnedEvidence evidence, LegacyItemUidReservationRequest request,
        out LegacyItemUidRegistry.Reservation? reservation, out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        ArgumentNullException.ThrowIfNull(evidence); ArgumentNullException.ThrowIfNull(request);
        evidence.RequireReservationReady(this, actualFrame, request);
        lock (gate)
        {
            reservation = null; receipt = null; reason = "relicDecomposeUidOwnerUnavailable";
            if (activeRelicDecomposeAllocation is not null || request.Writer != LegacyItemUidRegistry.RelicDecomposeWriter ||
                !LegacyItemUidRegistry.ValidRelicDecomposeCount(request.Count) || !Enum.IsDefined(RelicDecomposeNamespaceInterruption) ||
                !Enum.IsDefined(RelicDecomposeNamespaceRetirementInterruption) || !RelicDecomposeFrameIsCurrent(actualFrame, out reason)) return LegacyItemUidReservationOutcome.Rejected;
            registry.RequireRelicDecomposeCapacity(configured, request.Count);
            activeRelicDecomposeAllocation = evidence;
            try
            {
                var authority = new RelicDecomposeUidAuthority(this, actualFrame, evidence, request);
                var outcome = registry.ReserveRelicDecomposeNamespace(authority, request, out reservation, out receipt, out reason);
                if (outcome == LegacyItemUidReservationOutcome.Reserved &&
                    RelicDecomposeNamespaceInterruption == LegacyRelicDecomposeNamespaceInterruption.AfterReservation)
                {
                    registry.BlockRelicDecomposeAllocation();
                    reason = LegacyItemUidRegistry.RecoveryRequired + ":relicDecomposeAfterNamespaceReservation";
                    Recovery.Block(reason); reservation = null; receipt = null;
                    return LegacyItemUidReservationOutcome.Rejected;
                }
                if (reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal)) Recovery.Block(reason);
                return outcome;
            }
            catch
            {
                registry.BlockRelicDecomposeAllocation();
                Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":relicDecomposeReservationOutcomeUnknown");
                throw;
            }
            finally { activeRelicDecomposeAllocation = null; }
        }
    }

    internal bool IsRelicDecomposeAllocationCurrent(WorldServerFrameHandler actualFrame,
        LegacyRelicDecomposeOwnedEvidence evidence, LegacyItemUidReservationRequest request,
        LegacyItemUidReservationReceipt expected)
    {
        if (!evidence.IsIssuedAllocationCurrent(this, actualFrame, request, expected)) return false;
        lock (gate)
        {
            if (!RelicDecomposeFrameIsCurrent(actualFrame, out _)) return false;
            try { registry.RequireRelicDecomposeAllocation(request, expected); return true; }
            catch (InvalidDataException error)
            { Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":relicDecomposeAllocationRead:" + error.GetType().Name); return false; }
        }
    }
    internal bool RetireRelicDecomposeUids(WorldServerFrameHandler actualFrame, LegacyPreparedSpellFileWrite writer,
        LegacyRelicDecomposeOwnedEvidence evidence, LegacyItemUidReservationReceipt receipt, out string reason)
    {
        lock (gate)
        {
            reason = "relicDecomposeRetirementRuntimeUnavailable";
            if (!RelicDecomposeFrameIsCurrent(actualFrame, out reason)) { Recovery.Block(reason); return false; }
            try
            {
                if (registry.TryRetireRelicDecomposeNamespace(writer, evidence, receipt, RelicDecomposeNamespaceRetirementInterruption, out reason)) return true;
                Recovery.Block(reason); return false;
            }
            catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
            {
                Recovery.Block("relicDecomposeRetirementUnproven:" + error.GetType().Name);
                throw;
            }
        }
    }
    internal void FenceRelicDecomposeUid(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!ReferenceEquals(frame, actualFrame)) throw new InvalidOperationException("relicDecomposeForeignFrame");
            registry.BlockRelicDecomposeAllocation();
            Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":relicDecomposeAllocationUnproven");
        }
    }

    private bool RelicDecomposeFrameIsCurrent(WorldServerFrameHandler actualFrame, out string reason)
    {
        reason = "relicDecomposeOwnedRuntimeUnavailable";
        return Monitor.IsEntered(gate) && !closed && !stopping && runeDecomposeStartupComplete && runeBagStartupComplete && relicFuseStartupComplete && relicDecomposeStartupComplete &&
            ReferenceEquals(frame, actualFrame) && actualFrame is not null && Ordinary.IsCurrent(configured.Roots) &&
            Ordinary.IsReconciliationComplete && Recovery.IsReadyForOrdinaryAdmission(configured.Roots) &&
            registry.TryValidateProducerBinding(configured.ExpectedDomain, configured.UsersRoot,
                configured.ItemsRoot, configured.MailboxRoot, out reason);
    }
}
