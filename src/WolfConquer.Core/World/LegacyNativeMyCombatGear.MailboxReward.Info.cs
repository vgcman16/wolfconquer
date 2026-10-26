using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private sealed record MailboxInfoRow(string Key, double[] Owner, long[] Bits)
    {
        internal bool IsCurrent(KeyValuePair<string, double[]> row) => row.Key == Key &&
            ReferenceEquals(row.Value, Owner) && row.Value is not null && row.Value.Length == Bits.Length &&
            row.Value.Select(BitConverter.DoubleToInt64Bits).SequenceEqual(Bits);
    }

    private static MailboxInfoRow[] CaptureMailboxInfo(ConcurrentDictionary<string, double[]> info)
    {
        var rows = info.ToArray().OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
        var result = new MailboxInfoRow[rows.Length];
        int total = 0;
        for (int index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row.Key is null || row.Value is null || index > 0 && rows[index - 1].Key == row.Key)
                throw MailboxRewardUnavailable();
            total = checked(total + row.Value.Length);
            if (total > MailboxGearMaximumImage / sizeof(double)) throw MailboxRewardUnavailable();
            result[index] = new(row.Key, row.Value, row.Value.Select(BitConverter.DoubleToInt64Bits).ToArray());
        }
        return result;
    }

    private static bool MailboxInfoCurrent(ConcurrentDictionary<string, double[]> info, MailboxInfoRow[] rows)
    {
        var current = info.ToArray().OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
        if (current.Length != rows.Length) return false;
        for (int index = 0; index < rows.Length; index++)
            if (!rows[index].IsCurrent(current[index])) return false;
        return true;
    }
}
