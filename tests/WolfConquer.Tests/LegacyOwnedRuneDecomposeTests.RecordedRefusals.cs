using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    [Theory]
    [InlineData("missing-request")] [InlineData("unknown-request-field")]
    [InlineData("request-digest")] [InlineData("allocation-range")]
    [InlineData("allocation-generation")] [InlineData("allocation-domain")]
    [InlineData("missing-rewards")] [InlineData("fragment-cap")]
    [InlineData("invalid-color")] [InlineData("changed-reward")]
    [InlineData("unregistered-output")] [InlineData("tampered-stage")] [InlineData("foreign-stage")]
    public Task GenuineCapturedDecomposeIntentCannotAuthorizeChangedAllocationRecipeOrForeignEvidence(string fault) => Run(async f =>
    {
        var capture = await Capture(f, 3, flushed: fault == "tampered-stage");
        await f.CloseAsync();
        if (fault == "tampered-stage")
        {
            string path = StagePath(f, f.ItemsPath); var bytes = File.ReadAllBytes(path);
            bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
        }
        else if (fault == "foreign-stage")
            File.WriteAllBytes(f.ItemsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", capture.After[f.ItemsPath]);
        else
        {
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
            var operation = record["RuneDecompose"]!.AsObject();
            switch (fault)
            {
                case "missing-request": operation.Remove("Request"); break;
                case "unknown-request-field": operation["Request"]!["Accept"] = true; break;
                case "request-digest": operation["Request"]!["PayloadSha256"] = new string('0', 64); break;
                case "allocation-range":
                    operation["Allocation"]!["First"] = checked(operation["Allocation"]!["First"]!.GetValue<uint>() + 1); break;
                case "allocation-generation":
                    operation["Allocation"]!["Generation"] = checked(operation["Allocation"]!["Generation"]!.GetValue<ulong>() + 1); break;
                case "allocation-domain": operation["Allocation"]!["DomainId"] = Guid.NewGuid().ToString(); break;
                case "missing-rewards": operation["RewardMetadata"]!.AsArray().Clear(); break;
                case "fragment-cap":
                    var fragment = Assert.Single(operation["RewardMetadata"]!.AsArray(), row =>
                        row!["ItemId"]!.GetValue<uint>() == DecomposeFixture.FragmentType)!;
                    fragment["MaximumStackSize"] = 9; break;
                case "invalid-color": operation["FragmentColor"] = 9; break;
                case "changed-reward":
                case "unregistered-output":
                    var change = Assert.Single(record["Changes"]!.AsArray(), row =>
                        row!["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items)!;
                    var items = PlayerItemSnapshotLoader.Parse(Convert.FromBase64String(change["After"]!.GetValue<string>()));
                    var altered = fault == "changed-reward"
                        ? items with { RuneItems = items.RuneItems.Select(row => row.Uid == capture.Expected.Essences[0].Uid
                            ? row.WithStackSize(checked((ushort)(row.StackSize + 1))) : row).ToArray() }
                        : items with { InventoryItems = items.InventoryItems.Append(DecomposeFixture.Item(
                            checked(capture.Expected.NewUids[^1] + 1), 1_000_000, 0)).ToArray() };
                    byte[] bytes = PlayerItemSnapshotLoader.ToLegacyBytes(altered);
                    Assert.NotEqual(capture.After[f.ItemsPath], bytes);
                    change["After"] = Convert.ToBase64String(bytes);
                    // Keep the physical target equal to the deliberately bad
                    // captured After, so a third-state mismatch cannot mask it.
                    File.WriteAllBytes(f.ItemsPath, bytes); break;
                default: throw new InvalidOperationException("Unrecognized negative Decompose fixture.");
            }
            File.WriteAllText(f.PendingPath, record.ToJsonString());
        }

        var files = f.Data.Files(); var highWater = f.HighWater;
        bool success = LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason);
        try
        {
            Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason);
            f.Data.AssertFiles(files); Assert.Equal(highWater, f.HighWater);
            Assert.True(LegacyTradeJournal.HasPending(f.Data.Users));
            using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { if (opened is not null) await opened.DisposeAsync(); }
    });

    [Fact]
    public Task AnExternallyOccupiedGenuineReservedBirthUidCannotBeWrittenByPrefixZeroRecovery() => Run(async f =>
    {
        var capture = await Capture(f, 0);
        uint reserved = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!["RuneDecompose"]!["Allocation"]!["First"]!.GetValue<uint>();
        Assert.Equal(capture.Expected.NewUids[0], reserved);
        Assert.DoesNotContain(reserved, LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(capture.Seed.Items));
        await f.CloseAsync();
        string foreign = Path.Combine(f.Data.Items, (DecomposeFixture.Uid + 99).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin");
        Assert.False(File.Exists(foreign));
        var hostile = new PlayerItemSnapshot([DecomposeFixture.Item(reserved, 1_000_000, 0)], [], [], [], []);
        // Deliberate external corruption of an owned temporary fixture. The
        // cooperating saved writer correctly refuses while this WAL is pending.
        File.WriteAllBytes(foreign, PlayerItemSnapshotLoader.ToLegacyBytes(hostile));
        Assert.True(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem, f.Data.Claim,
            out var observed, out var scanReason), scanReason);
        Assert.True(observed.Maximum <= f.HighWater);
        foreach (var path in capture.Targets) Assert.Equal(capture.Before[path], File.ReadAllBytes(path));
        var files = f.Data.Files(); uint highWater = f.HighWater;
        bool success = LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason);
        try
        {
            Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason);
            f.Data.AssertFiles(files); Assert.Equal(highWater, f.HighWater);
            Assert.True(LegacyTradeJournal.HasPending(f.Data.Users));
            using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { if (opened is not null) await opened.DisposeAsync(); }
    });
}
