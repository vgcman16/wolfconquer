using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class CharacterSnapshotScalarPersistenceChiTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private static readonly IReadOnlyDictionary<string, string> UpdatedChiValues =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Dragon"] = "dragon-updated",
            ["Pheonix"] = "phoenix-updated",
            ["Tiger"] = "tiger-updated",
            ["Turtle"] = "turtle-updated",
            ["ChiPoints"] = "765432",
            ["DragonTime"] = "101",
            ["PhoenixTime"] = "202",
            ["TigerTime"] = "303",
            ["TurtleTime"] = "404",
            ["RetreatedDragon"] = "retreated-dragon",
            ["RetreatedPhoenix"] = "retreated-phoenix",
            ["RetreatedTiger"] = "retreated-tiger",
            ["RetreatedTurtle"] = "retreated-turtle",
        };

    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-character-scalar-chi-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Dragon")]
    [InlineData("Pheonix")]
    [InlineData("Phoenix")]
    [InlineData("Tiger")]
    [InlineData("Turtle")]
    [InlineData("ChiPoints")]
    [InlineData("DragonTime")]
    [InlineData("PhoenixTime")]
    [InlineData("TigerTime")]
    [InlineData("TurtleTime")]
    [InlineData("RetreatedDragon")]
    [InlineData("RetreatedPhoenix")]
    [InlineData("RetreatedTiger")]
    [InlineData("RetreatedTurtle")]
    public void EveryChiScalarAndPhoenixAliasTriggersChangeDetection(string runtimeKey)
    {
        var original = CharacterSnapshotParser.ParseText(BuildSnapshot());
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.Ordinal);
        if (runtimeKey == "Phoenix")
        {
            attributes.Remove("Pheonix");
        }

        attributes[runtimeKey] = "changed-" + runtimeKey;
        var updated = original with { Attributes = attributes };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
    }

    [Fact]
    public void PersistsEveryChiScalarUnderItsExactCanonicalKey()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteSnapshot();
        var original = LoadSnapshot();
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in UpdatedChiValues)
        {
            attributes[pair.Key] = pair.Value;
        }

        var updated = original with { Attributes = attributes };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out var persistedPath, out var reason),
            reason);
        Assert.Equal(path, persistedPath);

        var persistedText = File.ReadAllText(path);
        var persisted = IniDocument.Parse(persistedText).GetSection("Character");
        Assert.All(UpdatedChiValues, pair => Assert.Equal(pair.Value, persisted[pair.Key]));
        Assert.Equal("tiger-updated", persisted["Tiger"]);
        Assert.Equal("turtle-updated", persisted["Turtle"]);
        Assert.Equal("retreated-tiger", persisted["RetreatedTiger"]);
        Assert.Equal("retreated-turtle", persisted["RetreatedTurtle"]);
        Assert.Single(
            persistedText.Split(Environment.NewLine),
            line => line.StartsWith("Pheonix=", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            persistedText.Split(Environment.NewLine),
            line => line.StartsWith("Phoenix=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RuntimePhoenixAliasWritesOnlyCanonicalPheonixStorageKey()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteSnapshot();
        var original = LoadSnapshot();
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.Ordinal);
        attributes.Remove("Pheonix");
        attributes["Phoenix"] = "runtime-phoenix";
        var updated = original with { Attributes = attributes };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out _, out var reason),
            reason);

        var persistedText = File.ReadAllText(path);
        var persisted = IniDocument.Parse(persistedText).GetSection("Character");
        Assert.Equal("runtime-phoenix", persisted["Pheonix"]);
        Assert.Single(
            persistedText.Split(Environment.NewLine),
            line => line.StartsWith("Pheonix=", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            persistedText.Split(Environment.NewLine),
            line => line.StartsWith("Phoenix=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EquivalentPhoenixAliasesDoNotReportAChange()
    {
        var original = CharacterSnapshotParser.ParseText(BuildSnapshot());
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.Ordinal);
        var value = attributes["Pheonix"];
        attributes.Remove("Pheonix");
        attributes["Phoenix"] = value;

        var aliased = original with { Attributes = attributes };

        Assert.False(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, aliased));
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private CharacterSnapshot LoadSnapshot()
    {
        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(CharacterUid);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    private string WriteSnapshot()
    {
        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");
        File.WriteAllText(path, BuildSnapshot());
        return path;
    }

    private static string BuildSnapshot()
    {
        return $$"""
            [Character]
            UID={{CharacterUid}}
            Name=ChiPersistenceTester
            Body=1003
            Face=154
            Haire=27
            Class=15
            FirstClass=15
            SecoundeClass=15
            Reborn=0
            Level=110
            Experience=1000
            Map=1002
            X=430
            Y=377
            PMap=1002
            PMapX=430
            PMapY=377
            Strength=10
            Agility=20
            Vitaliti=30
            Spirit=40
            Atributes=5
            MinHitPoints=1000
            MinMana=500
            MaxMana=500
            Money=10000
            ConquerPoints=100
            BoundConquerPoints=50
            BankCps=25
            PkPoints=0
            Online=1
            ExpBallUsed=0
            Dragon=dragon-original
            Pheonix=phoenix-original
            Tiger=tiger-original
            Turtle=turtle-original
            ChiPoints=123456
            DragonTime=11
            PhoenixTime=22
            TigerTime=33
            TurtleTime=44
            RetreatedDragon=retreated-dragon-original
            RetreatedPhoenix=retreated-phoenix-original
            RetreatedTiger=retreated-tiger-original
            RetreatedTurtle=retreated-turtle-original
            """;
    }
}
