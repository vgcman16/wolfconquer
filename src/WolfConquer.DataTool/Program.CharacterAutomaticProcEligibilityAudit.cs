using System.Globalization;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

internal static partial class Program
{
    private const string CharacterAutomaticProcEligibilityAuditUsage =
        "Usage: WolfConquer.DataTool character-automatic-proc-eligibility-audit " +
        "(--character-uid UID | --character-name NAME | --character-snapshot path) " +
        "[--users-root path] [--spells-snapshot path] [--items-snapshot path] " +
        "[--magic-snapshot path | --world-data-root path] [--alternate-equipment]";

    private static readonly JsonSerializerOptions CharacterAutomaticProcJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    static int CharacterAutomaticProcEligibilityAudit(string[] args)
    {
        if (!TryParseCharacterAutomaticProcEligibilityAuditOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(CharacterAutomaticProcEligibilityAuditUsage);
            return 1;
        }

        CharacterAutomaticProcEligibilityAuditReport report;
        try
        {
            report = CharacterAutomaticProcEligibilityAuditor.Audit(options);
        }
        catch (Exception ex) when (ex is ArgumentException or
                                   IOException or
                                   UnauthorizedAccessException or
                                   InvalidDataException or
                                   FormatException or
                                   JsonException or
                                   OverflowException)
        {
            Console.Error.WriteLine($"Could not audit character automatic-proc eligibility: {ex.Message}");
            Console.Error.WriteLine(CharacterAutomaticProcEligibilityAuditUsage);
            return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(report, CharacterAutomaticProcJsonOptions));
        return 0;
    }

    private static bool TryParseCharacterAutomaticProcEligibilityAuditOptions(
        string[] args,
        out CharacterAutomaticProcEligibilityAuditOptions options,
        out string error)
    {
        options = new CharacterAutomaticProcEligibilityAuditOptions(
            null,
            null,
            null,
            CharacterSnapshotOptions.FromEnvironment().UsersRoot,
            null,
            null,
            null,
            null,
            AlternateEquipmentActive: false);
        error = "";

        long? characterUid = null;
        string? characterName = null;
        string? characterSnapshotPath = null;
        var usersRoot = options.UsersRoot;
        string? spellsSnapshotPath = null;
        string? itemsSnapshotPath = null;
        string? magicSnapshotPath = null;
        string? worldDataRoot = null;
        var alternateEquipmentActive = false;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            var normalized = argument.ToLowerInvariant();
            if (normalized == "--alternate-equipment")
            {
                alternateEquipmentActive = true;
                continue;
            }

            if (!TryReadCharacterAutomaticProcOptionValue(args, ref index, out var value, out error))
            {
                return false;
            }

            switch (normalized)
            {
                case "--character":
                    if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedCharacter) &&
                        parsedCharacter > 0)
                    {
                        characterUid = parsedCharacter;
                    }
                    else
                    {
                        characterName = value;
                    }

                    break;
                case "--character-uid":
                case "--uid":
                    if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedUid) ||
                        parsedUid <= 0)
                    {
                        error = $"Invalid character UID: {value}";
                        return false;
                    }

                    characterUid = parsedUid;
                    break;
                case "--character-name":
                case "--name":
                    characterName = value;
                    break;
                case "--character-snapshot":
                case "--character-path":
                    characterSnapshotPath = value;
                    break;
                case "--users-root":
                    usersRoot = value;
                    break;
                case "--spells-snapshot":
                case "--spell-snapshot":
                    spellsSnapshotPath = value;
                    break;
                case "--items-snapshot":
                case "--item-snapshot":
                    itemsSnapshotPath = value;
                    break;
                case "--magic-snapshot":
                case "--magic-path":
                    magicSnapshotPath = value;
                    break;
                case "--world-data-root":
                case "--seed-root":
                    worldDataRoot = value;
                    break;
                default:
                    error = $"Unknown option: {argument}";
                    return false;
            }
        }

        var selectorCount = (characterUid.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(characterName) ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(characterSnapshotPath) ? 1 : 0);
        if (selectorCount != 1)
        {
            error = "Specify exactly one character selector: UID, name, or character snapshot path.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(magicSnapshotPath) && !string.IsNullOrWhiteSpace(worldDataRoot))
        {
            error = "Specify either --magic-snapshot or --world-data-root, not both.";
            return false;
        }

        options = new CharacterAutomaticProcEligibilityAuditOptions(
            characterUid,
            characterName,
            characterSnapshotPath,
            usersRoot,
            spellsSnapshotPath,
            itemsSnapshotPath,
            magicSnapshotPath,
            worldDataRoot,
            alternateEquipmentActive);
        return true;
    }

    private static bool TryReadCharacterAutomaticProcOptionValue(
        string[] args,
        ref int index,
        out string value,
        out string error)
    {
        value = "";
        error = "";
        if (!args[index].StartsWith("--", StringComparison.Ordinal))
        {
            error = $"Unknown argument: {args[index]}";
            return false;
        }

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error = $"{args[index]} requires a value.";
            return false;
        }

        value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{args[index - 1]} requires a non-empty value.";
            return false;
        }

        return true;
    }
}

internal sealed record CharacterAutomaticProcEligibilityAuditOptions(
    long? CharacterUid,
    string? CharacterName,
    string? CharacterSnapshotPath,
    string UsersRoot,
    string? SpellsSnapshotPath,
    string? ItemsSnapshotPath,
    string? MagicSnapshotPath,
    string? WorldDataRoot,
    bool AlternateEquipmentActive);

internal sealed record CharacterAutomaticProcEligibilityAuditReport(
    string Command,
    bool ReadOnly,
    CharacterAutomaticProcSelectionReport Selection,
    CharacterAutomaticProcCharacterReport Character,
    CharacterAutomaticProcRuntimeConfigurationReport RuntimeConfiguration,
    CharacterAutomaticProcSourceSet Sources,
    IReadOnlyList<CharacterAutomaticProcWeaponReport> CurrentWeapons,
    IReadOnlyList<CharacterAutomaticProcLearnedSpellReport> LearnedSpells,
    IReadOnlyList<CharacterAutomaticProcCandidateSkillReport> CandidateSkills,
    IReadOnlyList<CharacterAutomaticProcMissingMetadataReport> MissingMetadata,
    CharacterAutomaticProcSummaryReport Summary);

internal sealed record CharacterAutomaticProcSelectionReport(
    string Mode,
    long? RequestedUid,
    string? RequestedName,
    string? RequestedPath);

internal sealed record CharacterAutomaticProcCharacterReport(
    long Uid,
    string Name,
    long Class,
    bool AlternateEquipmentActive,
    bool FatalStrikeActive);

internal sealed record CharacterAutomaticProcRuntimeConfigurationReport(
    bool CompatibilityFenceEnabled,
    uint ReplacementSelectionOuterChancePercent);

internal sealed record CharacterAutomaticProcSourceSet(
    CharacterAutomaticProcSourceReport Character,
    CharacterAutomaticProcSourceReport Spells,
    CharacterAutomaticProcSourceReport Items,
    CharacterAutomaticProcSourceReport Magic);

internal sealed record CharacterAutomaticProcSourceReport(
    string? Path,
    bool Exists,
    bool Explicit,
    string Format,
    int? EntryCount);

internal sealed record CharacterAutomaticProcWeaponReport(
    string Hand,
    bool Present,
    uint? ItemUid,
    uint? ItemId,
    ushort? Position,
    ushort? Durability,
    uint? Effect,
    ushort? WeaponType,
    ushort? EffectiveWeaponType);

internal sealed record CharacterAutomaticProcLearnedSpellReport(
    int SnapshotIndex,
    ushort SpellId,
    ushort Level,
    int Experience,
    string? Name,
    bool AutomaticProcSkill,
    bool CurrentCandidate,
    bool Eligible,
    string MetadataStatus,
    IReadOnlyList<string> CandidateSources);

internal sealed record CharacterAutomaticProcCandidateSkillReport(
    ushort SpellId,
    string? Name,
    bool Learned,
    ushort? LearnedLevel,
    IReadOnlyList<ushort> AvailableMetadataLevels,
    IReadOnlyList<CharacterAutomaticProcCandidateBranchReport> Branches,
    bool StructurallyEligible,
    bool Eligible,
    IReadOnlyList<string> IneligibilityReasons);

internal sealed record CharacterAutomaticProcCandidateBranchReport(
    string Phase,
    string Source,
    string? Hand,
    ushort? CurrentWeaponType,
    IReadOnlyList<uint> ConfiguredWeaponTypes,
    bool RequiresLearned,
    bool TemporaryWhenUnlearned,
    ushort? MetadataLevel,
    string MetadataStatus,
    uint? ProcChancePercent,
    uint? OuterChancePercent,
    bool StructurallyEligible,
    bool Eligible,
    IReadOnlyList<string> IneligibilityReasons);

internal sealed record CharacterAutomaticProcMissingMetadataReport(
    ushort? SpellId,
    ushort? Level,
    string Field,
    string Reason);

internal sealed record CharacterAutomaticProcSummaryReport(
    int LearnedSpellCount,
    int LearnedAutomaticProcSpellCount,
    int CandidateSkillCount,
    int StructurallyEligibleCandidateCount,
    int EligibleCandidateCount,
    int MissingMetadataCount);
