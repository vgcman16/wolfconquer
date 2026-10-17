using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class DataToolCharacterAutomaticProcEligibilityAuditTests
{
    [Fact]
    public async Task ExplicitSnapshotsReportDeterministicEligibilityAndMissingMetadataWithoutMutation()
    {
        var tempRoot = CreateTempRoot("explicit");
        var characterPath = Path.Combine(tempRoot, "character.ini");
        var spellsPath = Path.Combine(tempRoot, "spells.bin");
        var itemsPath = Path.Combine(tempRoot, "items.bin");
        var magicPath = Path.Combine(tempRoot, "magictype.txt");

        try
        {
            await File.WriteAllTextAsync(characterPath, CreateCharacterSnapshot(1_293_600, "AuditHero", 1_001));
            await File.WriteAllBytesAsync(
                spellsPath,
                PlayerSpellSnapshotLoader.ToLegacyBytes(
                    new PlayerSpellSnapshot(
                    [
                        CreatePlayerSpell(1_250, level: 0),
                        CreatePlayerSpell(1_260, level: 1),
                        CreatePlayerSpell(14_720, level: 0),
                        CreatePlayerSpell(9_999, level: 0),
                    ])));
            await File.WriteAllBytesAsync(
                itemsPath,
                PlayerItemSnapshotLoader.ToLegacyBytes(
                    new PlayerItemSnapshot(
                        [],
                        [],
                        [],
                        [CreateWeapon(uid: 50_001, itemId: 480_339, position: 4, effect: 200)],
                        [])));
            await File.WriteAllLinesAsync(
                magicPath,
                [
                    CreateMagicRow(1_250, level: 0, weaponType: 480, rate: 10),
                    CreateMagicRow(1_260, level: 0, weaponType: 480, rate: 20),
                    CreateMagicRow(1_290, level: 0, weaponType: 480, rate: 30),
                    CreateMagicRow(3_090, level: 0),
                    CreateMagicRow(14_720, level: 0),
                ]);
            var paths = new[] { characterPath, spellsPath, itemsPath, magicPath };
            var before = paths.ToDictionary(path => path, File.ReadAllBytes);

            var arguments = new[]
            {
                "character-automatic-proc-eligibility-audit",
                "--character-snapshot", characterPath,
                "--spells-snapshot", spellsPath,
                "--items-snapshot", itemsPath,
                "--magic-snapshot", magicPath,
            };
            var first = await RunDataToolAsync(arguments);
            var second = await RunDataToolAsync(arguments);

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(string.Empty, first.StandardError);
            Assert.Equal(first.StandardOutput, second.StandardOutput);
            Assert.Equal(string.Empty, second.StandardError);

            using var document = JsonDocument.Parse(first.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("character-automatic-proc-eligibility-audit", root.GetProperty("command").GetString());
            Assert.True(root.GetProperty("readOnly").GetBoolean());
            Assert.Equal("snapshot", root.GetProperty("selection").GetProperty("mode").GetString());
            Assert.Equal(1_293_600, root.GetProperty("character").GetProperty("uid").GetInt64());
            Assert.Equal("AuditHero", root.GetProperty("character").GetProperty("name").GetString());
            Assert.False(root.GetProperty("runtimeConfiguration").GetProperty("compatibilityFenceEnabled").GetBoolean());
            Assert.Equal(100u, root.GetProperty("runtimeConfiguration").GetProperty("replacementSelectionOuterChancePercent").GetUInt32());

            var sources = root.GetProperty("sources");
            Assert.True(sources.GetProperty("character").GetProperty("explicit").GetBoolean());
            Assert.True(sources.GetProperty("spells").GetProperty("explicit").GetBoolean());
            Assert.True(sources.GetProperty("items").GetProperty("explicit").GetBoolean());
            Assert.True(sources.GetProperty("magic").GetProperty("explicit").GetBoolean());
            Assert.Equal("legacyAtAtText", sources.GetProperty("magic").GetProperty("format").GetString());

            var rightWeapon = Assert.Single(
                root.GetProperty("currentWeapons").EnumerateArray(),
                weapon => weapon.GetProperty("hand").GetString() == "right");
            Assert.True(rightWeapon.GetProperty("present").GetBoolean());
            Assert.Equal(480, rightWeapon.GetProperty("effectiveWeaponType").GetUInt16());
            Assert.Equal(200u, rightWeapon.GetProperty("effect").GetUInt32());

            var candidateIds = root.GetProperty("candidateSkills")
                .EnumerateArray()
                .Select(candidate => candidate.GetProperty("spellId").GetUInt16())
                .ToArray();
            Assert.Equal(new ushort[] { 1_250, 1_260, 1_290, 3_090, 14_720 }, candidateIds);
            AssertCandidate(root, 1_250, learned: true, eligible: true, "weaponTable");
            AssertCandidate(root, 1_260, learned: true, eligible: false, "weaponTable");
            AssertCandidate(root, 1_290, learned: false, eligible: false, "weaponTable");
            AssertCandidate(root, 3_090, learned: false, eligible: false, "rightWeaponEffect");
            AssertCandidate(root, 14_720, learned: true, eligible: true, "directLearned", "postPhysicalLearned");

            var poisonBranch = Assert.Single(
                root.GetProperty("candidateSkills").EnumerateArray(),
                candidate => candidate.GetProperty("spellId").GetUInt16() == 3_090)
                .GetProperty("branches")[0];
            Assert.True(poisonBranch.GetProperty("requiresLearned").GetBoolean());
            Assert.False(poisonBranch.GetProperty("temporaryWhenUnlearned").GetBoolean());
            Assert.Contains(
                poisonBranch.GetProperty("ineligibilityReasons").EnumerateArray(),
                reason => reason.GetString() == "notLearned");

            var missing = Assert.Single(root.GetProperty("missingMetadata").EnumerateArray());
            Assert.Equal(1_260, missing.GetProperty("spellId").GetUInt16());
            Assert.Equal(1, missing.GetProperty("level").GetUInt16());
            Assert.Equal("levelRow", missing.GetProperty("field").GetString());
            Assert.Equal("No magic metadata row exists for learned level 1.", missing.GetProperty("reason").GetString());

            var summary = root.GetProperty("summary");
            Assert.Equal(4, summary.GetProperty("learnedSpellCount").GetInt32());
            Assert.Equal(3, summary.GetProperty("learnedAutomaticProcSpellCount").GetInt32());
            Assert.Equal(5, summary.GetProperty("candidateSkillCount").GetInt32());
            Assert.Equal(2, summary.GetProperty("structurallyEligibleCandidateCount").GetInt32());
            Assert.Equal(2, summary.GetProperty("eligibleCandidateCount").GetInt32());
            Assert.Equal(1, summary.GetProperty("missingMetadataCount").GetInt32());

            foreach (var path in paths)
            {
                Assert.Equal(before[path], await File.ReadAllBytesAsync(path));
            }
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CharacterNameAndUidResolveStoredSiblingSnapshots()
    {
        var tempRoot = CreateTempRoot("stored");
        var usersRoot = Path.Combine(tempRoot, "DBServer", "Users");
        var spellsRoot = Path.Combine(tempRoot, "DBServer", "PlayersSpells");
        var itemsRoot = Path.Combine(tempRoot, "DBServer", "PlayersItems");
        var characterPath = Path.Combine(usersRoot, "1293601.ini");
        var spellsPath = Path.Combine(spellsRoot, "1293601.bin");
        var itemsPath = Path.Combine(itemsRoot, "1293601.bin");
        var magicPath = Path.Combine(tempRoot, "magic.json");

        try
        {
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(spellsRoot);
            Directory.CreateDirectory(itemsRoot);
            await File.WriteAllTextAsync(characterPath, CreateCharacterSnapshot(1_293_601, "Stored Hero", 2_001));
            await File.WriteAllBytesAsync(
                spellsPath,
                PlayerSpellSnapshotLoader.ToLegacyBytes(
                    new PlayerSpellSnapshot([CreatePlayerSpell(1_250, level: 0)])));
            await File.WriteAllBytesAsync(
                itemsPath,
                PlayerItemSnapshotLoader.ToLegacyBytes(
                    new PlayerItemSnapshot(
                        [],
                        [],
                        [],
                        [CreateWeapon(uid: 50_002, itemId: 480_339, position: 4)],
                        [])));
            await File.WriteAllTextAsync(
                magicPath,
                JsonSerializer.Serialize(
                    new[] { CreateMagicType(1_250, level: 0, weaponType: 480, rate: 10) },
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

            var byName = await RunDataToolAsync(
                "character-auto-proc-eligibility-audit",
                "--character-name", "Stored Hero",
                "--users-root", usersRoot,
                "--magic-snapshot", magicPath);
            var byUid = await RunDataToolAsync(
                "character-automatic-proc-eligibility-audit",
                "--character-uid", "1293601",
                "--users-root", usersRoot,
                "--magic-snapshot", magicPath);

            Assert.Equal(0, byName.ExitCode);
            Assert.Equal(0, byUid.ExitCode);
            Assert.Equal(string.Empty, byName.StandardError);
            Assert.Equal(string.Empty, byUid.StandardError);
            using var nameDocument = JsonDocument.Parse(byName.StandardOutput);
            using var uidDocument = JsonDocument.Parse(byUid.StandardOutput);
            var nameRoot = nameDocument.RootElement;
            var uidRoot = uidDocument.RootElement;
            Assert.Equal("name", nameRoot.GetProperty("selection").GetProperty("mode").GetString());
            Assert.Equal("uid", uidRoot.GetProperty("selection").GetProperty("mode").GetString());
            Assert.Equal(1_293_601, nameRoot.GetProperty("character").GetProperty("uid").GetInt64());
            Assert.Equal("Stored Hero", uidRoot.GetProperty("character").GetProperty("name").GetString());
            Assert.Equal(
                Path.GetFullPath(spellsPath),
                nameRoot.GetProperty("sources").GetProperty("spells").GetProperty("path").GetString());
            Assert.Equal(
                Path.GetFullPath(itemsPath),
                uidRoot.GetProperty("sources").GetProperty("items").GetProperty("path").GetString());
            Assert.False(nameRoot.GetProperty("sources").GetProperty("spells").GetProperty("explicit").GetBoolean());
            Assert.False(uidRoot.GetProperty("sources").GetProperty("items").GetProperty("explicit").GetBoolean());
            Assert.Equal(
                nameRoot.GetProperty("candidateSkills").GetRawText(),
                uidRoot.GetProperty("candidateSkills").GetRawText());
            AssertCandidate(nameRoot, 1_250, learned: true, eligible: true, "weaponTable");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MissingCharacterSelectorReturnsUsage()
    {
        var result = await RunDataToolAsync("character-automatic-proc-eligibility-audit");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Contains("Specify exactly one character selector", result.StandardError);
        Assert.Contains(
            "Usage: WolfConquer.DataTool character-automatic-proc-eligibility-audit",
            result.StandardError);
    }

    [Fact]
    public async Task HelpIncludesCharacterAutomaticProcEligibilityAuditUsage()
    {
        var result = await RunDataToolAsync("help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(
            "WolfConquer.DataTool character-automatic-proc-eligibility-audit " +
            "(--character-uid UID | --character-name NAME | --character-snapshot path)",
            result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    private static void AssertCandidate(
        JsonElement root,
        ushort spellId,
        bool learned,
        bool eligible,
        params string[] expectedSources)
    {
        var candidate = Assert.Single(
            root.GetProperty("candidateSkills").EnumerateArray(),
            item => item.GetProperty("spellId").GetUInt16() == spellId);
        Assert.Equal(learned, candidate.GetProperty("learned").GetBoolean());
        Assert.Equal(eligible, candidate.GetProperty("eligible").GetBoolean());
        var sources = candidate.GetProperty("branches")
            .EnumerateArray()
            .Select(branch => branch.GetProperty("source").GetString())
            .Where(source => source is not null)
            .Cast<string>()
            .ToArray();
        foreach (var expectedSource in expectedSources)
        {
            Assert.Contains(expectedSource, sources);
        }
    }

    private static PlayerSpellSnapshotEntry CreatePlayerSpell(ushort id, ushort level)
    {
        return new PlayerSpellSnapshotEntry(id, level, Experience: 0, PreviousLevel: 0, SoulLevel: 0, UseSpellSoul: 0);
    }

    private static PlayerItemSnapshotItem CreateWeapon(
        uint uid,
        uint itemId,
        ushort position,
        uint effect = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = position,
            Durability = 100,
            MaximumDurability = 100,
            Effect = effect,
        };
    }

    private static string CreateMagicRow(
        ushort spellId,
        byte level,
        uint weaponType = 0,
        uint rate = 100)
    {
        return string.Join("@@", CreateMagicFields(spellId, level, weaponType, rate));
    }

    private static MagicType CreateMagicType(
        ushort spellId,
        byte level,
        uint weaponType = 0,
        uint rate = 100)
    {
        var fields = CreateMagicFields(spellId, level, weaponType, rate);
        return new MagicType(spellId, spellId, $"Spell{spellId}", fields);
    }

    private static string[] CreateMagicFields(
        ushort spellId,
        byte level,
        uint weaponType,
        uint rate)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = "1";
        fields[3] = $"Spell{spellId}";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[10] = "120";
        fields[12] = rate.ToString(CultureInfo.InvariantCulture);
        fields[14] = "1";
        fields[15] = "10";
        fields[20] = "1";
        fields[22] = weaponType.ToString(CultureInfo.InvariantCulture);
        return fields;
    }

    private static string CreateCharacterSnapshot(long uid, string name, long classCode)
    {
        return $$"""
            [Character]
            UID={{uid}}
            Name={{name}}
            Body=1003
            Face=154
            Haire=27
            Class={{classCode}}
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=100
            Experience=0
            Map=1002
            X=430
            Y=378
            PMap=1002
            PMapX=430
            PMapY=378
            Strength=10
            Agility=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=1000
            MinMana=100
            Money=0
            ConquerPoints=0
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=0
            """;
    }

    private static async Task<ConsoleRunResult> RunDataToolAsync(params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, "WolfConquer.DataTool");
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.Environment[
            LegacyClientCompatibilityOptions.DisableLegacyWeaponProcMonsterAttacksEnvironmentVariable] = "0";
        Assert.True(process.Start(), "Could not start DataTool.");
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

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WolfConquer.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private static string CreateTempRoot(string suffix)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"wolfconquer-character-auto-proc-audit-{suffix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record ConsoleRunResult(int ExitCode, string StandardOutput, string StandardError);
}
