using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneComposeTests
{
    [Theory]
    [InlineData("invalid-later")] [InlineData("duplicate")] [InlineData("self")]
    [InlineData("missing")] [InlineData("short-count")] [InlineData("locked")]
    [InlineData("deposit")] [InlineData("missing-material-progress")] [InlineData("nonpositive-progress")]
    [InlineData("overflow")] [InlineData("missing-successor")] [InlineData("malformed-successor")]
    [InlineData("stacked-target")]
    [InlineData("stacked-rune-material")]
    [InlineData("over-material-cap")] [InlineData("malformed-material-cap")]
    public Task ActualOwnedComposePreflightRefusesEveryUnsafeSelectionBeforeAnyPrefixBecomesDurable(string fault) => Run(async f =>
    {
        byte[] request = fault switch
        {
            "invalid-later" => ComposeFixture.Request(2, ComposeFixture.Material, ComposeFixture.Invalid),
            "duplicate" => ComposeFixture.Request(2, ComposeFixture.Material, ComposeFixture.Material),
            "self" => ComposeFixture.Request(1, ComposeFixture.Target),
            "missing" => ComposeFixture.Request(2, ComposeFixture.Material, 1_020_999),
            "short-count" => ComposeFixture.Request(2, ComposeFixture.Material),
            _ => ComposeFixture.Request(),
        };
        var current = f.Current; var files = f.Data.Files(); int first = f.Sent.Count;
        var binding = f.Native.OrdinarySpellBinding!; var nativeItems = binding.Items.Image; var nativeSpells = f.Native.MySpells!.ClientSpells;
        if (fault == "stacked-target")
        {
            var target = Assert.Single(current.PlayerItems.RuneItems, row => row.Uid == ComposeFixture.Target);
            Assert.Equal((ushort)212, target.Position); Assert.Equal((ushort)2, target.StackSize);
            Assert.Equal((ushort)2, Assert.Single(binding.Items.Rune.Objects, row => row.Uid == ComposeFixture.Target).StackSize);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Null(f.Origins);
        }
        if (fault == "stacked-rune-material")
        {
            var material = Assert.Single(current.PlayerItems.RuneItems, row => row.Uid == ComposeFixture.Material);
            Assert.Equal(ComposeFixture.OtherType, material.ItemId); Assert.Equal((ushort)212, material.Position); Assert.Equal((ushort)2, material.StackSize);
            Assert.Equal((ushort)2, Assert.Single(binding.Items.Rune.Objects, row => row.Uid == ComposeFixture.Material).StackSize);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Equal("14160:16", f.Origins);
            Assert.DoesNotContain(current.PlayerSpells.Spells, row => row.Id == 15470);
        }
        if (fault is "over-material-cap" or "malformed-material-cap")
        {
            ushort quantity = fault == "over-material-cap" ? (ushort)10001 : (ushort)1500;
            Assert.Equal(quantity, Assert.Single(current.PlayerItems.InventoryItems, row => row.Uid == ComposeFixture.Material).StackSize);
            Assert.Equal(quantity, f.Native.Inventory!.ClientItems[ComposeFixture.Material].StackSize);
            Assert.Equal(fault == "over-material-cap" ? "10000" : "invalid",
                Assert.Single(f.Catalog.ItemsById[(int)ComposeFixture.EssenceType]).Fields[ItemType.StackSizeFieldIndex]);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Equal("14160:16", f.Origins);
        }
        var ranks = Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler).GetField("_runeRankings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
        var ranking = ranks.Snapshot();
        await f.SendAsync(request);
        Assert.Same(current, f.Current); Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
        Assert.Same(nativeItems, binding.Items.Image); Assert.Same(nativeSpells, f.Native.MySpells.ClientSpells);
        Assert.Equal(ranking, ranks.Snapshot()); Assert.False(f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); f.AssertConverged();
    }, mode: fault == "stacked-target" ? "stored" : fault == "stacked-rune-material" ? "rune-material" : "active", fault: fault);

    [Theory]
    [InlineData("changed-outcome")] [InlineData("foreign-retired-uid")] [InlineData("tampered-stage")]
    [InlineData("missing-material-cap")] [InlineData("unused-material-cap")]
    public Task GenuineComposePendingIntentDoesNotAuthorizeChangedImagesOrForeignPhysicalOwnership(string fault) => Run(async f =>
    {
        var capture = await Capture(f, 3, flushed: fault == "tampered-stage");
        await f.CloseAsync();
        if (fault is "missing-material-cap" or "unused-material-cap")
        {
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
            var caps = record["RuneCompose"]!["MaterialMetadata"]!.AsArray();
            if (fault == "missing-material-cap") caps.Clear();
            else caps.Insert(0, new JsonObject { ["ItemId"] = 4_010_101U, ["MaximumStackSize"] = 0 });
            File.WriteAllText(f.PendingPath, record.ToJsonString());
        }
        else if (fault == "foreign-retired-uid")
        {
            // At all-AFTER the retired UID is absent from its captured target and
            // appears exactly once elsewhere: an ordinary duplicate scan is not enough.
            // Deliberate external corruption of this owned temporary fixture.
            // The normal saved writer correctly refuses while intent is pending.
            var foreignPath = Path.Combine(f.Data.Items, (ComposeFixture.Uid + 1).ToString(CultureInfo.InvariantCulture) + ".bin");
            var foreign = new PlayerItemSnapshot([ComposeFixture.Item(ComposeFixture.Material, ComposeFixture.EssenceType, 0)], [], [], [], []);
            File.WriteAllBytes(foreignPath, PlayerItemSnapshotLoader.ToLegacyBytes(foreign));
        }
        else if (fault == "tampered-stage")
        {
            var path = StagePath(f, f.SpellsPath); var image = File.ReadAllBytes(path); image[^1] ^= 1; File.WriteAllBytes(path, image);
        }
        else
        {
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
            var change = record["Changes"]!.AsArray().Single(node => node!["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Items)!;
            var image = Convert.FromBase64String(change["After"]!.GetValue<string>()); var parsed = PlayerItemSnapshotLoader.Parse(image);
            var altered = parsed with { RuneItems = parsed.RuneItems.Select(row => row.Uid == ComposeFixture.Target ? row.WithRuneExperience(1) : row).ToArray() };
            image = PlayerItemSnapshotLoader.ToLegacyBytes(altered); change["After"] = Convert.ToBase64String(image);
            File.WriteAllBytes(f.ItemsPath, image); File.WriteAllText(f.PendingPath, record.ToJsonString());
            Assert.NotEqual(capture.Expected.Items, image);
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
}
