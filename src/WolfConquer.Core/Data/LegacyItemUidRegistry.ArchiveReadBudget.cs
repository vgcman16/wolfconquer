using System.Text.Json;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // Resource policy for a complete history walk, independent of raw UID scan
    // bounds. These limits are not client population or reward entitlements.
    internal const int MaximumArchiveEntries = 65_535;
    internal const long MaximumTotalArchiveBytes = 256L * 1024 * 1024;

    internal sealed class ArchiveReadBudget
    {
        private int entries;
        private long bytes;

        internal void ChargeEntry()
        {
            if (entries == MaximumArchiveEntries)
                throw new JsonException("UID archive history exceeds its entry resource bound.");
            entries++;
        }

        internal void ChargeBytes(byte[] image)
        {
            if (image.LongLength > MaximumTotalArchiveBytes - bytes)
                throw new JsonException("UID archive history exceeds its byte resource bound.");
            bytes += image.LongLength;
        }
    }
}
