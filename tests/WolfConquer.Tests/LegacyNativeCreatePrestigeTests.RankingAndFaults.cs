using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking=WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Theory]
    [InlineData(1000u,true,1u)]
    [InlineData(10000u,false,0u)]
    [InlineData(10001u,false,0u)]
    [InlineData(10002u,false,0u)]
    [InlineData(13002u,true,8u)]
    [InlineData(14002u,true,9u)]
    public void ConfiguredServerIdentityAndLiteralClassExclusionsPublishSameRealEntryToExpectedRanks(uint job,bool classRank,uint rankType)
    {
        using var fixture=new Fixture();var client=fixture.Client;
        client.Player.ServerID=2;SavedField(client.Player,"_class",job);
        client.Equipment!.CurentEquip=[new(){ItemPoints=10000}];
        client.CreatePrestigePoints();
        var entry=client.PrestrigeEntry!;
        Assert.Equal(20000u,entry.TotalPoints);
        Assert.Equal((Ranking.Type)rankType,entry.type);
        Assert.Same(entry,Assert.Single(Ranking.Ranks[Ranking.Type.World].Items).Value);
        if(classRank)Assert.Same(entry,Assert.Single(Ranking.Ranks[(Ranking.Type)rankType].Items).Value);
        else Assert.All(Ranking.Ranks.Where(pair=>pair.Key!=Ranking.Type.World),pair=>Assert.Empty(pair.Value.Items));
        Assert.Equal(classRank?2:1,Ranking.Ranks.Values.Count(rank=>rank.Items.Count!=0));
    }

    [Fact]
    public void ActualHundredWeaponsScoreIsReadFromUnlockedOwnerMetadataAndNativeEquippedBit()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        SavedField(client.Player,"_class",1001u);SavedField(client.Player,"_level",(ushort)40);
        var type=LegacyNativeMagicType.WeaponsType.Blade;
        LegacyNativeHundredWeaponsTable.HundredWeaponsList[1]=new(){WeaponSubtype=type,Level=1,Attributes=new()};
        var row=new LegacyNativeHundredWeaponInfo(){WeaponSubtype=type,Level=1,Attributes=new(),BitVector=new(32)};
        row.BitVector.Add(1);client.HundredWeapons!.Objects[type]=row;client.HundredWeapons.Unlocked=true;
        client.CreatePrestigePoints();
        Assert.Equal(1000u,row.Score);Assert.Equal(1000u,client.PrestigePoints[38]);
        Assert.Equal(1400u,client.MyPrestigePoints);
        client.HundredWeapons.Unlocked=false;client.CreatePrestigePoints();
        Assert.Equal(0u,client.PrestigePoints[38]);
    }

    [Fact]
    public void MissingHundredWeaponsRefusesWithoutChangingClientOrRankOwners() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            client.HundredWeapons = null;
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void NullLaterEquipmentEntryRefusesWithoutPublishingItsEarlierPointPrefix() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            client.Equipment!.CurentEquip = [new() { ItemPoints = 17 }, null!];
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void MissingConfiguredIdentityRefusesBeforeChangingEntryRanksPerfectionOrGuild() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            LegacyNativeGroupServerList.MyServerInfo = null!;
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void MissingNamedRuneMetadataGrantsNoBonusAndAllowsCompletePrestigeAndGuildUpdate()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        client.Player.ServerID=2;client.Player.Reborn=2;SavedField(client.Player,"_level",(ushort)15);
        client.Player.MyGuild=LegacyNativeGuildRuntime.CreateLoadedGuild("Owned Guild");
        var member=new LegacyNativeGuildRuntime.Member(){UID=client.Player.UID,Name=client.Player.Name,PrestigePoints=777};
        client.Player.MyGuildMember=member;
        Assert.True(client.Player.MyGuild.Members.TryAdd(member.UID,member));
        client.PrestigeLevel=601;client.Equipment!.CurentEquip=[new(){ItemPoints=10000}];
        const uint id=4010001;
        LegacyNativePoolItems.ItemsBase![id]=new(){ID=id,Name="Sanctity"};
        var item=new LegacyNativeGameItemRuntime(){Uid=1,ITEM_ID=id,Position=101};
        Assert.True(client.Rune!.Add(item));
        var cache=client.Rune.Objects;
        var metadata=LegacyNativePoolItems.ItemsBase;
        Assert.True(LegacyNativePoolItems.ItemsBase.Remove(id)); // Real missing metadata only after genuine item birth.
        client.CreatePrestigePoints();
        var entry=client.PrestrigeEntry!;
        Assert.Equal(25150u,entry.TotalPoints);Assert.Equal(25150u,client.MyPrestigePoints);
        Assert.Same(entry,Ranking.Ranks[Ranking.Type.World].Items[41]);
        Assert.Same(entry,Ranking.Ranks[Ranking.Type.WaterTao].Items[41]);
        Assert.Equal(200d,client.PerfectionStatus.ToxinEraser);Assert.Equal(8d,client.PerfectionStatus.DivineGuard);
        Assert.Same(member,client.Player.MyGuildMember);Assert.Equal(25150u,member.PrestigePoints);
        Assert.Equal(client.PrestigePoints,entry.Points);Assert.NotSame(client.PrestigePoints,entry.Points);
        Assert.Same(metadata,LegacyNativePoolItems.ItemsBase);Assert.False(metadata!.ContainsKey(id));
        Assert.Equal(cache,client.Rune.Objects);Assert.Same(item,Assert.Single(cache));
        Assert.Equal(id,item.ITEM_ID);Assert.Equal((ushort)101,item.Position);Assert.Equal(1u,item.Uid);
    }

    [Fact]
    public void SuccessfulPerfectionRebuildThenWritesActualGuildMemberTotalNotAnotherProjection()
    {
        using var fixture=new Fixture();var client=fixture.Client;
        client.Player.MyGuild=LegacyNativeGuildRuntime.CreateLoadedGuild("Owned Guild");
        var member=new LegacyNativeGuildRuntime.Member(){UID=client.Player.UID,Name=client.Player.Name,PrestigePoints=777};client.Player.MyGuildMember=member;
        Assert.True(client.Player.MyGuild.Members.TryAdd(member.UID,member));
        client.Equipment!.CurentEquip=[new(){ItemPoints=10000}];client.PrestigeLevel=601;
        client.CreatePrestigePoints();
        Assert.Same(member,client.Player.MyGuildMember);Assert.Equal(20000u,member.PrestigePoints);
        Assert.Equal(200d,client.PerfectionStatus.ToxinEraser);
        Assert.Equal(7d,client.PerfectionStatus.AbsoluteLuck);
    }
}
