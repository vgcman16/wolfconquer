using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Shared owned data fixture for public runtime-API and separate real-host tests.
// Bootstrap belongs only to this explicitly quiescent test owner, never startup.
internal sealed class LegacyOwnedAdmissionRuntimeFixture : IDisposable
{
    internal const uint Uid = 2_399_700;
    internal const uint HistoricalFloor = 8_000_000;
    internal const string HistoricalEvidence = "owned synthetic quiescent startup evidence";
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-owned-admission-runtime-" + Guid.NewGuid().ToString("N"));
    internal string Users => Path.Combine(Root, "Users");
    internal string Items => Path.Combine(Root, "PlayersItems");
    internal string Spells => Path.Combine(Root, "PlayersSpells");
    internal string Proficiencies => Path.Combine(Root, "PlayersProfs");
    internal string Quests => Path.Combine(Root, "PlayersQuests");
    internal string CombatGear => Path.Combine(Root, "PlayersCombatGear");
    internal string Mailbox => Path.Combine(Root, "PlayersMailList");
    internal string Redeem => Path.Combine(Root, "RedeemContainer.bin");
    internal string Claim => Path.Combine(Root, "ClaimContainer.bin");
    internal string Registry => Path.Combine(Items, ".managed-item-uids");
    internal string State => Path.Combine(Registry, "state.json");
    internal string OwnerLease => Path.Combine(Registry, "owner.lock");
    internal string UserPath => Path.Combine(Users, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    internal string ConfigPath => Path.Combine(Root, "owned-admission.json");
    internal Guid ExpectedDomain { get; private set; }
    internal CharacterSnapshotLoader Loader { get; }

    internal LegacyOwnedAdmissionRuntimeFixture(bool bootstrap = true)
    {
        foreach (var path in new[] { Users, Items, Spells, Proficiencies, Quests, CombatGear, Mailbox }) Directory.CreateDirectory(path);
        // Class 7051 with all four attributes at 10 has source max HP 330
        // (10*3 + 10*3 + 10*3 + 10*24) and max mana 50 (Spirit 10*5),
        // with no equipped/external bonuses. Missing MaxMana falls back to 50.
        // Keep this positive saved fixture coherent through real startup query.
        var fields = new[]
        {
            "; Owned complete startup Character image, preserve BOM/CRLF.", "[Character]", $"UID={Uid}",
            "Name=OwnedStartup", "Body=1008", "Face=154", "Haire=27", "Class=7051", "FirstClass=0",
            "SecoundeClass=0", "Reborn=2", "Level=140", "Experience=123", "Map=1002", "X=430", "Y=380",
            "PMap=1002", "PMapX=430", "PMapY=380", "Strength=10", "Agility=10", "Vitaliti=10",
            "Spirit=10", "Atributes=0", "MinHitPoints=330", "MinMana=50", "Money=777", "ConquerPoints=222",
            "BoundConquerPoints=333", "BankCps=444", "WHMoney=555", "PkPoints=0", "Online=0", "SecurityPass=0,0,0",
            "VipLevel=0", "KeepBytes=startup-owned-value", "DonatePoints=115", "", "[Unrelated]", "Sentinel=retain", "",
        };
        File.WriteAllText(UserPath, string.Join("\r\n", fields), new UTF8Encoding(true));
        new PlayerItemSnapshotLoader(Items).SaveByCharacterUid(Uid,
            new([new PlayerItemSnapshotItem { Uid = 1_000_091, ItemId = 1_000_000, StackSize = 1, Color = 5 }], [], [], [], []));
        new PlayerSpellSnapshotLoader(Spells).SaveByCharacterUid(Uid, PlayerSpellSnapshot.Empty);
        new PlayerProficiencySnapshotLoader(Proficiencies).SaveByCharacterUid(Uid, PlayerProficiencySnapshot.Empty);
        new PlayerQuestSnapshotLoader(Quests).SaveByCharacterUid(Uid, PlayerQuestSnapshot.Empty);
        new CombatGearSnapshotLoader(CombatGear).SaveByCharacterUid(Uid, CombatGearSnapshot.Empty);
        Loader = new(Users, Items, Spells, Proficiencies, CombatGear, Quests);
        if (bootstrap) Bootstrap();
    }

    internal LegacyItemUidRegistryOptions RegistryOptions() => new(Users, Items, Mailbox, Redeem, Claim,
        HistoricalFloor, HistoricalEvidence);

    internal LegacyOwnedAdmissionOptions Options() => new(ExpectedDomain, Users, Items, Spells, Proficiencies,
        Quests, CombatGear, Mailbox, Redeem, Claim, HistoricalFloor, HistoricalEvidence);

    internal string WriteConfiguration(LegacyOwnedAdmissionOptions? options = null)
    {
        File.WriteAllText(ConfigPath, Configuration(options ?? Options()).ToJsonString(), new UTF8Encoding(false));
        return ConfigPath;
    }

    internal static JsonObject Configuration(LegacyOwnedAdmissionOptions options) => new()
    {
        ["Version"] = 1,
        ["ExpectedDomain"] = options.ExpectedDomain.ToString("N"),
        ["UsersRoot"] = options.UsersRoot,
        ["ItemsRoot"] = options.ItemsRoot,
        ["SpellsRoot"] = options.SpellsRoot,
        ["ProficienciesRoot"] = options.ProficienciesRoot,
        ["QuestsRoot"] = options.QuestsRoot,
        ["CombatGearRoot"] = options.CombatGearRoot,
        ["MailboxRoot"] = options.MailboxRoot,
        ["RedeemFile"] = options.RedeemFile,
        ["ClaimFile"] = options.ClaimFile,
        ["HistoricalHighWater"] = options.HistoricalHighWater,
        ["HistoricalEvidence"] = options.HistoricalEvidence,
    };

    internal void StagePendingSnapshotJournal()
    {
        var before = File.ReadAllBytes(UserPath);
        var after = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(before).Replace("Online=0\r\n", "Online=1\r\n", StringComparison.Ordinal));
        Assert.NotEqual(before, after);
        var items = File.ReadAllBytes(Path.Combine(Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin"));
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(Users, Items,
            [new(LegacyTradeJournalFileKind.Character, Uid, before, after),
             new(LegacyTradeJournalFileKind.Items, Uid, items, items)], count => count == 0, out var reason));
        Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.True(LegacyTradeJournal.HasPending(Users));
        Assert.Equal(before, File.ReadAllBytes(UserPath));
    }

    internal void Bootstrap()
    {
        Assert.False(Directory.Exists(Registry));
        Assert.True(LegacyItemUidRegistry.TryBootstrap(RegistryOptions(), out var registry, out var reason), reason);
        using (Assert.IsType<LegacyItemUidRegistry>(registry))
            ExpectedDomain = Guid.ParseExact(JsonNode.Parse(File.ReadAllBytes(State))!["Domain"]!.GetValue<string>(), "N");
    }

    internal Dictionary<string, byte[]> Files()
    {
        var paths = Directory.GetFiles(Root, "*", SearchOption.AllDirectories);
        if (Directory.Exists(Registry))
        {
            if (File.Exists(OwnerLease))
            {
                Assert.Contains(OwnerLease, paths);
                Assert.Equal(0L, new FileInfo(OwnerLease).Length);
            }
            else Assert.Empty(Directory.GetFileSystemEntries(Registry));
        }
        return paths.Where(path => !string.Equals(path, OwnerLease, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(path => Path.GetRelativePath(Root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    }

    internal void AssertFiles(Dictionary<string, byte[]> expected)
    {
        var actual = Files();
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    public void Dispose()
    {
        var path = Path.GetFullPath(Root);
        var name = Path.GetFileName(path);
        const string prefix = "wolf-owned-admission-runtime-";
        if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                StringComparison.OrdinalIgnoreCase) || !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..], "N", out _)) throw new InvalidOperationException("Invalid owned startup fixture root.");
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
