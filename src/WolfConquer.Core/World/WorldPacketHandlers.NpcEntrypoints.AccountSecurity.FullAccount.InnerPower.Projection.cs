using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCreateLegacyFullAccountInnerPowerProjection(
        CharacterSnapshot character,
        InnerPowerRankEntry row,
        WorldDataCatalog worldData,
        out LegacyFullAccountInnerPowerProjection projection,
        out string reason)
    {
        projection = default!;
        reason = string.Empty;
        if (!TryReadLegacyInnerPowerState(row, out var persistedState, out reason))
        {
            return false;
        }

        if (character.Uid < 0 ||
            character.Uid > uint.MaxValue ||
            persistedState.CharacterId != character.Uid)
        {
            reason = "inner-power-character-mismatch";
            return false;
        }

        if (worldData.NeiGongHeader.SecretCount < 0 ||
            worldData.NeiGongHeader.SecretCount != worldData.NeiGongSecrets.Count ||
            persistedState.Stages.Count > worldData.NeiGongSecrets.Count)
        {
            reason = "inner-power-stage-metadata-mismatch";
            return false;
        }

        var stages = new List<LegacyInnerPowerStageState>(worldData.NeiGongSecrets.Count);
        for (var stageIndex = 0; stageIndex < worldData.NeiGongSecrets.Count; stageIndex++)
        {
            var secret = worldData.NeiGongSecrets[stageIndex];
            if (secret.Id is <= 0 or > ushort.MaxValue || secret.NeiGongCount < 0)
            {
                reason = $"inner-power-stage-{stageIndex + 1}-metadata-out-of-range";
                return false;
            }

            var gongs = new LegacyInnerPowerGongState[secret.NeiGongCount];
            for (var slot = 1; slot <= secret.NeiGongCount; slot++)
            {
                if (!worldData.NeiGongTypeBySecretIdAndSlot.TryGetValue((secret.Id, slot), out var type) ||
                    type.SecretId != secret.Id ||
                    type.Slot != slot ||
                    type.TypeId is < byte.MinValue or > byte.MaxValue)
                {
                    reason = $"inner-power-stage-{secret.Id}-slot-{slot}-metadata-missing";
                    return false;
                }

                gongs[slot - 1] = new LegacyInnerPowerGongState(
                    checked((byte)type.TypeId),
                    Score: 0,
                    Unlocked: false,
                    Level: 0,
                    Complete: false);
            }

            var stage = new LegacyInnerPowerStageState(secret.Id, Unlocked: false, gongs);
            if (stageIndex < persistedState.Stages.Count)
            {
                var persistedStage = persistedState.Stages[stageIndex];
                if (persistedStage.Id is <= 0 or > ushort.MaxValue ||
                    persistedStage.Gongs.Count > gongs.Length)
                {
                    reason = $"inner-power-persisted-stage-{stageIndex + 1}-outside-source-shape";
                    return false;
                }

                for (var gongIndex = 0; gongIndex < persistedStage.Gongs.Count; gongIndex++)
                {
                    gongs[gongIndex] = persistedStage.Gongs[gongIndex];
                }

                stage = persistedStage with { Gongs = gongs };
            }

            stages.Add(stage);
        }

        int gongCount;
        long totalCost;
        try
        {
            gongCount = checked(stages.Sum(stage => stage.Gongs.Count));
            totalCost = checked((long)gongCount * LegacyFullAccountInnerPowerCostPerGong);
        }
        catch (OverflowException)
        {
            reason = "inner-power-total-cost-overflow";
            return false;
        }

        if (character.ConquerPoints < totalCost)
        {
            reason =
                $"aggregate-source-cost-underflow:required={totalCost.ToString(CultureInfo.InvariantCulture)}";
            return false;
        }

        var conquerPoints = character.ConquerPoints;
        var conquerPointUpdates = new List<long>(gongCount);
        var updatedStages = new LegacyInnerPowerStageState[stages.Count];
        for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
        {
            var stage = stages[stageIndex];
            var stageUnlocked = stage.Unlocked;
            var updatedGongs = new LegacyInnerPowerGongState[stage.Gongs.Count];
            for (var gongIndex = 0; gongIndex < stage.Gongs.Count; gongIndex++)
            {
                var gong = stage.Gongs[gongIndex];
                conquerPoints = checked(conquerPoints - LegacyFullAccountInnerPowerCostPerGong);
                conquerPointUpdates.Add(conquerPoints);
                stageUnlocked = true;

                var level = gong.Level;
                if (worldData.NeiGongTypesByTypeId.TryGetValue(gong.Id, out var type))
                {
                    if (type.MaxLevel is < byte.MinValue or > byte.MaxValue)
                    {
                        reason = $"inner-power-gong-{gong.Id}-max-level-out-of-range";
                        return false;
                    }

                    level = checked((byte)type.MaxLevel);
                }

                updatedGongs[gongIndex] = gong with
                {
                    Score = 100,
                    Unlocked = true,
                    Level = level,
                };
            }

            updatedStages[stageIndex] = stage with
            {
                Unlocked = stageUnlocked,
                Gongs = updatedGongs,
            };
        }

        var serializedStages = updatedStages.Where(stage => stage.Unlocked).ToArray();
        var updatedState = persistedState with { Stages = serializedStages };
        var updatedRow = CreateLegacyInnerPowerRankEntry(row, updatedState);
        var updatedCharacter = WithCharacterAttribute(
            character with { ConquerPoints = conquerPoints },
            "ConquerPoints",
            conquerPoints.ToString(CultureInfo.InvariantCulture));
        projection = new LegacyFullAccountInnerPowerProjection(
            updatedCharacter,
            updatedRow,
            conquerPointUpdates,
            updatedStages.Length,
            gongCount,
            totalCost);
        return true;
    }

    private static bool CanReplaceLegacyInnerPowerRow(
        IReadOnlyList<InnerPowerRankEntry> rows,
        int characterId,
        out string reason)
    {
        if (rows is InnerPowerRankEntry[] ||
            rows is IList<InnerPowerRankEntry> { IsReadOnly: false })
        {
            reason = string.Empty;
            return true;
        }

        reason = $"InnerPower.txt row for character {characterId} cannot be updated in memory";
        return false;
    }

    private static void ReplaceLegacyInnerPowerRow(
        IReadOnlyList<InnerPowerRankEntry> rows,
        InnerPowerRankEntry updatedRow)
    {
        if (rows is InnerPowerRankEntry[] rowArray)
        {
            rowArray[0] = updatedRow;
            return;
        }

        ((IList<InnerPowerRankEntry>)rows)[0] = updatedRow;
    }

    private sealed record LegacyFullAccountInnerPowerProjection(
        CharacterSnapshot UpdatedCharacter,
        InnerPowerRankEntry UpdatedRow,
        IReadOnlyList<long> ConquerPointUpdates,
        int StageCount,
        int GongCount,
        long TotalCost);
}
