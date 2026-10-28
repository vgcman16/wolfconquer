using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterItemsSpellsJournalTests
{
    private static void Run(Action<Fixture> action, bool missingSpells = false)
    {
        var f = new Fixture(); Exception? primary = null;
        try { f.Initialize(missingSpells); action(f); } catch (Exception e) { primary = e; }
        try { f.Dispose(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException($"Owned journal fixture {f.Root}: operation and cleanup failed.", primary, cleanup);
            ExceptionDispatchInfo.Capture(cleanup).Throw();
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    private sealed class Fixture : IDisposable
    {
        internal const long Account = 1294001;
        internal const uint Uid = 1294017;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-v9-journal-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "Items");
        internal string Spells => Path.Combine(Root, "Spells");
        internal string CharacterPath => Path.Combine(Users, Account + ".ini");
        internal string ItemsPath => Path.Combine(Items, Uid + ".bin");
        internal string SpellsPath => Path.Combine(Spells, Uid + ".bin");
        internal string Pending => Path.Combine(Users, ".managed-trade-journal", "pending.json");
        internal byte[] Character = null!, ItemBefore = null!, ItemAfter = null!, SpellBefore = null!, SpellAfter = null!;
        private bool owned;

        internal void Initialize(bool missingSpells)
        {
            Guard(); Assert.False(Directory.Exists(Root));
            Directory.CreateDirectory(Users); owned = true; Directory.CreateDirectory(Items);
            if (!missingSpells) Directory.CreateDirectory(Spells);
            Character = new UTF8Encoding(false).GetBytes($"""
                [Character]
                UID={Uid}
                Name=SyntheticJournalOwner
                Body=1003
                Face=0
                Haire=0
                Class=15
                FirstClass=13005
                SecoundeClass=2005
                Reborn=0
                Level=110
                Experience=777
                Map=1002
                X=430
                Y=378
                PMap=1002
                PMapX=430
                PMapY=378
                Strength=11
                Agility=12
                Vitaliti=13
                Spirit=14
                Atributes=10
                MinHitPoints=50
                MinMana=25
                MaxMana=25
                Money=100
                ConquerPoints=200
                BoundConquerPoints=300
                BankCps=400
                PkPoints=0
                Online=1
                Sentinel=preserve
                [Other]
                Note=owned fixture
                """);
            var kept = new PlayerItemSnapshotItem { Uid = 900001, ItemId = 753003, StackSize = 1,
                PerfectionLevel = 2, OwnerUid = Uid, OwnerName = "Fixture", Signature = "owned" };
            var consumed = new PlayerItemSnapshotItem { Uid = 900002, ItemId = 723001, StackSize = 1 };
            ItemBefore = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([kept, consumed], [], [], [], []));
            ItemAfter = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([kept], [], [], [], []));
            SpellBefore = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(6001, 4, 1111, 2, 3, 2)]));
            SpellBefore[15] = 0xA5;
            SpellAfter = Convert.FromHexString("02000000").Concat(SpellBefore.Skip(4))
                .Concat(Convert.FromHexString("721700000000000000000000")).ToArray(); // ID6002, independent raw row.
            File.WriteAllBytes(CharacterPath, Character); File.WriteAllBytes(ItemsPath, ItemBefore);
            if (!missingSpells) File.WriteAllBytes(SpellsPath, SpellBefore);
            Assert.NotEqual(Account, (long)Uid);
            Assert.Equal((long)Uid, CharacterSnapshotParser.ParseText(Encoding.UTF8.GetString(Character)).Uid);
            LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(ItemBefore);
            LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(ItemAfter);
        }

        internal LegacyTradeJournalChange[] Changes(bool missingSpells = false) => [
            new(LegacyTradeJournalFileKind.Character, Account, Character, Character.ToArray()),
            new(LegacyTradeJournalFileKind.Items, Uid, ItemBefore, ItemAfter),
            new(LegacyTradeJournalFileKind.Spells, Uid, missingSpells ? null : SpellBefore, SpellAfter)];
        internal LegacyTradeRecoveryState Recovery(string? items = null, string? spells = null) =>
            new(Users, items ?? Items, null, null, null, spells ?? Spells, null);
        internal void Images(byte[] character, byte[] items, byte[] spells)
        { Assert.Equal(character, File.ReadAllBytes(CharacterPath)); Assert.Equal(items, File.ReadAllBytes(ItemsPath)); Assert.Equal(spells, File.ReadAllBytes(SpellsPath)); }
        internal void Guard()
        {
            var root = Path.GetFullPath(Root); var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.True(Path.IsPathFullyQualified(Root)); Assert.Equal(parent, Path.GetDirectoryName(root));
            const string prefix = "wolf-v9-journal-"; var name = Path.GetFileName(root); Assert.StartsWith(prefix, name);
            Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(root);
        }
        public void Dispose()
        {
            if (!owned) return; Guard();
            void Delete(string path)
            {
                var full = Path.GetFullPath(path);
                if (!full.Equals(Root, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Cleanup escaped owned fixture.");
                var attrs = File.GetAttributes(full);
                if ((attrs & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Cleanup refuses reparse entries.");
                if ((attrs & FileAttributes.Directory) != 0) { foreach (var child in Directory.GetFileSystemEntries(full)) Delete(child); Directory.Delete(full); }
                else File.Delete(full);
            }
            Delete(Root);
        }
    }
}
