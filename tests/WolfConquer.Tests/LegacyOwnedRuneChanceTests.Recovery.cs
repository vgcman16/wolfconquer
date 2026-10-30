using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneChanceTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task ActualChanceV17RecoversEveryReplacementPrefixUsingItsRecordedServerRollWithoutReplay(int prefix) => Run(async f =>
    {
        var captured = await Capture(f, prefix);
        for (int index = 0; index < captured.Targets.Length; index++)
        {
            var path = captured.Targets[index];
            Assert.Equal(index < prefix ? captured.After[path] : captured.Before[path], File.ReadAllBytes(path));
        }
        await RestartAndAssert(f, captured);
    });

    [Fact]
    public Task ActualChanceThreeFlushedStagesRecoverTheSameCapturedDecisionBeforeAnyNewLogin() => Run(async f =>
    {
        var captured = await Capture(f, 3, flushed: true);
        var stages = captured.Targets.Select(path => StagePath(f, path)).ToArray();
        foreach (var path in captured.Targets)
        {
            Assert.Equal(captured.Before[path], File.ReadAllBytes(path));
            Assert.Equal(captured.After[path], File.ReadAllBytes(StagePath(f, path)));
        }
        await RestartAndAssert(f, captured);
        Assert.All(stages, path => Assert.False(File.Exists(path)));
    });

    private sealed record ChanceCapture(Dictionary<string, byte[]> Before, Dictionary<string, byte[]> After,
        ExpectedImages Expected, byte RecordedRoll)
    {
        internal string[] Targets => Before.Keys.ToArray();
        internal void AssertRecovered(ChanceFixture f)
        {
            foreach (var pair in After) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Expected.AssertFiles(f); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Equal(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(Before[f.ItemsPath]).Order(),
                LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(After[f.ItemsPath]).Order());
            var target = Assert.Single(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath)).RuneItems,
                row => row.Uid == ChanceFixture.Target);
            Assert.Equal(ChanceFixture.TargetType + (RecordedRoll < 50 ? 1U : 0U), target.ItemId);
            Assert.Equal(0U, target.RuneExperience);
        }
    }

    // The fixture's actual Frame Equip created row16. XP7500 and the actual
    // adopted94000->109000 curve give50 percent; only the real server draws.
    // This helper reads that durable decision and independently builds all three
    // expected images. It never supplies a roll or constructs a positive WAL.
    private static async Task<ChanceCapture> Capture(ChanceFixture f, int boundary, bool flushed = false)
    {
        Assert.Equal("14160:16", f.Origins);
        Assert.Equal((ushort)16, f.Native.MySpells!.ClientSpells[14160].Level);
        Assert.Equal(7500U, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == ChanceFixture.Target).RuneExperience);
        var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        var before = targets.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var winning = Expect(f, ChanceFixture.TargetType + 1, 0);
        var losing = Expect(f, ChanceFixture.TargetType, 0);
        var current = f.Current; int first = f.Sent.Count;
        var rankings = f.Rankings.Snapshot();
        var binding = f.Native.OrdinarySpellBinding!; var nativeItems = binding.Items.Image;
        var nativeSpells = f.Native.MySpells.ClientSpells; var nativeGrant = nativeSpells[14160];
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.DispatchAsync(ChanceFixture.Request()); }
            finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Same(current, f.Current); Assert.Equal(first, f.Sent.Count);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(nativeItems, binding.Items.Image);
        Assert.Same(nativeSpells, f.Native.MySpells.ClientSpells); Assert.Same(nativeGrant, nativeSpells[14160]);
        Assert.Equal(rankings, f.Rankings.Snapshot());
        Assert.Equal(f.Ledger, File.ReadAllBytes(f.Data.State));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
        Assert.Equal(17, record["Version"]!.GetValue<int>()); Assert.NotNull(record["RuneChance"]);
        Assert.Null(record["RuneCompose"]); Assert.Null(record["RuneOperation"]); Assert.Null(record["RuneContainer"]);
        var operation = record["RuneChance"]!.AsObject();
        Assert.Equal(new[] { "Metadata", "Progress", "Roll", "SelectedUid" }, operation.Select(pair => pair.Key).Order(StringComparer.Ordinal));
        Assert.Equal(ChanceFixture.Target, operation["SelectedUid"]!.GetValue<uint>());
        Assert.NotNull(operation["Roll"]); byte roll = operation["Roll"]!.GetValue<byte>(); Assert.InRange(roll, (byte)0, (byte)99);
        var expected = roll < 50 ? winning : losing;
        var after = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        for (int index = 0; index < changes.Count; index++)
        {
            var change = changes[index]!; Assert.Equal((long)ChanceFixture.Uid, change["FileId"]!.GetValue<long>());
            Assert.Equal((int)new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells }[index],
                change["Kind"]!.GetValue<int>());
            Assert.Equal(before[targets[index]], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            after.Add(targets[index], Convert.FromBase64String(change["After"]!.GetValue<string>()));
        }
        Assert.Equal(expected.Character, after[f.CharacterPath]); Assert.Equal(expected.Items, after[f.ItemsPath]);
        Assert.Equal(expected.Spells, after[f.SpellsPath]);
        return new(before, after, expected, roll);
    }

    private static async Task RestartAndAssert(ChanceFixture f, ChanceCapture captured)
    {
        await f.CloseAsync();
        var recovered = f.Open();
        Assert.True(recovered.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
        captured.AssertRecovered(f); // First recovery: no new Frame/login exists.
        await f.CloseAsync(); var files = f.Data.Files();
        var second = f.Open();
        Assert.True(second.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
        captured.AssertRecovered(f); f.Data.AssertFiles(files); // Second startup: no journal or reroll.
        await f.CloseAsync();
        await f.ReopenFrameAsync(() => captured.AssertRecovered(f)); // Raw proof precedes fresh admission too.
        captured.AssertRecovered(f); f.AssertConverged(); Assert.Equal("14160:16", f.Origins);
        Assert.Equal((ushort)16, f.Native.MySpells!.ClientSpells[14160].Level);
    }

    private static string StagePath(ChanceFixture f, string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
}
