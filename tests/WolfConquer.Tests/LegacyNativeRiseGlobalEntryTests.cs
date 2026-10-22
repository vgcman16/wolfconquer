using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRiseGlobalRideDenialPrecedesLearnedBookAndLeavesResourcesUnspent(bool learned)
    {
        using var fixture = new NativeRiseGlobalEntryFixture(learned: learned);
        await fixture.QueryAsync();
        await fixture.MountAsync();
        var hadStamp = fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var beforeStamp);
        var start = fixture.Order.Count;
        await fixture.CastRiseAsync();
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x32));
        Assert.Equal(hadStamp, fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var afterStamp));
        Assert.Equal(beforeStamp, afterStamp);
        fixture.AssertUnspent();
        fixture.AssertNoRiseSince(start);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var vector = Assert.Single(ParseMsgUpdates(fixture.FramesSince(uid, start)), packet =>
                packet.Uid == PlayerUid && packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
            Assert.Contains(vector.Updates, entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is { } bits && !IsLegacyStatusFlagSet(bits, 0x32));
        }
    }

    [Fact]
    public async Task NativeRiseGlobalMissingLearnedBookRejectsBeforeStampAndManualMpPrefix()
    {
        using var fixture = new NativeRiseGlobalEntryFixture(learned: false, mpPrefix: true);
        await fixture.QueryAsync();
        var start = fixture.Order.Count;
        await fixture.CastRiseAsync();
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        fixture.AssertUnspent();
        fixture.AssertNoRiseSince(start);
        Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
    }

    [Fact]
    public async Task NativeRiseGlobalEntryChargesElevenAndThirtySevenExactlyOnceWithoutColdTimePacket()
    {
        using var fixture = new NativeRiseGlobalEntryFixture();
        await fixture.QueryAsync();
        var start = fixture.Order.Count;
        await fixture.CastRiseAsync();
        fixture.AssertCompletedSince(start, expectedStamina: 89, expectedMana: 463, dead: false);
        Assert.Equal(new[]
        {
            (LegacyMsgUpdateDataType.Stamina, 89UL),
            (LegacyMsgUpdateDataType.Mana, 463UL),
        }, fixture.CostsSince(start));
    }

    [Fact]
    public async Task NativeRiseGlobalEarnedAttackStampRejectsRepeatUntilExactFiveHundredMilliseconds()
    {
        using var fixture = new NativeRiseGlobalEntryFixture(coldTime: 0);
        await fixture.QueryAsync();
        var first = fixture.Order.Count;
        await fixture.CastRiseAsync();
        fixture.AssertCompletedSince(first, 89, 463, dead: false);
        var originalStamp = fixture.Owner.Character.Attributes["LegacyAttackStampUtc"];
        foreach (var elapsed in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(499) })
        {
            fixture.Advance(elapsed);
            var start = fixture.Order.Count;
            await fixture.CastRiseAsync();
            Assert.Equal(originalStamp, fixture.Owner.Character.Attributes["LegacyAttackStampUtc"]);
            Assert.Equal("89", fixture.Owner.Character.Attributes["Stamina"]);
            Assert.Equal(463L, fixture.Owner.Character.MinMana);
            Assert.Empty(fixture.CostsSince(start));
            fixture.AssertNoRiseSince(start);
        }
        fixture.Advance(TimeSpan.FromMilliseconds(1));
        var accepted = fixture.Order.Count;
        await fixture.CastRiseAsync();
        fixture.AssertCompletedSince(accepted, 78, 426, dead: false);
        Assert.Equal(new[]
        {
            (LegacyMsgUpdateDataType.Stamina, 78UL),
            (LegacyMsgUpdateDataType.Mana, 426UL),
        }, fixture.CostsSince(accepted));
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("dead")]
    [InlineData("rebind")]
    public async Task NativeRiseGlobalManualMpCallbackCarriesOriginalClientAndPreparesOnlyOnce(string mutation)
    {
        using var fixture = new NativeRiseGlobalEntryFixture(mpPrefix: true, mutation: mutation);
        await fixture.QueryAsync();
        var start = fixture.Order.Count;
        fixture.Arm();
        await fixture.CastRiseAsync();
        fixture.Disarm();
        Assert.Equal(1, fixture.CallbackCount);
        if (mutation == "rebind")
        {
            fixture.AssertReplacementUntouched();
            return;
        }
        fixture.AssertCompletedSince(start, 89, 763, dead: mutation == "dead");
        Assert.Equal(mutation, fixture.Owner.Character.Attributes["RiseGlobalPrefixMarker"]);
        Assert.Equal((ushort)1_175, fixture.Owner.LegacyRandomSpell);
        Assert.Equal(new[]
        {
            (LegacyMsgUpdateDataType.Mana, 800UL),
            (LegacyMsgUpdateDataType.Stamina, 89UL),
            (LegacyMsgUpdateDataType.Mana, 763UL),
        }, fixture.CostsSince(start));
        Assert.Equal(new[]
        {
            $"{PlayerUid}:mana:800", $"{PlayerUid}:spell:1175", $"{TargetPlayerUid}:spell:1175",
            $"{PlayerUid}:stamina:89", $"{PlayerUid}:mana:763",
        }, fixture.PrefixAndCostTraceSince(start));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animation = Assert.Single(ParseSpellAnimations(fixture.FramesSince(uid, start)),
                packet => packet.SpellId == 1_175);
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(((ushort)830, (ushort)584), (animation.X, animation.Y));
            var target = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, target.Uid);
            Assert.Equal(300U, target.Damage);
            Assert.True(target.Hit);
        }
    }
}
