using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Genuine disposable files and v2 restart reconciliation, not live client/SQL,
// a process-kill/power-loss test or an accepting persistence provider.
public sealed partial class LegacyDragonSkinPersistenceTests
{
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;

    private static void Run(Action<Fixture> action)
    {
        var fixture = new Fixture();
        Exception? primary = null;
        Exception? cleanup = null;
        try { action(fixture); }
        catch (Exception error) { primary = error; }
        try { fixture.Cleanup(); }
        catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    private sealed class Fixture
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-dragon-skin-persist-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(root, "Users");
        internal string Items => Path.Combine(root, "PlayersItems");
        internal string CharacterPath => Path.Combine(Users, AccountId + ".ini");
        internal string ItemPath => Path.Combine(Items, CharacterUid + ".bin");
        internal string PendingPath => Path.Combine(Users, ".managed-trade-journal", "pending.json");

        internal WorldSession Seed(string anima = "0/1/2/", string encodingName = "utf8", string newline = "\n",
            PlayerItemSnapshot? items = null, bool includeSkin = true)
        {
            ValidateRoot();
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            var zeroFields = new[] { "Body", "Face", "Haire", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
                "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Strength", "Agility", "Vitaliti", "Spirit",
                "Atributes", "MinHitPoints", "MinMana", "BoundConquerPoints", "BankCps", "PkPoints", "Online" };
            var text = "; preserved heading\n[Character]\nUID=" + CharacterUid + "\nName=Andreé\n" +
                string.Join('\n', zeroFields.Select(key => key + "=0")) + "\nMoney=1234\nConquerPoints=55\n" +
                "SecurityPass=1234,0,0\nSentinel = preserve café \n" +
                (includeSkin ? "AnimaSkin=" + anima + "\n" : string.Empty) +
                "[Unrelated]\nAnimaSkin=not-the-entitlement\nNote=preserve café\n";
            text = text.Replace("\n", newline, StringComparison.Ordinal);
            var encoding = EncodingFor(encodingName);
            File.WriteAllBytes(CharacterPath, Encode(text, encoding));
            items ??= FullItems();
            File.WriteAllBytes(ItemPath, PlayerItemSnapshotLoader.ToLegacyBytes(items));
            // Parse the actual owned Character file core. This is not auth/login
            // evidence that independent AccountId and CharacterUid can log in.
            var parsed = CharacterSnapshotParser.ParseText(text, CharacterPath);
            var parsedItems = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid);
            Assert.Equal(File.ReadAllBytes(ItemPath), PlayerItemSnapshotLoader.ToLegacyBytes(parsedItems));
            return new(AccountId, "fixture", parsed, parsedItems, PlayerSpellSnapshot.Empty,
                PlayerProficiencySnapshot.Empty, UsersRoot: Users);
        }

        internal static WorldSession Unlock(WorldSession source)
        {
            var cost = source.PlayerItems.InventoryItems[0];
            var items = source.PlayerItems with
            {
                InventoryItems = new[] { cost.WithStackSize((ushort)(cost.StackSize - 1)) }
                    .Concat(source.PlayerItems.InventoryItems.Skip(1)).ToArray(),
            };
            return WithSkin(source, LegacyDragonSkinState.FromSnapshot(source.Character).AddOwned(1), items);
        }

        internal static WorldSession Retrieve(WorldSession source)
        {
            var item = new PlayerItemSnapshotItem { Uid = 9000, ItemId = 4_200_018, Durability = 150,
                MaximumDurability = 150, StackSize = 1 };
            var items = source.PlayerItems with { InventoryItems = source.PlayerItems.InventoryItems.Concat([item]).ToArray() };
            return WithSkin(source, LegacyDragonSkinState.FromSnapshot(source.Character).RemoveOwned(1), items);
        }

        internal static WorldSession WithSkin(WorldSession source, LegacyDragonSkinState state, PlayerItemSnapshot items)
        {
            var fields = new Dictionary<string, string>(source.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["AnimaSkin"] = state.ToSnapshotValue() };
            return source with { Character = source.Character with { Attributes = fields }, PlayerItems = items };
        }

        internal (byte[] Character, byte[]? Items) Capture() =>
            (File.ReadAllBytes(CharacterPath), File.Exists(ItemPath) ? File.ReadAllBytes(ItemPath) : null);

        internal void AssertImages((byte[] Character, byte[]? Items) expected)
        {
            Assert.Equal(expected.Character, File.ReadAllBytes(CharacterPath));
            Assert.Equal(expected.Items, File.Exists(ItemPath) ? File.ReadAllBytes(ItemPath) : null);
        }

        internal void AssertAfter(WorldSession updated)
        {
            var text = CharacterSnapshotFileEncoding.ReadAllText(CharacterPath, out _);
            var parsed = CharacterSnapshotParser.ParseText(text, CharacterPath);
            Assert.Equal(updated.Character.Attributes["AnimaSkin"], parsed.Attributes["AnimaSkin"]);
            Assert.Equal(1234, parsed.Money);
            Assert.Equal(55U, parsed.ConquerPoints);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems), File.ReadAllBytes(ItemPath));
            Assert.False(LegacyTradeJournal.HasPending(Users));
        }

        internal void Cleanup()
        {
            // All component operations are synchronous and completed before here.
            // Never infer an original/user root from a file under test.
            ValidateRoot();
            DeleteOrdinaryTree(root);
        }

        private void ValidateRoot()
        {
            var full = Path.GetFullPath(root);
            var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("wolf-dragon-skin-persist-", StringComparison.Ordinal))
                throw new InvalidOperationException("Owned component cleanup path is not the exact temporary domain.");
            for (string? current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Owned component domain has a reparse ancestor.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private void DeleteOrdinaryTree(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (!Path.GetFullPath(entry).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Owned cleanup entry is outside the ordinary temporary domain.");
                if (Directory.Exists(entry)) DeleteOrdinaryTree(entry); else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }
    }

    private static PlayerItemSnapshot FullItems() => new(
        [Cost(), Item(11)], [Item(20, position: 213)], [Item(30, position: 217)], [Item(40, position: 4)],
        [Item(50, position: 211)], [Item(60, warehouse: 100)])
    { WarehouseItems = [Item(70, warehouse: 1000).WithDepositedItems([Item(71)])] };

    private static PlayerItemSnapshotItem Cost() => new()
    {
        Uid = 10, ItemId = 4_200_018, StackSize = 2, Bound = 0, Durability = 99, MaximumDurability = 100,
        Plus = 3, SocketOne = 1, PerfectionLevel = 2, OwnerUid = 123, OwnerName = "keeper", Signature = "cost",
    };

    private static PlayerItemSnapshotItem Item(uint uid, ushort position = 0, uint warehouse = 0) => new()
    {
        Uid = uid, ItemId = 1_000_000 + uid, Position = position, WarehouseId = warehouse,
        Durability = 100, MaximumDurability = 100, StackSize = 1, OwnerName = "keeper", Signature = "retain",
    };

    private static Encoding EncodingFor(string name) => name switch
    {
        "utf8-bom" => new UTF8Encoding(true, true),
        "windows1252" => CodePagesEncodingProvider.Instance.GetEncoding(1252)!,
        _ => new UTF8Encoding(false, true),
    };

    private static byte[] Encode(string text, Encoding encoding) => encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
}
