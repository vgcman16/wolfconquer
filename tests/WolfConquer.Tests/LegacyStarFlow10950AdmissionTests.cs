using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort StarAdmissionSpell = 10_950;

    [Fact]
    public async Task StarFlow10950AdmissionPublishesCostsThenCommitsCooldownBeforeItsActualReceive()
    {
        var armed = false;
        var entered = 0;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow(),
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == StarAdmissionSpell))
            {
                entered++;
                Assert.Equal("90", current.Owner.Character.Attributes["Stamina"]);
                Assert.Equal(960, current.Owner.Character.MinMana);
                Assert.Equal(ArrowLifeOrigin.AddSeconds(12), current.Owner.MagicCooldowns![StarAdmissionSpell]);
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, entered);
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 960UL) }, StarAdmissionCosts(owner));
        Assert.Empty(StarAdmissionCosts(peer));
        var add = Array.FindIndex(owner, bytes => ArrowThirdVector(bytes, false, true));
        var stamina = Array.FindIndex(owner, bytes => StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Stamina, 90));
        var mana = Array.FindIndex(owner, bytes => StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Mana, 960));
        var cast = Array.FindIndex(owner, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == StarAdmissionSpell));
        Assert.True(add >= 0 && stamina > add && mana > stamina && cast > mana);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        fixture.AssertRegistration(336, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(uid, start);
            Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == StarAdmissionSpell);
            Assert.DoesNotContain(frames, bytes => LegacyMagicColdTimePacket.TryParse(ReadFrame(bytes), out _));
        }
    }

    [Theory]
    [InlineData((byte)1, false)]
    [InlineData((byte)5, true)]
    public async Task StarFlow10950SourceOpponentZeroRangeGatePrecedesCosts(byte sort, bool admitted)
    {
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow(sort));
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(admitted ? "90" : "100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(admitted ? 960 : 1_000, fixture.Owner.Character.MinMana);
        Assert.Equal(admitted ? 1 : 0, ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)).Count(packet => packet.SpellId == StarAdmissionSpell));
        Assert.Equal(admitted, fixture.Owner.MagicCooldowns?.ContainsKey(StarAdmissionSpell) == true);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        fixture.AssertRegistration(336, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public async Task StarFlow10950RejectsInsufficientStaminaBeforeManaOrCooldown(int stamina, bool admitted)
    {
        var armed = false;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow(),
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
                current.ChangeOwnerDuringArrowSend(value => StarAdmissionStamina(value, stamina));
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(admitted ? "0" : "9", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(admitted ? 960 : 1_000, fixture.Owner.Character.MinMana);
        Assert.Equal(admitted ? 1 : 0, ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)).Count(packet => packet.SpellId == StarAdmissionSpell));
        Assert.Equal(admitted, fixture.Owner.MagicCooldowns?.ContainsKey(StarAdmissionSpell) == true);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task StarFlow10950ManaFailureRetainsAlreadySpentStaminaAndSourcePrefix()
    {
        var armed = false;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow(),
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, false, true))
                current.ChangeOwnerDuringArrowSend(value => value with { Character = value.Character with { MinMana = 5 } });
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(5, fixture.Owner.Character.MinMana);
        Assert.False(fixture.Owner.MagicCooldowns?.ContainsKey(StarAdmissionSpell) == true);
        Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 90UL) }, StarAdmissionCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == StarAdmissionSpell);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        fixture.AssertRegistration(336, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
    }

    [Fact]
    public async Task StarFlow10950CooldownFailureStillConsumesBothCostsAndRenewsThirdPrefix()
    {
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow());
        await ArrowThirdWarmAsync(fixture);
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(ArrowLifeOrigin.AddSeconds(12), fixture.Owner.MagicCooldowns![StarAdmissionSpell]);
        await fixture.OrdinaryMagicAsync();
        await fixture.OrdinaryMagicAsync();
        var start = fixture.Order.Count;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(ArrowLifeOrigin.AddSeconds(5), fixture.Now);
        Assert.Equal("80", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(920, fixture.Owner.Character.MinMana);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(12), fixture.Owner.MagicCooldowns![StarAdmissionSpell]);
        Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 80UL), (LegacyMsgUpdateDataType.Mana, 920UL) }, StarAdmissionCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == StarAdmissionSpell);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        fixture.AssertRegistration(336, ArrowLifeOrigin.AddSeconds(5).DateTime, 10);
    }

    [Fact]
    public async Task StarFlow10950HpOnlyAliveGateRunsAfterCostsAndCooldownWithoutRegatingIncomingReceive()
    {
        var armed = false;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow(),
            onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Stamina, 90))
                current.ChangeOwnerDuringArrowSend(value => value with { Character = value.Character with { MinHitPoints = 0 } });
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        var targetHp = fixture.Peer.Character.MinHitPoints;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.Equal("90", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(960, fixture.Owner.Character.MinMana);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(12), fixture.Owner.MagicCooldowns![StarAdmissionSpell]);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.True(fixture.Peer.Character.MinHitPoints < targetHp);
        var casts = ParseSpellAnimations(fixture.FramesSince(PlayerUid, start));
        Assert.DoesNotContain(casts, packet => packet.SpellId == StarAdmissionSpell);
        Assert.True(Assert.Single(Assert.Single(casts, packet => packet.SpellId == 1_000).Targets).Damage > 0);
    }

    [Fact]
    public async Task StarFlow10950UsesFreshManaAndClockButKeepsQueryCoordinatesCapturedBeforeCostSends()
    {
        var armed = false;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow(),
            onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid) return ValueTask.CompletedTask;
            if (StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Stamina, 90))
                current.ChangeOwnerDuringArrowSend(value => value with { Character = value.Character with { MinMana = 77 } });
            if (StarAdmissionScalar(bytes, LegacyMsgUpdateDataType.Mana, 37))
            {
                current.AdvanceUnstartedArrowInputClock(TimeSpan.FromSeconds(1));
                current.ChangeOwnerDuringArrowSend(value => value with { Character = value.Character with
                    { MinMana = 11, MinHitPoints = 4_444, X = 831, Y = 585 } });
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(ArrowLifeOrigin.AddSeconds(3), fixture.Now);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(13), fixture.Owner.MagicCooldowns![StarAdmissionSpell]);
        Assert.Equal(11, fixture.Owner.Character.MinMana);
        Assert.Equal(4_444, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(831, fixture.Owner.Character.X);
        Assert.Equal(585, fixture.Owner.Character.Y);
        Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 37UL) }, StarAdmissionCosts(fixture.FramesSince(PlayerUid, start)));
        var cast = Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == StarAdmissionSpell);
        Assert.Equal((ushort)830, cast.X);
        Assert.Equal((ushort)584, cast.Y);
        Assert.True(Assert.Single(cast.Targets, target => target.Uid == TargetPlayerUid).Damage > 0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StarFlow10950CostPublicationOriginalOwnerLossAbortsWithoutStaleSetOrVictimReceive(bool atMana, bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        WorldSession? replacement = null;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow(),
            onSend: (current, recipient, bytes) =>
        {
            var phase = atMana ? LegacyMsgUpdateDataType.Mana : LegacyMsgUpdateDataType.Stamina;
            var value = atMana ? 960UL : 90UL;
            if (armed && recipient == PlayerUid && StarAdmissionScalar(bytes, phase, value))
            {
                armed = false;
                callbacks++;
                Assert.Equal(4U, ArrowLifeCounter(current.Owner, "StarFlow"));
                current.AssertRegistration(336, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
                if (rebind) replacement = current.RebindOwner();
                else current.RemoveOwner();
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        var targetHp = fixture.Peer.Character.MinHitPoints;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, callbacks);
        Assert.Equal(targetHp, fixture.Peer.Character.MinHitPoints);
        if (rebind)
        {
            Assert.Same(replacement, fixture.OwnerSlot);
            Assert.Equal(77_777, fixture.OwnerSlot!.Character.MinHitPoints);
            Assert.Equal(77, fixture.OwnerSlot.Character.MinMana);
            Assert.False(fixture.OwnerSlot.MagicCooldowns?.ContainsKey(StarAdmissionSpell) == true);
            Assert.False(HasLegacyStatusFlag(fixture.OwnerSlot.Character.Attributes, 336));
        }
        else Assert.Null(fixture.OwnerSlot);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(uid, start);
            Assert.Empty(ParseSpellAnimations(frames));
            ArrowThirdAssertNoVictimReceive(frames);
        }
    }

    [Fact]
    public async Task StarFlow10950Map1039SkipsCostsButStillCommitsItsNaturalCooldown()
    {
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, starFlowRow: StarAdmissionRow(), mapId: 1_039);
        Assert.Equal("1", fixture.Owner.Character.Attributes["DynamicID"]);
        Assert.Equal("1", fixture.Peer.Character.Attributes["DynamicID"]);
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(12), fixture.Owner.MagicCooldowns![StarAdmissionSpell]);
        Assert.Empty(StarAdmissionCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == StarAdmissionSpell);
    }

    [Fact]
    public async Task StarFlow10950CatalogRowWithoutLearnedBookStillConsumesThirdPrefixWithoutCast()
    {
        await using var fixture = new ArrowLifecycleFixture(catalogStarFlow: true, starFlowRow: StarAdmissionRow());
        Assert.False(fixture.Owner.PlayerSpells.ContainsSpell(StarAdmissionSpell));
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        fixture.AssertRegistration(336, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
        Assert.False(fixture.Owner.MagicCooldowns?.ContainsKey(StarAdmissionSpell) == true);
        Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == StarAdmissionSpell);
    }

    private static MagicType StarAdmissionRow(byte sort = 5) => CreateMagicRow(StarAdmissionSpell, sort: sort,
        useMana: 40, useStamina: 10, damage: 100, range: 3, maxTargets: 1, coolDown: 0, coldTime: 10_000);

    private static WorldSession StarAdmissionStamina(WorldSession value, int stamina) => value with
    { Character = value.Character with { Attributes = new Dictionary<string, string>(value.Character.Attributes)
        { ["Stamina"] = stamina.ToString(CultureInfo.InvariantCulture) } } };

    private static bool StarAdmissionScalar(byte[] bytes, LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == type && entry.Value == value);

    private static (LegacyMsgUpdateDataType Type, ulong Value)[] StarAdmissionCosts(byte[][] frames) =>
        ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid).SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana)
            .Select(entry => (entry.Type, entry.Value)).ToArray();
}
