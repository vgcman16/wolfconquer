using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyQuestOwnerJournalTests : IDisposable
{
    private const string Prefix = "wolf-quest-owner-journal-";
    private const long CharacterUid = 1_293_600;
    private readonly string root = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
    private string Users => Path.Combine(root, "Users");
    private string Items => Path.Combine(root, "PlayersItems");
    private string Quests => Path.Combine(root, "Quests");
    private string Pending => Path.Combine(Users, ".managed-trade-journal", "pending.json");

    public LegacyQuestOwnerJournalTests()
    {
        ValidateRoot();
        Directory.CreateDirectory(Users);
        Directory.CreateDirectory(Items);
        Directory.CreateDirectory(Quests);
    }

    private LegacyTradeJournalChange[] Seed(long accountId = 301, long uid = CharacterUid)
    {
        var characterText = $$"""
            ; Preserve this actual authority file unchanged.
            [Character]
            UID={{uid}}
            Name=QuestOwner
            Body=1003
            Face=0
            Haire=0
            Class=7051
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=140
            Experience=123
            Map=10250
            X=1009
            Y=1283
            PMap=1002
            PMapX=577
            PMapY=586
            Strength=10
            Agility=20
            Vitaliti=30
            Spirit=40
            Atributes=5
            MinHitPoints=123
            MinMana=45
            Money=1234
            ConquerPoints=55
            BoundConquerPoints=37
            BankCps=50
            PkPoints=0
            Online=0
            Sentinel = retain me
            [Unrelated]
            Keep=unchanged
            """;
        var character = Encoding.UTF8.GetBytes(characterText);
        var parsed = CharacterSnapshotParser.ParseText(characterText, Path.Combine(Users, accountId + ".ini"));
        Assert.Equal(uid, parsed.Uid);
        var offset = checked((uint)(uid - CharacterUid) * 10_000U);
        var items = new PlayerItemSnapshot([Row(900 + offset)], [Row(3500 + offset).WithPosition(213)],
            [Row(3000 + offset).WithPosition(217)], [Row(5000 + offset).WithPosition(4)],
            [Row(4000 + offset).WithPosition(211)], [Row(2500 + offset).WithWarehouseId(100)])
        {
            WarehouseItems = [Row(4500 + offset).WithWarehouseId(1000).WithDepositedItems([Row(4510 + offset)])],
        };
        var afterItems = items with { InventoryItems = items.InventoryItems.Concat([Row(5001 + offset)]).ToArray() };
        var quest = new PlayerQuestSnapshotEntry(3944, PlayerQuestStatus.Accepted, 33, [100U]);
        var quests = new PlayerQuestSnapshot([quest, new(8906, PlayerQuestStatus.Finished, 44, [7U, 9U])]);
        var afterQuests = quests.WithQuest(quest with { Status = PlayerQuestStatus.Finished });
        LegacyTradeJournalChange[] changes =
        [
            new(LegacyTradeJournalFileKind.Character, accountId, character, character.ToArray()),
            new(LegacyTradeJournalFileKind.Items, uid,
                PlayerItemSnapshotLoader.ToLegacyBytes(items), PlayerItemSnapshotLoader.ToLegacyBytes(afterItems)),
            new(LegacyTradeJournalFileKind.Quests, uid,
                PlayerQuestSnapshotLoader.Serialize(quests), PlayerQuestSnapshotLoader.Serialize(afterQuests)),
        ];
        foreach (var change in changes) File.WriteAllBytes(Target(change), change.Before!);
        Assert.Equal(changes[1].Before, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(uid)));
        Assert.Equal(changes[2].Before, PlayerQuestSnapshotLoader.Serialize(new PlayerQuestSnapshotLoader(Quests).LoadByCharacterUid(uid)));
        return changes;
    }

    private static PlayerItemSnapshotItem Row(uint uid) => new()
    {
        Uid = uid, ItemId = 1_000_000 + uid, StackSize = 1, Durability = 91, MaximumDurability = 100,
        Plus = 3, Bless = 1, Enchant = 7, Color = 4, PerfectionLevel = 2,
        OwnerUid = 123, OwnerName = "keeper", Signature = "retain",
    };

    private string Target(LegacyTradeJournalChange change) => Path.Combine(change.Kind switch
    {
        LegacyTradeJournalFileKind.Character => Users,
        LegacyTradeJournalFileKind.Items => Items,
        LegacyTradeJournalFileKind.Quests => Quests,
        _ => throw new ArgumentException("Unknown test target."),
    }, change.FileId.ToString(CultureInfo.InvariantCulture) +
        (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin"));

    private Dictionary<string, byte[]> CaptureFiles() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);

    private void AssertFiles(Dictionary<string, byte[]> before)
    {
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
    }

    private void AssertImages(IEnumerable<LegacyTradeJournalChange> changes, bool after)
    {
        foreach (var change in changes) Assert.Equal(after ? change.After : change.Before, File.ReadAllBytes(Target(change)));
    }

    private void ValidateRoot()
    {
        var full = Path.GetFullPath(root);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        Assert.Equal(temp, Path.GetDirectoryName(full));
        var name = Path.GetFileName(full);
        Assert.StartsWith(Prefix, name);
        Assert.True(Guid.TryParseExact(name[Prefix.Length..], "N", out _));
        for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            if (File.Exists(path) || Directory.Exists(path))
                Assert.Equal(0, (int)(File.GetAttributes(path) & FileAttributes.ReparsePoint));
    }

    public void Dispose()
    {
        ValidateRoot();
        DeleteOrdinary(root);
    }

    private void DeleteOrdinary(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, Path.GetFullPath(entry),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            Assert.Equal(0, (int)(File.GetAttributes(entry) & FileAttributes.ReparsePoint));
            if (Directory.Exists(entry)) DeleteOrdinary(entry); else File.Delete(entry);
        }
        Directory.Delete(directory, recursive: false);
    }
}
