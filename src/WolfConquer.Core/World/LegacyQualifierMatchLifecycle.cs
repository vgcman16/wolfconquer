namespace WolfConquer.Core.World;

internal static class LegacyQualifierMatchLifecycle
{
    internal static readonly TimeSpan EndRetention = TimeSpan.FromSeconds(4);

    internal static bool HasExpired(DateTimeOffset endedAtUtc, DateTimeOffset nowUtc)
    {
        return nowUtc > endedAtUtc + EndRetention;
    }
}
