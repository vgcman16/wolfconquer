using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedTradeEligibilityTests
{
    [Theory]
    [InlineData("suspiciousRoot")] [InlineData("suspiciousChild")]
    [InlineData("rootOverCap")] [InlineData("childOverCap")]
    [InlineData("missingRoot")] [InlineData("missingChild")]
    [InlineData("malformedRootCap")] [InlineData("malformedChildCap")]
    [InlineData("emptyCatalog")]
    public Task NativeOfferRejectsTheWholeClosureBeforePeerPreviewOrAnySavedChange(string invalid)
    {
        var root = Root(stack: invalid == "rootOverCap" ? (ushort)2 : (ushort)1,
            suspicious: invalid == "suspiciousRoot" ? (byte)1 : (byte)0,
            children: [Child(stack: invalid == "childOverCap" ? (ushort)2 : (ushort)1,
                suspicious: invalid == "suspiciousChild" ? (byte)1 : (byte)0)]);
        var data = invalid switch
        {
            "missingRoot" => Fixture.Catalog(Fixture.Item(ChildType)),
            "missingChild" => Fixture.Catalog(Fixture.Item(Sash)),
            "emptyCatalog" => Fixture.Catalog(),
            _ => Catalog(rootCap: invalid == "malformedRootCap" ? "bad" : "1",
                childCap: invalid == "malformedChildCap" ? "bad" : "1")
        };
        return Run(root, data, async f =>
        {
            await Open(f);
            var before = f.Current(); var other = f.Current(Fixture.Peer);
            var itemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            var native = before.NativeGameClient!.Inventory!.ClientItems[OfferedUid];
            Assert.NotNull(before.NativeGameClient.OrdinarySpellBinding);
            Assert.False(f.Store.IsUnboundTradeItemOwnerCurrent(f.Connection(Fixture.Actor), before));
            var files = f.Data.Files(); var ownStart = f.ActorFrames.Count; var peerStart = f.Frames(Fixture.Peer).Count;

            await Trade(f, Fixture.Actor, new(6) { ItemUID = OfferedUid });

            Assert.Same(before, f.Current()); Assert.Same(other, f.Current(Fixture.Peer));
            Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems));
            Assert.Same(native, f.Current().NativeGameClient!.Inventory!.ClientItems[OfferedUid]);
            f.Data.AssertFiles(files); Assert.Empty(Party(f).Items);
            Assert.Equal(peerStart, f.Frames(Fixture.Peer).Count);
            Assert.DoesNotContain(f.ActorFrames.Skip(ownStart), IsCompletion);
            Assert.Contains(f.ActorFrames.Skip(ownStart), bytes => LegacyTradePacket.TryParse(Fixture.ReadFrame(bytes), out var p) && p.Action == 10);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }

    [Fact]
    public Task MutatingThePublicCapAfterPublicationCannotAuthorizeAnOverCapNativeOffer()
    {
        var row = Fixture.Item(Sash); var data = Fixture.Catalog(row);
        return Run(Root(stack: 2), data, async f =>
        {
            await Open(f); var files = f.Data.Files(); var before = f.Current();
            var native = before.NativeGameClient!.Inventory!.ClientItems[OfferedUid];
            Assert.IsType<string[]>(row.Fields)[ItemType.StackSizeFieldIndex] = "65535";
            var peerStart = f.Frames(Fixture.Peer).Count;
            await Trade(f, Fixture.Actor, new(6) { ItemUID = OfferedUid });
            Assert.Empty(Party(f).Items); Assert.Same(before, f.Current());
            Assert.Same(native, f.Current().NativeGameClient!.Inventory!.ClientItems[OfferedUid]);
            Assert.Equal(peerStart, f.Frames(Fixture.Peer).Count); f.Data.AssertFiles(files);
        });
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(10)]
    public Task ValidNativeZeroSingleAndFullStackKeepTheSameUidAcrossRealCommit(int stack)
    {
        var data = Catalog(rootCap: "10");
        return Run(Root(checked((ushort)stack)), data, async f =>
        {
            await Open(f); var registry = File.ReadAllBytes(f.Data.State);
            await Trade(f, Fixture.Actor, new(6) { ItemUID = OfferedUid });
            var offer = Assert.Single(Party(f).Items).Value;
            Assert.Same(data.TradeItemMetadata, offer.MetadataOwner); Assert.NotNull(offer.Lineage);
            await Trade(f, Fixture.Actor, new(9)); await Trade(f, Fixture.Peer, new(9));
            Assert.Empty(f.Current().PlayerItems.InventoryItems);
            var received = Assert.Single(f.Current(Fixture.Peer).PlayerItems.InventoryItems);
            Assert.Equal(OfferedUid, received.Uid); Assert.Equal((ushort)stack, received.StackSize);
            Assert.True(f.Current(Fixture.Peer).NativeGameClient!.Inventory!.ClientItems.ContainsKey(OfferedUid));
            Assert.Equal(registry, File.ReadAllBytes(f.Data.State));
            Assert.Contains(f.ActorFrames, IsCompletion); Assert.Contains(f.Frames(Fixture.Peer), IsCompletion);
            foreach (var uid in new[] { Fixture.Actor, Fixture.Peer })
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(uid).PlayerItems), File.ReadAllBytes(f.ItemPath(uid)));
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }
}
