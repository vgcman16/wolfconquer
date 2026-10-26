using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static string DebtWorkPath(Fixture fixture, PackageJoin join, Guid operation) =>
        ReadHistoryIndex(fixture, join)["Pending"]!.AsArray().Any(row => row!["Operation"]!.GetValue<string>() == operation.ToString("N"))
            ? HistoryIndexPath(fixture, join) : HistoryCompletedPath(fixture, join, operation.ToString("N"));

    private static Dictionary<string, byte[]> DebtAfterFiles(Fixture fixture, PackageJoinBefore initial,
        LegacyItemUidProducerIntentSnapshot intent)
    {
        var files = ExpectedPackageJoinUnallocatedFiles(fixture, initial, claimAll: true, job: 10);
        files[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] =
            PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedPackageJoinItems(initial, intent, claimAll: true));
        return files;
    }

    private static LegacyMailboxPackageWorkStore.Record PrepareDebtWork(Fixture fixture, PackageJoin join,
        PackageJoinBefore initial, LegacyItemUidProducerIntentSnapshot intent)
    {
        var producer = join.ReadDebtProducer();
        var work = new LegacyMailboxPackageWorkStore(producer);
        var after = DebtAfterFiles(fixture, initial, intent);
        var recipe = CreatePackageJoinRecipe(fixture, join, initial);
        Assert.True(producer.TryPrepareJointWork(recipe, intent.Allocation!, PackageJoinJournalChanges(fixture, initial.Files, after),
            out var prepared, out var reason), reason);
        Assert.NotNull(prepared);
        return Assert.IsType<LegacyMailboxPackageWorkStore.Record>(work.Read(intent.Request.OperationId));
    }

    private static void AssertDebtWork(Fixture fixture, PackageJoin join, PackageJoinBefore initial,
        LegacyItemUidProducerIntentSnapshot intent, Dictionary<string, byte[]> after, bool completed)
    {
        var producer = join.ReadDebtProducer();
        var store = new LegacyMailboxPackageWorkStore(producer);
        var row = Assert.IsType<LegacyMailboxPackageWorkStore.Record>(store.Read(intent.Request.OperationId));
        Assert.Equal(1, row.Version);
        Assert.Equal(join.Domain.ToString("N"), row.Domain);
        Assert.Equal(intent.Request.OperationId.ToString("N"), row.Operation);
        Assert.Equal(intent.Request.PayloadSha256, row.PayloadSha256);
        Assert.Equal(intent.Request.AccountId, row.AccountId);
        Assert.Equal(intent.Request.CharacterUid, row.CharacterUid);
        Assert.Equal(intent.Allocation!.First, row.First);
        Assert.Equal(intent.Allocation.Count, row.Count);
        Assert.Equal(fixture.Users, row.Users);
        Assert.Equal(fixture.Items, row.Items);
        Assert.Equal(fixture.PackageMailboxRoot, row.Mailbox);
        Assert.Equal(fixture.PackageGearRoot, row.CombatGear);
        Assert.Equal(completed, row.Completed);
        var paths = new[] { fixture.CharacterPath, fixture.ItemPath, fixture.AtomicMailPath, fixture.PackageGearPath };
        string Hash(Dictionary<string, byte[]> images, string path) =>
            Convert.ToHexString(SHA256.HashData(images[Path.GetRelativePath(fixture.Root, path)])).ToLowerInvariant();
        Assert.Equal(paths.Select(path => Hash(initial.Files, path)), row.Before);
        Assert.Equal(paths.Select(path => Hash(after, path)), row.After);
        using var document = JsonDocument.Parse(ReadHistoryWorkDocument(fixture, join, intent.Request.OperationId).ToJsonString());
        Assert.Equal(new[] { "Version", "Domain", "Operation", "PayloadSha256", "AccountId", "CharacterUid", "First", "Count",
            "Users", "Items", "Mailbox", "CombatGear", "Before", "After", "Completed" }.Order(),
            document.RootElement.EnumerateObject().Select(property => property.Name).Order());
    }

    private static void AssertDebtPackageCommitted(Fixture fixture, PackageJoin join, PackageJoinBefore initial,
        LegacyItemUidProducerIntentSnapshot intent)
    {
        var expected = DebtAfterFiles(fixture, initial, intent);
        AssertPackageJoinAllocation(fixture, join);
        AssertPackageJoinLedger(fixture, initial, expected);
        AssertDebtWork(fixture, join, initial, intent, expected, completed: true);
        IncludeValidatedHistoryMetadata(fixture, join, expected, 1, 0);
        AssertPackageJoinFiles(fixture, expected);
        var items = ExpectedPackageJoinItems(initial, intent, claimAll: true);
        Assert.Equal(JsonSerializer.Serialize(items), JsonSerializer.Serialize(join.Current.PlayerItems));
        Assert.Equal(initial.Owner.Character.Money + (long)AtomicMailMoney, join.Current.Character.Money);
        Assert.Equal(initial.Owner.Character.ConquerPoints + AtomicMailCp, join.Current.Character.ConquerPoints);
        Assert.Equal("1031", join.Current.Character.Attributes["ClassExperience"]);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static void IncludeDebtCompletedWorkFile(Fixture fixture, PackageJoin join, PackageJoinBefore initial,
        LegacyItemUidProducerIntentSnapshot intent, Dictionary<string, byte[]> expected)
    {
        AssertDebtWork(fixture, join, initial, intent, expected, completed: true);
        IncludeValidatedHistoryMetadata(fixture, join, expected, 1, 0);
    }

    private sealed partial class PackageJoin
    {
        internal LegacyMailboxPackageProducer ReadDebtProducer() => Handler is null
            ? Assert.IsType<LegacyMailboxPackageProducer>(componentContext!.MailboxPackages)
            : ActualDebtFrameOwner<LegacyMailboxPackageProducer>(this, "_mailboxPackages");
    }
}
