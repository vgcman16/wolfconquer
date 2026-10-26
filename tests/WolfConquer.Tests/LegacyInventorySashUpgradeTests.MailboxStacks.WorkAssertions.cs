using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static readonly string[] StackWorkKeys = ["Version", "Domain", "Operation", "PayloadSha256", "AccountId",
        "CharacterUid", "First", "Count", "Users", "Items", "Mailbox", "CombatGear", "Before", "After", "Completed"];

    private static LegacyMailboxPackageWorkStore.Record IncludeStackWorkFile(Fixture fixture, PackageJoin join,
        PackageJoinBefore before, Dictionary<string, byte[]> expected, int newCount, bool completed,
        LegacyMailboxModePacket? expectedRequest = null)
    {
        var index = ReadHistoryIndex(fixture, join);
        var body = completed ? Assert.Single(AssertHistoryChain(fixture, join, 1, 0)).Value :
            Assert.Single(index["Pending"]!.AsArray())!.AsObject();
        var operation = Guid.ParseExact(body["Operation"]!.GetValue<string>(), "N");
        using var json = JsonDocument.Parse(body.ToJsonString());
        var keys = newCount == 0 ? StackWorkKeys.Concat(["AllocationKind", "PayloadBase64"]) : StackWorkKeys;
        Assert.Equal(keys.Order(), json.RootElement.EnumerateObject().Select(row => row.Name).Order());
        var store = new LegacyMailboxPackageWorkStore(join.ReadDebtProducer());
        var row = Assert.IsType<LegacyMailboxPackageWorkStore.Record>(store.Read(operation));
        Assert.Equal(newCount == 0 ? 2 : 1, row.Version);
        Assert.Equal(join.Domain.ToString("N"), row.Domain);
        Assert.Equal(operation.ToString("N"), row.Operation);
        Assert.Equal(fixture.Account, row.AccountId);
        Assert.Equal(Fixture.Uid, row.CharacterUid);
        Assert.Equal(fixture.Users, row.Users);
        Assert.Equal(fixture.Items, row.Items);
        Assert.Equal(fixture.PackageMailboxRoot, row.Mailbox);
        Assert.Equal(fixture.PackageGearRoot, row.CombatGear);
        Assert.Equal(completed, row.Completed);
        byte[] payload;
        if (newCount == 0)
        {
            Assert.Equal(0U, row.First);
            Assert.Equal(0, row.Count);
            Assert.Equal("NoAllocation", row.AllocationKind);
            payload = Convert.FromBase64String(Assert.IsType<string>(row.PayloadBase64));
            Assert.Equal(row.PayloadBase64, Convert.ToBase64String(payload));
            AssertNoPackageJoinIntents(join);
            Assert.Equal(before.Files[Path.GetRelativePath(fixture.Root, fixture.PackageUidState)], File.ReadAllBytes(fixture.PackageUidState));
        }
        else
        {
            var intent = ReadStackAllocation(fixture, join, newCount);
            Assert.Equal(intent.Request.OperationId, operation);
            Assert.Equal(intent.Request.PayloadSha256, row.PayloadSha256);
            Assert.Equal(intent.Allocation!.First, row.First);
            Assert.Equal(newCount, row.Count);
            Assert.Null(row.AllocationKind);
            Assert.Null(row.PayloadBase64);
            payload = intent.CopyPayload();
        }
        Assert.Equal(0x32504d57U, BinaryPrimitives.ReadUInt32LittleEndian(payload)); // Real WMP2, not old WMP1.
        Assert.Equal(Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), row.PayloadSha256);
        Assert.True(LegacyMailboxPackageRecipe.TryReadOperationIdentity(payload, join.Domain,
            checked((uint)fixture.Account), Fixture.Uid, out var parsedOperation));
        Assert.Equal(operation, parsedOperation);
        Assert.True(LegacyMailboxPackageRecipe.TryReadWorkDescription(payload, join.Domain,
            checked((uint)fixture.Account), Fixture.Uid, out var description));
        Assert.Equal(newCount, description!.Count);
        if (expectedRequest is not null) Assert.Equal(expectedRequest.ToFrameBytes(), description.Request.ToFrameBytes());
        var paths = new[] { fixture.CharacterPath, fixture.ItemPath, fixture.AtomicMailPath, fixture.PackageGearPath };
        string Digest(Dictionary<string, byte[]> images, string file) =>
            Convert.ToHexString(SHA256.HashData(images[Path.GetRelativePath(fixture.Root, file)])).ToLowerInvariant();
        Assert.Equal(paths.Select(file => Digest(before.Files, file)), row.Before);
        Assert.Equal(paths.Select(file => Digest(expected, file)), row.After);
        Assert.Equal(row.Before, description.Before);
        Assert.Equal(new[] { row.After[0], row.After[3], row.After[2] }, description.After);
        Assert.Equal(newCount == 0 ? row.After[1] : null, description.ItemsAfterSha256);
        IncludeValidatedHistoryMetadata(fixture, join, expected, completed ? 1 : 0, completed ? 0 : 1);
        return row;
    }

    private static Dictionary<string, byte[]> ExpectedStackPendingFiles(Fixture fixture, PackageJoin join,
        PackageJoinBefore before, Dictionary<string, byte[]> expectedAfter)
    {
        var after = new Dictionary<string, byte[]>(expectedAfter, StringComparer.OrdinalIgnoreCase);
        var row = IncludeStackWorkFile(fixture, join, before, after, 0, completed: false);
        var path = DebtWorkPath(fixture, join, Guid.ParseExact(row.Operation, "N"));
        var relative = Path.GetRelativePath(fixture.Root, path);
        return new(before.Files, StringComparer.OrdinalIgnoreCase) { [relative] = after[relative] };
    }
}
