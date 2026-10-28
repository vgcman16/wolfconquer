using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyPaidSpellLearning
    {
        private (string Character, string Spells, LegacyTradeJournalChange[] Changes) PrepareImages()
        {
            var characterPath = Path.Combine(owner.UsersRoot, owner.Expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            var spellPath = Path.Combine(owner.SpellsRoot, owner.Expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            LegacyTradeJournalChange[]? result = null;
            SnapshotFileLockCoordinator.ExecuteLocked([characterPath, spellPath], () =>
            {
                if (!CharacterSnapshotFileBaseline.TryRead(characterPath, "paidSpell", out var baseline, out var reason) || baseline is null)
                    throw new LegacySpellMutationRefusalException(reason);
                var text = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out var encoding);
                if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(baseline.Bytes))
                    throw new LegacySpellMutationRefusalException("paidSpellEncodingUnproven");
                var before = CharacterSnapshotParser.ParseText(text);
                if (!FullAccountCharacterScalarsEqual(before, inputs.Stable.Character) ||
                    !FullAccountStoredFieldsMatch(before.Attributes, inputs.Stable.Character.Attributes) ||
                    !FullAccountStoredFieldsMatch(before.PaidNobility, inputs.Stable.Character.PaidNobility))
                    throw new LegacySpellMutationRefusalException("paidSpellCharacterBaselineStale");
                var afterText = LegacyOrdinaryAdmissionCharacterText.Project(text,
                    CharacterSnapshotScalarPersistence.BuildPersistedValues(Updated.Character), paid: null);
                if (!FullAccountCharacterScalarsEqual(CharacterSnapshotParser.ParseText(afterText), Updated.Character))
                    throw new LegacySpellMutationRefusalException("paidSpellAfterImageMismatch");
                var characterAfter = encoding.GetPreamble().Concat(encoding.GetBytes(afterText)).ToArray();
                var spellsBefore = spellWrite.CopyBeforeImage();
                var spellsAfter = spellWrite.CopyAfterImage();
                result = [new(LegacyTradeJournalFileKind.Character, owner.Expected.AccountId, baseline.Bytes, characterAfter),
                    new(LegacyTradeJournalFileKind.Spells, owner.Expected.Character.Uid, spellsBefore, spellsAfter)];
                return true;
            });
            return (characterPath, spellPath, result!);
        }

        internal bool TryPersist(CancellationToken cancellationToken, out string reason)
        {
            var failure = "paidSpellOwnerChanged";
            bool PersistLocked()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!owner.IsCurrent() || !LegacySecondaryPasswordRuntime.IsAdmitted(owner.Context.Connection, inputs.Stable) ||
                    !owner.Recovery.TryRequireLiveJournalClear(out failure)) return false;
                if (!CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "paidSpell", out var current, out failure) ||
                    !current.AsSpan().SequenceEqual(changes[0].Before) ||
                    !SpellMutationImagesEqual(changes[1].Before, ReadLegacySpellMutationFile(spellsPath)))
                { failure = "paidSpellBaselineStale"; return false; }
                if (!inputs.ConsumeCurrentPlain()) { failure = "paidSpellInputsChanged"; return false; }
                cancellationToken.ThrowIfCancellationRequested();
                if (!spellWrite.TryCommitCharacterSpells(changes, out failure)) return false;
                DurableCommitted = true; return true;
            }
            bool WithPin()
            {
                lock (LegacySecondaryPasswordRuntime.SyncRoot)
                    return SnapshotFileLockCoordinator.ExecuteLocked([characterPath, spellsPath], PersistLocked);
            }
            bool committed;
            if (owner.Native is { } native)
            {
                lock (native.SpellAdmissionSyncRoot)
                lock (owner.Player!.SpellAdmissionSyncRoot) committed = WithPin();
            }
            else committed = WithPin();
            reason = failure; return committed;
        }
    }
}
