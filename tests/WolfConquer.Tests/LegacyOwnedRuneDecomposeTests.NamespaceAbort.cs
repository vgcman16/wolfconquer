using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneDecomposeTests
{
    [Fact]
    public Task ActualPostReservationTargetReadFailureFencesIssuerWithoutGameWalAndStartupDiscardsOnlyBookkeeping() => Run(async f =>
    {
        var seed = Seed(f); var current = f.Current; var native = f.Native;
        var binding = native.OrdinarySpellBinding!; var image = binding.Items.Image; var spells = native.MySpells!.ClientSpells;
        var gameBefore = WithoutNamespaceState(f.Data); var ranks = f.Rankings.Snapshot();
        int logStart = f.Log.ToString().Length;
        using var fault = LegacyJournalInterruption.LeaseItemSpellTargetBeforeWriter(2);
        f.Handler.ItemSpellJournalInterruption = fault;
        try
        {
            await f.SendAsync(DecomposeFixture.Request());
            Assert.True(fault.ItemSpellTargetLeaseObserved); Assert.False(fault.PendingObserved); Assert.Empty(fault.Visited);
            Assert.Contains("IOException", f.Log.ToString()[logStart..], StringComparison.Ordinal);
            Assert.Throws<IOException>(() => File.ReadAllBytes(f.SpellsPath)); // The real OS lease remains held.
            Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(File.Exists(f.PendingPath));
            Assert.False(File.Exists(Path.Combine(f.Data.Registry, "pending.json")));
            Assert.Same(current, f.Current); Assert.Same(native, f.Native); Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Same(image, binding.Items.Image); Assert.True(image.IsCurrentPlain());
            Assert.Same(spells, native.MySpells.ClientSpells); Assert.Equal(ranks, f.Rankings.Snapshot());
            foreach (var row in seed.Session.PlayerSpells.Spells)
            {
                var actual = spells[row.Id]; Assert.Equal(row.Level, actual.Level); Assert.Equal(row.Experience, actual.Experience);
                Assert.Equal(row.PreviousLevel, actual.PreviousLevel); Assert.Equal(row.SoulLevel, actual.SoulLevel); Assert.Equal(row.UseSpellSoul, actual.UseSpellSoul);
            }
            Assert.Equal(seed.Character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(seed.Items, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(seed.HighWater + 2, f.HighWater);
            var state = ReadNamespaceState(f); var allocation = Assert.Single(state["Operations"]!.AsArray())!.AsObject();
            RequireActualKind(allocation); Assert.Equal(seed.HighWater + 1, allocation["First"]!.GetValue<uint>());
            Assert.Equal(2, allocation["Count"]!.GetValue<int>());
            Assert.DoesNotContain(binding.Items.Rune.Objects, row => row.Uid > seed.HighWater);
            Assert.DoesNotContain(native.Inventory!.ClientItems.Keys, uid => uid > seed.HighWater);
            var issuedState = File.ReadAllBytes(f.Data.State);
            Assert.Throws<InvalidDataException>(() => f.Runtime.RequireRuneDecomposePreflight(f.Handler, 2));
            await f.SendAsync(DecomposeFixture.Request()); // The real Frame cannot issue another reservation while fenced.
            Assert.Equal(issuedState, File.ReadAllBytes(f.Data.State)); Assert.Equal(seed.HighWater + 2, f.HighWater);
            Assert.Empty(f.Sent); Assert.Same(current, f.Current);
        }
        finally { f.Handler.ItemSpellJournalInterruption = null; fault.Dispose(); }

        AssertSameFiles(gameBefore, WithoutNamespaceState(f.Data));
        var reserved = ReadNamespaceState(f); await f.CloseAsync();
        using (f.Open())
        {
            AssertSameFiles(gameBefore, WithoutNamespaceState(f.Data)); Assert.Empty(ReadNamespaceState(f)["Operations"]!.AsArray());
            Assert.Equal(seed.HighWater + 2, f.HighWater);
            Assert.Equal(reserved["Generation"]!.GetValue<ulong>() + 1, ReadNamespaceState(f)["Generation"]!.GetValue<ulong>());
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }
        var settled = f.Data.Files();
        using (f.Open()) f.Data.AssertFiles(settled);
    });
}
