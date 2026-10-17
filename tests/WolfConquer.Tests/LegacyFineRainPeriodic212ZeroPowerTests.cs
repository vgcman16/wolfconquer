using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task FineRainPeriodicEarnedLiveZeroAdvancesInvokerBeforePowerGuardWithoutPublication()
    {
        const string prefix = "LegacyFineRain212Invocation.";
        var armed = false;
        var callbacks = 0;
        var callbackEnd = -1;
        string? identity = null;
        string? timer = null;
        string? invoker = null;
        string? owner = null;
        await using var fixture = new FineRainExpiryFixture(async (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid || !FineRainExpiryTryVector(bytes, out var first, out var second) ||
                first || !second || Interlocked.Exchange(ref callbacks, 1) != 0) return;
            current.SelectLearnedFineRainLevel(1);
            await current.CastAsync(); // REAL positive6/duration8 replacement atW1, not a zero-power seed.
            var attributes = current.Owner.Character.Attributes;
            identity = attributes[prefix + "Identity"]; // Capture actual replacement identity; never predict a GUID.
            timer = attributes[prefix + "TimerTicks"];
            invoker = attributes[prefix + "InvokerTicks"];
            owner = attributes[prefix + "OwnerUid"];
            Assert.Equal(current.Origin.AddSeconds(1).DateTime.Ticks.ToString(CultureInfo.InvariantCulture), timer);
            Assert.Equal(timer, invoker);
            Assert.Equal(PlayerUid.ToString(CultureInfo.InvariantCulture), owner);
            FineRainPeriodicAssertPower(current.Owner, "6", "6");
            Assert.Equal(506, current.Owner.Character.MinHitPoints);
            DirectFineRainHpAssertOwnedMaximum(current.Owner, 506);
            callbackEnd = current.Order.Count;
        }, duration: 1, power: 6, recastPower: 6, recastDuration: 8);
        await fixture.AcquireAsync();
        await fixture.StartAsync();
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        armed = false;
        Assert.Equal(1, callbacks); // Explicit modeled212-first capture, not a source enumeration guarantee.
        Assert.True(callbackEnd > 0);
        Assert.Collection(fixture.OwnerFramesSince(callbackEnd),
            bytes => Assert.True(FineRainExpiryIsWait(bytes)),
            bytes => FineRainPeriodicAssertCountdown(bytes, 0, 0),
            bytes => Assert.True(FineRainExpiryTryVector(bytes, out var first, out var second) && first && !second),
            bytes => Assert.True(FineRainExpiryIsWait(bytes)));
        var afterOld = fixture.Owner.Character.Attributes;
        Assert.Equal(identity, afterOld[prefix + "Identity"]);
        Assert.Equal(timer, afterOld[prefix + "TimerTicks"]); // Old captured TimerW0 must NOT overwrite new TimerW1.
        Assert.Equal(invoker, afterOld[prefix + "InvokerTicks"]);
        Assert.Equal(owner, afterOld[prefix + "OwnerUid"]);
        Assert.Equal((long)PlayerUid, fixture.Owner.Character.Uid);
        Assert.True(HasLegacyStatusFlag(afterOld, 212));
        FineRainExpiryAssertAbsent(fixture.Owner, 213);
        FineRainPeriodicAssertPower(fixture.Owner, "0", "6"); // Current6 / OLDduration1 =>0, quotient becomes0.
        Assert.Equal(fixture.Origin.AddSeconds(9).ToString("O"), afterOld["LegacyFineRain1ExpiresAtUtc"]);
        Assert.Equal(506U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(506, fixture.Owner.Character.MinHitPoints);

        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Empty(fixture.OwnerFramesSince(start)); // Power0 branch emits no countdown/Max/HP.
        Assert.Empty(fixture.PeerFramesSince(start));
        var afterZero = fixture.Owner.Character.Attributes;
        Assert.Equal(fixture.Origin.AddSeconds(2).DateTime.Ticks.ToString(CultureInfo.InvariantCulture),
            afterZero[prefix + "InvokerTicks"]); // PRODUCED current registration, no private helper or desired-state seed.
        Assert.Equal(identity, afterZero[prefix + "Identity"]);
        Assert.Equal(timer, afterZero[prefix + "TimerTicks"]);
        Assert.Equal(owner, afterZero[prefix + "OwnerUid"]);
        Assert.True(HasLegacyStatusFlag(afterZero, 212));
        FineRainPeriodicAssertPower(fixture.Owner, "0", "6");
        Assert.Equal(506U, (await fixture.QueryAsync()).MaxHP);
        Assert.Equal(506, fixture.Owner.Character.MinHitPoints);
    }
}
