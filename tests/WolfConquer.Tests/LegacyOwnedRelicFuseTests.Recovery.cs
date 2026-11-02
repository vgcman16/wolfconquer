using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(3, true)]
    public Task ActualJournalPrefixesRecoverOneRetainedResultWithoutAnotherReservation(int boundary, bool flushed) => Run(async f =>
    {
        var before = f.Data.Files(); var highwater = f.HighWater;
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await Compose(f); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved);
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.Equal(highwater + 1, f.HighWater);
        string pending = Path.Combine(f.Data.Users, ".managed-trade-journal", "pending.json");
        var record = JsonNode.Parse(File.ReadAllBytes(pending))!;
        Assert.Equal(25, record["Version"]!.GetValue<int>());
        Assert.NotNull(record["RelicFuse"]);
        var expected = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var kinds = new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells };
        var roots = new[] { f.Data.Users, f.Data.Items, f.Data.Spells };
        var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        for (var index = 0; index < changes.Count; index++)
        {
            var row = changes[index]!;
            Assert.Equal((int)kinds[index], row["Kind"]!.GetValue<int>());
            Assert.Equal((long)FuseFixture.Uid, row["FileId"]!.GetValue<long>());
            string path = Path.Combine(roots[index], FuseFixture.Uid.ToString(CultureInfo.InvariantCulture) + (index == 0 ? ".ini" : ".bin"));
            Assert.Equal(before[Path.GetRelativePath(f.Data.Root, path)], Convert.FromBase64String(row["Before"]!.GetValue<string>()));
            expected.Add(path, Convert.FromBase64String(row["After"]!.GetValue<string>()));
        }
        var saved = PlayerItemSnapshotLoader.Parse(expected[f.ItemsPath]);
        AssertHeldResult(saved, highwater + 1);
        await f.CloseAsync(); f.OpenRuntime();
        Assert.True(f.Runtime.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
        foreach (var pair in expected) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        AssertSettled(f, highwater + 1);
        var settled = f.Data.Files();
        await f.CloseAsync(); f.OpenRuntime(); f.Data.AssertFiles(settled); AssertSettled(f, highwater + 1);
        await f.CloseAsync(); await f.ReopenFrameAsync();
        f.AssertConverged(); AssertHeldResult(f.Current.PlayerItems, highwater + 1);
        var afterLogin = f.Data.Files(); await Compose(f);
        Assert.Empty(f.Sent); f.Data.AssertFiles(afterLogin); AssertSettled(f, highwater + 1);
    });

    [Fact]
    public Task ActualReservedOrphanBurnsOneUidAndPreservesInputsUntilAFreshRequest() => Run(async f =>
    {
        var before = WithoutFuseUidState(f.Data); var highwater = f.HighWater;
        f.Runtime.RelicFuseNamespaceInterruption = LegacyRelicFuseNamespaceInterruption.AfterReservation;
        try { await Compose(f); } finally { f.Runtime.RelicFuseNamespaceInterruption = LegacyRelicFuseNamespaceInterruption.None; }
        Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.Equal(highwater + 1, f.HighWater);
        AssertFileMap(before, WithoutFuseUidState(f.Data));
        await f.CloseAsync(); f.OpenRuntime();
        AssertSettled(f, highwater + 1); AssertFileMap(before, WithoutFuseUidState(f.Data));
        Assert.Empty(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(FuseFixture.Uid).RelicFuseItems);
        var settled = f.Data.Files(); await f.CloseAsync(); f.OpenRuntime(); f.Data.AssertFiles(settled);
        await f.CloseAsync(); await f.ReopenFrameAsync(); await Compose(f);
        Assert.Equal(2, f.Sent.Count); AssertHeldResult(f.Current.PlayerItems, highwater + 2);
        AssertSettled(f, highwater + 2); f.AssertConverged();
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualRetirementInterruptionRecoversBookkeepingWithoutChangingCommittedResult(bool afterStateReplace) => Run(async f =>
    {
        var highwater = f.HighWater;
        f.Runtime.RelicFuseNamespaceRetirementInterruption = afterStateReplace
            ? LegacyItemUidInterruption.AfterStateReplace : LegacyItemUidInterruption.AfterPendingFlush;
        try { await Compose(f); } finally { f.Runtime.RelicFuseNamespaceRetirementInterruption = LegacyItemUidInterruption.None; }
        Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        AssertHeldResult(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(FuseFixture.Uid), highwater + 1);
        var committed = WithoutFuseUidState(f.Data);
        await f.CloseAsync(); f.OpenRuntime();
        AssertFileMap(committed, WithoutFuseUidState(f.Data)); AssertSettled(f, highwater + 1);
        var settled = f.Data.Files(); await f.CloseAsync(); f.OpenRuntime(); f.Data.AssertFiles(settled);
        await f.CloseAsync(); await f.ReopenFrameAsync();
        AssertHeldResult(f.Current.PlayerItems, highwater + 1); f.AssertConverged();
    });

    private static Task Compose(FuseFixture f) =>
        f.SendAsync(new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes());

    private static void AssertHeldResult(PlayerItemSnapshot items, uint uid)
    {
        Assert.Equal(new[] { FuseFixture.Kept }, items.InventoryItems.Select(row => row.Uid));
        Assert.Equal(new[] { FuseFixture.First, FuseFixture.Second, uid }, items.RelicFuseItems.Select(row => row.Uid));
        Assert.Equal(new ushort[] { 213, 214, 215 }, items.RelicFuseItems.Select(row => row.Position));
        Assert.Single(items.RelicFuseItems, row => row.Uid == uid && row.ItemId == 4_100_001);
    }

    private static void AssertSettled(FuseFixture f, uint highwater)
    {
        Assert.Equal(highwater, f.HighWater);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.False(File.Exists(Path.Combine(f.Data.Registry, "pending.json")));
        Assert.Empty(JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["Operations"]!.AsArray());
    }

    private static Dictionary<string, byte[]> WithoutFuseUidState(LegacyOwnedAdmissionRuntimeFixture data)
    {
        var files = data.Files();
        files.Remove(Path.GetRelativePath(data.Root, data.State));
        files.Remove(Path.GetRelativePath(data.Root, Path.Combine(data.Registry, "pending.json")));
        return files;
    }

    private static void AssertFileMap(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
