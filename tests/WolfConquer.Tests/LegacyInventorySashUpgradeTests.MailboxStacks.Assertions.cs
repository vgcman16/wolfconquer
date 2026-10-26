using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static LegacyItemUidProducerIntentSnapshot ReadStackAllocation(Fixture fixture, PackageJoin join, int count)
    {
        Assert.True(count > 0);
        Assert.True(join.Registry.TryReadProducerIntents(join.Domain, out var intents, out var reason), reason);
        var intent = Assert.Single(intents);
        Assert.Equal(LegacyMailboxPackageProducer.Writer, intent.Request.Writer);
        Assert.Equal(fixture.Account, intent.Request.AccountId);
        Assert.Equal(Fixture.Uid, intent.Request.CharacterUid);
        Assert.Equal(count, intent.Request.Count);
        Assert.Equal(1UL, intent.PreparedGeneration);
        Assert.Equal(intent.Request.PayloadSha256, Convert.ToHexString(SHA256.HashData(intent.CopyPayload())), ignoreCase: true);
        var allocation = Assert.IsType<LegacyItemUidReservationReceipt>(intent.Allocation);
        Assert.Equal(join.Domain, allocation.DomainId);
        Assert.Equal(intent.Request.OperationId, allocation.OperationId);
        Assert.Equal(intent.Request.Writer, allocation.Writer);
        Assert.Equal(intent.Request.PayloadSha256, allocation.PayloadSha256);
        Assert.Equal(PackageHistoricalFloor + 1, allocation.First);
        Assert.Equal(count, allocation.Count);
        Assert.Equal(2UL, allocation.Generation);
        return intent;
    }

    private static PlayerItemSnapshotItem ExpectedFreshStack(Fixture fixture, PackageJoin join,
        int count, int index, uint itemId, ushort quantity)
    {
        var intent = ReadStackAllocation(fixture, join, count);
        var uid = checked(intent.Allocation!.First + (uint)index);
        var actual = Assert.Single(join.Current.PlayerItems.InventoryItems, item => item.Uid == uid);
        Assert.InRange(actual.Color, 3U, 8U);
        // Color is deliberately variable within the real source range. Full file
        // postimages and exact retained payload/replay checks bind that chosen value.
        return StackOwnedRow(uid, itemId, quantity, actual.Color);
    }

    private static void AssertStackCommitted(Fixture fixture, PackageJoin join, PackageJoinBefore before,
        PlayerItemSnapshot expectedItems, LegacyMailboxEntry[] selected, bool claimAll, uint classAfter, int newCount)
    {
        var expected = ExpectedStackBusinessFiles(fixture, before, expectedItems, selected, claimAll, classAfter);
        var ledger = Path.GetRelativePath(fixture.Root, fixture.PackageUidState);
        if (newCount == 0)
        {
            AssertNoPackageJoinIntents(join);
            Assert.Equal(before.Files[ledger], File.ReadAllBytes(fixture.PackageUidState));
        }
        else
        {
            var allocation = ReadStackAllocation(fixture, join, newCount);
            var colors = ReferencePackageColors(before, join.Domain, selected, newCount);
            for (var index = 0; index < newCount; index++)
            {
                var generated = Assert.Single(expectedItems.InventoryItems,
                    item => item.Uid == allocation.Allocation!.First + (uint)index);
                Assert.InRange(generated.Color, 3U, 8U);
                Assert.Equal(colors[index], generated.Color);
            }
            var bytes = File.ReadAllBytes(fixture.PackageUidState);
            var current = JsonNode.Parse(bytes)!.AsObject();
            var initial = JsonNode.Parse(before.Files[ledger])!.AsObject();
            Assert.Equal(3, current["Version"]!.GetValue<int>());
            Assert.Equal(2UL, current["Generation"]!.GetValue<ulong>());
            Assert.Equal(PackageHistoricalFloor + (uint)newCount, current["HighWater"]!.GetValue<uint>());
            Assert.Single(current["Operations"]!.AsArray());
            Assert.Single(current["ProducerIntents"]!.AsArray());
            foreach (var row in initial.Where(row => row.Key is not ("Version" or "Generation" or "HighWater" or "Operations" or "ProducerIntents")))
                Assert.True(JsonNode.DeepEquals(row.Value, current[row.Key]), row.Key);
            Assert.Equal(initial.Select(row => row.Key).Union(["Operations", "ProducerIntents"]).Order(), current.Select(row => row.Key).Order());
            expected[ledger] = bytes;
        }
        var request = claimAll
            ? new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimAll, 0, selected.Select(mail => (ulong)mail.Id).ToArray())
            : new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, selected[0].Id, []);
        IncludeStackWorkFile(fixture, join, before, expected, newCount, completed: true, expectedRequest: request);
        AssertPackageJoinFiles(fixture, expected);
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(join.Current.PlayerItems));
        Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid)));
        Assert.Equal(classAfter, uint.Parse(join.Current.Character.Attributes["ClassExperience"], CultureInfo.InvariantCulture));
        Assert.Equal(checked(before.Owner.Character.Money + (claimAll ? (long)selected.Sum(mail => (decimal)mail.Money) : 0)),
            join.Current.Character.Money);
        Assert.Equal(checked(before.Owner.Character.ConquerPoints + (claimAll ? (long)selected.Sum(mail => (decimal)mail.EMoney) : 0)),
            join.Current.Character.ConquerPoints);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static Dictionary<string, byte[]> ExpectedStackBusinessFiles(Fixture fixture, PackageJoinBefore before,
        PlayerItemSnapshot expectedItems, LegacyMailboxEntry[] selected, bool claimAll, uint classAfter)
    {
        var result = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, string> { ["ClassExperience"] = classAfter.ToString(CultureInfo.InvariantCulture) };
        if (claimAll)
        {
            fields["Money"] = checked(before.Owner.Character.Money + (long)selected.Sum(mail => checked((decimal)mail.Money))).ToString(CultureInfo.InvariantCulture);
            fields["ConquerPoints"] = checked(before.Owner.Character.ConquerPoints + (long)selected.Sum(mail => checked((decimal)mail.EMoney))).ToString(CultureInfo.InvariantCulture);
        }
        var character = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
        result[character] = ProjectSashFields(result[character], fields);
        result[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems);
        var mailPath = Path.GetRelativePath(fixture.Root, fixture.AtomicMailPath);
        var bytes = result[mailPath].ToArray();
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        Assert.Equal(4 + count * 740, bytes.Length);
        var ids = selected.Select(mail => mail.Id).ToHashSet();
        for (var index = 0; index < count; index++)
        {
            var offset = 4 + index * 740;
            if (!ids.Contains(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 704)))) continue;
            bytes.AsSpan(offset + 728, 4).Clear();
            if (claimAll) bytes.AsSpan(offset + 708, 16).Clear();
        }
        result[mailPath] = bytes;
        return result;
    }
}
