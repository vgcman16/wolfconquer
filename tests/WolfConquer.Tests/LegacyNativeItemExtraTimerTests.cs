using System.Reflection;
using WolfConquer.Core.World;
using Extra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

public sealed class LegacyNativeItemExtraTimerTests
{
    private static bool PurificationAvailable(Extra.Purification item) => (bool)typeof(Extra.Purification)
        .GetField("Available", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item)!;

    private static Extra.Purification Purification(bool available)
    {
        var item = new Extra.Purification { ItemUID = 71, PurificationItemID = 1001 };
        if (available) Assert.True(item.InLife);
        return item;
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void WholePurificationTimerClearsExpiredAndOverflowDurationAvailability(bool overflow, bool wasAvailable)
    {
        var item = Purification(wasAvailable); item.PurificationDuration = overflow ? uint.MaxValue : 1;
        item.AddedOn = overflow ? DateTime.Now : DateTime.Now.AddMinutes(-1); var added = item.AddedOn;
        Assert.Equal(0, item.SecoundsLeft); Assert.False(item.InLife); Assert.False(PurificationAvailable(item));
        Assert.Equal(added, item.AddedOn); Assert.Equal(overflow ? uint.MaxValue : 1u, item.PurificationDuration);
        Assert.Equal(1001u, item.PurificationItemID);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void WholeRefineryTimerClearsExpiredAndOverflowDurationAvailability(bool overflow, bool wasAvailable)
    {
        var item = new Extra.Refinery { ItemUID = 82, EffectID = 1001, Available = wasAvailable,
            EffectDuration = overflow ? uint.MaxValue : 1, AddedOn = overflow ? DateTime.Now : DateTime.Now.AddMinutes(-1) };
        var added = item.AddedOn;
        Assert.Equal(0, item.SecoundsLeft); Assert.False(item.InLife); Assert.False(item.Available);
        Assert.Equal(added, item.AddedOn); Assert.Equal(overflow ? uint.MaxValue : 1u, item.EffectDuration); Assert.Equal(1001u, item.EffectID);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WholeZeroDurationPathsRetainLowIdLatchThenPermanentPathSetsItWithoutReadingInvalidDate(bool wasAvailable)
    {
        var p = Purification(wasAvailable); p.PurificationItemID = 1000; p.AddedOn = DateTime.MaxValue;
        var r = new Extra.Refinery { EffectID = 1000, AddedOn = DateTime.MaxValue, Available = wasAvailable };
        Assert.Equal(0, p.SecoundsLeft); Assert.Equal(0, r.SecoundsLeft);
        Assert.Equal(wasAvailable, p.InLife); Assert.Equal(wasAvailable, r.InLife);
        p.PurificationItemID = 1001; r.EffectID = 1001;
        Assert.Equal(0, p.SecoundsLeft); Assert.Equal(0, r.SecoundsLeft); Assert.True(p.InLife); Assert.True(r.InLife);
        p.PurificationItemID = 0; r.EffectID = 0;
        Assert.True(p.InLife); Assert.True(r.InLife); Assert.True(PurificationAvailable(p)); Assert.True(r.Available);
        Assert.Equal(DateTime.MaxValue, p.AddedOn); Assert.Equal(DateTime.MaxValue, r.AddedOn);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WholeAddSecondsFaultsPrecedeEitherAvailabilityMutation(bool wasAvailable)
    {
        var p = Purification(wasAvailable); p.PurificationDuration = 1; p.AddedOn = DateTime.MaxValue;
        var r = new Extra.Refinery { EffectDuration = 1, AddedOn = DateTime.MaxValue, Available = wasAvailable };
        Assert.Throws<ArgumentOutOfRangeException>(() => p.SecoundsLeft); Assert.Equal(wasAvailable, PurificationAvailable(p));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.InLife); Assert.Equal(wasAvailable, PurificationAvailable(p));
        Assert.Throws<ArgumentOutOfRangeException>(() => r.SecoundsLeft); Assert.Equal(wasAvailable, r.Available);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.InLife); Assert.Equal(wasAvailable, r.Available);
        Assert.Equal(1u, p.PurificationDuration); Assert.Equal(1u, r.EffectDuration);
    }

    [Theory]
    [InlineData(300u)] [InlineData(2147483646u)]
    public void WholePositiveTimersUseRealClockAndReturnActiveNativeSeconds(uint duration)
    {
        DateTime added = DateTime.Now.AddSeconds(-7);
        var p = Purification(false); p.AddedOn = added; p.PurificationDuration = duration;
        var r = new Extra.Refinery { EffectID = 1001, AddedOn = added, EffectDuration = duration };
        DateTime earliest = DateTime.Now; int ps = p.SecoundsLeft; int rs = r.SecoundsLeft; DateTime latest = DateTime.Now;
        AssertExpiryWindow(added, duration, earliest, latest, ps); AssertExpiryWindow(added, duration, earliest, latest, rs);
        Assert.True(p.InLife); Assert.True(r.InLife); Assert.True(PurificationAvailable(p)); Assert.True(r.Available);
    }

    // Independent relative-tick bounds allow the native absolute-seconds rounding
    // window. They do not reuse the proposed numeric helper or inject a clock.
    internal static void AssertExpiryWindow(DateTime added, uint duration, DateTime earliest, DateTime latest, int actual)
    {
        long expires = added.AddSeconds(duration).Ticks;
        long minimum = (expires - latest.Ticks) / TimeSpan.TicksPerSecond - 1;
        long maximum = (expires - earliest.Ticks) / TimeSpan.TicksPerSecond + 1;
        Assert.InRange((long)actual, Math.Max(1L, minimum), Math.Min(int.MaxValue, maximum));
    }
}
