using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyOwnedPlayerInspectionTests : IDisposable
{
    private const uint Account = 1399701;
    private const uint InitialItem = 2399701;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-owned-admission-" + Guid.NewGuid().ToString("N"));
    private string Configuration => Path.Combine(root, LegacyOwnedAdmissionFixture.ConfigurationFileName);

    private LegacyOwnedAdmissionOptions Create()
    {
        Assert.True(LegacyOwnedAdmissionFixture.TryCreate(root, Account, Account, InitialItem,
            ConquerProtocolProfile.VisibleDx8D13, out _, out var reason), reason);
        Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(Configuration, out var options, out reason), reason);
        return Assert.IsType<LegacyOwnedAdmissionOptions>(options);
    }

    [Fact]
    public void InspectionWhileActualRegistryLeaseIsHeldDoesNotOpenOwnerOrModifyFilesOrExposeSecrets()
    {
        var options = Create();
        var character = Path.Combine(options.UsersRoot, Account + ".ini");
        File.AppendAllText(character, "\r\n[UnrelatedPrivateSection]\r\nPassword=NEVER-OUTPUT-THIS\r\n");
        Assert.True(LegacyItemUidRegistry.TryOpen(options.RegistryOptions, options.ExpectedDomain, out var registry, out var reason), reason);
        using (registry)
        {
            var before = CaptureFiles();
            Assert.True(LegacyOwnedPlayerInspection.TryInspect(Configuration, Account, out var report, out reason), reason);
            var actual = Assert.IsType<LegacyOwnedPlayerInspectionReport>(report);
            Assert.Equal(options.ExpectedDomain, actual.Domain);
            Assert.Equal(Account, actual.AccountId);
            Assert.Equal((long)Account, actual.Character.Uid);
            Assert.Equal((1002L, 410L, 354L), (actual.Character.Map, actual.Character.X, actual.Character.Y));
            Assert.Equal((100L, 50L), (actual.Character.Gold, actual.Character.ConquerPoints));
            Assert.Equal(1, actual.InventoryCount);
            Assert.Equal(0, actual.EquipmentCount);
            Assert.Equal(0, actual.SpellCount);
            Assert.Equal(0, actual.ProficiencyCount);
            var item = Assert.Single(actual.Items);
            Assert.Equal(InitialItem, item.Uid);
            Assert.Equal(1001000U, item.ItemId);
            Assert.Null(actual.ProtocolProfile);
            Assert.False(actual.LiveStoreSynchronized);
            Assert.All(actual.Files, file => Assert.True(file.BytesAndMetadataUnchanged));
            var json = JsonSerializer.Serialize(actual);
            foreach (var forbidden in new[] { "NEVER-OUTPUT-THIS", "SecurityPass", "Password", root, "SourceFile", "HistoricalEvidence" })
                Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
            AssertFilesUnchanged(before);
        }
    }

    [Fact]
    public void ActualSavedMailAttachmentAndInventoryLocationAreReportedWithoutMutatingDetachedOrLiveData()
    {
        var options = Create();
        var items = new PlayerItemSnapshotLoader(options.ItemsRoot);
        var original = items.LoadByCharacterUid(Account);
        var attached = Assert.Single(original.InventoryItems).WithWarehouseId(1000);
        items.SaveByCharacterUid(Account, original with { InventoryItems = [], WarehouseItems = new[] { attached } });
        var mails = new LegacyMailboxStore(root);
        mails.Add(Account, new LegacyMailboxEntry { Id = 17, ItemId = attached.Uid, Timer = DateTime.Now.AddDays(1).Ticks,
            Title = "Private title omitted", SenderName = "Private sender omitted", Content = "Private body omitted" });
        mails.Save(Account);
        var before = CaptureFiles();

        Assert.True(LegacyOwnedPlayerInspection.TryInspect(Configuration, Account, out var report, out var reason), reason);

        var actual = Assert.IsType<LegacyOwnedPlayerInspectionReport>(report);
        Assert.Equal(0, actual.InventoryCount);
        Assert.Equal(1, actual.WarehouseCount);
        var mail = Assert.Single(actual.Mails);
        Assert.Equal(17U, mail.MailId);
        Assert.Equal(attached.Uid, mail.AttachedItemUid);
        Assert.True(mail.AttachmentPresentInInbox);
        Assert.DoesNotContain("Private", JsonSerializer.Serialize(actual), StringComparison.Ordinal);
        AssertFilesUnchanged(before);
    }

    [Fact]
    public void PendingRealJournalNamespaceRejectsWithoutReplayingOrEditingIt()
    {
        var options = Create();
        var journal = Path.Combine(options.UsersRoot, ".managed-trade-journal");
        Directory.CreateDirectory(journal);
        File.WriteAllText(Path.Combine(journal, "pending.json"), "retained inspection refusal sentinel");
        var before = CaptureFiles();

        Assert.False(LegacyOwnedPlayerInspection.TryInspect(Configuration, Account, out var report, out var reason));

        Assert.Null(report);
        Assert.Equal("ownedPlayerInspectionUnstable:pendingTradeJournal", reason);
        AssertFilesUnchanged(before);
    }

    [Fact]
    public void CountTrailingBytesRejectAndUnknownFileIsNeverTreatedAsConfiguration()
    {
        var options = Create();
        var spells = Path.Combine(options.SpellsRoot, Account + ".bin");
        File.WriteAllBytes(spells, new byte[5]);
        var before = CaptureFiles();
        Assert.False(LegacyOwnedPlayerInspection.TryInspect(Configuration, Account, out var report, out var reason));
        Assert.Null(report);
        Assert.Equal("ownedPlayerInspectionSnapshotUnreadableOrInvalid", reason);
        AssertFilesUnchanged(before);
        var unrelated = Path.Combine(root, "credentials.json");
        File.WriteAllText(unrelated, "UNRELATED-SECRET");
        using var locked = new FileStream(unrelated, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.False(LegacyOwnedPlayerInspection.TryInspect(unrelated, Account, out report, out reason));
        Assert.Null(report);
        Assert.Equal("ownedPlayerInspectionRequestInvalid", reason);
    }

    [Fact]
    public void ActualChangedFileObservationsCannotQualifyAsUnchangedBaseline()
    {
        var options = Create();
        var path = Path.Combine(options.UsersRoot, Account + ".ini");
        var before = OwnedPlayerFileObservation.Read("Character", path, CharacterSnapshotFileBaseline.MaximumBytes, true);
        File.AppendAllText(path, "\r\n; real changed file\r\n");
        var after = OwnedPlayerFileObservation.Read("Character", path, CharacterSnapshotFileBaseline.MaximumBytes, true);
        Assert.False(before.SameImage(after));
    }

    [Fact]
    public void GenuinelyReservedSavedRowsHaveBoundedSummaryButFullCounts()
    {
        var options = Create();
        Assert.True(LegacyItemUidRegistry.TryOpen(options.RegistryOptions, options.ExpectedDomain, out var registry, out var reason), reason);
        using (registry)
        {
            Assert.True(registry!.TryReserve(204, out var reservation, out reason), reason);
            Assert.True(reservation!.TryTake(out var uids));
            var loader = new PlayerItemSnapshotLoader(options.ItemsRoot);
            var original = loader.LoadByCharacterUid(Account);
            loader.SaveByCharacterUid(Account, original with { InventoryItems = original.InventoryItems.Concat(uids.Select(uid =>
                new PlayerItemSnapshotItem { Uid = uid, ItemId = 1001000, Position = 0, StackSize = 1,
                    Durability = 1, MaximumDurability = 1 })).ToArray() });
            var before = CaptureFiles();

            Assert.True(LegacyOwnedPlayerInspection.TryInspect(Configuration, Account, out var report, out reason), reason);

            var actual = Assert.IsType<LegacyOwnedPlayerInspectionReport>(report);
            Assert.Equal(205, actual.InventoryCount);
            Assert.Equal(205, actual.PhysicalItemCount);
            Assert.Equal(200, actual.Items.Length + actual.Mails.Length);
            Assert.True(actual.SummariesTruncated);
            AssertFilesUnchanged(before);
        }
    }

    private Dictionary<string, (byte[] Bytes, long Length, DateTime Changed)> CaptureFiles() =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "owner.lock")
            .ToDictionary(path => path, path => (File.ReadAllBytes(path), new FileInfo(path).Length, File.GetLastWriteTimeUtc(path)));

    private void AssertFilesUnchanged(Dictionary<string, (byte[] Bytes, long Length, DateTime Changed)> before)
    {
        var after = CaptureFiles();
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var (path, value) in before)
        {
            Assert.Equal(value.Bytes, after[path].Bytes);
            Assert.Equal(value.Length, after[path].Length);
            Assert.Equal(value.Changed, after[path].Changed);
        }
    }

    public void Dispose()
    {
        var full = Path.GetFullPath(root);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(full).StartsWith(LegacyOwnedAdmissionFixture.DirectoryPrefix, StringComparison.Ordinal))
            throw new IOException("Owned inspection fixture cleanup target invalid.");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
