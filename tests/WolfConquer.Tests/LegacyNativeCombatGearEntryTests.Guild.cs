using System.Reflection;
using WolfConquer.Core.World;
using Guild = WolfConquer.Core.World.LegacyNativeGuildRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearEntryTests
{
    [Fact]
    public void LoadedGuildBirthCreatesTheActualNullClientStorageWithoutCounterOrGlobalPublication()
    {
        var counter = Guild.Counter;
        var before = counter.Count;
        var poll = Guild.GuildPoll;
        var pollCount = poll.Count;
        var changes = Guild.ChangeNameRecords;
        var changesCount = changes.Count;
        var name = new string("Loaded Guild".ToCharArray());

        var first = Guild.CreateLoadedGuild(name);
        var second = Guild.CreateLoadedGuild(name);

        Assert.Same(name, first.GuildName);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Info, second.Info);
        Assert.NotSame(first.Members, second.Members);
        Assert.NotSame(first.Ally, second.Ally);
        Assert.NotSame(first.Enemy, second.Enemy);
        Assert.NotSame(first.Constructs, second.Constructs);
        Assert.Empty(first.Members);
        Assert.Empty(first.Ally);
        Assert.Empty(first.Enemy);
        Assert.NotSame(first.Ally, first.Enemy);
        Assert.Empty(first.Message);
        Assert.Equal("None", first.Bulletin);
        Assert.True(first.CanSave);
        Assert.Equal(0u, first.GuildID);
        Assert.Same(counter, Guild.Counter);
        Assert.Equal(before, counter.Count);
        Assert.Same(poll, Guild.GuildPoll);
        Assert.Equal(pollCount, poll.Count);
        Assert.DoesNotContain(poll.Values, guild => ReferenceEquals(guild, first) || ReferenceEquals(guild, second));
        Assert.Same(changes, Guild.ChangeNameRecords);
        Assert.Equal(changesCount, changes.Count);
        Assert.Empty(typeof(Guild).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        // This is the explicit null-client branch, not full Guild construction or AddPlayer/membership.
    }

    [Fact]
    public void NullClientGuildInfoKeepsAllSourceDefaultsExceptTheFullRecruitmentFlag()
    {
        var guild = Guild.CreateLoadedGuild("Defaults");
        var info = guild.Info;
        Assert.Equal(0u, info.GuildID);
        Assert.Equal(0L, info.SilverFund);
        Assert.Equal(0u, info.ConquerPointFund);
        Assert.Equal(0u, info.MyRank);
        Assert.Equal(0u, info.Level);
        Assert.Equal(0u, info.CreateTime);
        Assert.Null(info.LeaderName);
        Assert.Equal(0u, info.Leaderid);
        Assert.Equal(0u, info.ArsenalBP);
        Assert.Equal(0ul, info.Material);
        Assert.Equal(0ul, info.Prestige);
        Assert.Equal(Guild.ClassFlag.Full, info.Recruitment_Flag);
        Assert.Equal(91126u, (uint)info.Recruitment_Flag);
        Assert.Equal(0u, info.Recruitment_Battle_Power);
        Assert.False(info.RecruitmentON);
        Assert.False(info.RecruitmentOFF);
    }

    [Fact]
    public void LoadedGuildHasAllFourteenDistinctNativeConstructsWithExactCompositeKeys()
    {
        var guild = Guild.CreateLoadedGuild("Constructs");
        var expected = new uint[] { 100, 200, 201, 202, 300, 301, 302, 303, 304, 305, 306, 400, 500, 600 };
        Assert.Equal(expected, guild.Constructs.Keys.OrderBy(key => key));
        Assert.Equal(14, guild.Constructs.Values.Distinct().Count());
        foreach (var (key, value) in guild.Constructs)
        {
            Assert.Equal(key / 100u, value.ID);
            Assert.Equal((ulong)(key % 100u), value.Beast);
            Assert.Equal(1u, value.Level);
            Assert.Equal(0ul, value.Exp);
            Assert.Equal(0ul, value.v2);
            Assert.Equal(0ul, value.v3);
            Assert.Equal(0ul, value.v4);
            Assert.Equal(key, (uint)(value.ID * 100 + value.Beast));
        }
        var explicitValues = new Guild.Construct(9, 8, 7, 6, 5, 4, 3);
        Assert.Equal(9u, explicitValues.ID);
        Assert.Equal(8u, explicitValues.Level);
        Assert.Equal(7ul, explicitValues.Beast);
        Assert.Equal(6ul, explicitValues.Exp);
        Assert.Equal(5ul, explicitValues.v2);
        Assert.Equal(4ul, explicitValues.v3);
        Assert.Equal(3ul, explicitValues.v4);
    }

    [Fact]
    public void GuildNameIsReadDirectlyIncludingNullAndReplacementGuildReferences()
    {
        using var scope = new NativeCombatGearQueryScope(false);
        var owner = PopulatedOwner();
        owner.Player.GuildID = 88;
        var gear = new LegacyNativeMyCombatGear(owner);
        Assert.Equal("", gear.GetEntry().GuildName);
        var actual = Guild.CreateLoadedGuild(null!);
        Assert.Null(actual.GuildName); // Native null-client constructor does not normalize the supplied name.
        owner.Player.MyGuild = actual;
        Assert.Null(gear.GetEntry().GuildName);
        var name = new string("A different guild".ToCharArray());
        actual.GuildName = name;
        Assert.Same(name, gear.GetEntry().GuildName);
        var replacement = Guild.CreateLoadedGuild("Replacement");
        owner.Player.MyGuild = replacement;
        Assert.Same(replacement.GuildName, gear.GetEntry().GuildName);
        Assert.Equal(88u, owner.Player.GuildID);
        Assert.Empty(actual.Members);
        Assert.Empty(replacement.Members);
    }

    [Fact]
    public void ActualMemberClassIsAnUnnormalizedMutableScalarStorageSlotNotAMembershipOperation()
    {
        var field = typeof(Guild.Member).GetField("Class", BindingFlags.Instance | BindingFlags.Public)!;
        Assert.Equal(typeof(uint), field.FieldType);
        Assert.False(field.IsInitOnly);
        Assert.Null(typeof(Guild.Member).GetProperty("Class"));
        var member = new Guild.Member();
        Assert.Equal(0u, member.Class);
        var alias = member;
        alias.Class = uint.MaxValue;
        Assert.Same(member, alias);
        Assert.Equal(uint.MaxValue, member.Class);
        Assert.False(member.IsOnline);
        Assert.Null(member.Name);
        Assert.Equal((byte)2, member.Graden);
        member.Mesh = 17;
        Assert.Equal((byte)1, member.Graden);
        member.CpsDonate = uint.MaxValue;
        member.VirtutePointes = 2;
        member.MoneyDonate = -2;
        member.ArsenalDonation = 3;
        member.PkDonation = 4;
        Assert.Equal(6ul, member.TotalDonation);
        Assert.Equal(uint.MaxValue, member.Class);
    }
}
