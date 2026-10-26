using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests : IDisposable
{
    private const uint AccountUid = 1_399_701;
    private const uint ItemUid = 2_399_701;
    private readonly string parent = Path.Combine(Path.GetTempPath(), "wolf-owned-fixture-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string basename = "wolf-owned-admission-" + Guid.NewGuid().ToString("N");
    private string Destination => Path.Combine(parent, basename);
    private string Staging => Path.Combine(parent, "." + basename + ".staging");
    private string ConfigurationPath => Path.Combine(Destination, "owned-admission.json");

    public LegacyOwnedAdmissionFixtureTests() => Directory.CreateDirectory(parent);

    private LegacyOwnedAdmissionFixtureResult Create()
    {
        Assert.True(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid, out var fixture, out var reason), reason);
        return Assert.IsType<LegacyOwnedAdmissionFixtureResult>(fixture);
    }

    private static Dictionary<string, byte[]> Files(string root)
    {
        var paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        var registry = Path.Combine(root, "PlayersItems", ".managed-item-uids");
        var lease = Path.Combine(registry, "owner.lock");
        if (Directory.Exists(registry))
        {
            Assert.Contains(lease, paths);
            Assert.Equal(0L, new FileInfo(lease).Length);
        }
        return paths.Where(path => !string.Equals(path, lease, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertFiles(string root, Dictionary<string, byte[]> expected)
    {
        var actual = Files(root);
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static void AssertSnapshotSemantics(string root)
    {
        var loader = new CharacterSnapshotLoader(Path.Combine(root, "Users"));
        var character = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(AccountUid));
        Assert.Equal((long)AccountUid, character.Uid);
        Assert.Equal("LootFixture", character.Name);
        Assert.Equal((120L, 10L, 0L), (character.Level, character.Class, character.Experience));
        Assert.Equal((1002L, 410L, 354L), (character.MapId, character.X, character.Y));
        Assert.Equal((100L, 50L, 0L), (character.Money, character.ConquerPoints, character.BankConquerPoints));
        Assert.Equal(0L, character.BoundConquerPoints);
        Assert.Equal((100L, 50L, 100L, 0L, 0L),
            (character.Strength, character.Agility, character.Vitality, character.Spirit, character.AttributePoints));
        Assert.Equal(0L, character.Online);
        Assert.Equal("0,0,0", character.Attributes["SecurityPass"]);
        foreach (var forbidden in new[] { "Password", "Admin", "IsGM", "IsCheckedPass", "CompleteLogin", "OnMyOwnServer",
                     "ServerID", "MyServerID", "DonatePoints", "ChargeChi", "ChargePromotion", "VipLevel" })
            Assert.False(character.Attributes.ContainsKey(forbidden));
        var items = loader.LoadPlayerItemsByCharacterUid(AccountUid);
        var item = Assert.Single(items.InventoryItems);
        Assert.Equal(ItemUid, item.Uid);
        Assert.Equal(1_001_000u, item.ItemId);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Equal((ushort)1, item.Durability);
        Assert.Equal((ushort)1, item.MaximumDurability);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal((byte)0, item.Locked);
        Assert.Empty(items.EquipmentItems);
        Assert.Empty(items.WarehouseItems);
        Assert.Empty(loader.LoadPlayerSpellsByCharacterUid(AccountUid).Spells);
        Assert.Empty(loader.LoadPlayerProficienciesByCharacterUid(AccountUid).Proficiencies);
        Assert.Empty(loader.LoadPlayerQuestsByCharacterUid(AccountUid).Quests);
        Assert.Empty(loader.LoadCombatGearByCharacterUid(AccountUid).Items);
    }

    private static void AssertInitialNamespace(string root, Guid domain)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "PlayersItems", ".managed-item-uids", "state.json")));
        var state = document.RootElement;
        Assert.Equal(domain.ToString("N"), state.GetProperty("Domain").GetString());
        Assert.Equal(0UL, state.GetProperty("Generation").GetUInt64());
        Assert.Equal(ItemUid, state.GetProperty("HistoricalFloor").GetUInt32());
        Assert.Equal(ItemUid, state.GetProperty("HighWater").GetUInt32());
        Assert.False(state.TryGetProperty("Operations", out _));
        Assert.False(state.TryGetProperty("ProducerIntents", out _));
        Assert.False(File.Exists(Path.Combine(root, "PlayersItems", ".managed-item-uids", "pending.json")));
        Assert.False(File.Exists(Path.Combine(root, "RedeemContainer.bin")));
        Assert.False(File.Exists(Path.Combine(root, "ClaimContainer.bin")));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(root, "PlayersMailList")));
    }

    public void Dispose()
    {
        if (collectionHost is { Stopped: false })
            throw new IOException("Owned WorldServer is still active; collection-startup fixture retained: " + parent);
        var full = Path.GetFullPath(parent);
        var name = Path.GetFileName(full);
        const string prefix = "wolf-owned-fixture-tests-";
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full));
        Assert.StartsWith(prefix, name);
        Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
