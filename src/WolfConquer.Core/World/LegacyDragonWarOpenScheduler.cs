using System.Globalization;

namespace WolfConquer.Core.World;

public sealed class LegacyDragonWarOpenScheduler
{
    public const int OpenMinute = 11;
    public const int OpenSecond = 9;

    private readonly HashSet<string> _openedSlots = [];

    public bool TryConsumeDueOpen(DateTimeOffset now)
    {
        var local = now.LocalDateTime;
        if (local.Minute != OpenMinute || local.Second != OpenSecond)
        {
            return false;
        }

        return _openedSlots.Add(CreateSlotKey(local));
    }

    private static string CreateSlotKey(DateTime local)
    {
        return local.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    }
}
