using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyRuneBagNamespaceInterruption { None, AfterReservation }

public sealed partial class LegacyOwnedAdmissionRuntime
{
    private RuneBagNamespaceAuthority? activeRuneBagNamespace;
    private RuneBagNamespaceStartupAuthority? activeRuneBagStartup;
    private bool runeBagStartupComplete;
    internal LegacyRuneBagNamespaceInterruption RuneBagNamespaceInterruption { get; set; }
    internal LegacyItemUidInterruption RuneBagNamespaceRetirementInterruption { get; set; }

    internal sealed class RuneBagNamespaceAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime runtime;
        private readonly WorldServerFrameHandler actualFrame;
        private readonly LegacyItemUidReservationRequest request;
        private readonly int thread;
        private RuneBagNamespaceAuthority(LegacyOwnedAdmissionRuntime runtime, WorldServerFrameHandler actualFrame,
            LegacyItemUidReservationRequest request)
        { this.runtime = runtime; this.actualFrame = actualFrame; this.request = request; thread = Environment.CurrentManagedThreadId; }
        internal static RuneBagNamespaceAuthority Create(LegacyOwnedAdmissionRuntime runtime,
            WorldServerFrameHandler actualFrame, LegacyItemUidReservationRequest request)
        {
            if (!Monitor.IsEntered(runtime.gate) || runtime.closed || runtime.stopping ||
                !ReferenceEquals(runtime.frame, actualFrame) || runtime.activeRuneBagNamespace is not null ||
                !runtime.runeBagStartupComplete)
                throw new InvalidOperationException("Rune bag namespace requires the actual Runtime gate.");
            return new(runtime, actualFrame, request);
        }
        internal Guid Domain => runtime.configured.ExpectedDomain;
        internal LegacyOwnedAdmissionOptions Configuration => runtime.configured;
        internal bool IsCurrentPlain(LegacyItemUidRegistry registry, LegacyItemUidReservationRequest candidate) =>
            Monitor.IsEntered(runtime.gate) && thread == Environment.CurrentManagedThreadId &&
            ReferenceEquals(runtime.registry, registry) && ReferenceEquals(runtime.frame, actualFrame) &&
            ReferenceEquals(runtime.activeRuneBagNamespace, this) && ReferenceEquals(request, candidate) &&
            !runtime.closed && !runtime.stopping && runtime.runeBagStartupComplete;
    }

    internal sealed class RuneBagNamespaceStartupAuthority
    {
        private readonly LegacyOwnedAdmissionRuntime runtime;
        private readonly int thread;
        private RuneBagNamespaceStartupAuthority(LegacyOwnedAdmissionRuntime runtime)
        { this.runtime = runtime; thread = Environment.CurrentManagedThreadId; }
        internal static RuneBagNamespaceStartupAuthority Create(LegacyOwnedAdmissionRuntime runtime)
        {
            if (!Monitor.IsEntered(runtime.gate) || runtime.closed || runtime.stopping || runtime.frame is not null ||
                !runtime.runeDecomposeStartupComplete || runtime.runeBagStartupComplete || runtime.activeRuneBagStartup is not null)
                throw new InvalidOperationException("Rune bag startup requires the fresh actual Runtime gate.");
            return new(runtime);
        }
        internal LegacyOwnedAdmissionOptions Configuration => runtime.configured;
        internal bool IsCurrentPlain(LegacyItemUidRegistry registry) =>
            Monitor.IsEntered(runtime.gate) && thread == Environment.CurrentManagedThreadId &&
            ReferenceEquals(runtime.registry, registry) && ReferenceEquals(runtime.activeRuneBagStartup, this) &&
            runtime.frame is null && !runtime.closed && !runtime.stopping &&
            runtime.runeDecomposeStartupComplete && !runtime.runeBagStartupComplete;
    }

    private bool TryCompleteRuneBagNamespaceStartup(out string reason)
    {
        lock (gate)
        {
            reason = "runeBagNamespaceStartupUnavailable";
            if (closed || stopping || frame is not null || !runeDecomposeStartupComplete || runeBagStartupComplete ||
                activeRuneBagStartup is not null || !Ordinary.IsCurrent(configured.Roots) ||
                !Ordinary.IsReconciliationComplete || !Recovery.IsReadyForOrdinaryAdmission(configured.Roots)) return false;
            activeRuneBagStartup = RuneBagNamespaceStartupAuthority.Create(this);
            try
            {
                if (!registry.TryRetireStartupRuneBagNamespaces(activeRuneBagStartup, out reason))
                { Recovery.Block(reason); return false; }
                runeBagStartupComplete = true; reason = string.Empty; return true;
            }
            finally { activeRuneBagStartup = null; }
        }
    }

    internal LegacyItemUidReservationOutcome ReserveRuneBagNamespace(WorldServerFrameHandler actualFrame,
        LegacyRuneBagEvidence evidence, LegacyItemUidReservationRequest request,
        out LegacyItemUidRegistry.Reservation? reservation, out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        ArgumentNullException.ThrowIfNull(evidence); ArgumentNullException.ThrowIfNull(request);
        // Real Store/binding/dependency proof precedes Runtime/Registry gates.
        evidence.RequireReservationReady(this, actualFrame, request);
        lock (gate)
        {
            reservation = null; receipt = null; reason = "runeBagNamespaceRuntimeUnavailable";
            if (request.Writer != LegacyItemUidRegistry.RuneBagWriter || request.Count != 1 ||
                !Enum.IsDefined(RuneBagNamespaceInterruption) || !Enum.IsDefined(RuneBagNamespaceRetirementInterruption) ||
                activeRuneBagNamespace is not null || !RuneBagFrameIsCurrent(actualFrame, out reason))
                return LegacyItemUidReservationOutcome.Rejected;
            activeRuneBagNamespace = RuneBagNamespaceAuthority.Create(this, actualFrame, request);
            try
            {
                var result = registry.ReserveRuneBagNamespace(activeRuneBagNamespace, request,
                    out reservation, out receipt, out reason);
                if (result == LegacyItemUidReservationOutcome.Reserved &&
                    RuneBagNamespaceInterruption == LegacyRuneBagNamespaceInterruption.AfterReservation)
                {
                    registry.BlockRuneBagNamespace();
                    reason = LegacyItemUidRegistry.RecoveryRequired + ":runeBagAfterNamespaceReservation";
                    Recovery.Block(reason); reservation = null; receipt = null;
                    return LegacyItemUidReservationOutcome.Rejected;
                }
                if (reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal)) Recovery.Block(reason);
                return result;
            }
            catch (Exception)
            {
                // No returned receipt is not proof that issuance never became
                // durable. Deny retry and preserve the original exception.
                registry.BlockRuneBagNamespace();
                Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":runeBagReservationOutcomeUnproven");
                throw;
            }
            finally { activeRuneBagNamespace = null; }
        }
    }

    internal void RequireRuneBagAllocation(WorldServerFrameHandler actualFrame,
        LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        lock (gate)
        {
            if (!RuneBagFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRuneBagAllocation(request, receipt);
        }
    }

    internal Guid RequireRuneBagDomain(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!RuneBagFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRuneBagReservationCapacity(configured, 0);
            return configured.ExpectedDomain;
        }
    }

    internal LegacyOwnedAdmissionOptions RequireRuneBagConfiguration(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!RuneBagFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            return configured;
        }
    }

    internal void RequireRuneBagObservedMaximum(WorldServerFrameHandler actualFrame,
        LegacyRuneBagEvidence evidence, uint observed)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        // Actual Store/native/saved-before proof must precede Runtime/Registry
        // gates. It must not call reservation/current-operation checks here.
        evidence.RequireNamespaceObservationReady(this, actualFrame);
        lock (gate)
        {
            if (!RuneBagFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRuneBagObservedMaximum(evidence, observed);
        }
    }

    internal void RequireRuneBagPreflight(WorldServerFrameHandler actualFrame, int count)
    {
        lock (gate)
        {
            if (!RuneBagFrameIsCurrent(actualFrame, out var reason)) throw new InvalidDataException(reason);
            registry.RequireRuneBagReservationCapacity(configured, count);
        }
    }

    // Denial only. The actual issued range stays burned; fresh configured
    // startup alone may reconcile its bookkeeping after full clearance.
    internal void FenceRuneBagNamespace(WorldServerFrameHandler actualFrame)
    {
        lock (gate)
        {
            if (!ReferenceEquals(frame, actualFrame)) throw new InvalidOperationException("Rune bag abort belongs to another Frame.");
            registry.BlockRuneBagNamespace();
            Recovery.Block(LegacyItemUidRegistry.RecoveryRequired + ":runeBagReservationAborted");
        }
    }

    internal bool RetireRuneBagNamespace(WorldServerFrameHandler actualFrame,
        LegacyPreparedSpellFileWrite.RuneCharacterPublication proof, LegacyItemUidReservationReceipt receipt, out string reason)
    {
        lock (gate)
        {
            reason = "runeBagNamespaceRuntimeUnavailable";
            if (!RuneBagFrameIsCurrent(actualFrame, out reason))
            { registry.BlockRuneBagNamespace(); Recovery.Block(reason); return false; }
            try
            {
                if (registry.TryRetireRuneBagNamespace(proof, receipt, RuneBagNamespaceRetirementInterruption, out reason)) return true;
                Recovery.Block(reason); return false;
            }
            catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
            {
                registry.BlockRuneBagNamespace();
                Recovery.Block("runeBagNamespaceRetirementUnproven:" + error.GetType().Name);
                throw;
            }
        }
    }

    private bool RuneBagFrameIsCurrent(WorldServerFrameHandler actualFrame, out string reason)
    {
        reason = "runeBagNamespaceRuntimeUnavailable";
        return Monitor.IsEntered(gate) && !closed && !stopping && runeDecomposeStartupComplete && runeBagStartupComplete &&
            ReferenceEquals(frame, actualFrame) && actualFrame is not null && Ordinary.IsCurrent(configured.Roots) &&
            Ordinary.IsReconciliationComplete && Recovery.IsReadyForOrdinaryAdmission(configured.Roots) &&
            registry.TryValidateProducerBinding(configured.ExpectedDomain, configured.UsersRoot,
                configured.ItemsRoot, configured.MailboxRoot, out reason);
    }
}
