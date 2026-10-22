using System.Globalization;
using WolfConquer.Core.World;
using Quest = WolfConquer.Core.Data.LegacyNativeQuestInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeQuestMetadataTests
{
    [Fact]
    public void NativeDefaultsAndMissingIniDoNotInventArraysOrClearExistingCatalogs()
    {
        using var scope = new Scope(); var row = new Quest.DBQuest();
        Assert.Equal((byte)0, row.TypeId); Assert.Equal((byte)0, row.CompleteFlag); Assert.Equal(0u, row.ActivityType);
        Assert.Equal((ushort)0, row.MissionId); Assert.Equal("", row.Name);
        Assert.Equal((ushort)0, row.Lv_min); Assert.Equal((ushort)0, row.Lv_max); Assert.Equal((ushort)0, row.First); Assert.Equal((ushort)0, row.Map);
        Assert.Null(row.Profession); Assert.Equal(0ul, row.Exp); Assert.Equal(0ul, row.Gold); Assert.Equal((byte)0, row.Sex);
        Assert.Equal(0u, row.FinishTime); Assert.Equal(0u, row.ActivityBeginTime); Assert.Equal(0u, row.ActivityEndTime);
        Assert.Null(row.BeginNpcId); Assert.Null(row.FinishNpcId); Assert.Equal(0u, row.Intentions); Assert.Null(row.Prize); Assert.Null(row.Prequest);
        Quest.AllQuests.Add(17, row); Npc(8, row);
        var all = Quest.AllQuests; var npcs = Quest.NpcQuests;
        Quest.Init(); Assert.Same(all, Quest.AllQuests); Assert.Same(npcs, Quest.NpcQuests);
        Assert.Same(row, Quest.AllQuests[17]); Assert.Same(row, Assert.Single(Quest.NpcQuests[8].Quests));
    }

    [Fact]
    public void InitReadsEveryReachedFieldButLeavesDeclaredUnusedIniFieldsAtNativeDefaults()
    {
        using var scope = new Scope(); scope.Seed(Ini(3,
            "[1]\r\nTypeId=255\r\nCompleteFlag=254\r\nActivityType=253\r\nMissionId=65535\r\nIntentAmount=65534\r\n" +
            "Prequest=1|65535\r\nProfession=1005,4294967295\r\nPrize=[a 100,1][b 200,2]\r\n" +
            "BeginNpcId=4294967295,65535,65534,65533,Start,Town\r\nFinishNpcId=7,8,9,10,Finish,Field\r\n" +
            "Name=Ignored\r\nLv_min=99\r\nLv_max=100\r\nFirst=8\r\nMap=9\r\nExp=999\r\nGold=999\r\nSex=2\r\nFinishTime=9\r\nActivityBeginTime=9\r\nActivityEndTime=9\r\n",
            "[2]\r\n", "[3]\r\nMissionId=777\r\n"));
        Quest.Init(); Assert.Equal(2, Quest.AllQuests.Count); Assert.False(Quest.AllQuests.ContainsKey(777));
        var row = Quest.AllQuests[65535];
        Assert.Equal((byte)255, row.TypeId); Assert.Equal((byte)254, row.CompleteFlag); Assert.Equal(253u, row.ActivityType);
        Assert.Equal(65534u, row.Intentions); Assert.Equal(new ushort[] { 1, 65535 }, row.Prequest);
        Assert.Equal(new uint[] { 1005, uint.MaxValue }, row.Profession); Assert.Equal(new uint[] { 100, 200 }, row.Prize);
        Assert.Equal(uint.MaxValue, row.BeginNpcId.ID); Assert.Equal(ushort.MaxValue, row.BeginNpcId.Map);
        Assert.Equal((ushort)65534, row.BeginNpcId.X); Assert.Equal((ushort)65533, row.BeginNpcId.Y);
        Assert.Equal("Start", row.BeginNpcId.Name); Assert.Equal("Town", row.BeginNpcId.MapName);
        Assert.Equal(7u, row.FinishNpcId.ID); Assert.Equal((ushort)8, row.FinishNpcId.Map);
        Assert.Equal((ushort)9, row.FinishNpcId.X); Assert.Equal((ushort)10, row.FinishNpcId.Y);
        Assert.Equal("Finish", row.FinishNpcId.Name); Assert.Equal("Field", row.FinishNpcId.MapName);
        Assert.Equal("", row.Name); Assert.Equal((ushort)0, row.Lv_min); Assert.Equal((ushort)0, row.Lv_max);
        Assert.Equal((ushort)0, row.First); Assert.Equal((ushort)0, row.Map); Assert.Equal(0ul, row.Exp); Assert.Equal(0ul, row.Gold);
        Assert.Equal((byte)0, row.Sex); Assert.Equal(0u, row.FinishTime); Assert.Equal(0u, row.ActivityBeginTime); Assert.Equal(0u, row.ActivityEndTime);
        var blank = Quest.AllQuests[0]; Assert.Equal(new ushort[] { 0 }, blank.Prequest); Assert.Equal(new uint[] { 0 }, blank.Profession);
        Assert.Null(blank.Prize); Assert.NotSame(blank.BeginNpcId, blank.FinishNpcId);
        Assert.Same(row, Assert.Single(Quest.NpcQuests[uint.MaxValue].Quests));
        Assert.Same(blank, Assert.Single(Quest.NpcQuests[0].Quests));
    }

    [Fact]
    public void LiteralConfiguredPrefixChoosesActualIniAndTypedOverflowFallsBackToZero()
    {
        using var scope = new Scope(); scope.Seed(Ini(2, "[1]\r\nMissionId=99\r\n"));
        scope.Seed(Ini(2, "[1]\r\nMissionId=70000\r\nTypeId=256\r\nCompleteFlag=-1\r\nActivityType=256\r\nIntentAmount=70000\r\n"), true);
        LegacyNativeServerConfig.DbLocation = Path.Combine(scope.Root, "prefix");
        Assert.Equal(LegacyNativeServerConfig.DbLocation + "\\Questinfo.ini", new LegacyNativeItemCounterShellIni("\\Questinfo.ini").FileName);
        Quest.Init(); var row = Assert.Single(Quest.AllQuests).Value;
        Assert.Equal((ushort)0, row.MissionId); Assert.Equal((byte)0, row.TypeId); Assert.Equal((byte)0, row.CompleteFlag);
        Assert.Equal(0u, row.ActivityType); Assert.Equal(0u, row.Intentions); Assert.False(Quest.AllQuests.ContainsKey(99));
    }

    [Fact]
    public void DuplicateMissionKeepsFirstGlobalObjectButAppendsDistinctNpcRowsOnEveryInit()
    {
        using var scope = new Scope(); scope.Seed(Ini(3,
            "[1]\r\nMissionId=17\r\nTypeId=1\r\nBeginNpcId=5,1,2,3,A,B\r\n",
            "[2]\r\nMissionId=17\r\nTypeId=2\r\nBeginNpcId=5,1,2,3,C,D\r\n"));
        var retained = Row(88, 1); Quest.AllQuests.Add(88, retained); Npc(88, retained);
        var all = Quest.AllQuests; var npcs = Quest.NpcQuests;
        Quest.Init(); var first = Quest.AllQuests[17]; var list = Quest.NpcQuests[5].Quests;
        Assert.Equal(2, list.Count); Assert.Same(first, list[0]); Assert.NotSame(first, list[1]); Assert.Equal((byte)2, list[1].TypeId);
        first.TypeId = 71; Assert.Equal((byte)71, list[0].TypeId);
        Quest.Init(); Assert.Same(all, Quest.AllQuests); Assert.Same(npcs, Quest.NpcQuests); Assert.Same(first, Quest.AllQuests[17]);
        Assert.Same(list, Quest.NpcQuests[5].Quests); Assert.Equal(4, list.Count); Assert.NotSame(list[0], list[2]); Assert.NotSame(list[1], list[3]);
        Assert.Same(retained, Quest.AllQuests[88]); Assert.Same(retained, Assert.Single(Quest.NpcQuests[88].Quests));
    }

    [Fact]
    public void CatalogFaultOrderPublishesGlobalQuestBeforeNpcCatalogAccessButNotTheReverse()
    {
        using var scope = new Scope(); scope.Seed(Ini(2, "[1]\r\nMissionId=17\r\nBeginNpcId=5,1,2,3,N,M\r\n"));
        var npcs = Quest.NpcQuests; Quest.AllQuests = null!;
        Assert.Throws<NullReferenceException>(() => Quest.Init()); Assert.Same(npcs, Quest.NpcQuests); Assert.Empty(npcs);
        Quest.AllQuests = new(); Quest.NpcQuests = null!;
        Assert.Throws<NullReferenceException>(() => Quest.Init());
        var row = Assert.Single(Quest.AllQuests).Value;
        Assert.Equal((ushort)17, row.MissionId); Assert.Equal(5u, row.BeginNpcId.ID); Assert.Null(Quest.NpcQuests);
    }

    [Theory]
    [InlineData("Prequest=1||2")]
    [InlineData("Prequest=1,2|3")]
    [InlineData("Profession=1|2")]
    public void UncaughtArrayParseFaultRetainsOnlyPreviouslyPublishedRowsAndNeverClears(string bad)
    {
        using var scope = new Scope(); scope.Seed(Ini(4, "[1]\r\nMissionId=1\r\nBeginNpcId=5,1,2,3,A,B\r\n",
            "[2]\r\nMissionId=2\r\n" + bad + "\r\n", "[3]\r\nMissionId=3\r\n"));
        Assert.Throws<FormatException>(() => Quest.Init());
        Assert.Equal((ushort)1, Assert.Single(Quest.AllQuests).Value.MissionId);
        Assert.Same(Quest.AllQuests[1], Assert.Single(Assert.Single(Quest.NpcQuests).Value.Quests));
    }

    [Fact]
    public void PrequestCommaSingleAndPrizeCaughtPrefixKeepNativeDistinctArrayShapes()
    {
        using var scope = new Scope(); scope.Seed(Ini(4,
            "[1]\r\nMissionId=1\r\nPrequest=7,8\r\nPrize=[a 100,1][malformed][b 200,2]\r\n",
            "[2]\r\nMissionId=2\r\nPrequest=9\r\nProfession=1001\r\nPrize=not bracketed\r\n",
            "[3]\r\nMissionId=3\r\nPrize=[\r\n"));
        Quest.Init(); Assert.Equal(new ushort[] { 7, 8 }, Quest.AllQuests[1].Prequest);
        Assert.Equal(new uint[] { 100, 0, 0 }, Quest.AllQuests[1].Prize);
        Assert.Equal(new ushort[] { 9 }, Quest.AllQuests[2].Prequest); Assert.Equal(new uint[] { 1001 }, Quest.AllQuests[2].Profession);
        Assert.Null(Quest.AllQuests[2].Prize); Assert.Equal(new uint[] { 0 }, Quest.AllQuests[3].Prize);
    }

    [Fact]
    public void BothIniConversionsAndDirectArrayNpcParsersUseActualCurrentCulture()
    {
        using var scope = new Scope(); var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone(); culture.NumberFormat.PositiveSign = "~";
        CultureInfo.CurrentCulture = culture;
        scope.Seed("[TotalMission]\r\nTotalMission=~2\r\n[1]\r\nMissionId=~17\r\nTypeId=~7\r\nActivityType=~8\r\n" +
            "IntentAmount=~9\r\nPrequest=~10|~11\r\nProfession=~1005,~3005\r\nPrize=[a ~700,1]\r\nBeginNpcId=~5,~6,~7,~8,N,M\r\n");
        Quest.Init(); var row = Assert.Single(Quest.AllQuests).Value;
        Assert.Equal((ushort)17, row.MissionId); Assert.Equal((byte)7, row.TypeId); Assert.Equal(8u, row.ActivityType); Assert.Equal(9u, row.Intentions);
        Assert.Equal(new ushort[] { 10, 11 }, row.Prequest); Assert.Equal(new uint[] { 1005, 3005 }, row.Profession);
        Assert.Equal(new uint[] { 700 }, row.Prize); Assert.Equal(5u, row.BeginNpcId.ID); Assert.Equal((ushort)6, row.BeginNpcId.Map);
        Assert.Equal((ushort)7, row.BeginNpcId.X); Assert.Equal((ushort)8, row.BeginNpcId.Y);
    }
}
