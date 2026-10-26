using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryArchiveCompletedOrdinaryAdmission(Guid domain, Guid operation,
        LegacyOrdinaryAdmissionCompletedWorkProof completed, out string reason)
    {
        lock (gate)
        {
            reason = "ordinaryAdmissionUidArchiveInvalid";
            if (operation == Guid.Empty || completed is null || !TryAdmitProducerOwner(domain, out reason)) return false;
            try
            {
                RememberArchiveIndex(state, ReadArchiveCatalog(paths, state, null, false));
                var key = operation.ToString("N");
                if (CurrentArchives.Entries.ContainsKey(key))
                {
                    if (!TryReadArchivedIntent(key, out var archived, out reason) ||
                        !completed.MatchesProducerIntent(domain, archived!)) return false;
                    reason = string.Empty; return true;
                }
                var intent = FindProducerIntent(key);
                var allocation = state.Operations?.SingleOrDefault(row => row.OperationId == key);
                if (intent is null || allocation is null || intent.Writer != LegacyOrdinaryAdmissionUidOwner.Writer ||
                    !completed.MatchesProducerIntent(domain, ProducerSnapshot(intent, allocation)))
                { reason = "ordinaryAdmissionUidArchiveCompletionUnproven"; return false; }
                if (state.Generation == ulong.MaxValue || state.ArchiveCount == ulong.MaxValue)
                { reason = "itemUidNamespaceExhausted"; return false; }
                var before = ReadBounded(paths.State);
                if (!SameState(ParseState(before), state)) throw new IOException("UID archive baseline changed.");
                var record = new ProducerArchive(1, state.Domain, key, state.ArchiveHead, state.ArchiveHeadSha256,
                    state.Generation, state.Generation + 1, ArchiveDigest(before), intent, allocation);
                var bytes = ArchiveBytes(record);
                if (bytes.Length > MaximumArchiveBytes) throw new InvalidDataException("UID archive is too large.");
                var entry = new ArchiveEntry(record, bytes, ArchiveDigest(bytes));
                if (TryPublishArchive(state, before, RetiredState(state, entry), entry,
                        LegacyItemUidArchiveInterruption.None, out reason)) return true;
                if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
                return false;
            }
            catch (Exception error) when (IsFileFailure(error))
            { reason = RecoveryRequired + ":ordinaryArchive:" + error.GetType().Name; blocked = reason; return false; }
        }
    }
}
