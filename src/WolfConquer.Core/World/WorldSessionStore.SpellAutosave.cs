using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Sole real Frame callback consumer. Memory was already published by Set/CAS;
    // this does not turn unrelated sidecars into a multi-file transaction.
    internal bool TryPersistPublishedOrdinarySpells(ConquerTcpConnection connection, WorldSession current,
        LegacyTradeRecoveryState recovery, string? spellsRoot, out string reason)
    {
        reason = "ordinarySpellAutosaveOwnerChanged";
        if (spellsRoot is null || !_connectionGates.TryGetValue(connection, out var gate) || !Monitor.IsEntered(gate) ||
            !_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), current) ||
            holder.SpellBinding is not { } binding || !ReferenceEquals(binding.Recovery, recovery) ||
            !BindingIsCurrentPlain(binding, holder, current)) return false;
        using var publication = new PreparedSessionSpellPublication(this, connection, holder, current, current);
        var write = PrepareSpellFileWrite(connection, current, recovery, binding.UsersRoot, spellsRoot);
        write.PrepareImage(current.PlayerSpells);
        var changes = new LegacyTradeJournalChange[]
        {
            write.CaptureUnchangedCharacter(),
            new(LegacyTradeJournalFileKind.Spells, current.Character.Uid, write.CopyBeforeImage(), write.CopyAfterImage()),
        };
        publication.ArmFinalProof();
        if (!publication.ConsumeBeforeWriterPlain()) throw InvalidSpellPublication();
        bool saved;
        try { saved = write.TryCommitCharacterSpells(changes, out reason); }
        catch (LegacyCommittedSpellPublicationException) { FailCommittedSpellPublication(holder, binding); throw; }
        if (!saved)
        {
            if (recovery.IsBlocked) FailCommittedSpellPublication(holder, binding);
            return false;
        }
        if (!publication.IsCurrentPlain()) FailCommittedSpellPublication(holder, binding);
        publication.PublishNative();
        publication.PublishHolder();
        return true;
    }
}
