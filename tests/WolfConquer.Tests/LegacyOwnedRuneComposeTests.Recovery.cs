using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneComposeTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task ActualComposeV16RecoversEveryCharacterItemsSpellsReplacementPrefixWithoutMintingOrReplay(int prefix) => Run(async f =>
    {
        var capture = await Capture(f, prefix);
        for (int index = 0; index < capture.Targets.Length; index++)
            Assert.Equal(index < prefix ? capture.After[capture.Targets[index]] : capture.Before[capture.Targets[index]], File.ReadAllBytes(capture.Targets[index]));
        await RestartAndAssert(f, capture);
    });

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task ActualComposeFlushedStagesRemainExactAndOwnedRecoveryCompletesTheRecordedIntent(int ordinal) => Run(async f =>
    {
        var capture = await Capture(f, ordinal, flushed: true);
        for (int index = 0; index < capture.Targets.Length; index++)
        {
            var path = capture.Targets[index]; Assert.Equal(capture.Before[path], File.ReadAllBytes(path));
            var stage = StagePath(f, path);
            if (index < ordinal) Assert.Equal(capture.After[path], File.ReadAllBytes(stage)); else Assert.False(File.Exists(stage));
        }
        var stages = capture.Targets.Select(path => StagePath(f, path)).ToArray();
        await RestartAndAssert(f, capture); Assert.All(stages, path => Assert.False(File.Exists(path)));
    });

    private sealed record ComposeCapture(Dictionary<string, byte[]> Before, Dictionary<string, byte[]> After, ExpectedImages Expected)
    {
        internal string[] Targets => Before.Keys.ToArray();
        internal void AssertRecovered(ComposeFixture f)
        {
            foreach (var (path, image) in After) Assert.Equal(image, File.ReadAllBytes(path)); Expected.AssertFiles(f);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.Equal(f.Ledger, File.ReadAllBytes(f.Data.State));
            Assert.Equal(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(Before[f.ItemsPath]).Where(uid => uid != ComposeFixture.Material).Order(),
                LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(After[f.ItemsPath]).Order());
        }
    }

    private static async Task<ComposeCapture> Capture(ComposeFixture f, int boundary, bool flushed = false)
    {
        var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        var before = targets.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var expected = Expect(f, ComposeFixture.TargetType + 1, 0, [ComposeFixture.Material]);
        var current = f.Current; int first = f.Sent.Count;
        var binding = f.Native.OrdinarySpellBinding!; var nativeItems = binding.Items.Image; var nativeSpells = f.Native.MySpells!.ClientSpells;
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.SendAsync(ComposeFixture.Request()); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Same(current, f.Current); Assert.Equal(first, f.Sent.Count);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(nativeItems, binding.Items.Image); Assert.Same(nativeSpells, f.Native.MySpells.ClientSpells);
        Assert.Equal(f.Ledger, File.ReadAllBytes(f.Data.State));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(16, record["Version"]!.GetValue<int>());
        Assert.NotNull(record["RuneCompose"]); Assert.Null(record["RuneOperation"]); Assert.Null(record["RuneContainer"]);
        var operation = record["RuneCompose"]!.AsObject();
        Assert.Equal(new[] { "MaterialMetadata", "Materials", "Metadata", "Progress", "SelectedUid" }, operation.Select(pair => pair.Key).Order(StringComparer.Ordinal));
        var materialMetadata = Assert.Single(operation["MaterialMetadata"]!.AsArray())!;
        Assert.Equal(2, materialMetadata.AsObject().Count); Assert.Equal(ComposeFixture.EssenceType, materialMetadata["ItemId"]!.GetValue<uint>());
        Assert.Equal((ushort)10000, materialMetadata["MaximumStackSize"]!.GetValue<ushort>());
        var after = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        for (int index = 0; index < changes.Count; index++)
        {
            var change = changes[index]!; Assert.Equal((long)ComposeFixture.Uid, change["FileId"]!.GetValue<long>());
            Assert.Equal((int)new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells }[index], change["Kind"]!.GetValue<int>());
            Assert.Equal(before[targets[index]], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            after.Add(targets[index], Convert.FromBase64String(change["After"]!.GetValue<string>()));
        }
        Assert.Equal(expected.Character, after[f.CharacterPath]); Assert.Equal(expected.Items, after[f.ItemsPath]); Assert.Equal(expected.Spells, after[f.SpellsPath]);
        return new(before, after, expected);
    }

    private static async Task RestartAndAssert(ComposeFixture f, ComposeCapture capture)
    {
        await f.CloseAsync(); await f.ReopenFrameAsync(); capture.AssertRecovered(f); f.AssertConverged();
        Assert.Equal("14160:16", f.Origins); Assert.Equal((ushort)16, f.Native.MySpells!.ClientSpells[14160].Level);
        await f.CloseAsync(); var files = f.Data.Files(); f.Open(); capture.AssertRecovered(f); f.Data.AssertFiles(files);
    }

    private static string StagePath(ComposeFixture f, string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
}
