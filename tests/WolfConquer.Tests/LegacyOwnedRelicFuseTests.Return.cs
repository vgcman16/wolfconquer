using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    [Theory]
    [InlineData(213, "valid")] [InlineData(214, "valid")] [InlineData(215, "valid")]
    [InlineData(213, "bound")] [InlineData(215, "bound")]
    public Task ActualComposeThenChooseOnePreservesSelectedUidAndPublishesOnlyCommittedReturn(int position, string input) => Run(async f =>
    {
        await Compose(f);
        Assert.True(f.Sent.Count == 2, $"Expected 2 committed compose frames; got {f.Sent.Count}. Handler log:\n{f.ReadDiagnostics()}");
        f.Sent.Clear();
        var seed = f.Current; var selected = seed.PlayerItems.RelicFuseItems.Single(row => row.Position == position);
        var expected = ExpectedReturn(seed.PlayerItems, selected);
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(expected);
        var ledger = File.ReadAllBytes(f.Data.State); var highwater = f.HighWater;
        var files = f.Data.Files();
        // The source only selects RelicID1. Retain ignored request words in the
        // successful echo without promoting them to additional item authority.
        var request = new LegacyRelicFusePacket(1, selected.Uid, uint.MaxValue, 0xabcdef00);
        f.BeforeSend = _ =>
        {
            Assert.Equal(bytes, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); f.AssertConverged();
        };
        try { await f.SendAsync(request.ToFrameBytes()); } finally { f.BeforeSend = null; }
        Assert.True(f.Sent.Count == 2, $"Expected 2 committed return frames; got {f.Sent.Count}. Handler log:\n{f.ReadDiagnostics()}");
        var actual = f.Current.PlayerItems.InventoryItems.Single(row => row.Uid == selected.Uid);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(selected.WithPosition(0), mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(), f.Sent[0]);
        Assert.Equal(request.ToFrameBytes(), f.Sent[1]);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { selected.WithPosition(0) } }),
            PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { actual } }));
        Assert.Empty(f.Current.PlayerItems.RelicFuseItems); Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Equal(highwater, f.HighWater); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
        files[Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = bytes; f.Data.AssertFiles(files);
        var settled = f.Data.Files(); await f.SendAsync(request.ToFrameBytes());
        Assert.Equal(2, f.Sent.Count); f.Data.AssertFiles(settled); f.AssertConverged();
    }, input);

    [Theory]
    [InlineData("return-locked")]
    [InlineData("return-locked-sibling")]
    [InlineData("return-stacked")]
    [InlineData("return-suspicious")]
    [InlineData("return-child")]
    [InlineData("return-invalid-item")]
    [InlineData("return-result-type")]
    [InlineData("return-unbound-result")]
    [InlineData("return-capacity")]
    public Task ActualMalformedOrIneligibleHeldStateRefusesWithoutAnyMoveOrUidChange(string fault) => Run(async f =>
    {
        var before = f.Current; var files = f.Data.Files(); var image = f.Native.OrdinarySpellBinding!.Items.Image;
        var saved = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        await f.SendAsync(new LegacyRelicFusePacket(1, FuseFixture.First, 0, 0).ToFrameBytes());
        Assert.Empty(f.Sent); Assert.Same(before, f.Current); f.Data.AssertFiles(files);
        Assert.Equal(saved, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Same(image, f.Native.OrdinarySpellBinding.Items.Image); Assert.True(image.IsCurrentPlain()); f.AssertConverged();
    }, fault);

    [Fact]
    public Task ActualMissingOrStaleChoicePublishesNoSuccessfulEcho() => Run(async f =>
    {
        await Compose(f); f.Sent.Clear();
        var files = f.Data.Files(); var owner = f.Current;
        await f.SendAsync(new LegacyRelicFusePacket(1, 999, 0, 0).ToFrameBytes());
        Assert.Empty(f.Sent); Assert.Same(owner, f.Current); f.Data.AssertFiles(files);
        // Declared negative mutable owner corruption after actual admission;
        // the old native binding must not authorize this stale logical image.
        var selected = owner.PlayerItems.RelicFuseItems[0]; var priorOwner = selected.OwnerUid;
        selected.OwnerUid = 123;
        try
        {
            await f.SendAsync(new LegacyRelicFusePacket(1, FuseFixture.First, 0, 0).ToFrameBytes());
            Assert.Empty(f.Sent); f.Data.AssertFiles(files);
        }
        finally { selected.OwnerUid = priorOwner; }
    });

    private static PlayerItemSnapshot ExpectedReturn(PlayerItemSnapshot before, PlayerItemSnapshotItem selected) => before with
    {
        InventoryItems = before.InventoryItems.Append(selected.WithPosition(0)).ToArray(),
        RelicFuseItems = [],
    };
}
