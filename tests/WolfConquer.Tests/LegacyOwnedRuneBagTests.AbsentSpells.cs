using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task ActualOwnedAbsentSpellsBaselineIsCapturedAsNullAndCanonicalizedOnlyByTheRealV20Producer(bool intoBag) => Run(async f =>
    {
        Assert.True(f.StartsWithoutSpells); Assert.False(File.Exists(f.SpellsPath));
        var before = f.Current; Assert.Empty(before.PlayerSpells.Spells); Assert.Empty(f.Native.MySpells!.ClientSpells);
        var files = f.Data.Files(); var character = File.ReadAllBytes(f.CharacterPath); var items = File.ReadAllBytes(f.ItemsPath);
        var ledger = File.ReadAllBytes(f.Data.State); uint high = f.HighWater;
        var target = f.Selected(); var moved = intoBag ? target.WithPosition(211) : target.WithUid(checked(high + 1)).WithPosition(0);
        var afterItems = intoBag ? before.PlayerItems with
        { InventoryItems = before.PlayerItems.InventoryItems.Where(row => row.Uid != target.Uid).ToArray(),
          RuneItems = before.PlayerItems.RuneItems.Append(moved).OrderBy(row => row.Position).ToArray() }
            : before.PlayerItems with
        { InventoryItems = before.PlayerItems.InventoryItems.Append(moved).ToArray(),
          RuneItems = before.PlayerItems.RuneItems.Where(row => row.Uid != target.Uid).ToArray() };
        var expectedItems = PlayerItemSnapshotLoader.ToLegacyBytes(afterItems);
        int first = f.Sent.Count; var binding = f.Native.OrdinarySpellBinding!; var image = binding.Items.Image;
        var spellMap = f.Native.MySpells.ClientSpells; var ranks = f.Rankings.Snapshot();
        using (var fault = LegacyJournalInterruption.StopAfter(0))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.DispatchAsync(); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString()); Assert.Equal(new[] { 0 }, fault.Visited);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Same(before, f.Current); Assert.Same(image, binding.Items.Image); Assert.Same(spellMap, f.Native.MySpells.ClientSpells);
        Assert.Equal(ranks, f.Rankings.Snapshot()); Assert.Equal(first, f.Sent.Count); Assert.False(File.Exists(f.SpellsPath));
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(items, File.ReadAllBytes(f.ItemsPath));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(20, record["Version"]!.GetValue<int>());
        Assert.NotNull(record["RuneBag"]); var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        Assert.Equal((int)LegacyTradeJournalFileKind.Spells, changes[2]!["Kind"]!.GetValue<int>());
        Assert.True(changes[2]!.AsObject().ContainsKey("Before"));
        Assert.Null(changes[2]!["Before"]); Assert.Equal(new byte[4], Convert.FromBase64String(changes[2]!["After"]!.GetValue<string>()));
        Assert.Equal(character, Convert.FromBase64String(changes[0]!["After"]!.GetValue<string>()));
        Assert.Equal(expectedItems, Convert.FromBase64String(changes[1]!["After"]!.GetValue<string>()));
        var expected = new BagExpected(character, expectedItems, new byte[4], moved, intoBag ? null : moved.Uid, null);
        var seed = new BagSeed(before, character, items, null, ledger, files, high);
        await f.CloseAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            var owner = f.Open(); Assert.True(owner.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            expected.AssertImages(f); AssertLedger(f, seed, expected); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            var expectedFiles = files.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            expectedFiles[Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = expectedItems;
            expectedFiles[Path.GetRelativePath(f.Data.Root, f.SpellsPath)] = new byte[4];
            expectedFiles[Path.GetRelativePath(f.Data.Root, f.Data.State)] = File.ReadAllBytes(f.Data.State);
            f.Data.AssertFiles(expectedFiles); Assert.Equal(first, f.Sent.Count);
            Assert.Empty(PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(f.SpellsPath)).Spells);
            await f.CloseAsync();
        }
        await f.ReopenFrameAsync(() => expected.AssertImages(f)); expected.AssertImages(f); f.AssertConverged();
        Assert.Empty(f.Current.PlayerSpells.Spells); Assert.Empty(f.Native.MySpells!.ClientSpells);
    }, intoBag ? "in" : "out", missingSpells: true);
}
