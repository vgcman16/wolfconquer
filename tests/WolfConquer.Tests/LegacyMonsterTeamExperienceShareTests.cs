using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterTeamExperienceShareTests
{
    [Fact]
    public void CalculateBaseShareDividesTargetFamilyMaxHealthByFifty()
    {
        Assert.Equal(466, LegacyMonsterTeamExperienceShare.CalculateBaseShare(23_346));
    }

    [Fact]
    public void CalculateComputedShareDoublesWhenAliveNewbieIsOnTargetMapWithinInclusiveEighteenTiles()
    {
        var target = Target(familyMaxHealth: 23_346, level: 83, mapId: 1000, x: 500, y: 500);
        var members = new[]
        {
            Member(uid: 1, level: 62, mapId: 1000, x: 518, y: 482, alive: true),
        };

        var share = LegacyMonsterTeamExperienceShare.CalculateComputedShare(target, members);

        Assert.Equal(932, share);
    }

    [Fact]
    public void CalculateComputedShareKeepsBaseWhenNewbiePredicateIsNotSatisfied()
    {
        var target = Target(familyMaxHealth: 23_346, level: 83, mapId: 1000, x: 500, y: 500);
        var members = new[]
        {
            Member(uid: 1, level: 62, mapId: 1000, x: 518, y: 500, alive: false),
            Member(uid: 2, level: 62, mapId: 1001, x: 518, y: 500, alive: true),
            Member(uid: 3, level: 62, mapId: 1000, x: 519, y: 500, alive: true),
            Member(uid: 4, level: 63, mapId: 1000, x: 518, y: 500, alive: true),
        };

        var share = LegacyMonsterTeamExperienceShare.CalculateComputedShare(target, members);

        Assert.Equal(466, share);
    }

    [Fact]
    public void IsRecipientEligibleRequiresNonKillerAliveSameKillerMapWithinInclusiveEighteenTiles()
    {
        var killer = Member(uid: 10, level: 100, mapId: 1002, x: 400, y: 400, alive: true);
        var recipient = Member(uid: 11, level: 80, mapId: 1002, x: 418, y: 382, alive: true);

        Assert.True(LegacyMonsterTeamExperienceShare.IsRecipientEligible(recipient, killer));
        Assert.False(LegacyMonsterTeamExperienceShare.IsRecipientEligible(killer, killer));
        Assert.False(LegacyMonsterTeamExperienceShare.IsRecipientEligible(
            recipient with { Alive = false },
            killer));
        Assert.False(LegacyMonsterTeamExperienceShare.IsRecipientEligible(
            recipient with { MapId = 1003 },
            killer));
        Assert.False(LegacyMonsterTeamExperienceShare.IsRecipientEligible(
            recipient with { X = 419, Y = 400 },
            killer));
    }

    [Fact]
    public void CalculateRecipientAmountDoublesSpouseAndReturnsZeroWhenIneligible()
    {
        var killer = Member(uid: 10, level: 100, mapId: 1002, x: 400, y: 400, alive: true);
        var spouse = Member(uid: 20, level: 80, mapId: 1002, x: 418, y: 400, alive: true);
        var farRecipient = spouse with { Uid = 21, X = 419 };

        Assert.Equal(1_864, LegacyMonsterTeamExperienceShare.CalculateRecipientAmount(932, spouse, killer, killerSpouseUid: 20));
        Assert.Equal(932, LegacyMonsterTeamExperienceShare.CalculateRecipientAmount(932, spouse, killer, killerSpouseUid: 99));
        Assert.Equal(0, LegacyMonsterTeamExperienceShare.CalculateRecipientAmount(932, farRecipient, killer, killerSpouseUid: 21));
    }

    private static LegacyMonsterTeamExperienceShareTarget Target(
        int familyMaxHealth,
        int level,
        int mapId,
        ushort x,
        ushort y)
    {
        return new LegacyMonsterTeamExperienceShareTarget(familyMaxHealth, level, mapId, x, y);
    }

    private static LegacyMonsterTeamExperienceShareMember Member(
        uint uid,
        int level,
        int mapId,
        ushort x,
        ushort y,
        bool alive)
    {
        return new LegacyMonsterTeamExperienceShareMember(uid, level, mapId, x, y, alive);
    }
}
