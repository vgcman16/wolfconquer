using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryReadLegacyInnerPowerState(
        InnerPowerRankEntry row,
        out LegacyInnerPowerState state,
        out string deferReason)
    {
        const int innerPowerFixedFieldCount = 4;
        const int stageFixedFieldCount = 3;
        const int neiGongFieldCount = 5;

        state = default!;
        deferReason = string.Empty;

        var fields = row.Fields;
        if (fields.Count < innerPowerFixedFieldCount + 1 ||
            fields[^1].Length != 0 ||
            !TryReadLegacyInnerPowerInt(fields, 1, out var rowCharacterId) ||
            rowCharacterId != row.CharacterId ||
            rowCharacterId < 0 ||
            !TryReadLegacyInnerPowerInt(fields, 2, out var potency) ||
            !TryReadLegacyInnerPowerInt(fields, 3, out var stageCount) ||
            stageCount < 0)
        {
            deferReason = $"parseable InnerPower.txt row for character {row.CharacterId} is not available";
            return false;
        }

        var parsedStages = new List<LegacyInnerPowerStageState>();
        var offset = innerPowerFixedFieldCount;
        for (var stageIndex = 0; stageIndex < stageCount; stageIndex++)
        {
            if (fields.Count <= offset + stageFixedFieldCount ||
                !TryReadLegacyInnerPowerInt(fields, offset, out var stageId) ||
                !TryReadLegacyInnerPowerBoolByte(fields, offset + 1, out var stageUnlocked) ||
                !TryReadLegacyInnerPowerInt(fields, offset + 2, out var gongCount) ||
                stageId <= 0 ||
                gongCount < 0)
            {
                deferReason = $"parseable InnerPower.txt stage {stageIndex + 1} for character {row.CharacterId} is not available";
                return false;
            }

            offset += stageFixedFieldCount;
            var gongs = new List<LegacyInnerPowerGongState>();
            for (var gongIndex = 0; gongIndex < gongCount; gongIndex++)
            {
                if (fields.Count <= offset + neiGongFieldCount ||
                    !TryReadLegacyInnerPowerByte(fields, offset, out var gongId) ||
                    !TryReadLegacyInnerPowerByte(fields, offset + 1, out var score) ||
                    !TryReadLegacyInnerPowerBoolByte(fields, offset + 2, out var gongUnlocked) ||
                    !TryReadLegacyInnerPowerByte(fields, offset + 3, out var level) ||
                    !TryReadLegacyInnerPowerBoolByte(fields, offset + 4, out var complete))
                {
                    deferReason = $"parseable InnerPower.txt stage {stageId} gong {gongIndex + 1} for character {row.CharacterId} is not available";
                    return false;
                }

                gongs.Add(new LegacyInnerPowerGongState(gongId, score, gongUnlocked, level, complete));
                offset += neiGongFieldCount;
            }

            parsedStages.Add(new LegacyInnerPowerStageState(stageId, stageUnlocked, gongs));
        }

        if (offset != fields.Count - 1)
        {
            deferReason = $"parseable InnerPower.txt row for character {row.CharacterId} is not available";
            return false;
        }

        state = new LegacyInnerPowerState(checked((uint)rowCharacterId), potency, parsedStages);
        return true;
    }

    private static bool TryGetLegacyInnerPowerStageState(
        LegacyInnerPowerState state,
        int stageOrdinal,
        WorldDataCatalog worldData,
        out LegacyInnerPowerStageState stage,
        out string deferReason)
    {
        deferReason = string.Empty;
        foreach (var candidate in state.Stages)
        {
            if (candidate.Id == stageOrdinal)
            {
                stage = candidate;
                return true;
            }
        }

        if (!worldData.NeiGongSecretsById.ContainsKey(stageOrdinal))
        {
            stage = default!;
            deferReason = $"NeiGongInfo.ini stage {stageOrdinal} is not loaded";
            return false;
        }

        stage = new LegacyInnerPowerStageState(stageOrdinal, Unlocked: false, []);
        return true;
    }

    private static bool TryValidateLegacyInnerPowerGongMetadata(
        NeiGongType type,
        out string deferReason)
    {
        deferReason = string.Empty;

        if (type.MaxLevel <= 0 ||
            type.MaxLevel > byte.MaxValue ||
            type.CultureValues.Count == 0)
        {
            deferReason = $"parseable NeiGongInfo.ini type metadata for gong {type.TypeId} is not available";
            return false;
        }

        foreach (var value in type.CultureValues)
        {
            if (value < 0)
            {
                deferReason = $"parseable NeiGongInfo.ini type metadata for gong {type.TypeId} is not available";
                return false;
            }
        }

        return true;
    }

    private static bool TryFindLegacyInnerPowerGongState(
        LegacyInnerPowerState state,
        byte gongId,
        out int stageIndex,
        out int gongIndex,
        out LegacyInnerPowerStageState stage,
        out LegacyInnerPowerGongState gong)
    {
        for (stageIndex = 0; stageIndex < state.Stages.Count; stageIndex++)
        {
            stage = state.Stages[stageIndex];
            for (gongIndex = 0; gongIndex < stage.Gongs.Count; gongIndex++)
            {
                gong = stage.Gongs[gongIndex];
                if (gong.Id == gongId)
                {
                    return true;
                }
            }
        }

        stageIndex = -1;
        gongIndex = -1;
        stage = default!;
        gong = default!;
        return false;
    }

    private static byte CalculateLegacyInnerPowerGongScore(int maxLevel, byte level)
    {
        return checked((byte)Math.Ceiling(100d / maxLevel * level));
    }

    private static InnerPowerRankEntry CreateLegacyInnerPowerRankEntry(
        InnerPowerRankEntry row,
        LegacyInnerPowerState state)
    {
        var fields = new List<string>
        {
            row.CharacterName,
            state.CharacterId.ToString(CultureInfo.InvariantCulture),
            state.Potency.ToString(CultureInfo.InvariantCulture),
            state.Stages.Count.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var stage in state.Stages)
        {
            fields.Add(stage.Id.ToString(CultureInfo.InvariantCulture));
            fields.Add(stage.Unlocked ? "1" : "0");
            fields.Add(stage.Gongs.Count.ToString(CultureInfo.InvariantCulture));

            foreach (var gong in stage.Gongs)
            {
                fields.Add(gong.Id.ToString(CultureInfo.InvariantCulture));
                fields.Add(gong.Score.ToString(CultureInfo.InvariantCulture));
                fields.Add(gong.Unlocked ? "1" : "0");
                fields.Add(gong.Level.ToString(CultureInfo.InvariantCulture));
                fields.Add(gong.Complete ? "1" : "0");
            }
        }

        fields.Add(string.Empty);
        var fieldArray = fields.ToArray();
        return row with
        {
            RankingFields = fieldArray.Skip(2).Take(fieldArray.Length - 3).ToArray(),
            RawLine = string.Join('/', fieldArray),
            Fields = fieldArray,
        };
    }

    private static bool TryReplaceLegacyInnerPowerRow(
        IReadOnlyList<InnerPowerRankEntry> rows,
        InnerPowerRankEntry updatedRow,
        out string deferReason)
    {
        if (rows is InnerPowerRankEntry[] rowArray)
        {
            rowArray[0] = updatedRow;
            deferReason = string.Empty;
            return true;
        }

        if (rows is IList<InnerPowerRankEntry> mutableRows && !mutableRows.IsReadOnly)
        {
            mutableRows[0] = updatedRow;
            deferReason = string.Empty;
            return true;
        }

        deferReason = $"InnerPower.txt row for character {updatedRow.CharacterId} cannot be updated in memory";
        return false;
    }

    private static uint CalculateLegacyInnerPowerStageScore(LegacyInnerPowerStageState stage)
    {
        uint score = 0;
        foreach (var gong in stage.Gongs)
        {
            score += gong.Score;
        }

        return score;
    }

    private sealed record LegacyInnerPowerGongUpdateResult(
        uint CharacterId,
        int Potency,
        int UpgradeCount,
        IReadOnlyList<int> PotencyUpdates,
        LegacyInnerPowerGuiPacket Gui,
        LegacyInnerPowerStageInfoPacket UpdateStage,
        LegacyInnerPowerStageInfoPacket UpdateScore);

    private sealed record LegacyInnerPowerState(
        uint CharacterId,
        int Potency,
        IReadOnlyList<LegacyInnerPowerStageState> Stages);

    private sealed record LegacyInnerPowerStageState(
        int Id,
        bool Unlocked,
        IReadOnlyList<LegacyInnerPowerGongState> Gongs);

    private sealed record LegacyInnerPowerGongState(
        byte Id,
        byte Score,
        bool Unlocked,
        byte Level,
        bool Complete);

    private static bool TryReadLegacyInnerPowerInt(IReadOnlyList<string> fields, int index, out int value)
    {
        value = 0;
        return fields.Count > index &&
            int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadLegacyInnerPowerByte(IReadOnlyList<string> fields, int index, out byte value)
    {
        value = 0;
        return fields.Count > index &&
            byte.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadLegacyInnerPowerBoolByte(IReadOnlyList<string> fields, int index, out bool value)
    {
        value = false;
        if (!TryReadLegacyInnerPowerInt(fields, index, out var raw) || raw is not (0 or 1))
        {
            return false;
        }

        value = raw == 1;
        return true;
    }
}
