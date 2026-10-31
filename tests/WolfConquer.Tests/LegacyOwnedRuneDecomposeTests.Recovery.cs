using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    [Theory]
    [InlineData(0, "active")] [InlineData(1, "active")] [InlineData(2, "active")] [InlineData(3, "active")]
    [InlineData(0, "stored-sibling")] [InlineData(1, "stored-sibling")]
    [InlineData(2, "stored-sibling")] [InlineData(3, "stored-sibling")]
    public Task ActualDecomposeV18RecoversEveryReplacementPrefixWithTheOriginalRegisteredRangeAndColor(int prefix, string mode) => Run(async f =>
    {
        var capture = await Capture(f, prefix);
        for (int index = 0; index < capture.Targets.Length; index++)
        {
            var path = capture.Targets[index];
            Assert.Equal(index < prefix ? capture.After[path] : capture.Before[path], File.ReadAllBytes(path));
        }
        await RestartAndAssert(f, capture);
    }, mode: mode);

    [Fact]
    public Task ThreeActualFlushedMergeStagesRetainTheirNullColorAndNeverReserveAgain() => Run(async f =>
    {
        var capture = await Capture(f, 3, flushed: true); var stages = capture.Targets.Select(path => StagePath(f, path)).ToArray();
        foreach (var path in capture.Targets)
        { Assert.Equal(capture.Before[path], File.ReadAllBytes(path)); Assert.Equal(capture.After[path], File.ReadAllBytes(StagePath(f, path))); }
        await RestartAndAssert(f, capture); Assert.All(stages, path => Assert.False(File.Exists(path)));
    }, mode: "merge");

    private sealed record DecomposeCapture(DecomposeSeed Seed, Dictionary<string, byte[]> Before,
        Dictionary<string, byte[]> After, ExpectedDecompose Expected, byte? FragmentColor)
    {
        internal string[] Targets => Before.Keys.ToArray();
        internal void AssertRecovered(DecomposeFixture f) => AssertWholeFiles(f, Seed, Expected);
    }

    private static async Task<DecomposeCapture> Capture(DecomposeFixture f, int boundary, bool flushed = false)
    {
        var seed = Seed(f); var current = f.Current; var binding = f.Native.OrdinarySpellBinding!;
        var nativeItems = binding.Items.Image; var nativeSpells = f.Native.MySpells!.ClientSpells; var ranks = f.Rankings.Snapshot(); int first = f.Sent.Count;
        var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        var before = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        { [f.CharacterPath] = seed.Character, [f.ItemsPath] = seed.Items, [f.SpellsPath] = seed.Spells };
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.SendAsync(DecomposeFixture.Request()); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString()); if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Same(current, f.Current); Assert.Equal(first, f.Sent.Count);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(nativeItems, binding.Items.Image);
        Assert.Same(nativeSpells, f.Native.MySpells.ClientSpells); Assert.Equal(ranks, f.Rankings.Snapshot());
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(18, record["Version"]!.GetValue<int>());
        Assert.Null(record["RuneChance"]); Assert.Null(record["RuneCompose"]); Assert.Null(record["RuneOperation"]); Assert.Null(record["RuneContainer"]);
        var operation = record["RuneDecompose"]!.AsObject();
        Assert.Equal(new[] { "Allocation", "FragmentColor", "Progress", "Request", "RewardMetadata", "RuneMetadata", "SelectedUid" },
            operation.Select(pair => pair.Key).Order(StringComparer.Ordinal));
        Assert.Equal(DecomposeFixture.Target, operation["SelectedUid"]!.GetValue<uint>());
        byte? color = operation["FragmentColor"] is null ? null : operation["FragmentColor"]!.GetValue<byte>();
        if (color is { } chosen) Assert.InRange(chosen, (byte)3, (byte)8);
        // The only nondeterministic input is read from the actual producer's
        // durable operation. Expected IDs derive from the real prior ledger.
        var expected = Expect(seed, color);
        Assert.Equal(new[] { DecomposeFixture.FragmentType, DecomposeFixture.EssenceType }, operation["RewardMetadata"]!.AsArray()
            .Select(row => row!["ItemId"]!.GetValue<uint>()));
        foreach (var row in operation["RewardMetadata"]!.AsArray())
        {
            Assert.Equal((ushort)10000, row!["MaximumStackSize"]!.GetValue<ushort>()); Assert.Equal(0U, row["TimeMinutes"]!.GetValue<uint>());
            if (row["ItemId"]!.GetValue<uint>() == DecomposeFixture.EssenceType) Assert.Null(row["Durability"]);
            else Assert.Equal((ushort)1, row["Durability"]!.GetValue<ushort>());
        }
        var allocation = operation["Allocation"]!; var request = operation["Request"]!;
        Assert.Equal(seed.HighWater + 1, allocation["First"]!.GetValue<uint>());
        Assert.Equal(expected.NewUids.Length, allocation["Count"]!.GetValue<int>()); Assert.Equal(expected.NewUids.Length, request["Count"]!.GetValue<int>());
        Assert.Equal(f.Data.ExpectedDomain, Guid.Parse(allocation["DomainId"]!.GetValue<string>()));
        var operationId = Guid.Parse(request["OperationId"]!.GetValue<string>());
        Assert.Equal(operationId, Guid.Parse(allocation["OperationId"]!.GetValue<string>()));
        var state = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!;
        Assert.Equal(expected.NewUids[^1], state["HighWater"]!.GetValue<uint>());
        var typed = Assert.Single(state["Operations"]!.AsArray(), row => row!["OperationId"]!.GetValue<string>() == operationId.ToString("N"))!;
        Assert.Equal("RuneDecompose", typed["NamespaceReservation"]!["Kind"]!.GetValue<string>());
        Assert.Equal(allocation["Generation"]!.GetValue<ulong>(), state["Generation"]!.GetValue<ulong>());
        Assert.DoesNotContain(binding.Items.Rune.Objects, row => expected.NewUids.Contains(row.Uid));
        Assert.DoesNotContain(f.Native.Inventory!.ClientItems.Keys, expected.NewUids.Contains);
        var after = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase); var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        for (int index = 0; index < changes.Count; index++)
        {
            var change = changes[index]!; Assert.Equal((long)DecomposeFixture.Uid, change["FileId"]!.GetValue<long>());
            Assert.Equal((int)new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells }[index], change["Kind"]!.GetValue<int>());
            Assert.Equal(before[targets[index]], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            after.Add(targets[index], Convert.FromBase64String(change["After"]!.GetValue<string>()));
        }
        Assert.Equal(expected.Character, after[f.CharacterPath]); Assert.Equal(expected.Items, after[f.ItemsPath]); Assert.Equal(expected.Spells, after[f.SpellsPath]);
        return new(seed, before, after, expected, color);
    }

    private static async Task RestartAndAssert(DecomposeFixture f, DecomposeCapture capture)
    {
        await f.CloseAsync(); var opened = f.Open(); capture.AssertRecovered(f);
        Assert.True(opened.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
        await f.CloseAsync(); var files = f.Data.Files(); opened = f.Open(); capture.AssertRecovered(f); f.Data.AssertFiles(files);
        Assert.True(opened.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
        await f.CloseAsync(); await f.ReopenFrameAsync(() => capture.AssertRecovered(f));
        capture.AssertRecovered(f); f.AssertConverged(); Assert.Equal("", f.Origins);
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 14160);
    }

    private static string StagePath(DecomposeFixture f, string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
}
