using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LegacyGuildConstructTableTests
{
    [Fact]
    public void ParseConstructLineReadsLegacySevenFieldConstructRows()
    {
        var constructs = LegacyGuildConstructTable.ParseConstructLine(
            "2/2/20/0/1/0/0/0/1/20/0/0/0/0/0/");

        Assert.Equal(2, constructs.Count);
        Assert.Equal(2U, constructs[0].Id);
        Assert.Equal(20U, constructs[0].Level);
        Assert.Equal(0UL, constructs[0].Experience);
        Assert.Equal(1U, constructs[0].Beast);
        Assert.Equal(201, constructs[0].FormationType);
        Assert.Equal(1U, constructs[1].Id);
        Assert.Equal(20U, constructs[1].Level);
        Assert.Equal(100, constructs[1].FormationType);
    }

    [Fact]
    public void TryLoadGuildConstructsByUsersRootFindsConstructRowBesideUsersDirectory()
    {
        var root = CreateTempRoot();
        try
        {
            var usersRoot = Path.Combine(root, "DBServer", "Users");
            var guildsRoot = Path.Combine(root, "DBServer", "Guilds");
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(guildsRoot);
            File.WriteAllLines(
                Path.Combine(guildsRoot, "1363.txt"),
                [
                    "Count=10",
                    "1363/fredoome/9/elkomy/46998100/0/20221217/None/0/0/1296374/0/0/91126/0/0/0/15/1296374/",
                    "0/",
                    "0/",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0",
                    "1/STR_SYNDICATE_EVENT_UPGRADE_BUILDING@@Gate@@20@@/1671289441/",
                    "3/2/20/0/1/0/0/0/1/20/0/0/0/0/0/3/1/0/4/0/0/0/",
                ]);

            var loaded = LegacyGuildConstructTable.TryLoadGuildConstructsByUsersRoot(
                usersRoot,
                1_363,
                out var constructs);

            Assert.True(loaded);
            Assert.Equal(3, constructs.Count);
            Assert.True(LegacyGuildConstructTable.TryFindConstruct(constructs, 1, out var gate));
            Assert.Equal(20U, gate.Level);
            Assert.Equal(100, gate.FormationType);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void TryParseConstructLineRejectsNonConstructGuildRows()
    {
        Assert.False(LegacyGuildConstructTable.TryParseConstructLine("Count=10", out _));
        Assert.False(LegacyGuildConstructTable.TryParseConstructLine("1363/fredoome/9/elkomy/", out _));
        Assert.False(LegacyGuildConstructTable.TryParseConstructLine("1/STR_SYNDICATE_EVENT_UPGRADE_BUILDING@@Gate@@20@@/1671289441/", out _));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "WolfConquer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
