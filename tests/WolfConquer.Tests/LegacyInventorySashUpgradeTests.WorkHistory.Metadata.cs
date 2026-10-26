using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static JsonObject ReadHistoryWorkDocument(Fixture fixture, PackageJoin join, Guid operation)
    {
        var text = operation.ToString("N");
        var pending = ReadHistoryIndex(fixture, join)["Pending"]!.AsArray()
            .SingleOrDefault(node => node!["Operation"]!.GetValue<string>() == text);
        return pending is not null ? pending.DeepClone().AsObject() :
            JsonNode.Parse(File.ReadAllBytes(HistoryCompletedPath(fixture, join, text)))!["Work"]!.DeepClone().AsObject();
    }

    private static void WriteHistoryPendingDocument(Fixture fixture, PackageJoin join, Guid operation, JsonObject replacement)
    {
        var index = ReadHistoryIndex(fixture, join);
        var pending = index["Pending"]!.AsArray();
        var row = Assert.Single(pending, node => node!["Operation"]!.GetValue<string>() == operation.ToString("N"));
        pending[pending.IndexOf(row)] = replacement;
        File.WriteAllText(HistoryIndexPath(fixture, join), index.ToJsonString());
    }

    private static void IncludeValidatedHistoryMetadata(Fixture fixture, PackageJoin join,
        Dictionary<string, byte[]> expected, int completedCount, int pendingCount)
    {
        var records = AssertHistoryChain(fixture, join, completedCount, pendingCount);
        var paths = new HashSet<string>(StringComparer.Ordinal) { HistoryIndexPath(fixture, join) };
        foreach (var pair in records)
        {
            paths.Add(HistoryCompletedPath(fixture, join, pair.Key));
            var work = JsonSerializer.Deserialize<LegacyMailboxPackageWorkStore.Record>(pair.Value.ToJsonString())!;
            var workBytes = JsonSerializer.SerializeToUtf8Bytes(work, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            byte[] payload;
            if (work.Version == 2) payload = Convert.FromBase64String(work.PayloadBase64!);
            else
            {
                Assert.True(join.Registry.TryReadProducerIntent(join.Domain, Guid.ParseExact(work.Operation, "N"), out var intent, out var reason), reason);
                payload = intent!.CopyPayload();
                Assert.Equal(intent.Request.PayloadSha256, HistoryDigest(payload));
            }
            Assert.Contains(BinaryPrimitives.ReadUInt32LittleEndian(payload), new[] { 0x31504d57U, 0x32504d57U });
            Assert.Equal(join.Domain, new Guid(payload.AsSpan(4, 16)));
            Assert.Equal(work.AccountId, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(20)));
            Assert.Equal(work.CharacterUid, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(24)));
            var count = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(28));
            Assert.InRange(count, 1, 40);
            for (var row = 0; row < count; row++)
            {
                var lineage = payload.AsSpan(32 + row * 32, 32).ToArray();
                var composite = new byte[56];
                join.Domain.ToByteArray().CopyTo(composite, 0);
                BinaryPrimitives.WriteUInt32LittleEndian(composite.AsSpan(16), work.AccountId);
                BinaryPrimitives.WriteUInt32LittleEndian(composite.AsSpan(20), work.CharacterUid);
                lineage.CopyTo(composite, 24);
                var name = HistoryDigest(composite);
                var path = Path.Combine(HistoryRoot(fixture, join), "lineages", name[..2], name + ".json");
                Assert.True(paths.Add(path));
                var record = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
                Assert.Equal(new[] { "Version", "Domain", "AccountId", "CharacterUid", "Lineage", "Operation", "RecordSha256" }.Order(), record.Select(value => value.Key).Order());
                Assert.Equal(1, record["Version"]!.GetValue<int>());
                Assert.Equal(join.Domain.ToString("N"), record["Domain"]!.GetValue<string>());
                Assert.Equal(work.AccountId, record["AccountId"]!.GetValue<uint>());
                Assert.Equal(work.CharacterUid, record["CharacterUid"]!.GetValue<uint>());
                Assert.Equal(Convert.ToHexString(lineage), record["Lineage"]!.GetValue<string>());
                Assert.Equal(work.Operation, record["Operation"]!.GetValue<string>());
                Assert.Equal(HistoryDigest(workBytes), record["RecordSha256"]!.GetValue<string>());
            }
        }
        Assert.Equal(paths.Order(), Directory.GetFiles(HistoryRoot(fixture, join), "*", SearchOption.AllDirectories).Order());
        var directories = paths.Select(Path.GetDirectoryName).Where(path => path != HistoryRoot(fixture, join))
            .SelectMany(path => new[] { path!, Path.GetDirectoryName(path!)! }).Distinct(StringComparer.Ordinal).Order();
        Assert.Equal(directories, Directory.GetDirectories(HistoryRoot(fixture, join), "*", SearchOption.AllDirectories).Order());
        foreach (var path in paths) expected[Path.GetRelativePath(fixture.Root, path)] = File.ReadAllBytes(path);
    }
}
