using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal void RequireAuthenticatedCreationStartupNoDebt(
        LegacyOwnedAdmissionRuntime.AuthenticatedCreationStartupAuthority authority)
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            if (!authority.IsCurrentPlain(this) || !IsOrdinaryAdmissionOwner(authority.Ordinary))
                throw new InvalidDataException("Creation startup clearance has no actual current owner.");
            _ = RequireCompletedOrdinaryHistory(authority.Configuration);
        }
    }

    internal void RequireAuthenticatedCreationStartupPreflight(
        LegacyOwnedAdmissionRuntime.AuthenticatedCreationStartupAuthority authority,
        LegacyItemUidProducerIntentSnapshot intent)
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            if (!authority.IsCurrentPlain(this) || !ReferenceEquals(authority.Ordinary.Registry, this) ||
                !IsOrdinaryAdmissionOwner(authority.Ordinary))
                throw new InvalidDataException("Creation startup lacks the actual current registry/ordinary owner.");
            var configured = authority.Configuration;
            var active = state.ProducerIntents ?? [];
            var request = intent.Request;
            var row = active.SingleOrDefault(value => value.OperationId == request.OperationId.ToString("N"));
            if (active.Length != 1 || row is null || row.PreparedGeneration != intent.PreparedGeneration ||
                !Convert.FromBase64String(row.PayloadBase64).AsSpan().SequenceEqual(intent.CopyPayload()))
                throw new InvalidDataException("Creation startup requires its exact sole issued ordinary intent.");
            var bound = LegacyOrdinaryProducerRecipe.Read(intent, configured.ExpectedDomain, configured.Roots, configured.MailboxRoot);
            if (bound.Creation is null) throw new InvalidDataException("Old hash-only work cannot authorize new starter reconstruction.");
            var allocation = state.Operations?.SingleOrDefault(value => value.OperationId == row.OperationId);
            if ((allocation is null) != (intent.Allocation is null) ||
                allocation is not null && CreateReceipt(allocation) != intent.Allocation)
                throw new InvalidDataException("Creation startup allocation identity changed.");
            var historical = state with { ProducerIntents = [], Operations = (state.Operations ?? [])
                .Where(value => value.OperationId != row.OperationId).ToArray() };
            var history = RequireCompletedOrdinaryHistory(paths, historical, configured,
                ReadArchiveCatalog(paths, state, null, allowNext: false));
            if (state.Generation > ulong.MaxValue - (allocation is null ? 2UL : 1UL) ||
                allocation is null && ((state.Operations?.Length ?? 0) >= MaximumOperationReceipts ||
                    (ulong)state.HighWater + (uint)request.Count > uint.MaxValue) ||
                history.Count >= MaximumArchiveEntries || history.Bytes > MaximumTotalArchiveBytes - MaximumArchiveBytes)
                throw new InvalidDataException("Creation startup lacks complete allocation/archive capacity.");
        }
    }

    internal sealed partial class OwnedStartupCustody
    {
        internal static OwnedStartupCustody BorrowAuthenticatedCreation(
            LegacyOwnedAdmissionRuntime.AuthenticatedCreationStartupAuthority authority,
            LegacyAuthenticatedOrdinaryCreationOperation operation)
        {
            var registry = authority.Registry;
            lock (registry.gate)
            {
                if (!authority.IsCurrentPlain(registry) || !registry.IsOrdinaryAdmissionOwner(authority.Ordinary))
                    throw new InvalidDataException("Creation startup cannot borrow a foreign registry lease.");
                registry.RequireRuneDecomposeStateCurrent();
                var bytes = ReadBounded(registry.paths.State);
                if (!SameState(ParseState(bytes), registry.state))
                    throw new InvalidDataException("Creation startup state changed while its actual lease was borrowed.");
                var archives = ReadArchiveCatalog(registry.paths, registry.state, null, allowNext: false);
                RequireAuthenticatedCreationIntent(registry.paths, registry.state, authority.Configuration, archives, operation);
                var custody = new OwnedStartupCustody(authority.Configuration, registry.paths,
                    registry.processLease, bytes, registry.state, archives);
                custody.RequireAuthenticatedCreationAllocation(operation);
                return custody; // Borrow only: the actual Registry still owns this exact OS handle.
            }
        }
    }
}
