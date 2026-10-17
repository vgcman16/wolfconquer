using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class CharacterSnapshotEpicTrojanSchemaTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private static readonly (string AttributeKey, int SlotIndex)[] ModeledSlots =
    [
        ("StageEpicTrojanQuest", 2),
        ("ChangeEpicTrojan", 3),
        ("ChangeArrayEpicTrojan", 6),
        ("EpicTrojanEvilArrayPoints", 9),
        ("EpicTrojanAbysalStage", 10),
        ("ChangeMr_MirrorEpicTrojan", 13),
        ("ChangeGeneralPakEpicTrojan", 17),
        ("EpicTrojanMrMirrorPrograss", 19),
    ];

    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-character-epic-trojan-schema-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ValidCompositeProjectsModeledRuntimeAttributesAndPreservesRawStorage()
    {
        var slots = CreateDistinctSlots();
        var composite = BuildComposite(slots);

        var snapshot = CharacterSnapshotParser.ParseText(BuildSnapshot("EpicTrojan=" + composite));

        Assert.Equal(composite, snapshot.Attributes["EpicTrojan"]);
        AssertModeledAttributes(snapshot, slots);
    }

    [Fact]
    public void ValidCompositeWinsAllFlattenedCompatibilityKeys()
    {
        var slots = CreateDistinctSlots();
        var characterLines = new List<string> { "EpicTrojan=" + BuildComposite(slots) };
        characterLines.AddRange(ModeledSlots.Select(field => field.AttributeKey + "=flattened"));

        var snapshot = CharacterSnapshotParser.ParseText(BuildSnapshot(characterLines.ToArray()));

        AssertModeledAttributes(snapshot, slots);
    }

    [Fact]
    public void ModeledRuntimeValuesRoundTripInCompositeAndPreserveEveryUnmodeledSlot()
    {
        Directory.CreateDirectory(_usersRoot);
        var originalSlots = CreateDistinctSlots();
        var path = WriteSnapshot("EpicTrojan=" + BuildComposite(originalSlots));
        var original = LoadSnapshot();
        var expectedModeledValues = ModeledSlots.ToDictionary(
            field => field.AttributeKey,
            field => (9_000 + field.SlotIndex).ToString(CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
        var updatedAttributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in expectedModeledValues)
        {
            updatedAttributes[pair.Key] = pair.Value;
        }

        var updated = original with { Attributes = updatedAttributes };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out var persistedPath, out var reason),
            reason);
        Assert.Equal(path, persistedPath);

        var persistedText = File.ReadAllText(path);
        var persistedComposite = IniDocument.Parse(persistedText).GetSection("Character")["EpicTrojan"];
        var persistedSlots = ParseComposite(persistedComposite);
        Assert.EndsWith("/", persistedComposite, StringComparison.Ordinal);
        for (var index = 0; index < originalSlots.Length; index++)
        {
            var modeledField = ModeledSlots.SingleOrDefault(field => field.SlotIndex == index);
            var expected = modeledField.AttributeKey is null
                ? originalSlots[index]
                : expectedModeledValues[modeledField.AttributeKey];
            Assert.Equal(expected, persistedSlots[index]);
        }

        var persistedLines = persistedText.Split(Environment.NewLine);
        Assert.All(ModeledSlots, field => Assert.DoesNotContain(
            persistedLines,
            line => line.StartsWith(field.AttributeKey + "=", StringComparison.OrdinalIgnoreCase)));
        AssertModeledAttributes(LoadSnapshot(), persistedSlots);
    }

    [Fact]
    public void MissingCompositePersistsModeledRuntimeValuesThroughSourceDefaultSlots()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteSnapshot();
        var original = LoadSnapshot();
        var updatedAttributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var field in ModeledSlots)
        {
            updatedAttributes[field.AttributeKey] = (7_000 + field.SlotIndex).ToString(CultureInfo.InvariantCulture);
        }

        var updated = original with { Attributes = updatedAttributes };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out _, out var reason),
            reason);

        var persistedText = File.ReadAllText(path);
        var slots = ParseComposite(IniDocument.Parse(persistedText).GetSection("Character")["EpicTrojan"]);
        Assert.All(ModeledSlots, field => Assert.Equal(
            (7_000 + field.SlotIndex).ToString(CultureInfo.InvariantCulture),
            slots[field.SlotIndex]));
        Assert.Equal("1", slots[4]);
        Assert.Equal("1", slots[5]);
        Assert.Equal("1", slots[14]);
        Assert.Equal("1", slots[18]);
        Assert.All(ModeledSlots, field => Assert.DoesNotContain(
            persistedText.Split(Environment.NewLine),
            line => line.StartsWith(field.AttributeKey + "=", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MalformedCompositeRemainsUntouchedWithoutWritingFlattenedModeledKeys()
    {
        Directory.CreateDirectory(_usersRoot);
        const string malformed = "0/1/2/";
        var path = WriteSnapshot("EpicTrojan=" + malformed);
        var originalBytes = File.ReadAllBytes(path);
        var original = LoadSnapshot();
        var updated = original with
        {
            Attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["StageEpicTrojanQuest"] = "30",
            },
        };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.False(CharacterSnapshotScalarPersistence.TryPersist(
            _usersRoot,
            updated,
            out _,
            out var reason));
        Assert.Equal("epicTrojanMalformed", reason);

        Assert.Equal(originalBytes, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0/1/2/")]
    [InlineData("0/1/2/3/4/5/6/7/8/9/10/11/12/13/14/15/16/17/18/19")]
    public void AbsentOrMalformedCompositePreservesFlattenedCompatibilityAttributes(string? composite)
    {
        var characterLines = ModeledSlots
            .Select(field => field.AttributeKey + "=fallback-" + field.SlotIndex)
            .ToList();
        if (composite is not null)
        {
            characterLines.Insert(0, "EpicTrojan=" + composite);
        }

        var snapshot = CharacterSnapshotParser.ParseText(BuildSnapshot(characterLines.ToArray()));

        Assert.All(ModeledSlots, field => Assert.Equal(
            "fallback-" + field.SlotIndex,
            snapshot.Attributes[field.AttributeKey]));
        if (composite is not null)
        {
            Assert.Equal(composite, snapshot.Attributes["EpicTrojan"]);
        }
    }

    [Fact]
    public void EphemeralGhostReaverFlagDoesNotTriggerScalarPersistenceOrGetWritten()
    {
        Directory.CreateDirectory(_usersRoot);
        var composite = BuildComposite(CreateDistinctSlots());
        var path = WriteSnapshot("EpicTrojan=" + composite);
        var original = LoadSnapshot();
        var updated = original with
        {
            Attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["EpicTrijanKillGhostReaver"] = "1",
            },
        };

        Assert.False(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out _, out var reason),
            reason);

        var persistedText = File.ReadAllText(path);
        Assert.Equal(composite, IniDocument.Parse(persistedText).GetSection("Character")["EpicTrojan"]);
        Assert.DoesNotContain("EpicTrijanKillGhostReaver=", persistedText, StringComparison.OrdinalIgnoreCase);
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

    private string WriteSnapshot(params string[] additionalCharacterLines)
    {
        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");
        File.WriteAllText(path, BuildSnapshot(additionalCharacterLines));
        return path;
    }

    private static void AssertModeledAttributes(CharacterSnapshot snapshot, IReadOnlyList<string> slots)
    {
        Assert.All(ModeledSlots, field => Assert.Equal(
            slots[field.SlotIndex],
            snapshot.Attributes[field.AttributeKey]));
    }

    private static string[] CreateDistinctSlots()
    {
        return Enumerable.Range(0, 20)
            .Select(index => (1_000 + index).ToString(CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static string BuildComposite(IEnumerable<string> slots)
    {
        return string.Join('/', slots) + "/";
    }

    private static string[] ParseComposite(string composite)
    {
        var parts = composite.Split('/', StringSplitOptions.None);
        Assert.Equal(21, parts.Length);
        Assert.Empty(parts[^1]);
        return parts[..^1];
    }

    private static string BuildSnapshot(params string[] additionalCharacterLines)
    {
        var lines = new List<string>
        {
            "[Character]",
            $"UID={CharacterUid}",
            "Name=EpicTrojan",
            "Body=1003",
            "Face=154",
            "Haire=27",
            "Class=15",
            "FirstClass=15",
            "SecoundeClass=15",
            "Reborn=0",
            "Level=110",
            "Experience=1000",
            "Map=1002",
            "X=430",
            "Y=377",
            "PMap=1002",
            "PMapX=430",
            "PMapY=377",
            "Strength=10",
            "Agility=20",
            "Vitaliti=30",
            "Spirit=40",
            "Atributes=5",
            "MinHitPoints=1000",
            "MinMana=500",
            "Money=10000",
            "ConquerPoints=100",
            "BoundConquerPoints=50",
            "BankCps=25",
            "PkPoints=0",
            "Online=1",
        };
        lines.AddRange(additionalCharacterLines);
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
