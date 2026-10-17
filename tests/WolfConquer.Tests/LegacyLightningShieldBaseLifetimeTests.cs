using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task DirectAzureBaseRealTogglePublishesTwoRemovalsBeforeResetAndRetainsIndependentMeter()
    {
        var armed = false;
        var callbacks = 0;
        await using var fixture = new DirectAzureLifeFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid) return ValueTask.CompletedTask;
            if (DirectAzureBaseVector(bytes, baseActive: false, activated: true))
            {
                Assert.Equal(0, Interlocked.Increment(ref callbacks) - 1);
                Assert.Equal("19600", current.Target.Character.Attributes["LightningShieldLeft"]);
                Assert.True(current.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
                Assert.False(current.Target.Character.Attributes.ContainsKey("LegacyLightningShieldExpiresAtUtc"));
                current.BindTarget(current.Target with
                {
                    Character = current.Target.Character with { MinHitPoints = 1_666, MinMana = 77 },
                });
            }
            else if (DirectAzureBaseVector(bytes, baseActive: false, activated: false))
            {
                Assert.Equal(2, Interlocked.Increment(ref callbacks));
                Assert.Equal("19600", current.Target.Character.Attributes["LightningShieldLeft"]);
                Assert.False(current.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
                Assert.Equal(1_666, current.Target.Character.MinHitPoints);
                Assert.Equal(77, current.Target.Character.MinMana);
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrimeAsync();
        await fixture.HitAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.ToggleAsync();
        armed = false;
        Assert.Equal(2, callbacks);
        Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);
        Assert.Equal(1_666, fixture.Target.Character.MinHitPoints);
        Assert.Equal(77, fixture.Target.Character.MinMana);
        var owner = fixture.OwnerFramesSince(start);
        Assert.Collection(owner,
            bytes => Assert.True(DirectAzureBaseVector(bytes, false, true)),
            bytes => Assert.True(DirectAzureBaseVector(bytes, false, false)),
            bytes => Assert.True(DirectAzureLifeReset(bytes)),
            bytes => Assert.True(DirectAzureBaseAnimation(bytes)));
        var peer = fixture.Order.ToArray().Skip(start).Where(frame => frame.Recipient == PlayerUid).ToArray();
        Assert.Collection(peer,
            frame => Assert.True(DirectAzureBaseVector(frame.Bytes, false, true)),
            frame => Assert.True(DirectAzureBaseVector(frame.Bytes, false, false)),
            frame => Assert.True(DirectAzureBaseAnimation(frame.Bytes)));
        DirectAzureBaseNoQueryOrHitpoints(owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectAzureBaseRealExpiryRemovesOnly240AtInclusiveDeadline(bool activated)
    {
        await using var fixture = new DirectAzureLifeFixture(origin: DateTimeOffset.UtcNow);
        await fixture.PrimeAsync();
        var expires = DateTimeOffset.Parse(fixture.Target.Character.Attributes["LegacyLightningShieldExpiresAtUtc"],
            CultureInfo.InvariantCulture, DateTimeStyles.None);
        fixture.Advance(expires - fixture.Origin - TimeSpan.FromSeconds(5));
        if (activated) await fixture.HitAsync();
        var activatedDeadline = activated ? fixture.Target.Character.Attributes[DirectAzureLifeDeadline] : null;
        await fixture.StartRuntimeAsync();
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromMilliseconds(4_980));
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldFlag));
        var start = fixture.Order.Count;
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromMilliseconds(20));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldFlag));
        Assert.False(fixture.Target.Character.Attributes.ContainsKey("LegacyLightningShieldExpiresAtUtc"));
        Assert.Equal(activated,
            HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal(activated, fixture.Target.Character.Attributes.ContainsKey("LightningShieldLeft"));
        if (activated)
        {
            Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);
            Assert.Equal(activatedDeadline, fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        }
        var owner = fixture.OwnerFramesSince(start);
        Assert.True(DirectAzureBaseVector(Assert.Single(owner), false, activated));
        Assert.True(DirectAzureBaseVector(Assert.Single(fixture.Order.ToArray().Skip(start),
            frame => frame.Recipient == PlayerUid).Bytes, false, activated));
        Assert.Empty(DirectAzureLifeEntries(owner));
        DirectAzureBaseNoQueryOrHitpoints(owner);
        if (!activated) return;

        // The independent237 timer is still live for another5 seconds, then its
        // already-owned special reset→removal path runs without base240.
        start = fixture.Order.Count;
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromSeconds(5));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);
        Assert.Collection(fixture.OwnerFramesSince(start),
            bytes => Assert.True(DirectAzureLifeReset(bytes)),
            bytes => Assert.True(DirectAzureBaseVector(bytes, false, false)));
    }

    [Theory]
    [InlineData(false, "remove")]
    [InlineData(false, "rebind")]
    [InlineData(true, "remove")]
    [InlineData(true, "rebind")]
    public async Task DirectAzureBaseFirstRemovalVectorIdentityLossStopsOwnedFanoutAndTail(
        bool expiry, string mutation)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        WorldSession? replacement = null;
        await using var fixture = new DirectAzureLifeFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid || !DirectAzureBaseVector(bytes, false, true) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return ValueTask.CompletedTask;
            Assert.Equal("19600", current.Target.Character.Attributes["LightningShieldLeft"]);
            Assert.True(current.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
            boundary = current.Order.Count;
            if (mutation == "remove") current.RemoveTarget();
            else replacement = current.RebindTarget();
            return ValueTask.CompletedTask;
        }, origin: DateTimeOffset.UtcNow);
        await fixture.PrimeAsync();
        DateTimeOffset expires = default;
        if (expiry)
        {
            expires = DateTimeOffset.Parse(fixture.Target.Character.Attributes["LegacyLightningShieldExpiresAtUtc"],
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            fixture.Advance(expires - fixture.Origin - TimeSpan.FromSeconds(5));
        }
        await fixture.HitAsync();
        armed = true;
        if (expiry)
        {
            await fixture.StartRuntimeAsync();
            await fixture.AdvanceRuntimeAsync(TimeSpan.FromSeconds(5));
        }
        else await fixture.ToggleAsync();
        armed = false;
        Assert.Equal(1, callbacks);
        Assert.True(boundary > 0);
        Assert.Empty(fixture.Order.ToArray().Skip(boundary));
        if (mutation == "remove") Assert.False(fixture.TryTarget(out _));
        else
        {
            Assert.True(fixture.TryTarget(out var current));
            Assert.Same(replacement, current);
            Assert.Equal(77_777, current!.Character.MinHitPoints);
            Assert.Equal("replacement", current.Character.Attributes["DirectAzureLifeIdentity"]);
            Assert.False(current.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
            Assert.False(current.Character.Attributes.ContainsKey("LightningShieldLeft"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectAzureBaseNestedToggleChangesTheOtherRegistrationAndStopsStalePeerVector(
        bool activatedRemoval)
    {
        var armed = false;
        var callbacks = 0;
        var callbackEnd = -1;
        await using var fixture = new DirectAzureLifeFixture(async (current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid ||
                !DirectAzureBaseVector(bytes, false, !activatedRemoval) ||
                Interlocked.CompareExchange(ref callbacks, 1, 0) != 0) return;
            await current.ToggleAsync(); // Re-add240 without changing the independent237 registration.
            if (!activatedRemoval)
                await current.ToggleAsync(); // Return240 to absent, but now remove237.
            Assert.Equal(activatedRemoval,
                HasLegacyStatusFlag(current.Target.Character.Attributes, LegacyLightningShieldFlag));
            Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
            Assert.Equal("19600", current.Target.Character.Attributes["LightningShieldLeft"]);
            callbackEnd = current.Order.Count;
        });
        await fixture.PrimeAsync();
        await fixture.HitAsync();
        armed = true;
        await fixture.ToggleAsync();
        armed = false;
        Assert.Equal(1, callbacks);
        Assert.True(callbackEnd > 0);
        Assert.Equal(activatedRemoval,
            HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldFlag));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);
        // Supersession stops this old full vector, not the entered off branch:
        // its unconditional owner reset and final animation still follow.
        Assert.Collection(fixture.OwnerFramesSince(callbackEnd),
            bytes => Assert.True(DirectAzureLifeReset(bytes)),
            bytes => Assert.True(DirectAzureBaseAnimation(bytes)));
        var peer = Assert.Single(fixture.Order.ToArray().Skip(callbackEnd),
            frame => frame.Recipient == PlayerUid);
        Assert.True(DirectAzureBaseAnimation(peer.Bytes));
    }

    private static bool DirectAzureBaseVector(byte[] bytes, bool baseActive, bool activated) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } values &&
            IsLegacyStatusFlagSet(values, LegacyLightningShieldFlag) == baseActive &&
            IsLegacyStatusFlagSet(values, LegacyLightningShieldActivatedFlag) == activated);

    private static bool DirectAzureBaseAnimation(byte[] bytes) =>
        LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.AttackerUid == TargetPlayerUid && packet.SpellId == 15_750 &&
        packet.Targets.Count == 1 && packet.Targets[0].Uid == TargetPlayerUid && packet.Targets[0].Damage == 0;

    private static void DirectAzureBaseNoQueryOrHitpoints(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(ParseMsgUpdates(frames).SelectMany(packet => packet.Updates),
            entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);
        Assert.DoesNotContain(frames, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
    }
}
