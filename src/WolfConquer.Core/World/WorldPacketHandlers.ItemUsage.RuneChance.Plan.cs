using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneChanceProgressRow(uint Family, byte Level, uint Progress);
internal sealed record LegacyRuneChanceAnalysis(PlayerItemSnapshotItem Target, int RatePercent,
    bool Gambled, IReadOnlySet<(uint Family, byte Level)> UsedProgress);
internal sealed record LegacyRuneChancePlan(PlayerItemSnapshotItem Target, int RatePercent,
    bool Gambled, bool Passed, IReadOnlySet<(uint Family, byte Level)> UsedProgress);

public static partial class WorldPacketHandlers
{
    // Deterministic projection only. A supplied roll is not admission evidence,
    // a live RNG provider, an accepting persistence callback or a permission.
    internal static LegacyRuneChancePlan PlanRecordedRuneChance(PlayerItemSnapshot before, uint uid,
        IReadOnlyDictionary<(uint Family, byte Level), LegacyRuneChanceProgressRow> progress, byte? roll) =>
        ResolveRecordedRuneChance(AnalyzeRecordedRuneChance(before, uid, progress), roll);

    internal static LegacyRuneChanceAnalysis AnalyzeRecordedRuneChance(PlayerItemSnapshot before, uint uid,
        IReadOnlyDictionary<(uint Family, byte Level), LegacyRuneChanceProgressRow> progress)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(progress);
        var targets = before.RuneItems.Where(row => row.Uid == uid).ToArray();
        if (uid == 0 || targets.Length != 1) throw InvalidSpellMutation();
        var target = targets[0];
        uint family = target.ItemId / 10_000, level = target.ItemId % 100;
        if (family is < 401 or > 403 || level == 0 || level > GetLegacyMaxRuneLevel(target.ItemId) || target.StackSize > 1)
            throw InvalidSpellMutation();
        var used = new HashSet<(uint Family, byte Level)>();
        if (level == GetLegacyMaxRuneLevel(target.ItemId) || target.RuneExperience == 0)
        {
            return new(target, 0, false, used);
        }
        uint current = Require(level), next = Require(level + 1);
        if (next <= current) throw InvalidSpellMutation();
        uint gap = checked(next - current);
        if (target.RuneExperience > gap) throw InvalidSpellMutation();
        int rate = checked((int)((ulong)target.RuneExperience * 100UL / gap));
        return new(target, rate, true, used);

        uint Require(uint requiredLevel)
        {
            if (requiredLevel is 0 or > 99 ||
                !progress.TryGetValue((family, checked((byte)requiredLevel)), out var row) ||
                row is null || row.Family != family || row.Level != requiredLevel) throw InvalidSpellMutation();
            used.Add((family, checked((byte)requiredLevel)));
            return row.Progress;
        }
    }

    internal static LegacyRuneChancePlan ResolveRecordedRuneChance(LegacyRuneChanceAnalysis analysis, byte? roll)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var target = analysis.Target;
        if (target is null || analysis.RatePercent is < 0 or > 100) throw InvalidSpellMutation();
        if (!analysis.Gambled)
        {
            if (roll is not null || analysis.RatePercent != 0) throw InvalidSpellMutation();
            return new(target, 0, false, false, analysis.UsedProgress);
        }
        int rate = analysis.RatePercent;
        bool passed;
        if (rate is 0 or 100)
        {
            if (roll is not null) throw InvalidSpellMutation();
            passed = rate == 100;
        }
        else
        {
            if (roll is null or >= 100) throw InvalidSpellMutation();
            passed = roll.Value < rate;
        }
        var updated = target.WithRuneExperience(0);
        if (passed) updated = updated.WithItemId(checked(target.ItemId + 1));
        return new(updated, rate, true, passed, analysis.UsedProgress);
    }
}
