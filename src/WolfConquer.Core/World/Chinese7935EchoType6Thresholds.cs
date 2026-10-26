namespace WolfConquer.Core.World;

/// <summary>Pure caller-data projection of the inspected Chinese 7931 DX8 type-6 threshold body.</summary>
public static class Chinese7935EchoType6Thresholds
{
    // Offline allocation guards; these are not client or gameplay limits.
    public const int MaximumRows = 4_096;
    public const int MaximumContributions = 4_096;

    public readonly record struct Row(int AttributeType, uint Threshold, int ExtraAttributeType, uint Bonus);
    public readonly record struct Contribution(int AttributeType, uint Value);

    public static uint Calculate(ReadOnlySpan<Row> rows, ReadOnlySpan<Contribution> contributions)
    {
        if (rows.Length > MaximumRows) throw new ArgumentOutOfRangeException(nameof(rows));
        if (contributions.Length > MaximumContributions) throw new ArgumentOutOfRangeException(nameof(contributions));
        var capturedRows = rows.ToArray();
        var capturedContributions = contributions.ToArray();
        var accumulated = Accumulate(capturedContributions);
        var maxima = new Dictionary<int, uint>();
        // The inspected cache retains equal primary keys and thresholds. Every
        // qualifying row participates in the per-key maximum, including duplicates.
        foreach (var row in capturedRows)
        {
            if (row.ExtraAttributeType != 6 || !accumulated.TryGetValue(row.AttributeType, out var current) ||
                current < row.Threshold)
                continue;
            if (!maxima.TryGetValue(row.AttributeType, out var previous) || row.Bonus > previous)
                maxima[row.AttributeType] = row.Bonus;
        }
        uint result = 0;
        foreach (var value in maxima.Values) result = checked(result + value);
        return result;
    }

    private static Dictionary<int, uint> Accumulate(ReadOnlySpan<Contribution> contributions)
    {
        var values = new Dictionary<int, uint>();
        foreach (var contribution in contributions)
        {
            values.TryGetValue(contribution.AttributeType, out var previous);
            values[contribution.AttributeType] = checked(previous + contribution.Value);
        }
        return values;
    }
}
