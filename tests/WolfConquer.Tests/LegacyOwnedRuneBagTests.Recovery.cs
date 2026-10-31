using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Theory]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)]
    public Task ActualV20RecoversEveryReplacementPrefixBeforeNewLoginWithoutQueryOrNewReservation(bool intoBag, int prefix) => Run(async f =>
    {
        var captured = await Capture(f, prefix);
        for (int index = 0; index < captured.Targets.Length; index++)
            Assert.Equal(index < prefix ? captured.After[captured.Targets[index]] : captured.Before[captured.Targets[index]],
                File.ReadAllBytes(captured.Targets[index]));
        await RestartAndAssert(f, captured);
    }, intoBag ? "in" : "out-rich");

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task ActualThreeFlushedStagesKeepExactImagesAndReservedFreshRootAcrossTwoRestarts(bool intoBag) => Run(async f =>
    {
        var captured = await Capture(f, 3, flushed: true);
        var stages = captured.Targets.Select(path => StagePath(f, path)).ToArray();
        foreach (var path in captured.Targets)
        {
            Assert.Equal(captured.Before[path], File.ReadAllBytes(path));
            Assert.Equal(captured.After[path], File.ReadAllBytes(StagePath(f, path)));
        }
        await RestartAndAssert(f, captured); Assert.All(stages, path => Assert.False(File.Exists(path)));
    }, intoBag ? "in-fragment" : "out-rich");

    [Theory]
    [InlineData("foreign-stage")] [InlineData("reserved-birth")] [InlineData("captured-before")]
    public Task GenuineV20PendingRefusesUnrelatedEvidenceWithoutChangingAnyByte(string corruption) => Run(async f =>
    {
        var captured = await Capture(f, 0); await f.CloseAsync();
        if (corruption == "foreign-stage")
            File.WriteAllBytes(f.SpellsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", [0xA5]);
        else
        {
            uint physical = corruption == "reserved-birth" ? captured.Expected.NewUid!.Value : BagFixture.Target;
            // DECLARED NEGATIVE raw input. Ordinary savers correctly refuse
            // during the genuine pending WAL and are not used to bypass it.
            var foreign = Path.Combine(f.Data.Items, (BagFixture.Uid + 99).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin");
            File.WriteAllBytes(foreign, PlayerItemSnapshotLoader.ToLegacyBytes(
                new([BagFixture.Item(physical, 1_000_000, 0)], [], [], [], [])));
        }
        var files = f.Data.Files();
        bool success = LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason);
        if (opened is not null) await opened.DisposeAsync();
        Assert.False(success, reason); Assert.Null(opened); f.Data.AssertFiles(files);
        using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }, "out-rich");

    private sealed record BagCapture(BagSeed Seed, Dictionary<string, byte[]> Before,
        Dictionary<string, byte[]> After, BagExpected Expected)
    {
        internal string[] Targets => Before.Keys.ToArray();
        internal void AssertRecovered(BagFixture f)
        {
            foreach (var pair in After) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            AssertWholeFiles(f, Seed, Expected); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }
    }

    private static async Task<BagCapture> Capture(BagFixture f, int boundary, bool flushed = false)
    {
        var seed = Seed(f); var expected = Expect(f, seed); int first = f.Sent.Count;
        var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        var before = targets.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var binding = f.Native.OrdinarySpellBinding!; var image = binding.Items.Image;
        var nativeSpells = f.Native.MySpells!.ClientSpells; var ranks = f.Rankings.Snapshot();
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.DispatchAsync(); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.Same(seed.Session, f.Current); Assert.Equal(first, f.Sent.Count); Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(image, binding.Items.Image);
        Assert.Same(nativeSpells, f.Native.MySpells.ClientSpells); Assert.Equal(ranks, f.Rankings.Snapshot());
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(seed.Session.PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(image.FinalSnapshot));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
        Assert.Equal(20, record["Version"]!.GetValue<int>()); var operation = Assert.IsType<JsonObject>(record["RuneBag"]);
        Assert.Equal(BagFixture.Target, operation["SelectedUid"]!.GetValue<uint>()); Assert.Equal(f.IntoBag, operation["IntoBag"]!.GetValue<bool>());
        Assert.Null(record["RuneDecompose"]); Assert.Null(record["RuneOperation"]);
        if (expected.NewUid is { } fresh)
        {
            Assert.Equal(fresh, operation["Allocation"]!["First"]!.GetValue<uint>());
            Assert.Equal(1, operation["Allocation"]!["Count"]!.GetValue<int>());
            Assert.Equal(fresh, f.HighWater);
        }
        else { Assert.Null(operation["Request"]); Assert.Null(operation["Allocation"]); Assert.Equal(seed.Ledger, File.ReadAllBytes(f.Data.State)); }
        var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        var after = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < changes.Count; index++)
        {
            var change = changes[index]!; Assert.Equal((long)BagFixture.Uid, change["FileId"]!.GetValue<long>());
            Assert.Equal((int)new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells }[index],
                change["Kind"]!.GetValue<int>());
            Assert.Equal(before[targets[index]], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            after.Add(targets[index], Convert.FromBase64String(change["After"]!.GetValue<string>()));
        }
        Assert.Equal(expected.Character, after[f.CharacterPath]); Assert.Equal(expected.Items, after[f.ItemsPath]);
        Assert.Equal(expected.Spells, after[f.SpellsPath]); return new(seed, before, after, expected);
    }

    private static async Task RestartAndAssert(BagFixture f, BagCapture captured)
    {
        await f.CloseAsync(); var first = f.Open();
        Assert.True(first.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
        captured.AssertRecovered(f); // No new Frame/login/query/clock exists yet.
        await f.CloseAsync(); var files = f.Data.Files(); var second = f.Open();
        Assert.True(second.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
        captured.AssertRecovered(f); f.Data.AssertFiles(files);
        await f.CloseAsync(); await f.ReopenFrameAsync(() => captured.AssertRecovered(f));
        captured.AssertRecovered(f); f.AssertConverged();
    }
    private static string StagePath(BagFixture f, string target) => target + ".managed-trade-" +
        JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!["TransactionId"]!.GetValue<string>() + ".tmp";
}
