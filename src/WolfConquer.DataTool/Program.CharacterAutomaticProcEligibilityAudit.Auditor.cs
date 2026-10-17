using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

internal static partial class CharacterAutomaticProcEligibilityAuditor
{
    private const int MagicTypeIdField = 1;
    private const int MagicSortField = 2;
    private const int MagicAttackInFlyField = 5;
    private const int MagicLevelField = 8;
    private const int MagicUseManaField = 9;
    private const int MagicDamageField = 10;
    private const int MagicRateField = 12;
    private const int MagicDurationField = 13;
    private const int MagicMaxTargetsField = 14;
    private const int MagicRangeField = 15;
    private const int MagicExperienceField = 18;
    private const int MagicNeedLevelField = 20;
    private const int MagicWeaponTypeField = 22;
    private const int MagicUseStaminaField = 29;
    private const int MagicCoolDownField = 33;
    private const int MagicDamageOnHumanField = 35;
    private const int MagicDamage2Field = 36;
    private const int MagicDamage3Field = 37;
    private const int MagicDamageOnMonsterField = 44;
    private const int MagicColdTimeField = 47;
    private const int MagicMinimumRuntimeFieldCount = MagicColdTimeField + 1;
    private const int FatalStrikeStatusFlag = 0x2F;
    private const uint PoisonWeaponEffect = 200;
    private const uint ManaWeaponEffect = 202;
    private const ushort PoisonWeaponEffectSpellId = 3_090;
    private const ushort ManaWeaponEffectSpellId = 1_175;

    private static readonly ushort[] WeaponTableProcSpellIds =
    [
        1_250, 1_260, 1_290, 1_300,
        5_010, 5_020, 5_030, 5_040, 5_050,
        7_000, 7_010, 7_020, 7_030, 7_040,
        10_490, 11_140, 11_990,
        12_110, 12_210, 12_220, 12_230, 12_240,
        12_580, 12_590, 12_600,
        12_740, 12_750, 12_760,
        13_040, 13_050, 13_060,
        14_570, 14_580,
        15_670, 15_680, 15_690,
    ];

    private static readonly AutomaticProcFixedBranchDefinition[] ReplacementDirectBranches =
    [
        new(11_140, "replacementSelection", "directLearned", 10, UsesOuterChance: false),
        new(14_720, "replacementSelection", "directLearned", 10, UsesOuterChance: false),
        new(12_670, "replacementSelection", "directLearned", 10, UsesOuterChance: false),
        new(15_800, "replacementSelection", "directLearned", 10, UsesOuterChance: false),
    ];

    private static readonly AutomaticProcFixedBranchDefinition[] ReplacementPostTableBranches =
    [
        new(15_950, "replacementSelection", "postTableLearned", 30, UsesOuterChance: true),
        new(7_040, "replacementSelection", "postTableLearned", 30, UsesOuterChance: true),
    ];

    private static readonly AutomaticProcFixedBranchDefinition[] PostPhysicalBranches =
    [
        new(7_030, "postPhysical", "postPhysicalLearned", 80, UsesOuterChance: false, RequiresTrojan: true),
        new(11_140, "postPhysical", "postPhysicalLearned", 10, UsesOuterChance: false),
        new(14_720, "postPhysical", "postPhysicalLearned", 42, UsesOuterChance: false),
        new(6_011, "postPhysical", "postPhysicalLearned", 100, UsesOuterChance: false),
        new(12_670, "postPhysical", "postPhysicalLearned", 10, UsesOuterChance: false),
    ];

    private static readonly HashSet<ushort> KnownAutomaticProcSpellIds = CreateKnownAutomaticProcSpellIds();

    private static readonly JsonSerializerOptions MagicJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static CharacterAutomaticProcEligibilityAuditReport Audit(
        CharacterAutomaticProcEligibilityAuditOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loadedCharacter = LoadCharacter(options);
        var sidecars = LoadSidecars(options, loadedCharacter);
        var magic = LoadMagic(options, loadedCharacter.Path);
        var magicBySpellId = magic.Rows
            .Where(row => row.TypeId is > 0 and <= ushort.MaxValue)
            .GroupBy(row => checked((ushort)row.TypeId))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MagicType>)group.ToArray());
        var learnedById = new Dictionary<ushort, PlayerSpellSnapshotEntry>();
        foreach (var spell in sidecars.Spells.Spells)
        {
            learnedById.TryAdd(spell.Id, spell);
        }

        var compatibility = LegacyClientCompatibilityOptions.FromEnvironment();
        var fatalStrikeActive = LegacyPlayerDeathState.HasStatusFlag(
            loadedCharacter.Snapshot.Attributes,
            FatalStrikeStatusFlag);
        var outerChance = IsTrojanClass(loadedCharacter.Snapshot.Class) ? 100u : 42u;
        var weapons = ResolveCurrentWeapons(sidecars.Items, options.AlternateEquipmentActive);
        var branches = BuildCandidateBranchSeeds(
            loadedCharacter.Snapshot,
            learnedById,
            magic.Rows,
            magicBySpellId,
            weapons,
            fatalStrikeActive,
            outerChance);

        var missingMetadata = new List<CharacterAutomaticProcMissingMetadataReport>();
        var candidates = branches
            .GroupBy(branch => branch.SpellId)
            .OrderBy(group => group.Key)
            .Select(group => BuildCandidateSkillReport(
                group.Key,
                group.ToArray(),
                learnedById,
                magicBySpellId,
                compatibility.DisableLegacyWeaponProcMonsterAttacks,
                missingMetadata))
            .ToArray();
        var candidateById = candidates.ToDictionary(candidate => candidate.SpellId);

        foreach (var learned in learnedById.Values
                     .Where(spell => KnownAutomaticProcSpellIds.Contains(spell.Id))
                     .OrderBy(spell => spell.Id))
        {
            var resolution = ResolveMagicMetadata(magicBySpellId, learned.Id, learned.Level);
            AddMissingMetadata(missingMetadata, learned.Id, learned.Level, resolution);
        }

        if (!magic.Source.Exists && learnedById.Keys.Any(KnownAutomaticProcSpellIds.Contains))
        {
            missingMetadata.Add(new CharacterAutomaticProcMissingMetadataReport(
                null,
                null,
                "magicSnapshot",
                $"Magic metadata source was not found at '{magic.Source.Path}'."));
        }

        var normalizedMissingMetadata = missingMetadata
            .DistinctBy(item => (item.SpellId, item.Level, item.Field, item.Reason))
            .OrderBy(item => item.SpellId ?? 0)
            .ThenBy(item => item.Level ?? 0)
            .ThenBy(item => item.Field, StringComparer.Ordinal)
            .ThenBy(item => item.Reason, StringComparer.Ordinal)
            .ToArray();
        var learnedReports = sidecars.Spells.Spells
            .Select((spell, index) => BuildLearnedSpellReport(
                spell,
                index,
                magicBySpellId,
                candidateById))
            .OrderBy(spell => spell.SpellId)
            .ThenBy(spell => spell.SnapshotIndex)
            .ToArray();

        var selection = options.CharacterUid.HasValue
            ? new CharacterAutomaticProcSelectionReport("uid", options.CharacterUid, null, null)
            : !string.IsNullOrWhiteSpace(options.CharacterName)
                ? new CharacterAutomaticProcSelectionReport("name", null, options.CharacterName, null)
                : new CharacterAutomaticProcSelectionReport(
                    "snapshot",
                    null,
                    null,
                    Path.GetFullPath(options.CharacterSnapshotPath!));
        var sources = new CharacterAutomaticProcSourceSet(
            new CharacterAutomaticProcSourceReport(
                loadedCharacter.Path,
                Exists: true,
                Explicit: !string.IsNullOrWhiteSpace(options.CharacterSnapshotPath),
                "legacyIni",
                EntryCount: 1),
            sidecars.SpellsSource,
            sidecars.ItemsSource,
            magic.Source);

        return new CharacterAutomaticProcEligibilityAuditReport(
            "character-automatic-proc-eligibility-audit",
            ReadOnly: true,
            selection,
            new CharacterAutomaticProcCharacterReport(
                loadedCharacter.Snapshot.Uid,
                loadedCharacter.Snapshot.Name,
                loadedCharacter.Snapshot.Class,
                options.AlternateEquipmentActive,
                fatalStrikeActive),
            new CharacterAutomaticProcRuntimeConfigurationReport(
                compatibility.DisableLegacyWeaponProcMonsterAttacks,
                outerChance),
            sources,
            weapons.Select(weapon => weapon.Report).ToArray(),
            learnedReports,
            candidates,
            normalizedMissingMetadata,
            new CharacterAutomaticProcSummaryReport(
                learnedReports.Length,
                learnedReports.Count(spell => spell.AutomaticProcSkill),
                candidates.Length,
                candidates.Count(candidate => candidate.StructurallyEligible),
                candidates.Count(candidate => candidate.Eligible),
                normalizedMissingMetadata.Length));
    }

    private sealed record LoadedCharacter(string Path, CharacterSnapshot Snapshot);

    private sealed record LoadedSidecars(
        PlayerSpellSnapshot Spells,
        PlayerItemSnapshot Items,
        CharacterAutomaticProcSourceReport SpellsSource,
        CharacterAutomaticProcSourceReport ItemsSource);

    private sealed record LoadedMagic(
        IReadOnlyList<MagicType> Rows,
        CharacterAutomaticProcSourceReport Source);

    private sealed record ResolvedWeapon(
        CharacterAutomaticProcWeaponReport Report,
        PlayerItemSnapshotItem? Item);

    private sealed record AutomaticProcFixedBranchDefinition(
        ushort SpellId,
        string Phase,
        string Source,
        uint ProcChancePercent,
        bool UsesOuterChance,
        bool RequiresTrojan = false);

    private sealed record CandidateBranchSeed(
        ushort SpellId,
        string Phase,
        string Source,
        string? Hand,
        ushort? CurrentWeaponType,
        IReadOnlyList<uint> ConfiguredWeaponTypes,
        uint? FixedProcChancePercent,
        uint? OuterChancePercent,
        bool RequiresLearned,
        bool TemporaryWhenUnlearned,
        IReadOnlyList<string> PreconditionFailures);

    private sealed record WeaponTableMatch(ushort SpellId, uint ConfiguredWeaponTypes);

    private sealed record MagicMetadataResolution(
        string Status,
        MagicType? Row,
        IReadOnlyList<string> InvalidFields);
}
