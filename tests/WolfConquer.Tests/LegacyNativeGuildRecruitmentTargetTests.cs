using Guild = WolfConquer.Core.World.LegacyNativeGuildRuntime;

namespace WolfConquer.Tests;

[Collection("NativeGuildRecruitmentPolicy")]
public sealed partial class LegacyNativeGuildRecruitmentTargetTests
{
    public static IEnumerable<object[]> TargetRecruitmentStages()
    {
        foreach (var family in PrestigeTargetClassFixture.Families)
        for (uint job = family.Last - 5; job <= family.Last; job++)
            yield return [job, (uint)PermissionFor(family.Category)];
    }

    [Fact]
    public void EveryConfirmedTargetIdHasItsExistingRecruitmentPermission()
    {
        var guild = CreateTargetGuild();
        uint observed = 0;
        foreach (var family in PrestigeTargetClassFixture.Families)
        {
            guild.Info.Recruitment_Flag = PermissionFor(family.Category);
            var before = new RecruitmentState(guild);
            for (uint job = family.First; job <= family.Last; job++)
            {
                Assert.True(guild.Profession(job));
                observed++;
            }
            before.RequireUnchanged();
        }
        Assert.Equal(634u, observed);
    }

    [Theory]
    [MemberData(nameof(TargetRecruitmentStages))]
    public void TargetStagesCannotBorrowAnUnselectedPermissionAndQueriesLeaveRealGuildStateUntouched(
        uint profession, uint permission)
    {
        var guild = CreateTargetGuild();
        uint wrong = permission == (uint)Guild.ClassFlag.Trojan
            ? (uint)Guild.ClassFlag.Warrior : (uint)Guild.ClassFlag.Trojan;
        foreach (uint mask in new[] { 0u, wrong, (uint)Guild.ClassFlag.Full & ~permission, 0x80000000u })
        {
            guild.Info.Recruitment_Flag = (Guild.ClassFlag)mask;
            var before = new RecruitmentState(guild);

            Assert.False(guild.Profession(profession));

            before.RequireUnchanged();
        }
        foreach (uint mask in new[] { permission, (uint)Guild.ClassFlag.Full })
        {
            guild.Info.Recruitment_Flag = (Guild.ClassFlag)mask;
            var before = new RecruitmentState(guild);

            Assert.True(guild.Profession(profession));

            before.RequireUnchanged();
        }
    }

    [Fact]
    public void WorldOnlyUnmodeledAndAdjacentUnsupportedJobsRemainRefusedEvenWithEveryStoredBit()
    {
        var guild = CreateTargetGuild();
        var jobs = PrestigeTargetClassFixture.StrictRefusals().Select(row => (uint)row[0]);
        foreach (uint mask in new[] { (uint)Guild.ClassFlag.Full, uint.MaxValue })
        {
            guild.Info.Recruitment_Flag = (Guild.ClassFlag)mask;
            var before = new RecruitmentState(guild);

            foreach (uint job in jobs) Assert.False(guild.Profession(job));

            before.RequireUnchanged();
        }
    }

    [Theory]
    [MemberData(nameof(TargetRecruitmentStages))]
    public void MissingRequiredGuildInfoDoesNotBecomePermissionForAnExtendedStage(uint profession, uint permission)
    {
        var guild = CreateTargetGuild();
        guild.Info.Recruitment_Flag = (Guild.ClassFlag)permission;
        guild.Info = null!; // Corrupt the genuine required slot, never replace it with an accepting owner.
        var before = new RecruitmentState(guild);

        Assert.False(guild.Profession(profession));

        before.RequireUnchanged();
    }

    private static Guild.ClassFlag PermissionFor(uint category) => category switch
    {
        1 => Guild.ClassFlag.Trojan,
        2 => Guild.ClassFlag.Warrior,
        3 => Guild.ClassFlag.Archer,
        4 => Guild.ClassFlag.Ninja,
        5 => Guild.ClassFlag.Monk,
        6 => Guild.ClassFlag.Pirate,
        7 => Guild.ClassFlag.DragonWarrior,
        8 => Guild.ClassFlag.Water,
        9 => Guild.ClassFlag.Fire,
        10 => Guild.ClassFlag.Windwalker,
        11 => Guild.ClassFlag.Thunderstriker,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };
}
