using System.Text.Json;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task StarFlow10950EarnedFlashingNameExpiresAtInclusiveThirtySeconds()
    {
        await using var fixture = Flash0LifetimeFixture();
        var registration = await Flash0AcquireAsync(fixture);
        var hitpoints = fixture.Peer.Character.MinHitPoints;
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(29_980));
        Assert.Same(registration, Flash0Read(fixture.Owner));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        Flash0AssertRetired(fixture.Owner);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        Assert.Equal(hitpoints, fixture.Peer.Character.MinHitPoints);
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        Assert.True(Flash0Vector(Assert.Single(owner), active: false));
        Assert.Equal(Assert.Single(owner), Assert.Single(peer));
    }

    [Fact]
    public async Task StarFlow10950DuplicateFlashingNameDoesNotRenewOrPublishAnotherFlashVector()
    {
        await using var fixture = Flash0LifetimeFixture();
        var registration = await Flash0AcquireAsync(fixture); // Real first registration W2.
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(10));
        await fixture.OrdinaryMagicAsync(); // W12 Star1.
        await fixture.OrdinaryMagicAsync(); // W13 Star2.
        var start = fixture.Order.Count;
        await fixture.OrdinaryMagicAsync(); // W14 third step; duplicate AddFlag0 is quiet.
        Assert.Equal(ArrowLifeOrigin.AddSeconds(14), fixture.Now);
        Assert.Same(registration, Flash0Read(fixture.Owner));
        Assert.Equal(ArrowLifeOrigin.AddSeconds(32).DateTime, registration.ExpiresAtLocal);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.Equal(2, ArrowLifeVectors(fixture.FramesSince(uid, start)).Length); // Remove337 then Add336 only.
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(17_980));
        Assert.Same(registration, Flash0Read(fixture.Owner));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20)); // OLD W32, not hypothetical W44.
        Flash0AssertRetired(fixture.Owner);
    }

    [Fact]
    public async Task StarFlow10950NormalDeathRetiresActuallyEarnedFlashingNameRegistration()
    {
        await using var fixture = Flash0LifetimeFixture();
        await Flash0AcquireAsync(fixture);
        await fixture.KillOwnerAsync();
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Flash0AssertRetired(fixture.Owner);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StarFlow10950FlashingExpiryOriginalOwnerLossStopsLaterPeerVector(bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        WorldSession? replacement = null;
        await using var fixture = Flash0LifetimeFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && Flash0Vector(bytes, active: false))
            {
                armed = false;
                callbacks++;
                Flash0AssertRetired(current.Owner); // Store mutation precedes entered self frame.
                if (rebind) replacement = current.RebindOwner();
                else current.RemoveOwner();
            }
            return ValueTask.CompletedTask;
        });
        await Flash0AcquireAsync(fixture);
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(29_980));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        Assert.Equal(1, callbacks);
        Assert.True(Flash0Vector(Assert.Single(fixture.FramesSince(PlayerUid, start)), active: false));
        Assert.Empty(fixture.FramesSince(TargetPlayerUid, start));
        if (rebind)
        {
            Assert.Same(replacement, fixture.OwnerSlot);
            Assert.Equal(77_777, fixture.OwnerSlot!.Character.MinHitPoints);
            Assert.Equal(77, fixture.OwnerSlot.Character.MinMana);
            Assert.Null(fixture.OwnerSlot.CanAttackFlashingRegistration);
        }
        else Assert.Null(fixture.OwnerSlot);
    }

    [Fact]
    public async Task StarFlow10950JsonRoundtripDoesNotRestoreTransientFlashDeadlineForRawFlag()
    {
        await using var fixture = Flash0LifetimeFixture();
        await Flash0AcquireAsync(fixture);
        var json = JsonSerializer.Serialize(fixture.Owner);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("CanAttackFlashingRegistration", out _));
        var owner = fixture.Owner;
        var character = Assert.IsType<WolfConquer.Core.Data.CharacterSnapshot>(
            JsonSerializer.Deserialize<WolfConquer.Core.Data.CharacterSnapshot>(
                document.RootElement.GetProperty(nameof(WorldSession.Character)).GetRawText()));
        var reloaded = Assert.IsType<WorldSession>(new WorldSession(
            owner.AccountId, owner.Username, character, owner.PlayerItems, owner.PlayerSpells, owner.PlayerProficiencies));
        Assert.True(HasLegacyStatusFlag(reloaded.Character.Attributes, 0));
        Assert.Null(reloaded.CanAttackFlashingRegistration);
        Assert.False(LegacyCanAttackFlashingRegistration.TryRead(reloaded, out _));
        fixture.ChangeOwnerDuringArrowSend(_ => reloaded);
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(30));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
        Assert.Null(fixture.Owner.CanAttackFlashingRegistration);
    }

    private static ArrowLifecycleFixture Flash0LifetimeFixture(
        Func<ArrowLifecycleFixture, uint, byte[], ValueTask>? observer = null)
    {
        var entered = false;
        return new ArrowLifecycleFixture(learnedStarFlow: true, starFlowTargetEffects: true,
            nativeStarFlowCallerQueryControls: true,
            onSend: (current, recipient, bytes) =>
            {
                if (!entered && recipient == PlayerUid && ArrowThirdVector(bytes, charge: false, triggered: true))
                {
                    entered = true;
                    current.EnterStarFlowPlayerGateView(); // Only current actor DynamicIDs; no0/timer seed.
                }
                return observer is null ? ValueTask.CompletedTask : observer(current, recipient, bytes);
            });
    }

    private static async Task<LegacyCanAttackFlashingRegistration> Flash0AcquireAsync(ArrowLifecycleFixture fixture)
    {
        await ArrowThirdWarmAsync(fixture);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0));
        Assert.Null(fixture.Owner.CanAttackFlashingRegistration);
        var start = fixture.Order.Count;
        await fixture.OrdinaryMagicAsync();
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == 10_950);
        var registration = Flash0Read(fixture.Owner);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(2).DateTime, registration.Timer);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(32).DateTime, registration.ExpiresAtLocal);
        Assert.Equal("true", fixture.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(0)]);
        Assert.DoesNotContain(fixture.Owner.Character.Attributes.Keys,
            key => key.StartsWith("LegacyCanAttackFlashing", StringComparison.Ordinal));
        return registration;
    }

    private static LegacyCanAttackFlashingRegistration Flash0Read(WorldSession session)
    {
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, 0));
        Assert.True(LegacyCanAttackFlashingRegistration.TryRead(session, out var registration));
        Assert.Equal((long)PlayerUid, registration.OwnerUid);
        Assert.Equal(DateTimeKind.Unspecified, registration.Timer.Kind);
        return registration;
    }

    private static void Flash0AssertRetired(WorldSession session)
    {
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, 0));
        Assert.Null(session.CanAttackFlashingRegistration);
        Assert.False(LegacyCanAttackFlashingRegistration.TryRead(session, out _));
        Assert.False(session.Character.Attributes.ContainsKey(LegacyStatusFlagDeathRegistration.Key(0)));
    }

    private static bool Flash0Vector(byte[] bytes, bool active) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && IsLegacyStatusFlagSet(values, 0) == active);
}
