using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedTradeEligibilityTests
{
    [Theory]
    [InlineData("allowed")] [InlineData("suspiciousRoot")] [InlineData("suspiciousChild")]
    public Task RealDirectedPartnerPermissionKeepsLockedExceptionButCannotAuthorizeSuspiciousRows(string mode)
    {
        var relation = SocialAssociationParser.ParseRow("Associate.txt", 2,
            $"{Fixture.Actor}/3/600/0/0/{Fixture.Peer}/0/0/0/0/Partner/0/0/0/0/1671008/140/5//14048/");
        var data = LegacyItemWorldDataFixture.Create([Fixture.Item(Sash), Fixture.Item(ChildType)], [],
            socialAssociations: [relation]);
        var root = Root(suspicious: mode == "suspiciousRoot" ? (byte)1 : (byte)0,
            children: [Child(suspicious: mode == "suspiciousChild" ? (byte)1 : (byte)0).WithLocked(2)]).WithLocked(1);
        return Run(root, data, async f =>
        {
            await Open(f); var files = f.Data.Files(); var before = f.Current();
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            var peerStart = f.Frames(Fixture.Peer).Count;
            await Trade(f, Fixture.Actor, new(6) { ItemUID = OfferedUid });
            if (mode != "allowed")
            {
                Assert.Empty(Party(f).Items); Assert.Same(before, f.Current());
                Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems));
                Assert.Equal(peerStart, f.Frames(Fixture.Peer).Count); f.Data.AssertFiles(files);
                return;
            }
            var offer = Assert.Single(Party(f).Items).Value;
            Assert.NotNull(offer.Lineage); Assert.Same(data.TradeItemMetadata, offer.MetadataOwner);
            await Trade(f, Fixture.Actor, new(9)); await Trade(f, Fixture.Peer, new(9));
            Assert.Empty(f.Current().PlayerItems.InventoryItems);
            var received = Assert.Single(f.Current(Fixture.Peer).PlayerItems.InventoryItems);
            Assert.Equal(OfferedUid, received.Uid); Assert.Equal((byte)1, received.Locked);
            Assert.Equal(ChildUid, Assert.Single(received.DepositedItems).Uid);
            Assert.Equal((byte)2, Assert.Single(received.DepositedItems).Locked);
            Assert.Contains(f.ActorFrames, IsCompletion); Assert.Contains(f.Frames(Fixture.Peer), IsCompletion);
            foreach (var uid in new[] { Fixture.Actor, Fixture.Peer })
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(uid).PlayerItems), File.ReadAllBytes(f.ItemPath(uid)));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }
}
