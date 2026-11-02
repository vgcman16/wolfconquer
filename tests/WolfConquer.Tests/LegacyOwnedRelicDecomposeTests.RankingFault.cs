using System.Collections;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicDecomposeTests
{
    [Fact]
    public Task InjectedPostcommitBeforeRankingFailureRetiresActualOwnersAndCannotReissue() => Run(async f =>
    {
        var request = DecomposeFixture.Request(f.Selected); var high = f.HighWater;
        var native = f.Native; var binding = native.OrdinarySpellBinding!;
        var starts = f.Peer!.WireStarts; var rank = Ranking(f).Snapshot();
        var character = File.ReadAllBytes(f.Data.UserPath);
        var spellsPath = Path.Combine(f.Data.Spells, DecomposeFixture.Uid + ".bin");
        var spells = File.ReadAllBytes(spellsPath);
        f.Runtime.RelicDecomposePublicationFault = LegacyRelicDecomposePublicationFault.BeforeRanking;
        await f.SendAsync(request);
        Assert.Equal(high + 9, f.HighWater);
        Assert.Empty(f.Sent); Assert.Empty(f.PeerSent); Assert.Equal(starts, f.Peer.WireStarts);
        Assert.False(f.Store.TryGet(f.ActualConnection, out _));
        Assert.True(binding.IsRetired); Assert.False(binding.Items.Image.IsCurrentPlain());
        Assert.True(f.Runtime.Recovery.IsBlocked);
        Assert.Equal(rank, Ranking(f).Snapshot());
        Assert.Equal(character, File.ReadAllBytes(f.Data.UserPath)); Assert.Equal(spells, File.ReadAllBytes(spellsPath));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var committed = File.ReadAllBytes(f.ItemsPath);
        var image = PlayerItemSnapshotLoader.Parse(committed);
        Assert.DoesNotContain(image.InventoryItems, row => f.Selected.Contains(row.Uid));
        Assert.Equal(9, image.InventoryItems.Count(row => row.ItemId == DecomposeFixture.Crystal));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable>(typeof(WorldSessionStore)
            .GetField("relicWireOutputs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Store)));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable>(typeof(WorldSessionStore)
            .GetField("relicWirePermits", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Store)));
        Assert.Contains("operation=decompose stage=Ranking", f.ReadDiagnostics());
        Assert.Contains("LegacyCommittedSpellPublicationException", f.ReadDiagnostics());
        await f.CloseAsync(); await f.ReopenFrameAsync();
        Assert.Equal(committed, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(high + 9, f.HighWater);
        f.AssertConverged();
        await AssertRefusal(f, request);
        Assert.Equal(committed, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(high + 9, f.HighWater);
    }, ranking: true);

    [Fact]
    public Task InvalidClosedPublicationPlanRefusesBeforeGlobalReservation() => Run(async f =>
    {
        f.Runtime.RelicDecomposePublicationFault = (LegacyRelicDecomposePublicationFault)99;
        try { await AssertRefusal(f, DecomposeFixture.Request(f.Selected)); }
        finally { f.Runtime.RelicDecomposePublicationFault = LegacyRelicDecomposePublicationFault.None; }
    }, ranking: true);
}
