using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedTradeEligibilityTests
{
    [Fact]
    public Task FullWidthRequestAliasCannotCreateAReciprocalPairOrPublishAWindow() =>
        Run(Root(), Catalog(), async f =>
        {
            var before = f.Current(); var other = f.Current(Fixture.Peer); var files = f.Data.Files();
            int ownStart = f.ActorFrames.Count, peerStart = f.Frames(Fixture.Peer).Count;
            var bytes = new LegacyTradePacket(0) { Action = 0x1_0000_0000UL, UID = Fixture.Peer }.ToNativeFrameBytes();
            Assert.True(LegacyTradePacket.TryParse(Fixture.ReadFrame(bytes), out var parsed));
            Assert.Equal(0x1_0000_0000UL, parsed.Action); Assert.Equal(0U, parsed.Type);

            await f.SendAsync(Fixture.Actor, bytes);

            Assert.Same(before, f.Current()); Assert.Same(other, f.Current(Fixture.Peer));
            Assert.Equal(ownStart, f.ActorFrames.Count); Assert.Equal(peerStart, f.Frames(Fixture.Peer).Count);
            Assert.False(f.Current().LegacyTradeActive); Assert.False(f.Current(Fixture.Peer).LegacyTradeActive);
            f.Data.AssertFiles(files);
            Assert.Contains("action outside supported width", f.Log.ToString());
            // A real supported request still works immediately after refusal.
            await Open(f);
        });

    [Theory]
    [InlineData(2U)] [InlineData(6U)] [InlineData(7U)] [InlineData(9U)] [InlineData(12U)]
    public Task FullWidthAliasesCannotCancelOfferDebitOrConfirmAnOpenOwnedTrade(uint lowAction) =>
        Run(Root(), Catalog(), async f =>
        {
            await Open(f);
            await Trade(f, Fixture.Actor, new(6) { ItemUID = OfferedUid });
            await Trade(f, Fixture.Peer, new(9));
            var party = Party(f); var offer = Assert.Single(party.Items).Value;
            var before = f.Current(); var other = f.Current(Fixture.Peer);
            var items = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            var peerItems = PlayerItemSnapshotLoader.ToLegacyBytes(other.PlayerItems);
            var native = before.NativeGameClient!.Inventory!.ClientItems[OfferedUid];
            var files = f.Data.Files(); int ownStart = f.ActorFrames.Count, peerStart = f.Frames(Fixture.Peer).Count;
            var bytes = new LegacyTradePacket(lowAction)
            { Action = 0x1_0000_0000UL + lowAction, ItemUID = OfferedUid, Money = 300, CPS = 10, UID = Fixture.Peer }.ToNativeFrameBytes();
            Assert.True(LegacyTradePacket.TryParse(Fixture.ReadFrame(bytes), out var parsed));
            Assert.Equal(0x1_0000_0000UL + lowAction, parsed.Action); Assert.Equal(lowAction, parsed.Type);

            await f.SendAsync(Fixture.Actor, bytes);

            Assert.Same(before, f.Current()); Assert.Same(other, f.Current(Fixture.Peer));
            Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems));
            Assert.Equal(peerItems, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(Fixture.Peer).PlayerItems));
            Assert.Same(native, f.Current().NativeGameClient!.Inventory!.ClientItems[OfferedUid]);
            Assert.Same(party, Party(f)); Assert.Same(offer, Assert.Single(party.Items).Value);
            Assert.False(party.Confirmed); Assert.True(party.Other.Confirmed);
            Assert.Equal(0L, party.Money); Assert.Equal(0U, party.ConquerPoints);
            Assert.True(before.LegacyTradeActive); Assert.True(other.LegacyTradeActive);
            Assert.Equal(ownStart, f.ActorFrames.Count); Assert.Equal(peerStart, f.Frames(Fixture.Peer).Count);
            f.Data.AssertFiles(files); Assert.False(f.Recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Contains("action outside supported width", f.Log.ToString());
        });
}
