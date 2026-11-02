using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRelicMixedProducerTests
{
    [Fact]
    public Task ActualFuseReturnDecomposeAndRuneShareVersionEightAcrossReopen() => Run(async f =>
    {
        var initial = f.HighWater;
        Assert.Equal(initial + 1, await ReturnRune(f, LegacyRelicMixedProducerData.StoredRuneUidA));
        Assert.Equal(initial + 2, await ComposeRelics(f));
        var chosen = await ChooseRelic(f, 213);
        var before = f.Current; var previous = f.HighWater;
        var selected = Assert.Single(before.PlayerItems.InventoryItems, row => row.Uid == chosen);
        Assert.Equal(0, selected.Bound);
        var request = DecomposeRequest(chosen);
        f.Sent.Clear();
        await f.SendAsync(request);
        Assert.Equal(11, f.Sent.Count); // One real removal, nine separate births, one echo.
        Assert.Equal(request, f.Sent[^1]);
        Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, row => row.Uid == chosen);
        var materials = f.Current.PlayerItems.InventoryItems.Where(row => row.ItemId == 3_306_885).ToArray();
        Assert.Equal(9, materials.Length);
        Assert.Equal(Enumerable.Range(1, 9).Select(offset => previous + (uint)offset), materials.Select(row => row.Uid));
        Assert.All(materials, row => { Assert.Equal((byte)0, row.Bound); Assert.Equal((ushort)1, row.StackSize); });
        f.AssertSettled(previous + 9, version: 8); f.AssertConverged();
        var saved = File.ReadAllBytes(f.ItemsPath);
        var state = File.ReadAllBytes(f.Data.State);
        await f.CloseAsync(); f.OpenRuntime();
        f.AssertSettled(previous + 9, version: 8);
        Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(state, File.ReadAllBytes(f.Data.State));
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.AssertConverged();
        var runeUid = await ReturnRune(f, LegacyRelicMixedProducerData.StoredRuneUidB);
        Assert.Equal(previous + 10, runeUid);
        f.AssertSettled(previous + 10, version: 8); f.AssertConverged();
        var fuseRequest = new LegacyRelicFusePacket(0, LegacyRelicMixedProducerData.AdditionalRelicUidA,
            LegacyRelicMixedProducerData.AdditionalRelicUidB, 0);
        f.Sent.Clear(); await f.SendAsync(fuseRequest.ToFrameBytes());
        Assert.Equal(2, f.Sent.Count);
        Assert.Equal(previous + 11, Assert.Single(f.Current.PlayerItems.RelicFuseItems, row => row.Position == 215).Uid);
        Assert.Equal((fuseRequest with { RelicID3 = previous + 11 }).ToFrameBytes(), f.Sent[1]);
        f.AssertSettled(previous + 11, version: 8); f.AssertConverged();
        var settled = f.Data.Files(); f.Sent.Clear(); await f.SendAsync(fuseRequest.ToFrameBytes());
        Assert.Empty(f.Sent); f.Data.AssertFiles(settled);
        await f.CloseAsync(); f.OpenRuntime(); f.Data.AssertFiles(settled);
        f.AssertSettled(previous + 11, version: 8);
    }, additionalRelics: true);

    [Fact]
    public Task ActualDecomposeOrphanBurnsWholeBatchAndColdReopenNeverReissuesIt() => Run(async f =>
    {
        var high = f.HighWater; var originalItems = File.ReadAllBytes(f.ItemsPath);
        var originalCharacter = File.ReadAllBytes(f.CharacterPath);
        var request = DecomposeRequest(LegacyRelicMixedProducerData.FirstRelicUid);
        f.Runtime.RelicDecomposeNamespaceInterruption = LegacyRelicDecomposeNamespaceInterruption.AfterReservation;
        try { await f.SendAsync(request); }
        finally { f.Runtime.RelicDecomposeNamespaceInterruption = LegacyRelicDecomposeNamespaceInterruption.None; }
        Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.Equal(high + 9, f.HighWater); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.Equal(originalItems, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(originalCharacter, File.ReadAllBytes(f.CharacterPath));
        await f.CloseAsync(); f.OpenRuntime(); f.AssertSettled(high + 9, version: 8);
        Assert.Equal(originalItems, File.ReadAllBytes(f.ItemsPath));
        await f.CloseAsync(); await f.ReopenFrameAsync();
        f.Sent.Clear(); await f.SendAsync(request);
        Assert.Equal(11, f.Sent.Count); Assert.Equal(high + 18, f.HighWater);
        var rewards = f.Current.PlayerItems.InventoryItems.Where(row => row.ItemId == 3_306_885).ToArray();
        Assert.Equal(Enumerable.Range(10, 9).Select(offset => high + (uint)offset), rewards.Select(row => row.Uid));
        f.AssertSettled(high + 18, version: 8); f.AssertConverged();
    });

    [Theory]
    [InlineData(0, false, false)] [InlineData(1, false, false)] [InlineData(2, false, false)] [InlineData(3, false, false)]
    [InlineData(3, true, false)] [InlineData(2, false, true)]
    public Task ActualDecomposeJournalPrefixesRecoverExactBatchWithNoSecondAllocation(int boundary, bool flushed, bool multi) => Run(async f =>
    {
        var high = f.HighWater; var before = f.Data.Files(); var count = multi ? 18 : 9;
        var seed = f.Current;
        var request = multi ? DecomposeRequest(LegacyRelicMixedProducerData.FirstRelicUid, LegacyRelicMixedProducerData.SecondRelicUid)
            : DecomposeRequest(LegacyRelicMixedProducerData.FirstRelicUid);
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.SendAsync(request); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved);
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Equal(high + (uint)count, f.HighWater);
        var pending = Path.Combine(f.Data.Users, ".managed-trade-journal", "pending.json");
        var record = JsonNode.Parse(File.ReadAllBytes(pending))!;
        Assert.Equal(27, record["Version"]!.GetValue<int>());
        Assert.Equal(count, record["RelicDecompose"]!["Allocation"]!["Count"]!.GetValue<int>());
        var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        var paths = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        var kinds = new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells };
        var fileIds = new[] { seed.AccountId, seed.Character.Uid, seed.Character.Uid };
        var expected = new byte[3][];
        for (var index = 0; index < changes.Count; index++)
        {
            var row = changes[index]!;
            Assert.Equal((int)kinds[index], row["Kind"]!.GetValue<int>());
            Assert.Equal(fileIds[index], row["FileId"]!.GetValue<long>());
            var prior = row["Before"] is null ? null : Convert.FromBase64String(row["Before"]!.GetValue<string>());
            var relative = Path.GetRelativePath(f.Data.Root, paths[index]);
            if (prior is null) Assert.False(before.ContainsKey(relative)); else Assert.Equal(before[relative], prior);
            expected[index] = Convert.FromBase64String(row["After"]!.GetValue<string>());
        }
        await f.CloseAsync(); f.OpenRuntime();
        for (var index = 0; index < paths.Length; index++) Assert.Equal(expected[index], File.ReadAllBytes(paths[index]));
        f.AssertSettled(high + (uint)count, version: 8);
        var recovered = f.Data.Files();
        await f.CloseAsync(); f.OpenRuntime(); f.Data.AssertFiles(recovered);
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.AssertConverged();
        f.Sent.Clear(); await f.SendAsync(request);
        Assert.Empty(f.Sent); Assert.Equal(high + (uint)count, f.HighWater); f.Data.AssertFiles(recovered);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualDecomposeRetirementInterruptionPreservesBatchAndNeverReissues(bool afterReplace) => Run(async f =>
    {
        var high = f.HighWater;
        var request = DecomposeRequest(LegacyRelicMixedProducerData.FirstRelicUid);
        f.Runtime.RelicDecomposeNamespaceRetirementInterruption = afterReplace
            ? LegacyItemUidInterruption.AfterStateReplace : LegacyItemUidInterruption.AfterPendingFlush;
        try { await f.SendAsync(request); }
        finally { f.Runtime.RelicDecomposeNamespaceRetirementInterruption = LegacyItemUidInterruption.None; }
        Assert.Empty(f.Sent); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.Equal(high + 9, f.HighWater); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var saved = File.ReadAllBytes(f.ItemsPath);
        var rewards = PlayerItemSnapshotLoader.Parse(saved).InventoryItems.Where(row => row.ItemId == 3_306_885).ToArray();
        Assert.Equal(Enumerable.Range(1, 9).Select(offset => high + (uint)offset), rewards.Select(row => row.Uid));
        await f.CloseAsync(); f.OpenRuntime(); f.AssertSettled(high + 9, version: 8);
        Assert.Equal(saved, File.ReadAllBytes(f.ItemsPath)); var recovered = f.Data.Files();
        await f.CloseAsync(); f.OpenRuntime(); f.Data.AssertFiles(recovered);
        await f.CloseAsync(); await f.ReopenFrameAsync(); f.AssertConverged();
        f.Sent.Clear(); await f.SendAsync(request);
        Assert.Empty(f.Sent); Assert.Equal(high + 9, f.HighWater); f.Data.AssertFiles(recovered);
    });

    [Fact]
    public Task ActualPendingDecomposeCannotBorrowRelicFuseWriterAtStartup() => Run(async f =>
    {
        var high = f.HighWater;
        using (var fault = LegacyJournalInterruption.StopAfter(2))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.SendAsync(DecomposeRequest(LegacyRelicMixedProducerData.FirstRelicUid)); }
            finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved);
        }
        Assert.Equal(high + 9, f.HighWater); Assert.Empty(f.Sent);
        await f.CloseAsync();
        var pending = Path.Combine(f.Data.Users, ".managed-trade-journal", "pending.json");
        var record = JsonNode.Parse(File.ReadAllBytes(pending))!;
        Assert.Equal(27, record["Version"]!.GetValue<int>());
        // Corrupt both recorded writer names in an actual engine-written
        // journal; no test-issued namespace or receipt supplies authority.
        record["RelicDecompose"]!["Request"]!["Writer"] = "relic-fuse-v1";
        record["RelicDecompose"]!["Allocation"]!["Writer"] = "relic-fuse-v1";
        File.WriteAllText(pending, record.ToJsonString(), new System.Text.UTF8Encoding(false));
        var corrupted = f.Data.Files();
        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out _));
        Assert.Null(opened); f.Data.AssertFiles(corrupted);
    });

    private static byte[] DecomposeRequest(params uint[] selected)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemUsage, MsgItemUsagePacket.PayloadLength + selected.Length * sizeof(uint));
        writer.WriteUInt32(0, 0); writer.WriteUInt64(4, 0);
        writer.WriteUInt16(12, (ushort)MsgItemUsageAction.DecomposeRelics);
        writer.WriteUInt32(14, 511_043_495); writer.WriteUInt32(18, checked((uint)selected.Length));
        writer.WriteUInt32(22, 0); writer.WriteUInt32(26, 0);
        for (var index = 0; index < selected.Length; index++)
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + index * sizeof(uint), selected[index]);
        return writer.ToArray();
    }
}
