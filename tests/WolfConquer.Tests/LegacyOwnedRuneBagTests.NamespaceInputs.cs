using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task ActualCurrentRegistryRefusesForeignAboveHighWaterBeforeAnyBagReservation(bool intoBag) => Run(async f =>
    {
        uint unknown = checked(f.HighWater + 1);
        string foreign = Path.Combine(f.Data.Items, (BagFixture.Uid + 99).ToString(CultureInfo.InvariantCulture) + ".bin");
        // Explicit NEGATIVE input in this owned fixture; no positive owner,
        // journal, reservation or grant is synthesized.
        File.WriteAllBytes(foreign, PlayerItemSnapshotLoader.ToLegacyBytes(
            new PlayerItemSnapshot([BagFixture.Item(unknown, 1_000_000, 0)], [], [], [], [])));
        await Refuse(f, BagFixture.Request(intoBag));
    }, intoBag ? "in" : "out-rich");

    [Fact]
    public Task ActualPostReservationTargetIoRefusalFencesRetryAndBurnsOnlyOneRange() => Run(async f =>
    {
        var seed = Seed(f); var binding = f.Native.OrdinarySpellBinding!; var image = binding.Items.Image;
        var stamp = f.Native.Player!.FinRane; var ranks = f.Rankings.Snapshot();
        using (var fault = LegacyJournalInterruption.LeaseItemSpellTargetBeforeWriter(0))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.DispatchAsync(); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.ItemSpellTargetLeaseObserved, f.Log.ToString()); Assert.False(fault.PendingObserved);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent); Assert.Same(seed.Session, f.Current);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(image, binding.Items.Image);
        Assert.Equal(stamp, f.Native.Player.FinRane); Assert.Equal(ranks, f.Rankings.Snapshot());
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); AssertBagFilesExceptState(seed.Files, f);
        Assert.Equal(seed.HighWater + 1, f.HighWater);
        var state = BagNamespaceState(f); AssertActualBagKind(Assert.Single(state["Operations"]!.AsArray())!.AsObject());
        var issued = f.Data.Files(); await f.DispatchAsync(); f.Data.AssertFiles(issued); Assert.Empty(f.Sent);
        await f.CloseAsync();
        using (f.Open())
        {
            Assert.Equal(seed.HighWater + 1, f.HighWater); Assert.Empty(BagNamespaceState(f)["Operations"]!.AsArray());
            AssertBagFilesExceptState(seed.Files, f);
        }
        var settled = f.Data.Files(); using (f.Open()) f.Data.AssertFiles(settled);
    }, "out-rich");
}
