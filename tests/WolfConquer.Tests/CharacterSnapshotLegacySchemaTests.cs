using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class CharacterSnapshotLegacySchemaTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-character-legacy-schema-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ClaimStateGiftWinsMainFlagCompatibilityFallbackAtRuntime()
    {
        var snapshot = CharacterSnapshotParser.ParseText(BuildSnapshot(
            "ClaimStateGift=8",
            "MainFlag=4"));

        Assert.Equal("8", snapshot.Attributes["MainFlag"]);
        Assert.Equal("8", snapshot.Attributes["ClaimStateGift"]);
    }

    [Fact]
    public void MainFlagRemainsRuntimeFallbackWhenClaimStateGiftIsAbsent()
    {
        var snapshot = CharacterSnapshotParser.ParseText(BuildSnapshot("MainFlag=4"));

        Assert.Equal("4", snapshot.Attributes["MainFlag"]);
        Assert.False(snapshot.Attributes.ContainsKey("ClaimStateGift"));
    }

    [Fact]
    public void RuntimeMainFlagRoundTripsThroughClaimStateGiftWithoutAddingMainFlag()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");
        File.WriteAllBytes(
            path,
            Windows1252().GetBytes(BuildSnapshot(
                "Unrelated=keep",
                "ClaimStateGift=2",
                "ClaimStateGift=4")));
        var original = LoadSnapshot();
        Assert.Equal("4", original.Attributes["MainFlag"]);

        var updated = original with
        {
            Attributes = new Dictionary<string, string>(original.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["MainFlag"] = "8",
            },
        };

        Assert.True(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(original, updated));
        Assert.True(
            CharacterSnapshotScalarPersistence.TryPersist(_usersRoot, updated, out var persistedPath, out var reason),
            reason);
        Assert.Equal(path, persistedPath);

        var bytes = File.ReadAllBytes(path);
        Assert.Throws<DecoderFallbackException>(() => StrictUtf8().GetString(bytes));
        var lines = Windows1252().GetString(bytes).Split(Environment.NewLine);
        var claimStateGiftLines = lines
            .Where(line => line.StartsWith("ClaimStateGift=", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(2, claimStateGiftLines.Length);
        Assert.All(claimStateGiftLines, line => Assert.Equal("ClaimStateGift=8", line));
        Assert.DoesNotContain(lines, line => line.StartsWith("MainFlag=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Unrelated=keep", lines);
        Assert.Equal("8", LoadSnapshot().Attributes["MainFlag"]);
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

    private static string BuildSnapshot(params string[] additionalCharacterLines)
    {
        var lines = new List<string>
        {
            "[Character]",
            $"UID={CharacterUid}",
            "Name=Andr\u00e9",
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

    private static Encoding Windows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private static UTF8Encoding StrictUtf8()
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }
}
