using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyRuneDecomposeNamespaceInterruption { None, AfterReservation }

public sealed partial class LegacyOwnedAdmissionRuntime
{
    private RuneDecomposeNamespaceAuthority? activeRuneDecomposeNamespace;
    private RuneDecomposeNamespaceStartupAuthority? activeRuneDecomposeStartup;
    private bool runeDecomposeStartupComplete;
    // Closed failure-only producer boundaries. Neither can replace an issuer,
    // choose a UID/image, supply a success result or resume interrupted work.
    internal LegacyRuneDecomposeNamespaceInterruption RuneDecomposeNamespaceInterruption { get; set; }
    internal LegacyItemUidInterruption RuneDecomposeNamespaceRetirementInterruption { get; set; }

    internal sealed class RuneDecomposeNamespaceAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime runtime;
        private readonly WorldServerFrameHandler actualFrame;
        private readonly LegacyItemUidReservationRequest request;
        private readonly int thread;
        private RuneDecomposeNamespaceAuthority(LegacyOwnedAdmissionRuntime runtime,
            WorldServerFrameHandler actualFrame, LegacyItemUidReservationRequest request)
        { this.runtime = runtime; this.actualFrame = actualFrame; this.request = request; thread = Environment.CurrentManagedThreadId; }
        // Constructing an inert object cannot issue it: only the enclosing
        // Runtime can install the private active reference after preflight.
        internal static RuneDecomposeNamespaceAuthority Create(LegacyOwnedAdmissionRuntime runtime,
            WorldServerFrameHandler actualFrame, LegacyItemUidReservationRequest request)
        {
            if (!Monitor.IsEntered(runtime.gate) || runtime.closed || runtime.stopping ||
                !ReferenceEquals(runtime.frame, actualFrame) || runtime.activeRuneDecomposeNamespace is not null ||
                !runtime.runeDecomposeStartupComplete)
                throw new InvalidOperationException("Decompose namespace authority requires the actual Runtime gate.");
            return new(runtime, actualFrame, request);
        }
        internal Guid Domain => runtime.configured.ExpectedDomain;
        internal LegacyOwnedAdmissionOptions Configuration => runtime.configured;
        internal bool IsCurrentPlain(LegacyItemUidRegistry registry, LegacyItemUidReservationRequest candidate) =>
            Monitor.IsEntered(runtime.gate) && thread == Environment.CurrentManagedThreadId &&
            ReferenceEquals(runtime.registry, registry) && ReferenceEquals(runtime.frame, actualFrame) &&
            ReferenceEquals(runtime.activeRuneDecomposeNamespace, this) && ReferenceEquals(request, candidate) &&
            !runtime.closed && !runtime.stopping && runtime.runeDecomposeStartupComplete;
    }

    internal sealed class RuneDecomposeNamespaceStartupAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime runtime;
        private readonly int thread;
        private RuneDecomposeNamespaceStartupAuthority(LegacyOwnedAdmissionRuntime runtime)
        { this.runtime = runtime; thread = Environment.CurrentManagedThreadId; }
        internal static RuneDecomposeNamespaceStartupAuthority Create(LegacyOwnedAdmissionRuntime runtime)
        {
            if (!Monitor.IsEntered(runtime.gate) || runtime.closed || runtime.stopping || runtime.frame is not null ||
                runtime.runeDecomposeStartupComplete || runtime.activeRuneDecomposeStartup is not null)
                throw new InvalidOperationException("Decompose startup authority requires the fresh actual Runtime gate.");
            return new(runtime);
        }
        internal LegacyOwnedAdmissionOptions Configuration => runtime.configured;
        internal bool IsCurrentPlain(LegacyItemUidRegistry registry) =>
            Monitor.IsEntered(runtime.gate) && thread == Environment.CurrentManagedThreadId &&
            ReferenceEquals(runtime.registry, registry) && ReferenceEquals(runtime.activeRuneDecomposeStartup, this) &&
            runtime.frame is null && !runtime.closed && !runtime.stopping && !runtime.runeDecomposeStartupComplete;
    }

    private bool TryCompleteRuneDecomposeNamespaceStartup(out string reason)
    {
        lock (gate)
        {
            reason = "runeDecomposeNamespaceStartupUnavailable";
            if (closed || stopping || frame is not null || runeDecomposeStartupComplete ||
                activeRuneDecomposeStartup is not null || !Ordinary.IsCurrent(configured.Roots) ||
                !Ordinary.IsReconciliationComplete || !Recovery.IsReadyForOrdinaryAdmission(configured.Roots)) return false;
            activeRuneDecomposeStartup = RuneDecomposeNamespaceStartupAuthority.Create(this);
            try
            {
                if (!registry.TryRetireStartupRuneDecomposeNamespaces(activeRuneDecomposeStartup, out reason))
                { Recovery.Block(reason); return false; }
                runeDecomposeStartupComplete = true; reason = string.Empty; return true;
            }
            finally { activeRuneDecomposeStartup = null; }
        }
    }

    internal LegacyItemUidReservationOutcome ReserveRuneDecomposeNamespace(WorldServerFrameHandler actualFrame,
        LegacyRuneDecomposeEvidence evidence, LegacyItemUidReservationRequest request,
        out LegacyItemUidRegistry.Reservation? reservation, out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(request);
        // This actual Store/dependency preflight must precede Runtime/Registry
        // gates. The Registry consumes only the closed plain authority below.
        evidence.RequireReservationReady(this, actualFrame, request);
        lock (gate)
        {
            reservation = null; receipt = null; reason = "runeDecomposeNamespaceRuntimeUnavailable";
            if (request.Writer != LegacyItemUidRegistry.RuneDecomposeWriter ||
                !Enum.IsDefined(RuneDecomposeNamespaceInterruption) || !Enum.IsDefined(RuneDecomposeNamespaceRetirementInterruption) ||
                activeRuneDecomposeNamespace is not null || !RuneDecomposeFrameIsCurrent(actualFrame, out reason))
                return LegacyItemUidReservationOutcome.Rejected;
            activeRuneDecomposeNamespace = RuneDecomposeNamespaceAuthority.Create(this, actualFrame, request);
            try
            {
                var result = registry.ReserveRuneDecomposeNamespace(activeRuneDecomposeNamespace, request,
                    out reservation, out receipt, out reason);
                if (result == LegacyItemUidReservationOutcome.Reserved &&
                    RuneDecomposeNamespaceInterruption == LegacyRuneDecomposeNamespaceInterruption.AfterReservation)
                {
                    registry.BlockRuneDecomposeNamespace();
                    reason = LegacyItemUidRegistry.RecoveryRequired + ":decomposeAfterNamespaceReservation";
                    Recovery.Block(reason); reservation = null; receipt = null;
                    return LegacyItemUidReservationOutcome.Rejected;
                }
                if (reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal)) Recovery.Block(reason);
                return result;
            }
            finally { activeRuneDecomposeNamespace = null; }
        }
    }

    internal void RequireRuneDecomposeAllocation(WorldServerFrameHandler actualFrame,
        LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        lock (gate)
        {
            if (!RuneDecomposeFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRuneDecomposeAllocation(request, receipt);
        }
    }

    internal Guid RequireRuneDecomposeDomain(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!RuneDecomposeFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            return configured.ExpectedDomain;
        }
    }

    internal void RequireRuneDecomposePreflight(WorldServerFrameHandler actualFrame, int count)
    {
        lock (gate)
        {
            if (!RuneDecomposeFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRuneDecomposeReservationCapacity(configured, count);
        }
    }

    // Denial only: an abandoned issued range stays burned and cannot be retried
    // by this live owner. Fresh configured startup owns bookkeeping cleanup.
    internal void FenceRuneDecomposeNamespace(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!ReferenceEquals(frame, actualFrame))
                throw new InvalidOperationException("Decompose abort belongs to a different actual Frame.");
            registry.BlockRuneDecomposeNamespace();
            Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":decomposeReservationAborted");
        }
    }

    internal bool RetireRuneDecomposeNamespace(WorldServerFrameHandler actualFrame,
        LegacyPreparedSpellFileWrite.RuneCharacterPublication proof, LegacyItemUidReservationReceipt receipt, out string reason)
    {
        lock (gate)
        {
            reason = "runeDecomposeNamespaceRuntimeUnavailable";
            if (!RuneDecomposeFrameIsCurrent(actualFrame, out reason)) { Recovery.Block(reason); return false; }
            try
            {
                if (registry.TryRetireRuneDecomposeNamespace(proof, receipt,
                    RuneDecomposeNamespaceRetirementInterruption, out reason)) return true;
                Recovery.Block(reason); return false;
            }
            catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
            {
                Recovery.Block("runeDecomposeNamespaceRetirementUnproven:" + error.GetType().Name);
                throw;
            }
        }
    }

    private bool RuneDecomposeFrameIsCurrent(WorldServerFrameHandler actualFrame, out string reason)
    {
        reason = "runeDecomposeNamespaceRuntimeUnavailable";
        return Monitor.IsEntered(gate) && !closed && !stopping && runeDecomposeStartupComplete &&
            ReferenceEquals(frame, actualFrame) && actualFrame is not null && Ordinary.IsCurrent(configured.Roots) &&
            Ordinary.IsReconciliationComplete && Recovery.IsReadyForOrdinaryAdmission(configured.Roots) &&
            registry.TryValidateProducerBinding(configured.ExpectedDomain, configured.UsersRoot,
                configured.ItemsRoot, configured.MailboxRoot, out reason);
    }
}
