using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyInnerPowerHitpointsBreakdown(
    uint Hitpoints,
    IReadOnlyList<string> MissingInputs);

public static class LegacyInnerPowerHitpointsCalculator
{
    private const int MaxHitpointsAttributeType = 1;
    private const int InnerPowerFixedFieldCount = 4;
    private const int StageFixedFieldCount = 3;
    private const int NeiGongFieldCount = 5;

    public static LegacyInnerPowerHitpointsBreakdown Calculate(
        CharacterSnapshot snapshot,
        WorldDataCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Calculate(
            CheckedInt32(snapshot.Uid, nameof(snapshot.Uid)),
            catalog?.InnerPowerRanksByCharacterId,
            catalog?.NeiGongSecretsById,
            catalog?.NeiGongTypeBySecretIdAndSlot);
    }

    public static LegacyInnerPowerHitpointsBreakdown Calculate(
        int characterId,
        IReadOnlyDictionary<int, IReadOnlyList<InnerPowerRankEntry>>? innerPowerRowsByCharacterId,
        IReadOnlyDictionary<int, NeiGongSecret>? neiGongSecretsById,
        IReadOnlyDictionary<(int SecretId, int Slot), NeiGongType>? neiGongTypeBySecretIdAndSlot)
    {
        var missingInputs = new List<string>();
        if (innerPowerRowsByCharacterId is null)
        {
            missingInputs.Add("InnerPower.txt rows");
        }

        if (neiGongSecretsById is null)
        {
            missingInputs.Add("NeiGongInfo.ini secret definitions");
        }

        if (neiGongTypeBySecretIdAndSlot is null)
        {
            missingInputs.Add("NeiGongInfo.ini type definitions");
        }

        if (missingInputs.Count > 0)
        {
            return new LegacyInnerPowerHitpointsBreakdown(0, missingInputs);
        }

        if (!innerPowerRowsByCharacterId!.TryGetValue(characterId, out var rows) ||
            rows.Count == 0)
        {
            return new LegacyInnerPowerHitpointsBreakdown(
                0,
                [$"InnerPower.txt row for character {characterId}"]);
        }

        var row = rows[0];
        if (!TryParseInnerPowerState(row, missingInputs, out var stages))
        {
            return new LegacyInnerPowerHitpointsBreakdown(0, missingInputs);
        }

        var hitpoints = 0u;
        foreach (var stage in stages)
        {
            if (!stage.Unlocked)
            {
                break;
            }

            if (!neiGongSecretsById!.TryGetValue(stage.Id, out var secret))
            {
                missingInputs.Add($"NeiGongInfo.ini secret {stage.Id}");
                continue;
            }

            for (var index = 0; index < stage.Gongs.Count; index++)
            {
                var gong = stage.Gongs[index];
                if (!gong.Unlocked || gong.Level == 0)
                {
                    break;
                }

                var slot = index + 1;
                if (!neiGongTypeBySecretIdAndSlot!.TryGetValue((stage.Id, slot), out var type))
                {
                    missingInputs.Add($"NeiGongInfo.ini type {stage.Id}-{slot}");
                    continue;
                }

                hitpoints += CalculateTypeMaxHitpoints(type, gong.Level);
            }

            if (stage.Gongs.Count > 0 && stage.Gongs.All(gong => gong.Complete))
            {
                hitpoints += CalculateSecretMaxHitpoints(secret);
            }
        }

        return new LegacyInnerPowerHitpointsBreakdown(hitpoints, missingInputs);
    }

    private static uint CalculateTypeMaxHitpoints(NeiGongType type, int level)
    {
        if (type.MaxLevel <= 0)
        {
            return 0;
        }

        var boundedLevel = Math.Min(level, type.MaxLevel);
        var total = 0u;
        for (var index = 0; index < type.AttributeTypes.Count && index < type.AttributeValues.Count; index++)
        {
            if (type.AttributeTypes[index] == MaxHitpointsAttributeType && type.AttributeValues[index] > 0)
            {
                total += checked((uint)(type.AttributeValues[index] / type.MaxLevel * boundedLevel));
            }
        }

        return total;
    }

    private static uint CalculateSecretMaxHitpoints(NeiGongSecret secret)
    {
        var total = 0u;
        for (var index = 0; index < secret.AttributeTypes.Count && index < secret.AttributeValues.Count; index++)
        {
            if (secret.AttributeTypes[index] == MaxHitpointsAttributeType && secret.AttributeValues[index] > 0)
            {
                total += checked((uint)secret.AttributeValues[index]);
            }
        }

        return total;
    }

    private static bool TryParseInnerPowerState(
        InnerPowerRankEntry row,
        List<string> missingInputs,
        out IReadOnlyList<InnerPowerStageState> stages)
    {
        stages = [];
        var fields = row.Fields;
        if (fields.Count < InnerPowerFixedFieldCount + 1 ||
            fields[^1].Length != 0 ||
            !TryReadInt(fields, 1, out var characterId) ||
            characterId != row.CharacterId ||
            !TryReadInt(fields, 3, out var stageCount) ||
            stageCount < 0)
        {
            missingInputs.Add($"parseable InnerPower.txt row for character {row.CharacterId}");
            return false;
        }

        var parsedStages = new List<InnerPowerStageState>(stageCount);
        var offset = InnerPowerFixedFieldCount;
        for (var stageIndex = 0; stageIndex < stageCount; stageIndex++)
        {
            if (fields.Count <= offset + StageFixedFieldCount ||
                !TryReadInt(fields, offset, out var stageId) ||
                !TryReadBoolByte(fields, offset + 1, out var stageUnlocked) ||
                !TryReadInt(fields, offset + 2, out var gongCount) ||
                stageId <= 0 ||
                gongCount < 0)
            {
                missingInputs.Add($"parseable InnerPower.txt stage {stageIndex + 1} for character {row.CharacterId}");
                return false;
            }

            offset += StageFixedFieldCount;
            var gongs = new List<InnerPowerNeiGongState>(gongCount);
            for (var gongIndex = 0; gongIndex < gongCount; gongIndex++)
            {
                if (fields.Count <= offset + NeiGongFieldCount ||
                    !TryReadInt(fields, offset, out var gongId) ||
                    !TryReadInt(fields, offset + 1, out _) ||
                    !TryReadBoolByte(fields, offset + 2, out var gongUnlocked) ||
                    !TryReadInt(fields, offset + 3, out var level) ||
                    !TryReadBoolByte(fields, offset + 4, out var complete) ||
                    gongId < 0 ||
                    level < 0)
                {
                    missingInputs.Add(
                        $"parseable InnerPower.txt stage {stageId} gong {gongIndex + 1} for character {row.CharacterId}");
                    return false;
                }

                gongs.Add(new InnerPowerNeiGongState(gongId, gongUnlocked, level, complete));
                offset += NeiGongFieldCount;
            }

            parsedStages.Add(new InnerPowerStageState(stageId, stageUnlocked, gongs));
        }

        if (offset != fields.Count - 1)
        {
            missingInputs.Add($"parseable InnerPower.txt row for character {row.CharacterId}");
            return false;
        }

        stages = parsedStages;
        return true;
    }

    private static bool TryReadInt(IReadOnlyList<string> fields, int index, out int value)
    {
        value = 0;
        return fields.Count > index &&
            int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadBoolByte(IReadOnlyList<string> fields, int index, out bool value)
    {
        value = false;
        if (!TryReadInt(fields, index, out var raw) || raw is not (0 or 1))
        {
            return false;
        }

        value = raw == 1;
        return true;
    }

    private static int CheckedInt32(long value, string name)
    {
        return value is >= int.MinValue and <= int.MaxValue
            ? (int)value
            : throw new ArgumentOutOfRangeException(name, value, "Value must fit in a signed 32-bit integer.");
    }

    private sealed record InnerPowerStageState(
        int Id,
        bool Unlocked,
        IReadOnlyList<InnerPowerNeiGongState> Gongs);

    private sealed record InnerPowerNeiGongState(
        int Id,
        bool Unlocked,
        int Level,
        bool Complete);
}
