using WolfConquer.Core.World;
using Guild = WolfConquer.Core.World.LegacyNativeGuildRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGuildRecruitmentTargetTests
{
    private static Guild CreateTargetGuild()
    {
        var guild = Guild.CreateLoadedGuild("TargetRecruitment");
        Assert.Equal(Guild.ClassFlag.Full, guild.Info.Recruitment_Flag);
        guild.Info.SilverFund = 12345;
        guild.Info.ConquerPointFund = 678;
        guild.Info.Recruitment_Battle_Power = 321;
        guild.Members[17] = new() { UID = 17, MoneyDonate = 456, CpsDonate = 789, PrestigePoints = 4321 };
        return guild;
    }

    private sealed class RecruitmentState(Guild guild)
    {
        private readonly LegacyNativeMsgGuildInformation? info = guild.Info;
        private readonly object?[] infoValues = InfoValues(guild.Info);
        private readonly object members = guild.Members;
        private readonly KeyValuePair<uint, Guild.Member>[] memberRows = guild.Members.ToArray();
        private readonly object?[][] memberValues = guild.Members.Select(row =>
            new object?[] { row.Value.UID, row.Value.Name, row.Value.Class, row.Value.MoneyDonate,
                row.Value.CpsDonate, row.Value.PrestigePoints }).ToArray();
        private readonly object constructs = guild.Constructs;
        private readonly KeyValuePair<uint, Guild.Construct>[] constructRows = guild.Constructs.ToArray();
        private readonly object?[][] constructValues = guild.Constructs.Select(row =>
            new object?[] { row.Value.ID, row.Value.Level, row.Value.Exp, row.Value.Beast,
                row.Value.v2, row.Value.v3, row.Value.v4 }).ToArray();
        private readonly object poll = Guild.GuildPoll;
        private readonly KeyValuePair<uint, Guild>[] published = Guild.GuildPoll.ToArray();
        private readonly object names = Guild.ChangeNameRecords;
        private readonly KeyValuePair<uint, string>[] nameRows = Guild.ChangeNameRecords.ToArray();
        private readonly object counter = Guild.Counter;
        private readonly uint counterValue = Guild.Counter.Count;
        private readonly object?[] guildValues = GuildValues(guild);

        internal void RequireUnchanged()
        {
            Assert.Same(info, guild.Info);
            Assert.Equal(infoValues, InfoValues(guild.Info));
            Assert.Equal(guildValues, GuildValues(guild));
            Assert.Same(members, guild.Members);
            Assert.Equal(memberRows, guild.Members.ToArray());
            for (int index = 0; index < memberRows.Length; index++)
            {
                var row = memberRows[index].Value;
                Assert.Equal(memberValues[index], new object?[] { row.UID, row.Name, row.Class,
                    row.MoneyDonate, row.CpsDonate, row.PrestigePoints });
            }
            Assert.Same(constructs, guild.Constructs);
            Assert.Equal(constructRows, guild.Constructs.ToArray());
            for (int index = 0; index < constructRows.Length; index++)
            {
                var row = constructRows[index].Value;
                Assert.Equal(constructValues[index], new object?[]
                    { row.ID, row.Level, row.Exp, row.Beast, row.v2, row.v3, row.v4 });
            }
            Assert.Same(poll, Guild.GuildPoll);
            Assert.Equal(published, Guild.GuildPoll.ToArray());
            Assert.Same(names, Guild.ChangeNameRecords);
            Assert.Equal(nameRows, Guild.ChangeNameRecords.ToArray());
            Assert.Same(counter, Guild.Counter);
            Assert.Equal(counterValue, Guild.Counter.Count);
        }
    }

    private static object?[] InfoValues(LegacyNativeMsgGuildInformation? info) => info is null ? [] :
        [info.Recruitment_Flag, info.SilverFund, info.ConquerPointFund, info.Recruitment_Battle_Power];

    private static object?[] GuildValues(Guild guild) =>
        [guild.GuildID, guild.GuildName, guild.UnionID, guild.CommandID, guild.CanSave,
            guild.ClaimCtfReward, guild.CTF_Exploits, guild.CTF_Next_ConquerPoints,
            guild.CTF_Next_Money, guild.CTF_Rank, guild.Bulletin, guild.AllowAlly, guild.BuletinEnrole];
}
