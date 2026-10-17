using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class DataToolMonsterFamilyAuditTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "wolf-monster-family-audit-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AuditNormalizesHighAttackNonBossDonorExactlyOnce()
    {
        var originalAttributes = StandardAttributes();
        originalAttributes["attack_min"] = "4000";
        originalAttributes["attack_max"] = "5000";
        originalAttributes["Boss"] = "0";
        var activeAttributes = CloneAttributes(originalAttributes);
        activeAttributes["attack_min"] = "2000";
        activeAttributes["attack_max"] = "2500";
        activeAttributes.Remove("defence");
        activeAttributes["defense"] = "125";
        var drops = new[] { new SpecialDrop(1_088_000, 4) };
        var fixture = WriteFixture(
            "normalized-once",
            [Monster(100, "Pheasant", activeAttributes, drops)],
            [new DonorFamily(100, "Pheasant", originalAttributes, drops)]);

        var report = Load(fixture);

        Assert.True(report.IsClean);
        Assert.Empty(report.MissingFamilies);
        Assert.Empty(report.ExcessFamilies);
        Assert.Empty(report.FieldMismatches);
    }

    [Fact]
    public void AuditReportsEveryRequiredCombatFieldAndSpecialDropMismatch()
    {
        var originalAttributes = StandardAttributes();
        var activeAttributes = CloneAttributes(originalAttributes);
        foreach (var key in ComparedAttributeKeys)
        {
            activeAttributes[key] = (int.Parse(activeAttributes[key], System.Globalization.CultureInfo.InvariantCulture) + 1)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        activeAttributes["defense"] = activeAttributes["defence"];
        activeAttributes.Remove("defence");
        var fixture = WriteFixture(
            "all-mismatches",
            [Monster(100, "ActivePheasant", activeAttributes, [new SpecialDrop(1_088_001, 5)])],
            [new DonorFamily(
                100,
                "Pheasant",
                originalAttributes,
                [new SpecialDrop(1_088_000, 4)])]);

        var report = Load(fixture);

        Assert.False(report.IsClean);
        Assert.Equal(
            [
                "name",
                "attack_min",
                "attack_max",
                "life",
                "defence",
                "defence2",
                "level",
                "lookface",
                "attack_range",
                "dodge",
                "Boss",
                "move_speed",
                "attack_speed",
                "magic_type",
                "critical",
                "break",
                "extra_battlelev",
                "extra_exp",
                "extra_damage",
                "specialDrops",
            ],
            report.FieldMismatches.Select(static mismatch => mismatch.Field));
        Assert.All(report.FieldMismatches, static mismatch => Assert.Equal(100, mismatch.FamilyId));
        Assert.Contains(
            report.FieldMismatches,
            static mismatch => mismatch.Field == "defence" && mismatch.ActiveValue == "126" && mismatch.OriginalValue == "125");
        Assert.Contains(
            report.FieldMismatches,
            static mismatch => mismatch.Field == "specialDrops" &&
                mismatch.ActiveValue!.Contains("1088001", StringComparison.Ordinal) &&
                mismatch.OriginalValue!.Contains("1088000", StringComparison.Ordinal));
    }

    [Fact]
    public void AuditReportsMissingAndExcessFamilies()
    {
        var attributes = StandardAttributes();
        var fixture = WriteFixture(
            "missing-excess",
            [
                Monster(100, "Pheasant", attributes),
                Monster(300, "ExcessRat", attributes),
            ],
            [
                new DonorFamily(100, "Pheasant", attributes, []),
                new DonorFamily(200, "MissingBird", attributes, []),
            ]);

        var report = Load(fixture);

        Assert.False(report.IsClean);
        var missing = Assert.Single(report.MissingFamilies);
        Assert.Equal((200, "MissingBird"), (missing.FamilyId, missing.FamilyName));
        Assert.EndsWith("200-MissingBird.ini", missing.SourceRelativePath, StringComparison.Ordinal);
        var excess = Assert.Single(report.ExcessFamilies);
        Assert.Equal((300, "ExcessRat", "monsters.json"), (excess.FamilyId, excess.FamilyName, excess.SourceRelativePath));
        Assert.Empty(report.FieldMismatches);
    }

    [Fact]
    public async Task JsonCommandReturnsZeroWhenCleanAndTwoForParityDrift()
    {
        var attributes = StandardAttributes();
        var cleanFixture = WriteFixture(
            "command-clean",
            [Monster(100, "Pheasant", attributes)],
            [new DonorFamily(100, "Pheasant", attributes, [])]);
        var driftAttributes = CloneAttributes(attributes);
        driftAttributes["life"] = "999";
        var driftFixture = WriteFixture(
            "command-drift",
            [Monster(100, "Pheasant", driftAttributes)],
            [new DonorFamily(100, "Pheasant", attributes, [])]);

        var cleanResult = await RunDataToolAsync(
            "monster-family-audit",
            "--world-data-root",
            cleanFixture.ActiveRoot,
            "--db-server-root",
            cleanFixture.DbServerRoot,
            "--json");
        var driftResult = await RunDataToolAsync(
            "monster-family-audit",
            "--world-data-root",
            driftFixture.ActiveRoot,
            "--db-server-root",
            driftFixture.DbServerRoot,
            "--json");
        var textResult = await RunDataToolAsync(
            "monster-family-audit",
            "--world-data-root",
            driftFixture.ActiveRoot,
            "--db-server-root",
            driftFixture.DbServerRoot);

        Assert.Equal(0, cleanResult.ExitCode);
        using (var cleanJson = JsonDocument.Parse(cleanResult.StandardOutput))
        {
            Assert.True(cleanJson.RootElement.GetProperty("isClean").GetBoolean());
            Assert.Equal(1, cleanJson.RootElement.GetProperty("activeFamilyCount").GetInt32());
            Assert.Empty(cleanJson.RootElement.GetProperty("fieldMismatches").EnumerateArray());
        }

        Assert.Equal(2, driftResult.ExitCode);
        using (var driftJson = JsonDocument.Parse(driftResult.StandardOutput))
        {
            Assert.False(driftJson.RootElement.GetProperty("isClean").GetBoolean());
            var mismatch = Assert.Single(driftJson.RootElement.GetProperty("fieldMismatches").EnumerateArray());
            Assert.Equal("life", mismatch.GetProperty("field").GetString());
            Assert.Equal("999", mismatch.GetProperty("activeValue").GetString());
            Assert.Equal("500", mismatch.GetProperty("originalValue").GetString());
        }

        Assert.Equal(string.Empty, cleanResult.StandardError);
        Assert.Equal(string.Empty, driftResult.StandardError);
        Assert.Equal(2, textResult.ExitCode);
        Assert.Contains("monster-family-audit schemaVersion=1", textResult.StandardOutput);
        Assert.Contains(
            "summary activeFamilies=1 originalFamilies=1 missingFamilies=0 excessFamilies=0 fieldMismatches=1",
            textResult.StandardOutput);
        Assert.Contains("fieldMismatch {\"familyId\":100", textResult.StandardOutput);
        Assert.Contains("result isClean=false", textResult.StandardOutput);
        Assert.Equal(string.Empty, textResult.StandardError);
    }

    [Fact]
    public async Task UsageAndLoadErrorsReturnOne()
    {
        var usageResult = await RunDataToolAsync("monster-family-audit", "--unknown");
        var loadResult = await RunDataToolAsync(
            "monster-family-audit",
            "--world-data-root",
            Path.Combine(_rootPath, "missing-active"),
            "--db-server-root",
            Path.Combine(_rootPath, "missing-db"));

        Assert.Equal(1, usageResult.ExitCode);
        Assert.Contains("Unknown monster-family-audit option: --unknown", usageResult.StandardError);
        Assert.Contains("Usage: WolfConquer.DataTool monster-family-audit", usageResult.StandardError);
        Assert.Equal(1, loadResult.ExitCode);
        Assert.Contains("Could not audit monster families:", loadResult.StandardError);
        Assert.Contains("Usage: WolfConquer.DataTool monster-family-audit", loadResult.StandardError);
    }

    [Fact]
    public async Task HelpIncludesMonsterFamilyAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool monster-family-audit [--world-data-root path] [--db-server-root path] [--json]",
            result.StandardOutput);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private static readonly string[] ComparedAttributeKeys =
    [
        "attack_min",
        "attack_max",
        "life",
        "defence",
        "defence2",
        "level",
        "lookface",
        "attack_range",
        "dodge",
        "Boss",
        "move_speed",
        "attack_speed",
        "magic_type",
        "critical",
        "break",
        "extra_battlelev",
        "extra_exp",
        "extra_damage",
    ];

    private static global::MonsterFamilyAuditReport Load(AuditFixture fixture)
    {
        return global::MonsterFamilyAuditLoader.LoadAndAudit(new global::MonsterFamilyAuditOptions(
            fixture.ActiveRoot,
            fixture.DbServerRoot,
            Json: false));
    }

    private AuditFixture WriteFixture(
        string name,
        IReadOnlyList<Monster> activeFamilies,
        IReadOnlyList<DonorFamily> originalFamilies)
    {
        var fixtureRoot = Path.Combine(_rootPath, name);
        var activeRoot = Path.Combine(fixtureRoot, "active");
        var dbServerRoot = Path.Combine(fixtureRoot, "original-dbserver");
        var monstersRoot = Path.Combine(dbServerRoot, "Monsters");
        Directory.CreateDirectory(activeRoot);
        Directory.CreateDirectory(monstersRoot);
        File.WriteAllText(
            Path.Combine(activeRoot, "monsters.json"),
            JsonSerializer.Serialize(activeFamilies, JsonOptions));

        foreach (var family in originalFamilies)
        {
            File.WriteAllText(
                Path.Combine(monstersRoot, $"{family.Id}-{family.Name}.ini"),
                SerializeDonorFamily(family));
        }

        return new AuditFixture(activeRoot, dbServerRoot);
    }

    private static string SerializeDonorFamily(DonorFamily family)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[cq_monstertype]");
        builder.Append("id=").AppendLine(family.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append("name=").AppendLine(family.Name);
        foreach (var attribute in family.Attributes)
        {
            builder.Append(attribute.Key).Append('=').AppendLine(attribute.Value);
        }

        if (family.SpecialDrops.Count > 0)
        {
            builder.AppendLine("[SpecialDrop]");
            builder.Append("Count=").AppendLine(family.SpecialDrops.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (var index = 0; index < family.SpecialDrops.Count; index++)
            {
                var drop = family.SpecialDrops[index];
                builder.Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(drop.ItemTypeId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append(',')
                    .AppendLine(drop.ChanceOrCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static Monster Monster(
        int id,
        string name,
        IReadOnlyDictionary<string, string> attributes,
        IReadOnlyList<SpecialDrop>? specialDrops = null)
    {
        return new Monster(id, name, CloneAttributes(attributes), specialDrops ?? []);
    }

    private static Dictionary<string, string> StandardAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["attack_min"] = "100",
            ["attack_max"] = "200",
            ["life"] = "500",
            ["defence"] = "125",
            ["defence2"] = "10000",
            ["level"] = "10",
            ["lookface"] = "50",
            ["attack_range"] = "1",
            ["dodge"] = "20",
            ["Boss"] = "0",
            ["move_speed"] = "800",
            ["attack_speed"] = "1000",
            ["magic_type"] = "0",
            ["critical"] = "2",
            ["break"] = "3",
            ["extra_battlelev"] = "4",
            ["extra_exp"] = "100",
            ["extra_damage"] = "5",
        };
    }

    private static Dictionary<string, string> CloneAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        return new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = CreateDataToolProcess(repositoryRoot);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"DataTool did not exit. stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ConsoleRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static Process CreateDataToolProcess(string repositoryRoot)
    {
        var copiedDataTool = Path.Combine(AppContext.BaseDirectory, "WolfConquer.DataTool.dll");
        if (!File.Exists(copiedDataTool))
        {
            return TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add(copiedDataTool);
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        return process;
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var fromOutputDirectory = TryFindRepositoryRoot(AppContext.BaseDirectory);
        if (fromOutputDirectory is not null)
        {
            return fromOutputDirectory;
        }

        var fromWorkingDirectory = TryFindRepositoryRoot(Environment.CurrentDirectory);
        if (fromWorkingDirectory is not null)
        {
            return fromWorkingDirectory;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (sourceDirectory is not null)
        {
            var fromSourceDirectory = TryFindRepositoryRoot(sourceDirectory);
            if (fromSourceDirectory is not null)
            {
                return fromSourceDirectory;
            }
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output or working directory.");
    }

    private static string? TryFindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed record AuditFixture(string ActiveRoot, string DbServerRoot);

    private sealed record DonorFamily(
        int Id,
        string Name,
        IReadOnlyDictionary<string, string> Attributes,
        IReadOnlyList<SpecialDrop> SpecialDrops);

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
