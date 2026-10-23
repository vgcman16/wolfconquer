namespace WolfConquer.Core.World;

internal partial class LegacyNativeHeroRewardsRuntime
{
    public bool ContainGoal(uint id)
    {
        if (!TryReadGoalRows(out var rows)) throw new InvalidDataException("Hero goal membership is unavailable.");
        return rows.Any(row => row.Key == id);
    }

    public bool FinishStage(uint id)
    {
        if (!TryReadGoalRows(out var rows)) throw new InvalidDataException("Hero goal stage membership is unavailable.");
        int count = rows.Count(row => row.Key / 100 == id);
        return LegacyNativePoolSaveRecords.TableHeroRewards.TryGetValue(id, out var stage) &&
            stage is not null && (ulong)count == stage.CountGoals;
    }

    public bool ComplateFullStage(uint id)
    {
        if (!TryReadGoalRows(out var rows)) throw new InvalidDataException("Hero goal claim membership is unavailable.");
        int count = rows.Count(row => (row.Key / 100 == id || row.Key == id) && row.Claim > 0);
        return LegacyNativePoolSaveRecords.TableHeroRewards.TryGetValue(id, out var stage) &&
            stage is not null && (ulong)count == (ulong)stage.CountGoals + 1;
    }
}
