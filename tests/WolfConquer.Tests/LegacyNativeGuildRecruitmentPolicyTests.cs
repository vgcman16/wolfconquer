using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[CollectionDefinition("NativeGuildRecruitmentPolicy", DisableParallelization = true)]
public sealed class NativeGuildRecruitmentPolicyCollection { }

[Collection("NativeGuildRecruitmentPolicy")]
public sealed class LegacyNativeGuildRecruitmentPolicyTests
{
    public static IEnumerable<object[]> ClassRanges =>
    [
        [1000u, 1057u, 2u], [2000u, 2057u, 4u], [4000u, 4057u, 16u],
        [5000u, 5057u, 32u], [6000u, 6057u, 64u], [7000u, 7057u, 128u],
        [8000u, 8057u, 256u], [9000u, 9057u, 512u], [13002u, 13057u, 8192u],
        [14002u, 14057u, 16384u], [16000u, 16057u, 65536u],
    ];

    // Keep every original probe ID; known stages 52 now require their real
    // permission bit. Adjacent stage 58 remains outside this target boundary.
    [Theory]
    [InlineData(1000u, true)] [InlineData(1051u, true)] [InlineData(1052u, true)]
    [InlineData(2000u, true)] [InlineData(2051u, true)] [InlineData(4000u, true)]
    [InlineData(5000u, true)] [InlineData(6000u, true)] [InlineData(7000u, true)]
    [InlineData(8000u, true)] [InlineData(9000u, true)] [InlineData(13001u, false)]
    [InlineData(13002u, true)] [InlineData(14002u, true)] [InlineData(16000u, true)]
    [InlineData(16051u, true)] [InlineData(16052u, true)] [InlineData(0u, false)]
    [InlineData(1057u, true)] [InlineData(1058u, false)]
    [InlineData(16057u, true)] [InlineData(16058u, false)]
    public void FullRecruitmentPreservesEveryEstablishedClassRange(uint profession, bool expected)
    {
        var guild = CreateGuild();
        Assert.Equal(LegacyNativeGuildRuntime.ClassFlag.Full, guild.Info.Recruitment_Flag);
        Assert.Equal(expected, guild.Profession(profession));
    }

    [Theory]
    [MemberData(nameof(ClassRanges))]
    public void SelectedClassBitAdmitsBothEndpointsButNotNeighboringOutOfRangeIds(uint first, uint last, uint flag)
    {
        var guild = CreateGuild();
        guild.Info.Recruitment_Flag = (LegacyNativeGuildRuntime.ClassFlag)flag;
        Assert.True(guild.Profession(first));
        Assert.True(guild.Profession(last));
        Assert.False(guild.Profession(first - 1));
        Assert.False(guild.Profession(last + 1));
        guild.Info.Recruitment_Flag = LegacyNativeGuildRuntime.ClassFlag.None;
        Assert.False(guild.Profession(first));
        Assert.False(guild.Profession(last));
    }

    [Theory]
    [MemberData(nameof(ClassRanges))]
    public void UnselectedClassesCannotBorrowAnyOtherClassPermission(uint first, uint last, uint required)
    {
        var guild = CreateGuild();
        foreach (var row in ClassRanges)
        {
            var other = (uint)row[2];
            if (other == required) continue;
            guild.Info.Recruitment_Flag = (LegacyNativeGuildRuntime.ClassFlag)other;
            Assert.False(guild.Profession(first));
            Assert.False(guild.Profession(last));
        }
        // Removing only this bit from an otherwise Full mask must also reject.
        guild.Info.Recruitment_Flag = (LegacyNativeGuildRuntime.ClassFlag)(91126u & ~required);
        Assert.False(guild.Profession(first));
        Assert.False(guild.Profession(last));
    }

    [Theory]
    [InlineData(0u, 2u, false)]
    [InlineData(2u, 2u, true)]
    [InlineData(4u, 2u, false)]
    [InlineData(91126u, 65536u, true)]
    [InlineData(16384u, 65536u, false)]
    [InlineData(65536u, 16384u, false)]
    [InlineData(6u, 6u, true)]
    [InlineData(2u, 6u, false)]
    [InlineData(91126u, 0u, false)]
    [InlineData(91126u, 2147483648u, false)]
    [InlineData(2147483648u, 2u, false)]
    [InlineData(91126u, 91126u, true)]
    public void RecruitmentMaskQueriesRequireAllRequestedKnownNonzeroBits(uint stored, uint requested, bool expected)
    {
        var guild = CreateGuild();
        guild.Info.Recruitment_Flag = (LegacyNativeGuildRuntime.ClassFlag)stored;
        Assert.Equal(expected, guild.ContainsFlag((LegacyNativeGuildRuntime.ClassFlag)requested));
        Assert.Equal(stored, (uint)guild.Info.Recruitment_Flag);
    }

    [Fact]
    public void MissingRequiredGuildInfoCannotGrantAClassOrThrow()
    {
        var guild = CreateGuild();
        guild.Info = null!;
        Assert.False(guild.Profession(1000));
        Assert.False(guild.Profession(16000));
        Assert.False(guild.ContainsFlag(LegacyNativeGuildRuntime.ClassFlag.Trojan));
        Assert.Null(guild.Info);
    }

    [Fact]
    public void RealLoadOnlyBirthRetainsTheCompleteKnownClassMask()
    {
        var guild = CreateGuild();
        uint union = 0;
        foreach (var row in ClassRanges) union |= (uint)row[2];
        Assert.Equal(91126u, union);
        Assert.Equal(union, (uint)guild.Info.Recruitment_Flag);
        foreach (var row in ClassRanges) Assert.True(guild.Profession((uint)row[0]));
    }

    [Fact]
    public void PermissionQueriesDoNotMutateActualGuildMembersFundsConstructsOrPublication()
    {
        var guild = CreateGuild();
        guild.Info.SilverFund = 12345;
        guild.Info.ConquerPointFund = 678;
        guild.Info.Recruitment_Battle_Power = 321;
        guild.Members[17] = new() { UID = 17, MoneyDonate = 456, CpsDonate = 789 };
        var member = guild.Members[17];
        var info = guild.Info;
        var constructs = guild.Constructs.ToArray();
        var guilds = LegacyNativeGuildRuntime.GuildPoll.ToArray();
        var names = LegacyNativeGuildRuntime.ChangeNameRecords.ToArray();
        var counter = LegacyNativeGuildRuntime.Counter.Count;
        guild.Info.Recruitment_Flag = LegacyNativeGuildRuntime.ClassFlag.Windwalker;

        Assert.True(guild.Profession(16000));
        Assert.False(guild.Profession(14002));
        Assert.False(guild.ContainsFlag(LegacyNativeGuildRuntime.ClassFlag.None));

        Assert.Same(info, guild.Info);
        Assert.Equal(12345L, info.SilverFund);
        Assert.Equal(678u, info.ConquerPointFund);
        Assert.Equal(321u, info.Recruitment_Battle_Power);
        Assert.Equal(LegacyNativeGuildRuntime.ClassFlag.Windwalker, info.Recruitment_Flag);
        Assert.Same(member, Assert.Single(guild.Members).Value);
        Assert.Equal(456L, member.MoneyDonate);
        Assert.Equal(789u, member.CpsDonate);
        Assert.Equal(constructs, guild.Constructs.ToArray());
        Assert.Equal(guilds, LegacyNativeGuildRuntime.GuildPoll.ToArray());
        Assert.Equal(names, LegacyNativeGuildRuntime.ChangeNameRecords.ToArray());
        Assert.Equal(counter, LegacyNativeGuildRuntime.Counter.Count);
    }

    private static LegacyNativeGuildRuntime CreateGuild() => LegacyNativeGuildRuntime.CreateLoadedGuild("OwnedPolicy");
}
