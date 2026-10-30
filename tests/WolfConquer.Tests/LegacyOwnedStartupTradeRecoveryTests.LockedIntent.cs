using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupTradeRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupTradeRecoveryTests
{
    [Theory]
    [InlineData("root", false)] [InlineData("deposit", false)]
    [InlineData("root", true)] [InlineData("deposit", true)]
    [InlineData("root-and-deposit", false)] [InlineData("root-and-deposit", true)]
    public Task ARealPartnerAuthorizedLockedClosureIntentFinishesAfterTheRelationIsRemoved(string locked, bool stageFlushed) =>
        WithFixture(async f =>
        {
            Assert.Equal(2, f.Catalog.SocialAssociations.Count);
            Assert.Contains(f.Catalog.SocialAssociations, row => row.OwnerId == Fixture.LeftUid &&
                row.TargetId == Fixture.RightUid && row.RelationType == 3);
            if (stageFlushed) await f.StopAfterStageFlushedAsync(4);
            else await f.StageAsync(3);
            f.RemoveTradePartnerRelations();
            Assert.Empty(f.Catalog.SocialAssociations);
            await f.CloseAsync();
            var runtime = f.Open();
            f.AssertRecoveredImages(); f.AssertLockedItemOwner(Fixture.RightUid);
            // The fresh Frame uses a real catalog with no current relation.
            // It admits the recorded committed intent without asking a new
            // permission owner to authorize the historical offer again.
            await f.AssertFreshFrameAsync(runtime);
            f.AssertLockedItemOwner(Fixture.RightUid);
            await f.CloseAsync();
            var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.AssertLockedItemOwner(Fixture.RightUid);
            f.Data.AssertFiles(recovered);
        }, locked: locked, tradePartner: true);

    [Theory]
    [InlineData("root")] [InlineData("deposit")] [InlineData("root-and-deposit")]
    public Task ANowRequestedLockedClosureOfferWithoutAnActualPartnerRelationCannotCreateATransferIntent(string locked) =>
        WithFixture(async f =>
        {
            Assert.Empty(f.Catalog.SocialAssociations);
            await f.AssertUnpartneredLockedOfferRejectedAsync();
            f.AssertLockedItemOwner(Fixture.LeftUid);
        }, locked: locked);
}

internal sealed partial class LegacyOwnedStartupTradeRecoveryFixture
{
    private void AddTradePartnerRelations()
    {
        SocialAssociationRecord Partner(uint owner, uint target, string name) => SocialAssociationParser.ParseRow(
            "owned-trade-partners.txt", 2,
            $"{owner}/3/0/0/0/{target}/0/0/0/0/{name}/0/0/0/0/0/0/0/owned-trade-partner/0/");
        relations.Add(Partner(LeftUid, RightUid, "OwnedStartupPeer"));
        relations.Add(Partner(RightUid, LeftUid, "OwnedStartup"));
    }

    internal void RemoveTradePartnerRelations() => relations.Clear();

    internal void AssertLockedItemOwner(uint uid)
    {
        var snapshot = new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(uid);
        var root = Assert.Single(snapshot.InventoryItems, row => row.Uid == LeftItemUid);
        if (lockedMode is "root" or "root-and-deposit") Assert.Equal((byte)1, root.Locked);
        if (lockedMode is "deposit" or "root-and-deposit")
        {
            var child = Assert.Single(root.DepositedItems);
            Assert.Equal(LeftItemUid + 200, child.Uid); Assert.Equal((byte)1, child.Locked);
        }
        Assert.DoesNotContain(new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(uid == LeftUid ? RightUid : LeftUid)
            .InventoryItems, row => row.Uid == LeftItemUid);
    }

    internal async Task AssertUnpartneredLockedOfferRejectedAsync()
    {
        var trades = Assert.IsType<LegacyTradeStateStore>(typeof(WorldServerFrameHandler)
            .GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        lock (trades.SyncRoot)
        {
            Assert.True(trades.TryGet(connections[LeftUid], out var party));
            Assert.DoesNotContain(LeftItemUid, party!.Items.Keys);
        }
        Assert.Contains(frames[LeftUid], bytes => LegacyTradePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Action == 10);
        Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        Assert.Equal(Before[ItemsPath(LeftUid)], File.ReadAllBytes(ItemsPath(LeftUid)));
        Assert.Equal(Before[ItemsPath(RightUid)], File.ReadAllBytes(ItemsPath(RightUid)));
        await TradeAsync(LeftUid, new(2));
        Assert.False(Current(LeftUid).LegacyTradeActive); Assert.False(Current(RightUid).LegacyTradeActive);
        AssertWallet(LeftUid, 777, 222, hasClaim: false); AssertWallet(RightUid, 2_000, 60, hasClaim: false);
        Assert.Equal(Before[ItemsPath(LeftUid)], File.ReadAllBytes(ItemsPath(LeftUid)));
        Assert.Equal(Before[ItemsPath(RightUid)], File.ReadAllBytes(ItemsPath(RightUid)));
        Assert.Equal(RegistryState, File.ReadAllBytes(Data.State));
        Assert.False(LegacyTradeJournal.HasPending(Data.Users));
    }
}
