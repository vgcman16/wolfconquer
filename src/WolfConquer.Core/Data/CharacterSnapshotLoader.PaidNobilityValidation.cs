using System.Globalization;

namespace WolfConquer.Core.Data;

public sealed partial class CharacterSnapshotLoader
{
    private sealed class PaidNobilityStorageException : FormatException
    {
        internal PaidNobilityStorageException() : base("paidNobilityStorageInvalid") { }
    }

    private static bool TryReadPaidNobilityExpiry(
        IReadOnlyDictionary<string, string> values, out DateTime period, out ulong donation)
    {
        period = default;
        donation = default;
        if (!values.TryGetValue("IsActive", out var raw)) return false;

        if (!bool.TryParse(raw, out var active))
        {
            if (!byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
                throw new PaidNobilityStorageException();
            active = numeric != 0;
        }
        if (!active) return false;

        if (!values.TryGetValue("PeriodTime", out var rawPeriod) ||
            !long.TryParse(rawPeriod, NumberStyles.Integer, CultureInfo.InvariantCulture, out var binary) ||
            !values.TryGetValue("LastNobilityDonation", out var rawDonation) ||
            !ulong.TryParse(rawDonation, NumberStyles.Integer, CultureInfo.InvariantCulture, out donation))
            throw new PaidNobilityStorageException();
        try
        {
            period = DateTime.FromBinary(binary);
        }
        catch (ArgumentException)
        {
            throw new PaidNobilityStorageException();
        }
        return true;
    }
}
