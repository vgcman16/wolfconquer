using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static string HistoryRoot(Fixture fixture, PackageJoin join) =>
        Path.Combine(fixture.Users, ".mailbox-package-work", join.Domain.ToString("N"));
    private static string HistoryIndexPath(Fixture fixture, PackageJoin join) => Path.Combine(HistoryRoot(fixture, join), ".index.json");
    private static JsonObject ReadHistoryIndex(Fixture fixture, PackageJoin join) =>
        JsonNode.Parse(File.ReadAllBytes(HistoryIndexPath(fixture, join)))!.AsObject();
    private static string HistoryCompletedPath(Fixture fixture, PackageJoin join, string operation) =>
        Path.Combine(HistoryRoot(fixture, join), "completed", operation[..2], operation + ".json");
    private static string HistoryDigest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static WorldDataCatalog HistoryLargeStackCatalog() => LegacyItemWorldDataFixture.Create(RankOnePackageRows.Select(row =>
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = row.Id.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedHistoryStack" + row.Id;
        fields[ItemType.DurabilityFieldIndex] = "321";
        fields[ItemType.StackSizeFieldIndex] = ushort.MaxValue.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)row.Id), fields[1], fields);
    }).ToArray());

    private static void SeedHistoryMails(Fixture fixture, int count)
    {
        fixture.SetStackInventory(StackAllCompatible());
        var mailbox = new LegacyMailboxStore(fixture.Root);
        mailbox.Load(Fixture.Uid);
        for (var index = 1; index < count; index++) mailbox.Add(Fixture.Uid, new LegacyMailboxEntry
        {
            Id = checked((uint)(1000 + index)), Flag = 1, Title = "Owned completed history " + index,
            SenderName = "System", Content = "Real registered claim, no completion callback.", Timer = fixture.AtomicMailSeed.Timer,
        });
        mailbox.Save(Fixture.Uid);
    }

    private static Dictionary<string, JsonObject> AssertHistoryChain(Fixture fixture, PackageJoin join, int completedCount, int pendingCount)
    {
        var index = ReadHistoryIndex(fixture, join);
        Assert.Equal(new[] { "LayoutVersion", "Domain", "OriginMigration", "Pending", "CompletedHead", "CompletedHeadSha256", "CompletedCount" }.Order(), index.Select(pair => pair.Key).Order());
        Assert.Equal(1, index["LayoutVersion"]!.GetValue<int>());
        Assert.Equal(join.Domain.ToString("N"), index["Domain"]!.GetValue<string>());
        Assert.Equal(pendingCount, index["Pending"]!.AsArray().Count);
        Assert.Equal((ulong)completedCount, index["CompletedCount"]!.GetValue<ulong>());
        var records = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var operation = index["CompletedHead"]!.GetValue<string>();
        var hash = index["CompletedHeadSha256"]!.GetValue<string>();
        for (var count = 0; count < completedCount; count++)
        {
            Assert.True(Guid.TryParseExact(operation, "N", out var id));
            Assert.NotEqual(Guid.Empty, id);
            var bytes = File.ReadAllBytes(HistoryCompletedPath(fixture, join, operation));
            Assert.Equal(hash, HistoryDigest(bytes));
            var entry = JsonNode.Parse(bytes)!.AsObject();
            Assert.Equal(new[] { "ArchiveVersion", "PreviousOperation", "PreviousSha256", "Work" }.Order(), entry.Select(pair => pair.Key).Order());
            Assert.Equal(1, entry["ArchiveVersion"]!.GetValue<int>());
            var work = entry["Work"]!.AsObject();
            Assert.True(work["Completed"]!.GetValue<bool>());
            Assert.Equal(operation, work["Operation"]!.GetValue<string>());
            Assert.True(records.TryAdd(operation, work));
            operation = entry["PreviousOperation"]!.GetValue<string>();
            hash = entry["PreviousSha256"]!.GetValue<string>();
        }
        Assert.Equal(Guid.Empty.ToString("N"), operation);
        Assert.Equal(new string('0', 64), hash);
        var completedRoot = Path.Combine(HistoryRoot(fixture, join), "completed");
        var actual = Directory.Exists(completedRoot)
            ? Directory.GetFiles(completedRoot, "*", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension).Order().ToArray()
            : Array.Empty<string>();
        Assert.Equal(records.Keys.Order(), actual);
        return records;
    }

    private static void AssertHistoryBusinessFiles(Fixture fixture, Dictionary<string, byte[]> expected)
    {
        foreach (var path in new[] { fixture.CharacterPath, fixture.ItemPath, fixture.AtomicMailPath, fixture.PackageGearPath })
            Assert.Equal(expected[Path.GetRelativePath(fixture.Root, path)], File.ReadAllBytes(path));
    }
}
