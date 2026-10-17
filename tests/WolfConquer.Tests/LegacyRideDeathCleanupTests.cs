using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRideDeathCleanupTests
{
    private const int RideStatusFlag = 0x32;

    [Fact]
    public void ApplyDeathClearsRideFlagAndRemoveOnDeadMetadataWithoutDiscardingOtherState()
    {
        var now = new DateTimeOffset(2026, 7, 10, 14, 30, 0, TimeSpan.Zero);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(RideStatusFlag),
            ["MinHitPoints"] = "450",
            ["LegacyRideExpiresAtUtc"] = now.AddDays(30).ToString("O", CultureInfo.InvariantCulture),
            ["LegacyRideRemoveOnDead"] = "true",
            ["LegacyRideStampSeconds"] = "1",
            ["UnrelatedRuntimeState"] = "keep",
        };

        var result = LegacyPlayerDeathState.ApplyDeath(
            attributes,
            now,
            deadX: 321,
            deadY: 456,
            killerUid: 987_654,
            killerName: "Birdman");

        Assert.False(LegacyPlayerDeathState.HasStatusFlag(result.Attributes, RideStatusFlag));
        Assert.True(result.HasDeadFlag);
        Assert.False(result.HasGhostFlag);
        Assert.False(result.Attributes.ContainsKey("LegacyRideExpiresAtUtc"));
        Assert.False(result.Attributes.ContainsKey("LegacyRideRemoveOnDead"));
        Assert.False(result.Attributes.ContainsKey("LegacyRideStampSeconds"));
        Assert.Equal("keep", result.Attributes["UnrelatedRuntimeState"]);
        Assert.Equal("0", result.Attributes["MinHitPoints"]);
        Assert.Equal(now, result.DeadStamp);
        Assert.Equal(now + LegacyPlayerDeathState.GhostEffectDelay, result.GhostStamp);
        Assert.Equal(now + LegacyPlayerDeathState.DefaultReviveDelay, result.ReviveAllowedAt);
        Assert.Equal(321, result.DeadX);
        Assert.Equal(456, result.DeadY);
        Assert.Equal(987_654U, result.KillerUid);
        Assert.Equal("Birdman", result.KillerName);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }
}
