using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

internal static partial class CharacterAutomaticProcEligibilityAuditor
{
    private static MagicMetadataResolution ResolveMagicMetadata(
        IReadOnlyDictionary<ushort, IReadOnlyList<MagicType>> magicBySpellId,
        ushort spellId,
        ushort level)
    {
        if (!magicBySpellId.TryGetValue(spellId, out var rows) || rows.Count == 0)
        {
            return new MagicMetadataResolution("missingSpell", null, []);
        }

        var row = rows.FirstOrDefault(candidate =>
            TryReadByte(candidate.Fields, MagicLevelField, out var candidateLevel) &&
            candidateLevel == level);
        if (row is null)
        {
            return new MagicMetadataResolution("missingLevel", null, []);
        }

        var invalidFields = GetInvalidRuntimeFields(row);
        return invalidFields.Count == 0
            ? new MagicMetadataResolution("present", row, [])
            : new MagicMetadataResolution("invalidRuntimeMetadata", row, invalidFields);
    }

    private static IReadOnlyList<string> GetInvalidRuntimeFields(MagicType row)
    {
        if (row.Fields.Count < MagicMinimumRuntimeFieldCount)
        {
            return ["rowFields"];
        }

        var invalid = new List<string>();
        ValidateUInt16(row, MagicTypeIdField, "typeId", invalid);
        ValidateByte(row, MagicSortField, "sort", invalid);
        ValidateByte(row, MagicAttackInFlyField, "attackInFly", invalid);
        ValidateByte(row, MagicLevelField, "level", invalid);
        ValidateUInt16(row, MagicUseManaField, "useMana", invalid);
        ValidateUInt32(row, MagicDamageField, "damage", invalid);
        ValidateUInt32(row, MagicRateField, "gDamageOrRate", invalid);
        ValidateUInt32(row, MagicDurationField, "duration", invalid);
        ValidateByte(row, MagicMaxTargetsField, "maxTargets", invalid);
        ValidateByte(row, MagicRangeField, "range", invalid);
        ValidateUInt32(row, MagicExperienceField, "experience", invalid);
        ValidateUInt16(row, MagicNeedLevelField, "needLevel", invalid);
        ValidateUInt32(row, MagicWeaponTypeField, "weaponType", invalid);
        ValidateByte(row, MagicUseStaminaField, "useStamina", invalid);
        ValidateUInt16(row, MagicCoolDownField, "coolDown", invalid);
        ValidateInt32(row, MagicDamageOnHumanField, "damageOnHuman", invalid);
        ValidateInt32(row, MagicDamage2Field, "damage2", invalid);
        ValidateInt32(row, MagicDamage3Field, "damage3", invalid);
        ValidateInt32(row, MagicDamageOnMonsterField, "damageOnMonster", invalid);
        ValidateInt32(row, MagicColdTimeField, "coldTime", invalid);
        return invalid;
    }

    private static void AddMissingMetadata(
        ICollection<CharacterAutomaticProcMissingMetadataReport> target,
        ushort spellId,
        ushort level,
        MagicMetadataResolution resolution)
    {
        if (resolution.Status == "missingSpell")
        {
            target.Add(new CharacterAutomaticProcMissingMetadataReport(
                spellId,
                level,
                "magicRow",
                "No magic metadata rows exist for this automatic-proc spell."));
        }
        else if (resolution.Status == "missingLevel")
        {
            target.Add(new CharacterAutomaticProcMissingMetadataReport(
                spellId,
                level,
                "levelRow",
                $"No magic metadata row exists for learned level {level}."));
        }
        else if (resolution.Status == "invalidRuntimeMetadata")
        {
            foreach (var field in resolution.InvalidFields)
            {
                target.Add(new CharacterAutomaticProcMissingMetadataReport(
                    spellId,
                    level,
                    field,
                    "The learned-level magic row does not contain valid runtime metadata for this field."));
            }
        }
    }

    private static string MetadataFailureReason(string status)
    {
        return status switch
        {
            "missingSpell" => "missingSpellMetadata",
            "missingLevel" => "missingLearnedLevelMetadata",
            "invalidRuntimeMetadata" => "invalidRuntimeMetadata",
            _ => "missingMetadata",
        };
    }

    private static string? ResolveCandidateName(IReadOnlyList<MagicType> rows, ushort? level)
    {
        if (level is { } requestedLevel)
        {
            var exact = rows.FirstOrDefault(row =>
                TryReadByte(row.Fields, MagicLevelField, out var rowLevel) &&
                rowLevel == requestedLevel);
            if (exact is not null)
            {
                return exact.Name;
            }
        }

        return rows.FirstOrDefault()?.Name;
    }

    private static bool WeaponTypeMatches(uint configuredWeaponTypes, ushort weaponType)
    {
        if (configuredWeaponTypes is 0 or 50_000 || weaponType == 0)
        {
            return false;
        }

        var primary = (ushort)(configuredWeaponTypes % 1_000);
        var secondary = (ushort)(configuredWeaponTypes / 1_000 % 1_000);
        return primary == weaponType || secondary == weaponType;
    }

    private static bool IsTrojanClass(long classCode)
    {
        return classCode is >= 1_000 and <= 1_057;
    }

    private static string ResolveUsersRoot(string usersRoot)
    {
        var fullPath = Path.GetFullPath(usersRoot);
        if (string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(fullPath)), "DBServer", StringComparison.OrdinalIgnoreCase))
        {
            var usersChild = Path.Combine(fullPath, "Users");
            if (Directory.Exists(usersChild))
            {
                return usersChild;
            }
        }

        return fullPath;
    }

    private static HashSet<ushort> CreateKnownAutomaticProcSpellIds()
    {
        var values = new HashSet<ushort>(WeaponTableProcSpellIds)
        {
            PoisonWeaponEffectSpellId,
            ManaWeaponEffectSpellId,
        };
        foreach (var definition in ReplacementDirectBranches
                     .Concat(ReplacementPostTableBranches)
                     .Concat(PostPhysicalBranches))
        {
            values.Add(definition.SpellId);
        }

        return values;
    }

    private static int PhaseOrder(string phase)
    {
        return phase switch
        {
            "replacementSelection" => 0,
            "checkMelee" => 1,
            "postPhysical" => 2,
            _ => 3,
        };
    }

    private static void ValidateByte(MagicType row, int index, string name, ICollection<string> invalid)
    {
        if (!TryReadByte(row.Fields, index, out _))
        {
            invalid.Add(name);
        }
    }

    private static void ValidateUInt16(MagicType row, int index, string name, ICollection<string> invalid)
    {
        if (!TryReadUInt16(row.Fields, index, out _))
        {
            invalid.Add(name);
        }
    }

    private static void ValidateUInt32(MagicType row, int index, string name, ICollection<string> invalid)
    {
        if (!TryReadUInt32(row.Fields, index, out _))
        {
            invalid.Add(name);
        }
    }

    private static void ValidateInt32(MagicType row, int index, string name, ICollection<string> invalid)
    {
        if (!TryReadInt32(row.Fields, index, out _))
        {
            invalid.Add(name);
        }
    }

    private static bool TryReadByte(IReadOnlyList<string> fields, int index, out byte value)
    {
        value = 0;
        return index >= 0 &&
            index < fields.Count &&
            byte.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt16(IReadOnlyList<string> fields, int index, out ushort value)
    {
        value = 0;
        return index >= 0 &&
            index < fields.Count &&
            ushort.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadUInt32(IReadOnlyList<string> fields, int index, out uint value)
    {
        value = 0;
        return index >= 0 &&
            index < fields.Count &&
            uint.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadInt32(IReadOnlyList<string> fields, int index, out int value)
    {
        value = 0;
        return index >= 0 &&
            index < fields.Count &&
            int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

}
