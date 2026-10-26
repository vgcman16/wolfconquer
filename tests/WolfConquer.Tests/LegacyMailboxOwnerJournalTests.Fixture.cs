using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxOwnerJournalTests
{
    private static void Run(Action<Fixture> body)
    {
        var fixture = new Fixture();
        var errors = new List<Exception>();
        try { body(fixture); } catch (Exception error) { errors.Add(error); }
        try { fixture.Cleanup(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed class Fixture
    {
        internal const long AccountId = 301;
        internal const uint CharacterUid = 1_293_600;
        internal const uint AttachmentUid = 900;
        private const string Prefix = "wolf-mailbox-owner-journal-";
        internal readonly string Root = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "PlayersItems");
        internal string Mailbox => Path.Combine(Root, "PlayersMailList");
        internal string Pending => Path.Combine(Users, ".managed-trade-journal", "pending.json");

        internal LegacyTradeJournalChange[] Seed()
        {
            ValidateRoot();
            Directory.CreateDirectory(Users);
            Directory.CreateDirectory(Items);
            Directory.CreateDirectory(Mailbox);
            var beforeText = $$"""
                ; Preserve unrelated bytes, including this comment.
                [Character]
                UID={{CharacterUid}}
                Name=MailboxOwner
                Body=1003
                Face=0
                Haire=0
                Class=7051
                FirstClass=7051
                SecoundeClass=0
                Reborn=0
                Level=140
                Experience=123
                Map=1002
                X=577
                Y=586
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
            var afterText = beforeText.Replace("Money=1234", "Money=1284", StringComparison.Ordinal)
                .Replace("ConquerPoints=55", "ConquerPoints=60", StringComparison.Ordinal);
            Assert.Equal(CharacterUid, CharacterSnapshotParser.ParseText(beforeText).Uid);
            var attachment = Row(AttachmentUid).WithWarehouseId(1000)
                .WithDepositedItems([Row(901)]);
            var items = new PlayerItemSnapshot([Row(800)], [Row(3500).WithPosition(213)],
                [Row(3000).WithPosition(217)], [Row(5000).WithPosition(4)],
                [Row(4000).WithPosition(211)], [Row(2500).WithWarehouseId(100)])
            { WarehouseItems = [Row(4500).WithWarehouseId(8), attachment] };
            var afterItems = items with
            {
                InventoryItems = [items.InventoryItems[0], attachment.WithWarehouseId(0, 0)],
                WarehouseItems = [items.WarehouseItems[0]],
            };

            // Construct both mailbox images through the existing managed codec.
            // This is fixture data, not claim authorization or UID allocation.
            var mailboxes = new LegacyMailboxStore(Root);
            var reward = new LegacyMailboxEntry
            {
                Id = 1, Title = "Fixture reward", SenderName = "Journal fixture", Content = "Preserve text",
                Money = 50, EMoney = 5, ItemId = AttachmentUid, EmoneyRecordType = 17,
                Timer = DateTime.Now.AddDays(1).Ticks,
            };
            mailboxes.Add(CharacterUid, reward);
            mailboxes.Add(CharacterUid, new LegacyMailboxEntry
            {
                Id = 2, Title = "Unrelated", SenderName = "Keep", Content = "Unchanged",
                Money = 7, EMoney = 3, Timer = DateTime.Now.AddDays(2).Ticks,
            });
            mailboxes.Save(CharacterUid);
            var mailBefore = File.ReadAllBytes(mailboxes.GetMailboxPath(CharacterUid));
            Assert.Equal(4 + 2 * 740, mailBefore.Length);
            Assert.True(mailboxes.TryReplace(CharacterUid, reward.WithClaimedMoney().WithClearedItemId()));
            mailboxes.Save(CharacterUid);
            var mailAfter = File.ReadAllBytes(mailboxes.GetMailboxPath(CharacterUid));
            Assert.Equal(mailBefore.Length, mailAfter.Length);

            LegacyTradeJournalChange[] changes =
            [
                new(LegacyTradeJournalFileKind.Character, AccountId, Encoding.UTF8.GetBytes(beforeText), Encoding.UTF8.GetBytes(afterText)),
                new(LegacyTradeJournalFileKind.Items, CharacterUid,
                    PlayerItemSnapshotLoader.ToLegacyBytes(items), PlayerItemSnapshotLoader.ToLegacyBytes(afterItems)),
                new(LegacyTradeJournalFileKind.Mailbox, CharacterUid, mailBefore, mailAfter),
            ];
            foreach (var change in changes) File.WriteAllBytes(Target(change), change.Before!);
            Assert.Equal(changes[1].Before,
                PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid)));
            Assert.Equal(AttachmentUid, new LegacyMailboxStore(Root).Load(CharacterUid)[0].ItemId);
            return changes;
        }

        private static PlayerItemSnapshotItem Row(uint uid) => new()
        {
            Uid = uid, ItemId = 1_000_000 + uid, StackSize = 1, Durability = 91, MaximumDurability = 100,
            Plus = 3, Bless = 1, Enchant = 7, Color = 4, PerfectionLevel = 2,
            OwnerUid = 123, OwnerName = "keeper", Signature = "retain",
        };

        internal string Target(LegacyTradeJournalChange change) => Path.Combine(change.Kind switch
        {
            LegacyTradeJournalFileKind.Character => Users,
            LegacyTradeJournalFileKind.Items => Items,
            LegacyTradeJournalFileKind.Mailbox => Mailbox,
            _ => throw new ArgumentException("Unknown test target."),
        }, change.FileId.ToString(CultureInfo.InvariantCulture) +
            (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin"));

        internal Dictionary<string, byte[]> CaptureFiles() => Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);

        internal void AssertFiles(Dictionary<string, byte[]> before)
        {
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal),
                Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach (var file in before) Assert.Equal(file.Value, File.ReadAllBytes(file.Key));
        }

        internal void AssertImages(IEnumerable<LegacyTradeJournalChange> changes, bool after)
        {
            foreach (var change in changes) Assert.Equal(after ? change.After : change.Before, File.ReadAllBytes(Target(change)));
        }

        internal void AssertClaimedImages()
        {
            var character = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(Users, AccountId + ".ini")));
            Assert.Equal(1284L, character.Money);
            Assert.Equal(60L, character.ConquerPoints);
            var items = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(CharacterUid);
            var claimed = Assert.Single(items.InventoryItems, row => row.Uid == AttachmentUid);
            Assert.Equal(901U, Assert.Single(claimed.DepositedItems).Uid);
            Assert.DoesNotContain(items.WarehouseItems, row => row.Uid == AttachmentUid);
            var mails = new LegacyMailboxStore(Root).Load(CharacterUid);
            Assert.Equal(2, mails.Count);
            var mail = Assert.Single(mails, row => row.Id == 1);
            Assert.Equal(0UL, mail.Money);
            Assert.Equal(0U, mail.EMoney);
            Assert.Equal(0U, mail.ItemId);
            Assert.Equal(7UL, Assert.Single(mails, row => row.Id == 2).Money);
        }

        private void ValidateRoot()
        {
            var full = Path.GetFullPath(Root);
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var name = Path.GetFileName(full);
            if (!string.Equals(temp, Path.GetDirectoryName(full), comparison) || !name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[Prefix.Length..], "N", out _))
                throw new InvalidOperationException("Fixture root is not its owned temporary directory.");
            for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
                if ((File.Exists(path) || Directory.Exists(path)) &&
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Fixture cleanup refuses a reparse path.");
        }

        internal void Cleanup()
        {
            ValidateRoot();
            DeleteOrdinary(Root);
        }

        private void DeleteOrdinary(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (!Path.GetFullPath(entry).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar, comparison) ||
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Fixture cleanup target is not an ordinary owned child.");
                if (Directory.Exists(entry)) DeleteOrdinary(entry); else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }
    }
}
