using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class CharacterSnapshotScalarPersistenceSkyTowerTests : IDisposable
{
    private const long CharacterUid = 1_293_601;

    private static readonly string[] LiveInstanceAttributeKeys =
    [
        "SkyTowerActiveChallengeId",
        "SkyTowerActiveCompletionId",
        "SkyTowerFloor",
        "DynamicID",
    ];

    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-character-scalar-sky-tower-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("SkyTowerCompletedChallengeId", "476")]
    [InlineData("SkyTowerAvailableAttempts", "9")]
    public void DurableSkyTowerProgressTriggersChangeDetection(string runtimeKey, string updatedValue)
    {
        var original = CharacterSnapshotParser.ParseText(BuildSnapshot());
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [runtimeKey] = updatedValue,
        };
        var updated = original with { Attributes = attributes };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
    }

    [Fact]
    public void DurableSkyTowerProgressSurvivesPersistenceRoundTrip()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteSnapshot();
        var original = LoadSnapshot();
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["SkyTowerCompletedChallengeId"] = "476",
            ["SkyTowerAvailableAttempts"] = "9",
        };
        var updated = original with { Attributes = attributes };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out var persistedPath, out var reason),
            reason);
        Assert.Equal(path, persistedPath);

        var reloaded = LoadSnapshot();
        Assert.Equal("476", reloaded.Attributes["SkyTowerCompletedChallengeId"]);
        Assert.Equal("9", reloaded.Attributes["SkyTowerAvailableAttempts"]);
    }

    [Fact]
    public void LiveSkyTowerInstanceStateIsNotPersisted()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteSnapshot();
        var original = LoadSnapshot();
        var attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["SkyTowerActiveChallengeId"] = "476",
            ["SkyTowerActiveCompletionId"] = "476",
            ["SkyTowerFloor"] = "1",
            ["DynamicID"] = "10428001",
        };
        var updated = original with { Attributes = attributes };

        Assert.False(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out _, out var reason),
            reason);

        var persisted = IniDocument.Parse(File.ReadAllText(path)).GetSection("Character");
        var reloaded = LoadSnapshot();
        Assert.All(LiveInstanceAttributeKeys, key => Assert.False(persisted.ContainsKey(key)));
        Assert.All(LiveInstanceAttributeKeys, key => Assert.False(reloaded.Attributes.ContainsKey(key)));
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
            Name=SkyTowerPersistenceTester
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
            SkyTowerCompletedChallengeId=475
            SkyTowerAvailableAttempts=10
            """;
    }
}
