using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal enum LegacyItemUidArchiveInterruption
{ None, AfterArchivePendingFlush, AfterArchivePublish, AfterPendingFlush, AfterStateReplace }

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryArchiveCompletedProducerIntent(Guid domain, Guid operationId,
        LegacyMailboxPackageCompletedWorkProof completed, out string reason) =>
        TryArchiveCompletedProducerIntent(domain, operationId, completed, LegacyItemUidArchiveInterruption.None, out reason);

    internal bool TryArchiveCompletedProducerIntent(Guid domain, Guid operationId,
        LegacyMailboxPackageCompletedWorkProof completed, LegacyItemUidArchiveInterruption interruption, out string reason)
    {
        lock (gate)
        {
            reason = "itemUidArchiveInvalid";
            if (operationId == Guid.Empty || completed is null || !Enum.IsDefined(interruption) ||
                !TryAdmitProducerOwner(domain, out reason)) return false;
            try { RememberArchiveIndex(state, ReadArchiveCatalog(paths, state, null, allowNext: false)); }
            catch (Exception error) when (IsFileFailure(error))
            { reason = RecoveryRequired + ":archiveMembership:" + error.GetType().Name; blocked = reason; return false; }
            reason = "itemUidArchiveCompletionUnproven";
            var key = operationId.ToString("N");
            if (CurrentArchives.Entries.ContainsKey(key))
            {
                // Idempotent data retirement only; it issues no materialization token.
                if (!TryReadArchivedIntent(key, out var archived, out reason) ||
                    !completed.MatchesProducerIntent(domain, archived!))
                { if (reason.Length == 0) reason = "itemUidArchiveCompletionUnproven"; return false; }
                reason = string.Empty; return true;
            }
            var intent = FindProducerIntent(key);
            var allocation = state.Operations?.SingleOrDefault(row => row.OperationId == key);
            if (intent is null || allocation is null || intent.Writer != LegacyMailboxPackageProducer.Writer ||
                !completed.MatchesProducerIntent(domain, ProducerSnapshot(intent, allocation))) return false;
            if (state.Generation == ulong.MaxValue || state.ArchiveCount == ulong.MaxValue)
            { reason = "itemUidNamespaceExhausted"; return false; }
            try
            {
                var beforeBytes = ReadBounded(paths.State);
                if (!SameState(ParseState(beforeBytes), state))
                { reason = RecoveryRequired + ":archiveStateChanged"; blocked = reason; return false; }
                var record = new ProducerArchive(1, state.Domain, key, state.ArchiveHead, state.ArchiveHeadSha256,
                    state.Generation, state.Generation + 1, ArchiveDigest(beforeBytes), intent, allocation);
                var bytes = ArchiveBytes(record);
                if (bytes.Length > MaximumArchiveBytes) return false;
                var entry = new ArchiveEntry(record, bytes, ArchiveDigest(bytes));
                var after = RetiredState(state, entry);
                if (TryPublishArchive(state, beforeBytes, after, entry, interruption, out reason)) return true;
                if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
                return false;
            }
            catch (Exception error) when (IsFileFailure(error))
            { reason = RecoveryRequired + ":archivePreparation:" + error.GetType().Name; blocked = reason; return false; }
        }
    }

    // A valid missing operation is distinct from a failed owner/history read.
    internal bool TryFindProducerIntent(Guid domain, Guid operationId,
        out LegacyItemUidProducerIntentSnapshot? snapshot, out bool found, out string reason)
    {
        lock (gate)
        {
            snapshot = null; found = false; reason = "itemUidProducerIntentInvalid";
            if (operationId == Guid.Empty || !TryAdmitProducerOwner(domain, out reason)) return false;
            var key = operationId.ToString("N");
            var active = FindProducerIntent(key);
            if (active is null)
            {
                if (!TryReadArchivedIntent(key, out snapshot, out reason)) return false;
            }
            else snapshot = ProducerSnapshot(active, state.Operations?.FirstOrDefault(row => row.OperationId == key));
            found = snapshot is not null;
            reason = string.Empty;
            return true;
        }
    }

    // Bounded active work discovery. Completed history is available through the
    // original full enumeration and direct operation/account history APIs.
    internal bool TryReadActiveProducerIntents(Guid domain,
        out LegacyItemUidProducerIntentSnapshot[] snapshots, out string reason)
    {
        lock (gate)
        {
            snapshots = [];
            if (!TryAdmitProducerOwner(domain, out reason)) return false;
            snapshots = (state.ProducerIntents ?? []).Select(row => ProducerSnapshot(row,
                state.Operations?.FirstOrDefault(receipt => receipt.OperationId == row.OperationId))).ToArray();
            return true;
        }
    }

    internal bool TryReadArchivedProducerIntents(Guid domain, uint accountId, uint characterUid,
        out LegacyItemUidProducerIntentSnapshot[] snapshots, out string reason)
    {
        lock (gate)
        {
            snapshots = [];
            reason = "itemUidArchiveQueryInvalid";
            if (accountId == 0 || characterUid == 0 || !TryAdmitProducerOwner(domain, out reason)) return false;
            var prepared = new List<LegacyItemUidProducerIntentSnapshot>();
            foreach (var pair in CurrentArchives.Entries.Where(pair => pair.Value.AccountId == accountId &&
                pair.Value.CharacterUid == characterUid).OrderBy(pair => pair.Value.PreparedGeneration))
            {
                if (!TryReadArchivedIntent(pair.Key, out var archived, out reason)) return false;
                prepared.Add(archived!);
            }
            snapshots = prepared.ToArray();
            return true;
        }
    }
}
