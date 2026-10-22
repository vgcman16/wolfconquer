using System.Diagnostics;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

// Child-process CLI/file proof only. This does not create or authenticate an
// account, start a server, or establish a game-client Drop/Pickup roundtrip.
public sealed class DataToolSeedLootRoundtripFixtureTests : IDisposable
{
    private const string AccountUid = "1399001";
    private const string ItemUid = "2399001";
    private readonly string parent = Path.Combine(Path.GetTempPath(), "wolf-loot-seed-" + Guid.NewGuid().ToString("N"));
    private string Destination => Path.Combine(parent, "DBServer");

    public DataToolSeedLootRoundtripFixtureTests() => Directory.CreateDirectory(parent);

    [Fact]
    public async Task NewAbsoluteRootContainsNormalCharacterPotionAndEmptySidecarsWithoutAuthAccount()
    {
        int dayBefore = DateTime.Now.DayOfYear;

        var result = await RunDataToolAsync("seed-loot-roundtrip-fixture", Destination, AccountUid, AccountUid, ItemUid);

        int dayAfter = DateTime.Now.DayOfYear;
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("dbServerRoot=" + Destination, result.Output);
        Assert.Contains("accountKey=" + AccountUid, result.Output);
        Assert.Contains("characterUid=" + AccountUid, result.Output);
        Assert.Contains("itemUid=" + ItemUid + " itemId=1001000 stack=1", result.Output);
        Assert.Contains("isolated file fixture only; no authentication account", result.Output);
        var users = Path.Combine(Destination, "Users");
        var loader = new CharacterSnapshotLoader(users);
        var character = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(1_399_001));
        Assert.Equal(1_399_001, character.Uid);
        Assert.Equal((1002L, 410L, 354L), (character.MapId, character.X, character.Y));
        Assert.Equal((120L, 10L, 1000L), (character.Level, character.Class, character.MinHitPoints));
        Assert.Equal((100L, 50L), (character.Money, character.ConquerPoints));
        Assert.Equal("0,0,0", character.Attributes["SecurityPass"]);
        Assert.Equal("0", character.Attributes["DynamicID"]);
        int day = int.Parse(character.Attributes["Day"], CultureInfo.InvariantCulture);
        Assert.Contains(day, new[] { dayBefore, dayAfter });
        foreach (var forbidden in new[] { "IsCheckedPass", "CompleteLogin", "OnMyOwnServer", "ServerID", "MyServerID", "Admin", "Password" })
            Assert.False(character.Attributes.ContainsKey(forbidden));
        var items = loader.LoadPlayerItemsByCharacterUid(character.Uid);
        var item = Assert.Single(items.InventoryItems);
        Assert.Equal(2_399_001u, item.Uid);
        Assert.Equal(1001000u, item.ItemId);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Equal((ushort)1, item.Durability);
        Assert.Equal((ushort)1, item.MaximumDurability);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal((byte)0, item.Locked);
        Assert.Equal((ushort)0, item.Inscribed);
        Assert.Empty(items.EquipmentItems);
        Assert.Empty(items.WarehouseItems);
        Assert.Empty(loader.LoadPlayerSpellsByCharacterUid(character.Uid).Spells);
        Assert.Empty(loader.LoadPlayerProficienciesByCharacterUid(character.Uid).Proficiencies);
        Assert.Empty(loader.LoadCombatGearByCharacterUid(character.Uid).Items);
        Assert.Empty(loader.LoadPlayerQuestsByCharacterUid(character.Uid).Quests);
        Assert.Equal(new[] { "DBServer" }, Directory.GetDirectories(parent).Select(Path.GetFileName));
        Assert.Equal(new[] { "PlayersCombatGear", "PlayersItems", "PlayersProfs", "PlayersSpells", "Quests", "Users" },
            Directory.GetDirectories(Destination).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(6, Directory.GetFiles(Destination, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task ExistingDestinationDirectoryIsRejectedWithoutReadingOrOverwritingItsFiles()
    {
        Directory.CreateDirectory(Destination);
        string marker = Path.Combine(Destination, "preserve.bin");
        byte[] original = [0, 255, 17, 31];
        await File.WriteAllBytesAsync(marker, original);

        var result = await RunDataToolAsync("seed-loot-roundtrip-fixture", Destination, AccountUid, AccountUid, ItemUid);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("already exists", result.Error);
        Assert.Equal(original, await File.ReadAllBytesAsync(marker));
        Assert.Equal(new[] { marker }, Directory.GetFiles(Destination));
        Assert.Empty(Directory.GetDirectories(Destination));
        Assert.Equal(new[] { Destination }, Directory.GetDirectories(parent));
    }

    [Fact]
    public async Task ExistingDestinationFileIsRejectedWithoutOverwrite()
    {
        byte[] original = [7, 8, 9, 10];
        await File.WriteAllBytesAsync(Destination, original);

        var result = await RunDataToolAsync("seed-loot-roundtrip-fixture", Destination, AccountUid, AccountUid, ItemUid);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("already exists", result.Error);
        Assert.Equal(original, await File.ReadAllBytesAsync(Destination));
        Assert.Empty(Directory.GetDirectories(parent));
    }

    [Theory]
    [InlineData("1", "2", "3")]
    [InlineData("1", "1", "1")]
    [InlineData("0", "0", "3")]
    [InlineData("1", "1", "0")]
    [InlineData("-1", "-1", "3")]
    [InlineData("4294967296", "4294967296", "3")]
    [InlineData("not-an-id", "not-an-id", "3")]
    public async Task InvalidMappingsDuplicateItemOrMalformedIdsCannotCreateAnyFixture(string account, string character, string item)
    {
        var result = await RunDataToolAsync("seed-loot-roundtrip-fixture", Destination, account, character, item);

        Assert.Equal(1, result.ExitCode);
        Assert.False(Directory.Exists(Destination));
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("root")]
    [InlineData("missing-parent")]
    public async Task RelativeRootOrMissingParentDestinationsAreRejectedBeforeWrites(string scenario)
    {
        string path = scenario switch
        {
            "relative" => "relative-fixture",
            "root" => Path.GetPathRoot(parent)!,
            _ => Path.Combine(parent, "missing-parent", "DBServer"),
        };

        var result = await RunDataToolAsync("seed-loot-roundtrip-fixture", path, AccountUid, AccountUid, ItemUid);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    [Fact]
    public async Task ReparseAncestorCannotRedirectFixtureWritesIntoAnotherDirectory()
    {
        string target = Path.Combine(parent, "target");
        string link = Path.Combine(parent, "linked");
        Directory.CreateDirectory(target);
        string marker = Path.Combine(target, "preserve.txt");
        await File.WriteAllTextAsync(marker, "untouched");
        if (OperatingSystem.IsWindows())
        {
            using var junction = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                },
            };
            foreach (string argument in new[] { "/c", "mklink", "/J", link, target })
                junction.StartInfo.ArgumentList.Add(argument);
            Assert.True(junction.Start());
            var output = junction.StandardOutput.ReadToEndAsync();
            var error = junction.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await junction.WaitForExitAsync(timeout.Token);
            Assert.True(junction.ExitCode == 0, (await output) + (await error));
        }
        else Directory.CreateSymbolicLink(link, target);
        try
        {
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);

            var result = await RunDataToolAsync("seed-loot-roundtrip-fixture",
                Path.Combine(link, "DBServer"), AccountUid, AccountUid, ItemUid);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("reparse", result.Error);
            Assert.Equal("untouched", await File.ReadAllTextAsync(marker));
            Assert.False(Directory.Exists(Path.Combine(target, "DBServer")));
            Assert.Equal(new[] { marker }, Directory.GetFiles(target));
        }
        finally
        {
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
            Directory.Delete(link); // Remove only the link, never its target tree.
        }
    }

    [Fact]
    public async Task HelpExposesTheExplicitNewRootFixtureCommand()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("seed-loot-roundtrip-fixture <NEW-absolute-DBServer-dir> <account-key> <character-uid> <item-uid>", result.Output);
    }

    private async Task<RunResult> RunDataToolAsync(params string[] arguments)
    {
        var repository = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repository, "WolfConquer.DataTool");
        // Even a regression in relative-path rejection stays in this owned temp tree.
        process.StartInfo.WorkingDirectory = parent;
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start(), "Could not start DataTool.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail("DataTool fixture command did not exit. " + await output + " " + await error);
        }
        return new(process.ExitCode, await output, await error);
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate WolfConquer.sln.");
    }

    public void Dispose()
    {
        if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
    }

    private sealed record RunResult(int ExitCode, string Output, string Error);
}
