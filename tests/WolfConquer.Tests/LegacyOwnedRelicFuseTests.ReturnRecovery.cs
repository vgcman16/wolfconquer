using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Fact]
    public Task ActualPrefixTwoCannotHideAnOutOfCustodyDiscardedUidInTheRetainedBeforeImage() => Run(async f =>
    {
        await Compose(f); f.Sent.Clear(); var highwater = f.HighWater;
        using (var fault = LegacyJournalInterruption.StopAfter(2))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.SendAsync(new LegacyRelicFusePacket(1, FuseFixture.First, 0, 0).ToFrameBytes()); }
            finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved); Assert.Equal(new[] { 0, 1, 2 }, fault.Visited);
        }
        Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.Empty(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath)).RelicFuseItems);
        await f.CloseAsync();
        string pending = Path.Combine(f.Data.Users, ".managed-trade-journal", "pending.json");
        var record = JsonNode.Parse(File.ReadAllBytes(pending))!;
        Assert.Equal(26, record["Version"]!.GetValue<int>());
        var itemChange = record["Changes"]!.AsArray().Single(row => row!["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items)!;
        var retained = PlayerItemSnapshotLoader.Parse(Convert.FromBase64String(itemChange["Before"]!.GetValue<string>()));
        var futureUid = checked(highwater + 100);
        // Declared negative corruption of an ACTUALLY produced pending record.
        // This discarded choice is absent from the physical prefix2 AFTER, so
        // independent retained-before custody validation must reject it.
        var corrupt = retained with { RelicFuseItems = retained.RelicFuseItems.Select(row =>
            row.Position == 214 ? row.WithUid(futureUid) : row).ToArray() };
        itemChange["Before"] = Convert.ToBase64String(PlayerItemSnapshotLoader.ToLegacyBytes(corrupt));
        File.WriteAllText(pending, record.ToJsonString(), new System.Text.UTF8Encoding(false));
        var expected = f.Data.Files();
        bool success = LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason);
        if (opened is not null) await opened.DisposeAsync();
        Assert.False(success, reason); Assert.Null(opened); f.Data.AssertFiles(expected);
        Assert.Equal(highwater, f.HighWater);
    });

    [Theory]
    [InlineData(213, 0, false)] [InlineData(214, 1, false)] [InlineData(215, 2, false)]
    [InlineData(213, 3, false)] [InlineData(215, 3, true)]
    public Task ActualReturnJournalRecoversTheRetainedExistingChoiceAcrossRepeatedStartup(int position, int prefix, bool flushed) => Run(async f =>
    {
        await Compose(f); f.Sent.Clear();
        var seed = f.Current; var chosen = seed.PlayerItems.RelicFuseItems.Single(row => row.Position == position);
        var after = PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedReturn(seed.PlayerItems, chosen));
        var before = f.Data.Files(); var ledger = File.ReadAllBytes(f.Data.State); var highwater = f.HighWater;
        var request = new LegacyRelicFusePacket(1, chosen.Uid, 0, 0);
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(prefix) : LegacyJournalInterruption.StopAfter(prefix))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.SendAsync(request.ToFrameBytes()); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved);
            if (!flushed) Assert.Equal(Enumerable.Range(0, prefix + 1), fault.Visited);
        }
        Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.Equal(ledger, File.ReadAllBytes(f.Data.State)); Assert.Equal(highwater, f.HighWater);
        var record = JsonNode.Parse(File.ReadAllBytes(Path.Combine(f.Data.Users, ".managed-trade-journal", "pending.json")))!;
        Assert.Equal(26, record["Version"]!.GetValue<int>()); Assert.NotNull(record["RelicFuseReturn"]);
        var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        var paths = new[] { f.Data.UserPath, f.ItemsPath, Path.Combine(f.Data.Spells, FuseFixture.Uid.ToString(CultureInfo.InvariantCulture) + ".bin") };
        var kinds = new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells };
        var fileIds = new[] { seed.AccountId, seed.Character.Uid, seed.Character.Uid };
        for (var index = 0; index < changes.Count; index++)
        {
            var row = changes[index]!; Assert.Equal((int)kinds[index], row["Kind"]!.GetValue<int>());
            Assert.Equal(fileIds[index], row["FileId"]!.GetValue<long>());
            Assert.Equal(before[Path.GetRelativePath(f.Data.Root, paths[index])], Convert.FromBase64String(row["Before"]!.GetValue<string>()));
            Assert.Equal(index == 1 ? after : before[Path.GetRelativePath(f.Data.Root, paths[index])],
                Convert.FromBase64String(row["After"]!.GetValue<string>()));
        }
        var expectedFiles = before.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
        expectedFiles[Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = after;
        await f.CloseAsync(); f.OpenRuntime();
        f.Data.AssertFiles(expectedFiles); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State)); AssertSettled(f, highwater);
        await f.CloseAsync(); f.OpenRuntime(); f.Data.AssertFiles(expectedFiles); AssertSettled(f, highwater);
        await f.CloseAsync(); await f.ReopenFrameAsync();
        f.AssertConverged(); Assert.Empty(f.Current.PlayerItems.RelicFuseItems);
        Assert.Equal(after, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Contains(f.Current.PlayerItems.InventoryItems, row => row.Uid == chosen.Uid && row.Position == 0);
        var settled = f.Data.Files(); await f.SendAsync(request.ToFrameBytes());
        Assert.Empty(f.Sent); f.Data.AssertFiles(settled); AssertSettled(f, highwater);
    });
}
