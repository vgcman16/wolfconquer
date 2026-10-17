using WolfConquer.Core.Data;
using WolfConquer.Core.World;

internal static partial class CharacterAutomaticProcEligibilityAuditor
{
    private static IReadOnlyList<CandidateBranchSeed> BuildCandidateBranchSeeds(
        CharacterSnapshot character,
        IReadOnlyDictionary<ushort, PlayerSpellSnapshotEntry> learnedById,
        IReadOnlyList<MagicType> magicRows,
        IReadOnlyDictionary<ushort, IReadOnlyList<MagicType>> magicBySpellId,
        IReadOnlyList<ResolvedWeapon> weapons,
        bool fatalStrikeActive,
        uint outerChance)
    {
        var branches = new List<CandidateBranchSeed>();
        AddLearnedFixedBranches(branches, ReplacementDirectBranches, learnedById, character, outerChance);

        var rightWeapon = weapons.Single(weapon => weapon.Report.Hand == "right");
        if (rightWeapon.Item is not null && rightWeapon.Item.Effect is PoisonWeaponEffect or ManaWeaponEffect)
        {
            var spellId = rightWeapon.Item.Effect == PoisonWeaponEffect
                ? PoisonWeaponEffectSpellId
                : ManaWeaponEffectSpellId;
            branches.Add(new CandidateBranchSeed(
                spellId,
                "replacementSelection",
                "rightWeaponEffect",
                "right",
                rightWeapon.Report.EffectiveWeaponType,
                [],
                FixedProcChancePercent: 20,
                OuterChancePercent: outerChance,
                RequiresLearned: true,
                TemporaryWhenUnlearned: false,
                fatalStrikeActive ? ["fatalStrikeActive"] : []));
        }

        foreach (var weapon in weapons.Where(weapon => weapon.Report.EffectiveWeaponType.HasValue))
        {
            var weaponType = weapon.Report.EffectiveWeaponType!.Value;
            var matching = magicRows
                .Select(row => TryCreateWeaponTableMatch(row, weaponType, out var match) ? match : null)
                .Where(match => match is not null)
                .Cast<WeaponTableMatch>()
                .GroupBy(match => match.SpellId)
                .OrderBy(group => group.Key);
            foreach (var group in matching)
            {
                branches.Add(new CandidateBranchSeed(
                    group.Key,
                    "replacementSelection",
                    "weaponTable",
                    weapon.Report.Hand,
                    weaponType,
                    group.Select(match => match.ConfiguredWeaponTypes).Distinct().OrderBy(value => value).ToArray(),
                    FixedProcChancePercent: null,
                    OuterChancePercent: outerChance,
                    RequiresLearned: true,
                    TemporaryWhenUnlearned: false,
                    []));
            }
        }

        AddLearnedFixedBranches(branches, ReplacementPostTableBranches, learnedById, character, outerChance);
        AddLearnedFixedBranches(branches, PostPhysicalBranches, learnedById, character, outerChance);

        var meleeTriggerEligible = branches.Any(branch =>
            branch.Source == "weaponTable" &&
            branch.SpellId is 14_570 or 14_580 &&
            IsBranchStructurallyUsable(branch, learnedById, magicBySpellId));
        if (meleeTriggerEligible && learnedById.ContainsKey(11_140))
        {
            branches.Add(new CandidateBranchSeed(
                11_140,
                "checkMelee",
                "checkMeleeFollowUp",
                null,
                null,
                [],
                FixedProcChancePercent: 46,
                OuterChancePercent: null,
                RequiresLearned: true,
                TemporaryWhenUnlearned: false,
                []));
        }

        return branches
            .OrderBy(branch => branch.SpellId)
            .ThenBy(branch => PhaseOrder(branch.Phase))
            .ThenBy(branch => branch.Source, StringComparer.Ordinal)
            .ThenBy(branch => branch.Hand, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddLearnedFixedBranches(
        ICollection<CandidateBranchSeed> branches,
        IEnumerable<AutomaticProcFixedBranchDefinition> definitions,
        IReadOnlyDictionary<ushort, PlayerSpellSnapshotEntry> learnedById,
        CharacterSnapshot character,
        uint outerChance)
    {
        foreach (var definition in definitions.Where(definition => learnedById.ContainsKey(definition.SpellId)))
        {
            var preconditions = definition.RequiresTrojan && !IsTrojanClass(character.Class)
                ? new[] { "classNotTrojan" }
                : [];
            branches.Add(new CandidateBranchSeed(
                definition.SpellId,
                definition.Phase,
                definition.Source,
                null,
                null,
                [],
                definition.ProcChancePercent,
                definition.UsesOuterChance ? outerChance : null,
                RequiresLearned: true,
                TemporaryWhenUnlearned: false,
                preconditions));
        }
    }

    private static bool TryCreateWeaponTableMatch(
        MagicType row,
        ushort weaponType,
        out WeaponTableMatch match)
    {
        match = default!;
        if (!TryReadUInt16(row.Fields, MagicTypeIdField, out var spellId) ||
            Array.BinarySearch(WeaponTableProcSpellIds, spellId) < 0 ||
            !TryReadUInt32(row.Fields, MagicWeaponTypeField, out var configuredWeaponTypes) ||
            !WeaponTypeMatches(configuredWeaponTypes, weaponType))
        {
            return false;
        }

        match = new WeaponTableMatch(spellId, configuredWeaponTypes);
        return true;
    }

    private static bool IsBranchStructurallyUsable(
        CandidateBranchSeed branch,
        IReadOnlyDictionary<ushort, PlayerSpellSnapshotEntry> learnedById,
        IReadOnlyDictionary<ushort, IReadOnlyList<MagicType>> magicBySpellId)
    {
        if (!learnedById.TryGetValue(branch.SpellId, out var learned))
        {
            return false;
        }

        var resolution = ResolveMagicMetadata(magicBySpellId, branch.SpellId, learned.Level);
        return resolution.Status == "present" &&
            resolution.Row is not null &&
            TryReadUInt32(resolution.Row.Fields, MagicRateField, out var rate) &&
            rate is > 0 and <= 100;
    }

    private static CharacterAutomaticProcCandidateSkillReport BuildCandidateSkillReport(
        ushort spellId,
        IReadOnlyList<CandidateBranchSeed> seeds,
        IReadOnlyDictionary<ushort, PlayerSpellSnapshotEntry> learnedById,
        IReadOnlyDictionary<ushort, IReadOnlyList<MagicType>> magicBySpellId,
        bool compatibilityFenceEnabled,
        ICollection<CharacterAutomaticProcMissingMetadataReport> missingMetadata)
    {
        learnedById.TryGetValue(spellId, out var learned);
        var branchReports = seeds
            .Select(seed => BuildCandidateBranchReport(
                seed,
                learned,
                magicBySpellId,
                compatibilityFenceEnabled,
                missingMetadata))
            .OrderBy(branch => PhaseOrder(branch.Phase))
            .ThenBy(branch => branch.Source, StringComparer.Ordinal)
            .ThenBy(branch => branch.Hand, StringComparer.Ordinal)
            .ToArray();
        var rows = magicBySpellId.GetValueOrDefault(spellId) ?? [];
        var availableLevels = rows
            .Select(row => TryReadByte(row.Fields, MagicLevelField, out var level) ? (ushort?)level : null)
            .Where(level => level.HasValue)
            .Select(level => level!.Value)
            .Distinct()
            .OrderBy(level => level)
            .ToArray();
        var resolvedName = ResolveCandidateName(rows, learned?.Level);
        var structurallyEligible = branchReports.Any(branch => branch.StructurallyEligible);
        var eligible = branchReports.Any(branch => branch.Eligible);
        var ineligibilityReasons = eligible
            ? []
            : branchReports.SelectMany(branch => branch.IneligibilityReasons)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(reason => reason, StringComparer.Ordinal)
                .ToArray();

        return new CharacterAutomaticProcCandidateSkillReport(
            spellId,
            resolvedName,
            learned is not null,
            learned?.Level,
            availableLevels,
            branchReports,
            structurallyEligible,
            eligible,
            ineligibilityReasons);
    }

    private static CharacterAutomaticProcCandidateBranchReport BuildCandidateBranchReport(
        CandidateBranchSeed seed,
        PlayerSpellSnapshotEntry? learned,
        IReadOnlyDictionary<ushort, IReadOnlyList<MagicType>> magicBySpellId,
        bool compatibilityFenceEnabled,
        ICollection<CharacterAutomaticProcMissingMetadataReport> missingMetadata)
    {
        var reasons = new List<string>(seed.PreconditionFailures);
        if (seed.RequiresLearned && learned is null)
        {
            reasons.Add("notLearned");
        }

        ushort? metadataLevel = learned?.Level;
        if (!metadataLevel.HasValue && seed.TemporaryWhenUnlearned)
        {
            metadataLevel = 0;
        }

        var metadataStatus = "availableUnlearned";
        MagicMetadataResolution? resolution = null;
        if (metadataLevel is { } requiredLevel)
        {
            resolution = ResolveMagicMetadata(magicBySpellId, seed.SpellId, requiredLevel);
            metadataStatus = resolution.Status;
            if (resolution.Status != "present")
            {
                reasons.Add(MetadataFailureReason(resolution.Status));
                AddMissingMetadata(missingMetadata, seed.SpellId, requiredLevel, resolution);
            }
        }
        else if (!magicBySpellId.ContainsKey(seed.SpellId))
        {
            metadataStatus = "missingSpell";
        }

        uint? procChance = seed.FixedProcChancePercent;
        if (seed.Source == "weaponTable" && resolution?.Row is { } row)
        {
            if (!TryReadUInt32(row.Fields, MagicRateField, out var rate) || rate is 0 or > 100)
            {
                reasons.Add("invalidProcRateMetadata");
                missingMetadata.Add(new CharacterAutomaticProcMissingMetadataReport(
                    seed.SpellId,
                    metadataLevel,
                    "rate",
                    "The learned-level magic row must contain a proc rate from 1 through 100 in field 12."));
            }
            else
            {
                procChance = rate;
            }
        }

        var structuralReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
        var structurallyEligible = structuralReasons.Length == 0;
        if (compatibilityFenceEnabled)
        {
            reasons.Add("compatibilityFenceEnabled");
        }

        var normalizedReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reason => reason, StringComparer.Ordinal)
            .ToArray();
        return new CharacterAutomaticProcCandidateBranchReport(
            seed.Phase,
            seed.Source,
            seed.Hand,
            seed.CurrentWeaponType,
            seed.ConfiguredWeaponTypes,
            seed.RequiresLearned,
            seed.TemporaryWhenUnlearned,
            metadataLevel,
            metadataStatus,
            procChance,
            seed.OuterChancePercent,
            structurallyEligible,
            structurallyEligible && !compatibilityFenceEnabled,
            normalizedReasons);
    }

    private static CharacterAutomaticProcLearnedSpellReport BuildLearnedSpellReport(
        PlayerSpellSnapshotEntry spell,
        int snapshotIndex,
        IReadOnlyDictionary<ushort, IReadOnlyList<MagicType>> magicBySpellId,
        IReadOnlyDictionary<ushort, CharacterAutomaticProcCandidateSkillReport> candidateById)
    {
        var automaticProcSkill = KnownAutomaticProcSpellIds.Contains(spell.Id);
        var resolution = ResolveMagicMetadata(magicBySpellId, spell.Id, spell.Level);
        candidateById.TryGetValue(spell.Id, out var candidate);
        return new CharacterAutomaticProcLearnedSpellReport(
            snapshotIndex,
            spell.Id,
            spell.Level,
            spell.Experience,
            resolution.Row?.Name ?? ResolveCandidateName(magicBySpellId.GetValueOrDefault(spell.Id) ?? [], spell.Level),
            automaticProcSkill,
            candidate is not null,
            candidate?.Eligible == true,
            resolution.Status,
            candidate?.Branches.Select(branch => branch.Source)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(source => source, StringComparer.Ordinal)
                .ToArray() ?? []);
    }

}
