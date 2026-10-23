namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRoleQuests
{
    // Uses the genuine persistence format's one operational bound, not a quest,
    // catalog, active-slot, intention gameplay, or native packet-count limit.
    internal static int GetProjectedQuestImageLength(ReadOnlySpan<int> currentCounts, uint? addedCount)
    {
        long length = 4;
        foreach (int count in currentCounts)
        {
            if (count < 0) throw new InvalidDataException("Quest intention count is negative.");
            length += 16L + (4L * count);
            if (length > MaximumImageBytes) throw new InvalidDataException("Quest book exceeds its persistence bound.");
        }
        if (addedCount is { } additional)
        {
            length += 16L + (4L * additional);
            if (length > MaximumImageBytes) throw new InvalidDataException("Accepted quest would exceed its persistence bound.");
        }
        return checked((int)length);
    }
}
