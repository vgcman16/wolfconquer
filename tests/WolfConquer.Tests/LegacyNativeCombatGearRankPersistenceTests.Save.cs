using System.Text;
using WolfConquer.Core.Data;
using RankTable = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearRankPersistenceTests
{
    [Fact]
    public void SaveIgnoresLoadGateAndUsesRankAndEntryValuesOrderIncludingAliases()
    {
        using var files = new NativeRankFileScope();
        LegacyNativeCombatGearTable.m_Load = false;
        var shared = Entry(7);
        var first = Place(shared, mapKey: 900, rankKey: 42);
        first._Type = (RankTable.Type)99;
        first.RankingList.Add(800, shared);
        Place(Entry(3), mapKey: 700, rankKey: 19);
        RankTable.Save();
        string[] lines = File.ReadAllLines(files.RankPath);
        Assert.Equal("Count=3", lines[0]);
        Assert.Equal(FullRow.Replace("41/Hero/", "7/Hero/", StringComparison.Ordinal), lines[1]);
        Assert.Equal(lines[1], lines[2]);
        Assert.Equal(FullRow.Replace("41/Hero/", "3/Hero/", StringComparison.Ordinal), lines[3]);
        Assert.Same(shared, first.RankingList[900]);
        Assert.Same(shared, first.RankingList[800]);
    }

    [Theory]
    [InlineData(275u, "19")]
    [InlineData(uint.MaxValue, "255")]
    public void SaveNarrowsEntryTypeToByteWithoutChangingStoredEnum(uint rawType, string encodedType)
    {
        using var files = new NativeRankFileScope();
        var entry = Entry();
        entry.Type = (RankTable.Type)rawType;
        Place(entry);
        RankTable.Save();
        Assert.Equal(encodedType, File.ReadAllLines(files.RankPath)[1].Split('/')[2]);
        Assert.Equal(rawType, (uint)entry.Type);
        if (rawType == 275)
        {
            RankTable.Ranks = new();
            RankTable.Load("");
            Assert.Equal((RankTable.Type)19, RankTable.Ranks[(RankTable.Type)19].RankingList[41].Type);
        }
    }

    [Fact]
    public void SaveLeavesSlashesAndEmptyGuildUnescapedWithEveryTrailingDelimiter()
    {
        using var files = new NativeRankFileScope();
        var entry = Entry();
        entry.Name = "A/B";
        entry.GuildName = "";
        var rank = Place(entry);
        var second = Entry(42);
        second.GuildName = "G/H";
        rank.RankingList.Add(42, second);
        RankTable.Save();
        Assert.Equal(NativeRankFileScope.Text("Count=2",
            "41/A/B/19/404/505/206/707/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020//2222/",
            "42/Hero/19/404/505/206/707/808/909/1010/1111/1212/1313/1414/1515/1616/1717/1818/1919/2020/G/H/2222/"),
            File.ReadAllText(files.RankPath));
    }

    [Fact]
    public void LateNullRankListEntryNameOrGuildFaultsBeforeExecuteAndPreservesDisk()
    {
        using var files = new NativeRankFileScope();
        byte[] sentinel = Encoding.UTF8.GetBytes("existing rank bytes\r\nnot rewritten");
        foreach (string fault in new[] { "rank", "list", "entry", "name", "guild" })
        {
            RankTable.Ranks = new();
            Place(Entry()); // A complete first row has already been constructed at each later fault.
            var bad = new RankTable.Rank((RankTable.Type)20) { MaxItems = 10 };
            RankTable.Ranks.Add((RankTable.Type)20, bad);
            switch (fault)
            {
                case "rank": RankTable.Ranks[(RankTable.Type)20] = null!; break;
                case "list": bad.RankingList = null!; break;
                case "entry": bad.RankingList.Add(42, null!); break;
                case "name": var name = Entry(42); name.Name = null!; bad.RankingList.Add(42, name); break;
                case "guild": var guild = Entry(42); guild.GuildName = null!; bad.RankingList.Add(42, guild); break;
            }
            File.WriteAllBytes(files.RankPath, sentinel);
            Assert.Throws<NullReferenceException>(RankTable.Save);
            Assert.Equal(sentinel, File.ReadAllBytes(files.RankPath));
            File.Delete(files.RankPath);
            Assert.Throws<NullReferenceException>(RankTable.Save);
            Assert.False(File.Exists(files.RankPath));
        }
    }

    [Fact]
    public void SaveAgainstAnExclusivelyHeldFileFailsWithoutClearingDiskOrRankAliases()
    {
        using var files = new NativeRankFileScope();
        byte[] sentinel = Encoding.UTF8.GetBytes("locked original");
        File.WriteAllBytes(files.RankPath, sentinel);
        var entry = Entry();
        var rank = Place(entry);
        var rows = rank.RankingList;
        using (var locked = new FileStream(files.RankPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.ThrowsAny<IOException>(RankTable.Save);
        Assert.Equal(sentinel, File.ReadAllBytes(files.RankPath));
        Assert.Same(rows, rank.RankingList);
        Assert.Same(entry, rank.RankingList[41]);
    }
}
