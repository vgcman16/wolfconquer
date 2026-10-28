using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    private static void SaveJoinedSpells(LegacyOrdinarySpellBinding binding)
    {
        var lease = binding.EnterMutation();
        bool persisted = false;
        try
        {
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
            lock (binding.Storage.MutationGate)
            lock (binding.Proficiencies.MutationGate)
            {
                var current = binding.Snapshot;
                var spells = LegacyNativeSpellStorage.PrepareSessionSnapshot(binding.Client,
                    binding.Player, current, current);
                var profs = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(binding.Client,
                    binding.Player, lease.ExpectedProficiencies, lease.ExpectedProficiencies);
                var write = lease.PrepareSpellFileWrite();
                write.PrepareImage(spells.FinalSnapshot);
                var character = write.CaptureUnchangedCharacter();
                LegacyTradeJournalChange[] changes = [character, new(LegacyTradeJournalFileKind.Spells,
                    binding.Uid, write.CopyBeforeImage(), write.CopyAfterImage())];
                spells.ArmFinalProof(); profs.ArmFinalProof();
                lease.PreparePublication(spells.FinalSnapshot, profs.FinalSnapshot);
                if (!spells.ConsumeBeforeWriterPlain() || !profs.ConsumeBeforeWriterPlain() || !lease.IsCurrentPlain())
                    throw new InvalidOperationException("Native spell save owner changed before persistence.");
                if (!write.TryCommitCharacterSpells(changes, out var reason))
                    throw new InvalidOperationException("Native spell save refused: " + reason);
                persisted = true;
                if (!spells.ConsumeCurrentPlain() || !profs.ConsumeCurrentPlain() || !lease.IsCurrentPlain())
                    lease.FailCommittedPublication();
                spells.PublishPreparedMap(); profs.PublishPreparedMap(); lease.PublishPrepared();
            }
        }
        catch (LegacyCommittedSpellPublicationException)
        {
            lease.FailCommittedPublication();
            throw;
        }
        catch when (persisted)
        {
            lease.FailCommittedPublication();
            throw;
        }
        finally { lease.Dispose(); }
    }
}
