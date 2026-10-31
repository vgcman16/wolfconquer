using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Filesystem evidence belongs to the actual serialized creation lease. The
// complete saved names, rather than hashes or a caller's free-name assertion,
// determine admission. Case-insensitive collision refusal is sanitized server
// policy, not a claim about client name casing.
internal sealed partial class LegacyCharacterCreationNameClaim
{
    private readonly LegacyOwnedAdmissionRuntime.CreationLease owner;
    private readonly Dictionary<string, byte[]> characters;
    private readonly LegacyCharacterCreationAbsence absence;
    private readonly bool journalPresent;
    internal string Name { get; }

    private LegacyCharacterCreationNameClaim(LegacyOwnedAdmissionRuntime.CreationLease owner, string name,
        Dictionary<string, byte[]> characters, bool journalPresent, LegacyCharacterCreationAbsence absence)
    { this.owner = owner; Name = name; this.characters = characters; this.journalPresent = journalPresent; this.absence = absence; }

    internal static LegacyCharacterCreationNameClaim Capture(LegacyOwnedAdmissionRuntime.CreationLease owner,
        string normalizedName)
    {
        owner.RequireOwnerCurrent();
        if (!WorldServerFrameHandler.TryNormalizeAuthenticatedCreationName(normalizedName, out var actual) ||
            !string.Equals(actual, normalizedName, StringComparison.Ordinal))
            throw new InvalidDataException("Creation name does not roundtrip through its actual saved representation.");
        var scan = Scan(owner.Configuration.UsersRoot, normalizedName, owner.AccountId, null, null);
        var absent = LegacyCharacterCreationAbsence.Capture(owner.Configuration, owner.AccountId);
        owner.RequireOwnerCurrent();
        return new(owner, normalizedName, scan.Characters, scan.JournalPresent, absent);
    }

    internal void RequireCurrentBefore()
    {
        owner.RequirePlainOwnerCurrent();
        RequireScan(Scan(owner.Configuration.UsersRoot, Name, owner.AccountId, null, null), allowJournalCreation: false);
        absence.RequireCurrentBefore();
        owner.RequirePlainOwnerCurrent();
    }

    internal void RequireProjectedCharacterFits(byte[] actualCharacterImage)
    {
        RequireCurrentBefore();
        RequireOwnCharacterProjection(actualCharacterImage);
        owner.RequirePlainOwnerCurrent();
    }

    // Called only from the real producer/journal's exact boundary. This permits
    // its own literal null->AFTER targets and bounded known stage prefixes;
    // unrelated file membership, names and bytes stay captured. It does not
    // authenticate a journal or authorize any gameplay projection.
    internal void RequireCurrentAtWriteBoundary(IReadOnlyList<LegacyTradeJournalChange> changes, string transactionId)
    {
        owner.RequirePlainOwnerCurrent();
        var byKind = absence.RequireCreationChanges(changes, transactionId);
        var ownCharacter = byKind[LegacyTradeJournalFileKind.Character].After;
        RequireOwnCharacterProjection(ownCharacter);
        RequireScan(Scan(owner.Configuration.UsersRoot, Name, owner.AccountId, ownCharacter, transactionId),
            allowJournalCreation: true);
        absence.RequireCurrentAtWriteBoundary(byKind, transactionId);
        owner.RequirePlainOwnerCurrent();
    }

    private void RequireOwnCharacterProjection(byte[] image)
    {
        if (image.Length is < 1 or > CharacterSnapshotFileBaseline.MaximumBytes)
            throw new InvalidDataException("Creation Character image exceeds its complete-image bound.");
        RequireProjectedNameBudget(new(characters, journalPresent), image);
        var parsed = ReadCharacter(image);
        if (parsed.Uid != owner.AccountId || !string.Equals(parsed.Name, Name, StringComparison.Ordinal))
            throw new InvalidDataException("Creation Character differs from its name/account claim.");
    }

    private void RequireScan(NameScan current, bool allowJournalCreation)
        => RequireSameCharacters(new(characters, journalPresent), current, allowJournalCreation);

    internal static void RequireSameCharacters(NameScan captured, NameScan current, bool allowJournalCreation)
    {
        if ((!allowJournalCreation || captured.JournalPresent) && current.JournalPresent != captured.JournalPresent ||
            current.Characters.Count != captured.Characters.Count || captured.Characters.Any(pair =>
                !current.Characters.TryGetValue(pair.Key, out var bytes) || !bytes.AsSpan().SequenceEqual(pair.Value)))
            throw new InvalidDataException("Complete Character name membership/images changed during creation.");
    }

    internal static CharacterSnapshot ReadCharacter(byte[] bytes)
    {
        var text = CharacterSnapshotFileEncoding.Decode(bytes, out _);
        var fields = ReadCharacterFields(text);
        var parsed = CharacterSnapshotParser.ParseText(text);
        LegacyOrdinaryAdmissionValidation.ValidateCharacter(parsed);
        if (!fields.TryGetValue("Name", out var rawName) || rawName != parsed.Name ||
            !WorldServerFrameHandler.TryNormalizeAuthenticatedCreationName(parsed.Name, out var normalized) ||
            !string.Equals(parsed.Name, normalized, StringComparison.Ordinal))
            throw new InvalidDataException("Saved Character name has an ambiguous representation.");
        return parsed;
    }

    private static Dictionary<string, string> ReadCharacterFields(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(text);
        bool inCharacter = false; int sections = 0;
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inCharacter = line[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (inCharacter && ++sections > 1)
                    throw new InvalidDataException("Creation name image has ambiguous Character sections.");
                continue;
            }
            if (!inCharacter || line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            if (equals <= 0 || raw[..equals].Trim().Length == 0 ||
                !result.TryAdd(raw[..equals].Trim(), raw[(equals + 1)..]))
                throw new InvalidDataException("Creation name image has ambiguous Character fields.");
        }
        if (sections != 1) throw new InvalidDataException("Creation name image has no Character section.");
        return result;
    }

    internal static void RequireProjectedNameBudget(NameScan captured, byte[] ownAfter)
    {
        // Reserve room before issuing work for the new Character, its one
        // possible stage and the canonical journal directory. These are the
        // same explicit filesystem-read budgets, not a gameplay population cap.
        if ((long)captured.Characters.Count + 3 > MaximumNameEntries ||
            captured.Characters.Values.Sum(bytes => (long)bytes.Length) + 2L * ownAfter.Length > MaximumNameBytes)
            throw new InvalidDataException("Projected creation exceeds the complete-name evidence budget.");
    }

    internal sealed record NameScan(Dictionary<string, byte[]> Characters, bool JournalPresent);
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
