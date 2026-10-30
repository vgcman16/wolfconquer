using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneChanceTests
{
    [Theory]
    [InlineData("unknown-uid")] [InlineData("stacked-target")] [InlineData("overfilled")]
    [InlineData("missing-current-progress")] [InlineData("missing-next-progress")]
    [InlineData("nonpositive-progress")] [InlineData("malformed-progress")]
    [InlineData("missing-successor")] [InlineData("malformed-successor")]
    public Task ActualOwnedChancePreflightPreservesEveryResourceAndOutputForInvalidRequiredState(string fault) => Run(async f =>
    {
        var target = Assert.Single(f.Current.PlayerItems.RuneItems);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Native.OrdinarySpellBinding!));
        if (fault == "stacked-target")
        {
            Assert.Equal((ushort)212, target.Position); Assert.Equal((ushort)2, target.StackSize);
            Assert.Equal((ushort)2, Assert.Single(f.Native.OrdinarySpellBinding!.Items.Rune.Objects).StackSize); Assert.Null(f.Origins);
        }
        if (fault == "overfilled") Assert.Equal(15001U, target.RuneExperience);
        await AssertNoMutation(f, () => f.DispatchAsync(ChanceFixture.Request(fault == "unknown-uid" ? 1_025_999U : ChanceFixture.Target)));
        f.AssertConverged();
    }, experience: fault == "overfilled" ? 15001U : 7500U,
       stack: fault == "stacked-target" ? (ushort)2 : (ushort)1,
       mode: fault == "stacked-target" ? "stored" : "active", fault: fault);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RealAdoptedMetadataDriftCannotAuthorizeAChanceDrawOrPublication(bool progress) => Run(async f =>
    {
        var fields = progress
            ? Assert.IsType<string[]>(Assert.Single(f.Catalog.RuneLevelExperience, row => row.RuneType == 402 && row.Level == 18).Fields)
            : Assert.IsType<string[]>(Assert.Single(f.Catalog.ItemsById[(int)ChanceFixture.TargetType]).Fields);
        int offset = progress ? 3 : ItemType.RequiredClassFieldIndex; string old = fields[offset];
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Native.OrdinarySpellBinding!)); fields[offset] = "invalid";
        try { await AssertNoMutation(f, () => f.DispatchAsync(ChanceFixture.Request())); }
        finally { fields[offset] = old; }
        f.AssertConverged();
    });

    [Fact]
    public Task AnEndedRealOwnedRuntimeCannotStartAChanceIntentThroughItsStillRetainedFrame() => Run(async f =>
    {
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Native.OrdinarySpellBinding!));
        await f.Runtime.DisposeAsync();
        await AssertNoMutation(f, () => f.DispatchAsync(ChanceFixture.Request()), expectedBlocked: true);
    });

    [Theory]
    [InlineData("missing-roll")] [InlineData("out-of-range-roll")]
    [InlineData("changed-outcome")] [InlineData("missing-progress")]
    [InlineData("tampered-stage")] [InlineData("foreign-stage")]
    public Task GenuineChanceIntentDoesNotAuthorizeMalformedDecisionsOrUnrelatedEvidence(string fault) => Run(async f =>
    {
        var capture = await Capture(f, 3, flushed: fault == "tampered-stage");
        await f.CloseAsync();
        if (fault == "tampered-stage")
        {
            var path = StagePath(f, f.ItemsPath); var image = File.ReadAllBytes(path); image[^1] ^= 1; File.WriteAllBytes(path, image);
        }
        else if (fault == "foreign-stage")
            File.WriteAllBytes(f.ItemsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", capture.After[f.ItemsPath]);
        else
        {
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
            if (fault == "missing-roll") record["RuneChance"]!.AsObject().Remove("Roll");
            else if (fault == "out-of-range-roll") record["RuneChance"]!["Roll"] = 100;
            else if (fault == "missing-progress") record["RuneChance"]!["Progress"]!.AsArray().Clear();
            else
            {
                var change = record["Changes"]!.AsArray().Single(node => node!["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items)!;
                var parsed = PlayerItemSnapshotLoader.Parse(Convert.FromBase64String(change["After"]!.GetValue<string>()));
                var altered = parsed with { RuneItems = parsed.RuneItems.Select(row => row.Uid == ChanceFixture.Target ? row.WithRuneExperience(1) : row).ToArray() };
                var image = PlayerItemSnapshotLoader.ToLegacyBytes(altered); change["After"] = Convert.ToBase64String(image);
                File.WriteAllBytes(f.ItemsPath, image); Assert.NotEqual(capture.After[f.ItemsPath], image);
            }
            File.WriteAllText(f.PendingPath, record.ToJsonString());
        }
        var files = f.Data.Files(); bool success = LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason);
        try
        {
            Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason); f.Data.AssertFiles(files);
            Assert.True(LegacyTradeJournal.HasPending(f.Data.Users));
            using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { if (opened is not null) await opened.DisposeAsync(); }
    });

    private static async Task AssertNoMutation(ChanceFixture f, Func<Task> action, bool expectedBlocked = false)
    {
        var current = f.Current; var files = f.Data.Files(); int first = f.Sent.Count;
        var binding = f.Native.OrdinarySpellBinding!; var nativeItems = binding.Items.Image;
        var nativeSpells = f.Native.MySpells!.ClientSpells; var ranks = f.Rankings.Snapshot();
        await action(); Assert.Same(current, f.Current); Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
        Assert.Same(nativeItems, binding.Items.Image); Assert.Same(nativeSpells, f.Native.MySpells.ClientSpells);
        Assert.Equal(ranks, f.Rankings.Snapshot()); Assert.Equal(expectedBlocked, f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
}
