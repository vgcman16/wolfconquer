namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    private static uint PrepareChiPrestige(LegacyNativeChiRuntime? chi)
    {
        if (chi is null) return 0;
        uint points = 0;
        foreach (var power in chi)
        {
            if (power is null) throw new InvalidOperationException("Prestige preparation found an invalid chi power.");
            var score = checked((int)PrepareChiPowerScore(power));
            points = checked(points + checked((uint)checked(score * 30)));
            if (power.Rank < 50) points = checked(points + checked((uint)checked(score * 10)));
        }
        return points;
    }

    private static uint PrepareChiPowerScore(LegacyNativeChiRuntime.ChiPower power)
    {
        if (!power.UnLocked) return 0;
        var attributes = power.Attributes
            ?? throw new InvalidOperationException("Prestige preparation requires unlocked chi attributes.");
        long total = 0;
        foreach (var attribute in attributes)
        {
            if (attribute.Type == LegacyNativeChiAttribute.None ||
                !Enum.IsDefined(typeof(LegacyNativeChiAttribute), attribute.Type))
                throw new InvalidOperationException("Prestige preparation requires a declared nonempty chi attribute type.");
            var minimum = LegacyNativeChiRuntime.ChiMinValues(attribute.Type);
            var maximum = LegacyNativeChiRuntime.ChiMaxValues(attribute.Type);
            if (attribute.Value < minimum || attribute.Value > maximum)
                throw new InvalidOperationException("Prestige preparation found a chi attribute outside its actual source bounds.");
            var range = maximum - minimum;
            var contribution = (attribute.Value - minimum) / (range / 100.0);
            if (!double.IsFinite(contribution) || contribution < 0 || contribution > uint.MaxValue)
                throw new InvalidOperationException("Prestige preparation found an invalid chi score component.");
            total = checked(total + checked((uint)contribution));
        }
        if (total > int.MaxValue || power.Score < 0 || power.Score != total)
            throw new InvalidOperationException("The real chi score is invalid or changed during prestige preparation.");
        return checked((uint)total);
    }
}
