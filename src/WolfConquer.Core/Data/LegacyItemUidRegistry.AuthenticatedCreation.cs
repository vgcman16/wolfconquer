using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // Read/deny only. Seven absent business files do not erase a completed
    // account's durable creation entitlement history.
    internal bool TryRequireFreshAuthenticatedCreationAccount(LegacyOwnedAdmissionOptions configuration,
        uint account, out string reason)
    {
        lock (gate)
        {
            reason = "authenticatedCreationAccountInvalid";
            try
            {
                RequireRuneDecomposeStateCurrent();
                if (account is 0 or > int.MaxValue) return false;
                if (RequireCompletedOrdinaryHistory(configuration).ContainsAccount(account))
                { reason = "authenticatedCreationAccountAlreadyCompleted"; return false; }
                reason = string.Empty; return true;
            }
            catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
            {
                reason = RecoveryRequired + ":authenticatedCreationHistory:" + error.GetType().Name;
                blocked = reason; return false;
            }
        }
    }

    internal void RequireAuthenticatedCreationPreflight(LegacyOwnedAdmissionRuntime.CreationLease lease,
        LegacyItemUidReservationRequest request, byte[] payload)
    {
        lease.RequireCurrentBefore();
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            if (!ReferenceEquals(lease.Registry, this)) throw new InvalidDataException("Creation registry owner differs.");
            var configured = lease.Configuration;
            var history = RequireCompletedOrdinaryHistory(configured);
            var recipe = LegacyAuthenticatedOrdinaryCreationRecipe.Decode(payload, configured.Roots, configured.MailboxRoot);
            if (history.ContainsAccount(lease.AccountId))
                throw new InvalidDataException("Authenticated creation account already has completed ordinary history.");
            if (request != recipe.CreateRequest() || recipe.Account != lease.AccountId || recipe.Username != lease.Username ||
                recipe.TicketDigest != lease.TicketDigest || recipe.Projection.Name != lease.NameClaim.Name ||
                (state.Operations?.Length ?? 0) >= MaximumOperationReceipts ||
                (state.ProducerIntents?.Length ?? 0) >= MaximumProducerIntents ||
                payload.Length > MaximumTotalProducerPayloadBytes || state.Generation > ulong.MaxValue - 3 ||
                history.Count >= MaximumArchiveEntries || history.Bytes > MaximumTotalArchiveBytes - MaximumArchiveBytes ||
                (ulong)state.HighWater + (uint)request.Count > uint.MaxValue)
                throw new InvalidDataException("Authenticated creation lacks complete intent/allocation/retirement capacity or identity.");
        }
        lease.RequireCurrentBefore();
    }

    internal void RequireAuthenticatedCreationJournal(LegacyOwnedAdmissionRuntime.CreationLease lease,
        LegacyAuthenticatedOrdinaryCreationOperation operation)
    {
        lease.RequirePlainOwnerCurrent();
        lock (gate)
        {
            if (!ReferenceEquals(lease.Registry, this) || disposed || blocked is not null ||
                processLease.SafeFileHandle.IsClosed || processLease.SafeFileHandle.IsInvalid ||
                !processLease.CanRead || !processLease.CanWrite)
                throw new InvalidDataException("Authenticated creation lost its actual exclusive registry owner.");
            _ = processLease.Length;
            EnsureOrdinaryPath(paths.Lease); RequireCreationUidTransitionClear(paths);
            if (!File.Exists(paths.Lease) || !TryValidateDirectory(paths, out _) || !SameState(ReadState(paths.State), state))
                throw new InvalidDataException("Authenticated creation registry state changed.");
            RequireAuthenticatedCreationIntent(paths, state, lease.Configuration,
                ReadArchiveCatalog(paths, state, null, allowNext: false), operation);
        }
        lease.RequirePlainOwnerCurrent();
    }

    internal void BlockAuthenticatedCreation()
    { lock (gate) blocked = RecoveryRequired + ":authenticatedCreationIncomplete"; }

    private static void RequireCreationUidTransitionClear(RegistryPaths paths)
    {
        EnsureOrdinaryPath(paths.Pending); EnsureOrdinaryPath(paths.ArchivePending);
        if (File.Exists(paths.Pending) || Directory.Exists(paths.Pending) ||
            File.Exists(paths.ArchivePending) || Directory.Exists(paths.ArchivePending))
            throw new InvalidDataException("Authenticated creation cannot combine an uncertain UID/archive transition.");
    }

    private static void RequireAuthenticatedCreationIntent(RegistryPaths paths, RegistryState current,
        LegacyOwnedAdmissionOptions configuration, ArchiveCatalog archives,
        LegacyAuthenticatedOrdinaryCreationOperation operation)
    {
        var recipe = LegacyAuthenticatedOrdinaryCreationRecipe.Decode(operation.Payload, configuration.Roots,
            configuration.MailboxRoot);
        var request = recipe.CreateRequest();
        var active = current.ProducerIntents ?? [];
        var row = active.SingleOrDefault(value => value.OperationId == request.OperationId.ToString("N"));
        var allocation = current.Operations?.SingleOrDefault(value => value.OperationId == request.OperationId.ToString("N"));
        if (active.Length != 1 || row is null || allocation is null || allocation.NamespaceReservation is not null ||
            row.Writer != request.Writer || row.AccountId != request.AccountId || row.CharacterUid != request.CharacterUid ||
            row.PayloadSha256 != request.PayloadSha256 || row.Count != request.Count ||
            row.PreparedGeneration != operation.PreparedGeneration ||
            !Convert.FromBase64String(row.PayloadBase64).AsSpan().SequenceEqual(operation.Payload))
            throw new InvalidDataException("Creation WAL does not own the exact sole active ordinary intent.");
        var domain = Guid.ParseExact(current.Domain, "N");
        var receipt = new LegacyItemUidReservationReceipt(domain, Guid.ParseExact(allocation.OperationId, "N"),
            allocation.Writer, allocation.AccountId, allocation.CharacterUid, allocation.PayloadSha256,
            allocation.Generation, allocation.First, allocation.Count);
        if (receipt != operation.Allocation)
            throw new InvalidDataException("Creation WAL does not own its exact registered allocation.");
        _ = LegacyOrdinaryProducerRecipe.Read(new(domain, request, row.PreparedGeneration,
            operation.Payload, receipt), domain, configuration.Roots, configuration.MailboxRoot);
        // Remove only this exact, independently bound active transaction from
        // the debt-classification view; actual state/ranges/history stay intact.
        var historical = current with { ProducerIntents = [],
            Operations = current.Operations!.Where(value => value != allocation).ToArray() };
        _ = RequireCompletedOrdinaryHistory(paths, historical, configuration, archives);
    }
}
