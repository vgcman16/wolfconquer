using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Data-only repeatable membership evidence. This type cannot issue a permit,
// allocate a UID, open a journal or recover files. The real owned-startup
// custody must separately bind the exact captured record and exclusive lease.
internal sealed class LegacyCharacterCreationRecoveryEvidence
{
    private readonly LegacyOwnedAdmissionOptions configuration;
    private readonly uint account;
    private readonly string name, transaction;
    private readonly LegacyTradeJournalChange[] changes;
    private readonly byte[] characterAfter;
    private readonly LegacyCharacterCreationNameClaim.NameScan names;
    private readonly LegacyCharacterCreationAbsence membership;

    private LegacyCharacterCreationRecoveryEvidence(LegacyOwnedAdmissionOptions configuration, uint account,
        string name, string transaction, LegacyTradeJournalChange[] changes, byte[] characterAfter,
        LegacyCharacterCreationNameClaim.NameScan names, LegacyCharacterCreationAbsence membership)
    {
        this.configuration = configuration; this.account = account; this.name = name; this.transaction = transaction;
        this.changes = changes; this.characterAfter = characterAfter; this.names = names; this.membership = membership;
    }

    internal static LegacyCharacterCreationRecoveryEvidence Capture(LegacyOwnedAdmissionOptions configuration,
        uint account, string name, IReadOnlyList<LegacyTradeJournalChange> changes, string transactionId)
    {
        RequireIdentity(configuration, account, name);
        if (changes.Count != 6 || changes.Any(row => row is null || row.After is null))
            throw new InvalidDataException("Recorded creation participants are incomplete.");
        var copied = changes.Select(row => row with { Before = row.Before?.ToArray(), After = row.After.ToArray() }).ToArray();
        var membership = LegacyCharacterCreationAbsence.CaptureRecorded(configuration, account, copied, transactionId);
        var image = copied.Single(row => row.Kind == LegacyTradeJournalFileKind.Character).After;
        var character = LegacyCharacterCreationNameClaim.ReadCharacter(image);
        if (character.Uid != account || character.Name != name)
            throw new InvalidDataException("Recorded creation Character differs from its exact name/account.");
        var names = LegacyCharacterCreationNameClaim.Scan(configuration.UsersRoot, name, account, image, transactionId);
        LegacyCharacterCreationNameClaim.RequireProjectedNameBudget(names, image);
        var result = new LegacyCharacterCreationRecoveryEvidence(configuration, account, name, transactionId,
            copied, image, names, membership);
        result.RequireCurrent(); return result;
    }

    internal static void RequireBeforeAllocation(LegacyOwnedAdmissionOptions configuration, uint account,
        string name, byte[] actualCharacter)
    {
        RequireIdentity(configuration, account, name);
        if (actualCharacter.Length is < 1 or > CharacterSnapshotFileBaseline.MaximumBytes)
            throw new InvalidDataException("Recorded creation Character exceeds its complete-image bound.");
        var character = LegacyCharacterCreationNameClaim.ReadCharacter(actualCharacter);
        if (character.Uid != account || character.Name != name)
            throw new InvalidDataException("Recorded creation Character differs from its exact name/account.");
        var names = LegacyCharacterCreationNameClaim.Scan(configuration.UsersRoot, name, account, null, null);
        var absence = LegacyCharacterCreationAbsence.Capture(configuration, account);
        LegacyCharacterCreationNameClaim.RequireProjectedNameBudget(names, actualCharacter);
        LegacyCharacterCreationNameClaim.RequireSameCharacters(names,
            LegacyCharacterCreationNameClaim.Scan(configuration.UsersRoot, name, account, null, null), allowJournalCreation: false);
        absence.RequireCurrentBefore();
    }

    private static void RequireIdentity(LegacyOwnedAdmissionOptions configuration, uint account, string name)
    {
        if (!configuration.TryNormalize(out var normalized, out var reason) || normalized != configuration || account == 0 ||
            !WorldServerFrameHandler.TryNormalizeAuthenticatedCreationName(name, out var actualName) || actualName != name)
            throw new InvalidDataException("Recorded creation routes/name/account are not canonical: " + reason);
    }

    internal void RequireCurrent()
    {
        var current = LegacyCharacterCreationNameClaim.Scan(configuration.UsersRoot, name, account, characterAfter, transaction);
        LegacyCharacterCreationNameClaim.RequireSameCharacters(names, current, allowJournalCreation: true);
        var byKind = membership.RequireCreationChanges(changes, transaction);
        membership.RequireCurrentAtWriteBoundary(byKind, transaction);
    }
}
