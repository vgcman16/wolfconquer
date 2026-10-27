using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed record PackageJoinBefore(WorldSession Owner, string Json, Dictionary<string, byte[]> Files, int FirstFrame,
        string[] BusinessPaths, LegacyMailboxEntry MailSeed, PackageReferenceCatalog[] Catalog);

    private static PackageJoinBefore CapturePackageJoin(Fixture fixture, PackageJoin join) =>
        new(join.Current, JsonSerializer.Serialize(join.Current), fixture.PackageFiles(), fixture.Sent.Count,
            [fixture.CharacterPath, fixture.ItemPath, fixture.AtomicMailPath, fixture.PackageGearPath], fixture.AtomicMailSeed,
            CapturePackageReferenceCatalog(join));

    private static void AssertPackageJoinFiles(Fixture fixture, Dictionary<string, byte[]> expected)
    {
        var actual = fixture.PackageFiles();
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static void AssertPackageJoinUnchanged(Fixture fixture, PackageJoin join, PackageJoinBefore before, int extraFrames = 0)
    {
        Assert.Same(before.Owner, join.Current);
        Assert.Equal(before.Json, JsonSerializer.Serialize(join.Current));
        AssertPackageJoinFiles(fixture, before.Files);
        Assert.Equal(before.FirstFrame + extraFrames, fixture.Sent.Count);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static LegacyItemUidProducerIntentSnapshot AssertPackageJoinAllocation(Fixture fixture, PackageJoin join)
    {
        Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var intents, out var reason), reason);
        var intent = Assert.Single(intents);
        Assert.Equal(join.Domain, intent.DomainId);
        Assert.Equal(fixture.Account, intent.Request.AccountId);
        Assert.Equal(Fixture.Uid, intent.Request.CharacterUid);
        Assert.Equal(6, intent.Request.Count);
        Assert.NotEqual(Guid.Empty, intent.Request.OperationId);
        Assert.Equal(1UL, intent.PreparedGeneration);
        var payload = intent.CopyPayload();
        Assert.InRange(payload.Length, 1, LegacyItemUidRegistry.MaximumProducerPayloadBytes);
        Assert.Equal(0x32504d57U, BinaryPrimitives.ReadUInt32LittleEndian(payload));
        Assert.Equal(intent.Request.PayloadSha256, Convert.ToHexString(SHA256.HashData(payload)), ignoreCase: true);
        var receipt = Assert.IsType<LegacyItemUidReservationReceipt>(intent.Allocation);
        Assert.Equal(PackageHistoricalFloor + 1, receipt.First);
        Assert.Equal(6, receipt.Count);
        Assert.Equal(2UL, receipt.Generation);
        Assert.Equal(intent.Request.OperationId, receipt.OperationId);
        return intent;
    }

    private static PlayerItemSnapshot ExpectedPackageJoinItems(PackageJoinBefore before,
        LegacyItemUidProducerIntentSnapshot intent, bool claimAll)
    {
        var first = intent.Allocation!.First;
        var colors = ReferencePackageColors(before, intent.DomainId, [before.MailSeed], 6);
        var generated = RankOnePackageRows.Select((row, index) => new PlayerItemSnapshotItem
        {
            Uid = checked(first + (uint)index), ItemId = row.Id, Durability = 321,
            MaximumDurability = 321, StackSize = row.Quantity, Color = colors[index],
        });
        var baseItems = claimAll ? ExpectedAtomicMailItems(before.Owner.PlayerItems) : before.Owner.PlayerItems;
        var attachment = claimAll ? baseItems.InventoryItems.Where(row => row.Uid == AtomicMailItemUid) : [];
        return baseItems with { InventoryItems = before.Owner.PlayerItems.InventoryItems.Concat(generated).Concat(attachment).ToArray() };
    }

    private static void AssertPackageJoinCommitted(Fixture fixture, PackageJoin join, PackageJoinBefore before,
        bool claimAll, long job)
    {
        var intent = AssertPackageJoinAllocation(fixture, join);
        var expected = ExpectedPackageJoinUnallocatedFiles(fixture, before, claimAll, job);
        var items = ExpectedPackageJoinItems(before, intent, claimAll);
        expected[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        AssertPackageJoinLedger(fixture, before, expected);
        IncludeDebtCompletedWorkFile(fixture, join, before, intent, expected);
        AssertPackageJoinFiles(fixture, expected);
        Assert.Equal(JsonSerializer.Serialize(items), JsonSerializer.Serialize(join.Current.PlayerItems));
        Assert.Equal(JsonSerializer.Serialize(items), JsonSerializer.Serialize(new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid)));
        Assert.Equal(JsonSerializer.Serialize(PackageJoinGearAfter(job)), JsonSerializer.Serialize(join.Current.CombatGear));
        Assert.Equal(1031U, uint.Parse(join.Current.Character.Attributes["ClassExperience"], CultureInfo.InvariantCulture));
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static Dictionary<string, byte[]> ExpectedPackageJoinUnallocatedFiles(Fixture fixture,
        PackageJoinBefore before, bool claimAll, long job)
    {
        var expected = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        var characterPath = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
        var fields = new Dictionary<string, string>
        { ["ClassExperience"] = (PackageClassExperience + 1000).ToString(CultureInfo.InvariantCulture) };
        if (job is >= 5000 and <= 5057) fields["MsgNinja"] = PackageJoinNinjaLine.Replace("1/25/", "1/75/", StringComparison.Ordinal);
        if (claimAll)
        {
            fields["Money"] = (before.Owner.Character.Money + (long)AtomicMailMoney).ToString(CultureInfo.InvariantCulture);
            fields["ConquerPoints"] = (before.Owner.Character.ConquerPoints + AtomicMailCp).ToString(CultureInfo.InvariantCulture);
        }
        expected[characterPath] = ProjectSashFields(expected[characterPath], fields);
        var mailPath = Path.GetRelativePath(fixture.Root, fixture.AtomicMailPath);
        var mail = expected[mailPath].ToArray();
        Assert.Equal(4 + 2 * 740, mail.Length);
        Assert.Equal(AtomicMailId, BinaryPrimitives.ReadUInt32LittleEndian(mail.AsSpan(4 + 704)));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(mail.AsSpan(4 + 728)));
        mail.AsSpan(4 + 728, 4).Clear();
        if (claimAll) mail.AsSpan(4 + 708, 16).Clear();
        expected[mailPath] = mail;
        expected[Path.GetRelativePath(fixture.Root, fixture.PackageGearPath)] =
            Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(PackageJoinGearAfter(job)));
        return expected;
    }

    private static CombatGearSnapshot PackageJoinGearAfter(long job)
    {
        var gear = PackageJoinGear();
        return job is >= 7000 and <= 7057 ? gear with { PirateExp = 67, AirPowers = gear.AirPowers with { Points = 69 } } : gear;
    }

    private static void AssertPackageJoinLedger(Fixture fixture, PackageJoinBefore before, Dictionary<string, byte[]> expected)
    {
        var path = Path.GetRelativePath(fixture.Root, fixture.PackageUidState);
        var previous = JsonNode.Parse(before.Files[path])!.AsObject();
        var bytes = File.ReadAllBytes(fixture.PackageUidState);
        var current = JsonNode.Parse(bytes)!.AsObject();
        Assert.Equal(3, current["Version"]!.GetValue<int>());
        Assert.Equal(2UL, current["Generation"]!.GetValue<ulong>());
        Assert.Equal(PackageHistoricalFloor + 6, current["HighWater"]!.GetValue<uint>());
        Assert.Single(current["ProducerIntents"]!.AsArray());
        Assert.Single(current["Operations"]!.AsArray());
        foreach (var property in previous.Where(property => property.Key is not ("Version" or "Generation" or "HighWater" or "Operations" or "ProducerIntents")))
            Assert.True(JsonNode.DeepEquals(property.Value, current[property.Key]), property.Key);
        Assert.Equal(previous.Select(property => property.Key).Union(["Operations", "ProducerIntents"]).Order(), current.Select(property => property.Key).Order());
        // UID state is checked independently above and through the real typed
        // receipt/payload; all four business images are independently projected.
        expected[path] = bytes;
    }
}
