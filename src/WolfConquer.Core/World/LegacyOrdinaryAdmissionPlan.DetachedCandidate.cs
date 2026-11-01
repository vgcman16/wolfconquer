using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionPlan
{
    private bool PendingQueryDataIsCurrent(WorldPacketHandlers.InitialWorldEntryQueryData data, WorldSession candidate) =>
        frame.DetachedInitialQueryDataIsCurrent(data, candidate, LoginSpellPublication) &&
        (!data.RequiresDerivedSpellWrite || roots.Items is { } items && roots.Spells is { } spells &&
            Path.IsPathFullyQualified(items) && Path.IsPathFullyQualified(spells) &&
            Recovery.IsReadyForCharacterItemsSpells(roots.Users, items, spells));

    // Called only under the real Store admission lease, before the journal or
    // native publication. The lease issues the actual detached native plans;
    // this candidate is not a live holder or query authority.
    private void PrepareDetachedCandidate(WorldSessionStore.OrdinaryAdmissionLease lease,
        LegacyTradeJournalChange[] changes)
    {
        var stored = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterAfter, out _),
            Path.GetFileName(roots.PathFor(LegacyTradeJournalFileKind.Character, account))) with { PlayerQuests = baseCharacter.PlayerQuests };
        Candidate = new(account, username, frame.ProjectOrdinaryStartupTitle(stored), normalization.RuleItems, normalization.Spells,
            profs, CombatGear: gear, UsersRoot: roots.Users) { NativeGameClient = native };
        if (frame.UsesOrdinarySpellBinding)
        {
            var savedSpellImage = changes.FirstOrDefault(change => change.Kind == LegacyTradeJournalFileKind.Spells)?.After ??
                images.GetValueOrDefault(LegacyTradeJournalFileKind.Spells);
            var savedProficiencyImage = changes.FirstOrDefault(change => change.Kind == LegacyTradeJournalFileKind.Proficiencies)?.After ??
                images.GetValueOrDefault(LegacyTradeJournalFileKind.Proficiencies);
            Candidate = lease.PrepareSpellAdmission(Candidate, Recovery, roots.Spells, savedSpellImage,
                roots.Proficiencies, savedProficiencyImage, prefix);
        }
        if (!LegacySpellPublicationPlan.TryPrepare(Candidate.PlayerSpells, newLoginSpellIds, out var finalPublication) ||
            finalPublication is null || finalPublication.Frames.Length != LoginSpellPublication.Frames.Length)
            throw new InvalidDataException("Ordinary canonical spell publication is invalid.");
        for (int index = 0; index < finalPublication.Frames.Length; index++)
            if (!finalPublication.Frames[index].AsSpan().SequenceEqual(LoginSpellPublication.Frames[index]))
                throw new InvalidDataException("Ordinary canonical spell publication changed its prepared wire frames.");
        LoginSpellPublication = finalPublication;
    }
}
