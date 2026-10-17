using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(11_000, 21_000, 4_294_967_279U)]
    [InlineData(-500, -1_500, 4_294_967_291U)]
    [InlineData(11_000, 2_147_483_000, 2_147_488U)]
    public async Task FineRainPeriodicCountdownKeepsIndependentSignedNativeEnvironmentAndUncheckedTime(
        int nativeAtInvoke, int environmentAtInvoke, uint expectedTime)
    {
        var armed = false;
        var native = new FineRainPeriodicClock(() => armed ? nativeAtInvoke : 10_000);
        var environment = new FineRainPeriodicClock(() => armed ? environmentAtInvoke : 10_000);
        await using var fixture = new FineRainExpiryFixture(power: 96,
            nativeClock: native, environmentClock: environment);
        await fixture.AcquireAsync(); // Normal positive native admission; injected signed values apply ONLY to invocation.
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        armed = false;
        FineRainPeriodicAssertPulse(fixture.OwnerFramesSince(start), expectedTime, 84, 584);
        Assert.Empty(fixture.PeerFramesSince(start));
        Assert.Equal(584U, (await fixture.QueryAsync()).MaxHP);
        // Literals independently pin signed division toward0, unchecked overflow,
        // separate native/env epochs and uint negative elapsed-time projection.
    }

    [Fact]
    public async Task FineRainPeriodicCountdownReadsNativeThenEnvironmentThenFreshWall()
    {
        FineRainExpiryFixture? current = null;
        var armed = false;
        var reads = new List<string>();
        var native = new FineRainPeriodicClock(() =>
        {
            if (armed) reads.Add("native");
            return armed ? 11_999 : 10_000;
        });
        var environment = new FineRainPeriodicClock(() =>
        {
            if (!armed) return 20_000;
            reads.Add("environment");
            current!.AdvanceWall(TimeSpan.FromMilliseconds(500));
            return 20_000;
        });
        await using var fixture = new FineRainExpiryFixture(power: 96,
            nativeClock: native, environmentClock: environment);
        current = fixture;
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        armed = false;
        Assert.Equal(new[] { "native", "environment" }, reads);
        // Native11999/1000=11. Env20000 + (W8 - freshW1.5)=26500 ->26.
        // uint(11-26)=-15, NOT -16 from incorrectly reusing pass-startW1.
        FineRainPeriodicAssertPulse(fixture.OwnerFramesSince(start), 4_294_967_281U, 84, 584);
        Assert.Equal(fixture.Origin.AddMilliseconds(1_500), fixture.WallNow);
        Assert.Empty(fixture.PeerFramesSince(start));
    }

    private sealed class FineRainPeriodicClock(Func<int> read) : ILegacySignedMillisecondClock
    {
        public int GetMilliseconds() => read();
    }
}
