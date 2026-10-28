using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyFullAccountClassPrepared
    {
        internal bool TryPersist(CancellationToken cancellationToken, out string reason)
        {
            var failure = "fullAccountClassOwnerChanged";
            bool result;
            if (Owner.Native is { } native)
            {
                lock (native.SpellAdmissionSyncRoot)
                lock (Owner.Player!.SpellAdmissionSyncRoot) result = WithPin();
            }
            else result = WithPin();
            reason = failure;
            return result;

            bool WithPin()
            {
                lock (LegacySecondaryPasswordRuntime.SyncRoot)
                    return SnapshotFileLockCoordinator.ExecuteLocked([characterPath, spellsPath], PersistLocked);
            }

            bool PersistLocked()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Owner.Recovery.TryRequireLiveJournalClear(out failure)) return false;
                if (!Owner.IsCurrent(Owner.Expected, checkInputs: true)) { failure = "fullAccountClassOwnerChanged"; return false; }
                if (!CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "fullAccountClass", out var character, out failure) ||
                    !character.AsSpan().SequenceEqual(changes[0].Before))
                { failure = "fullAccountClassCharacterBaselineStale"; return false; }
                var spells = ReadFullAccountClassSpells(spellsPath);
                if (spells is null ? changes[1].Before is not null : changes[1].Before is null || !spells.AsSpan().SequenceEqual(changes[1].Before))
                { failure = "fullAccountClassSpellBaselineStale"; return false; }
                if (!MetadataCurrent()) { failure = "fullAccountClassMetadataChanged"; return false; }
                cancellationToken.ThrowIfCancellationRequested();
                // Metadata/comparer/PIN reads precede complete source witnesses
                // and the final holder/lifetime check. No callouts follow commit.
                if (!Owner.IsCurrent(Owner.Expected, checkInputs: true) || !NativeMagicCurrent())
                { failure = "fullAccountClassOwnerChanged"; return false; }
                if (!LegacyTradeJournal.TryCommitCharacterSpells(Owner.UsersRoot, Owner.SpellsRoot, changes, out failure))
                {
                    if (failure.StartsWith("tradeJournalWriteFailed:", StringComparison.Ordinal))
                        failure = "transaction-write-failed:" + failure["tradeJournalWriteFailed:".Length..];
                    return false;
                }
                DurableCommitted = true;
                return true;
            }
        }
    }
}
