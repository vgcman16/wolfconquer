using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData(true, 6U)] [InlineData(false, 6U)]
    [InlineData(true, 7U)] [InlineData(false, 7U)]
    [InlineData(true, 12U)] [InlineData(false, 12U)]
    public Task ExistingTradeRejectsNewItemMoneyOrCpOffersAfterEitherEndpointEndsButLivePartnerCanStillCancel(
        bool senderEnded, uint action) => RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_298_021, 1_000, 50, [Item(10)]);
            var right = AddItemClosurePlayer(1_298_022, 2_000, 60, [Item(20)]);
            await OpenItemClosureAsync(left, right);
            var ended = senderEnded ? left : right;
            var live = senderEnded ? right : left;
            MarkEndedTradeEndpoint(ended);
            Assert.True(trades.TryGet(left.Connection, out var party));
            var beforeLeft = CaptureItemClosure(left);
            var beforeRight = CaptureItemClosure(right);

            await SendItemClosureAsync(left, new(action)
            { ItemUID = action == 6 ? 10UL : 0, Money = action == 7 ? 300 : 0, CPS = action == 12 ? 10U : 0 });

            AssertEndedTradeActionUnchanged(left, right, beforeLeft, beforeRight);
            Assert.Same(party, AssertTradeParty(left));
            Assert.Empty(party.Items);
            Assert.Empty(party.Other.Items);
            Assert.Equal(0L, party.Money);
            Assert.Equal(0L, party.Other.Money);
            Assert.Equal(0U, party.ConquerPoints);
            Assert.Equal(0U, party.Other.ConquerPoints);
            Assert.False(party.Confirmed);
            Assert.False(party.Other.Confirmed);
            AssertTradeClaimsConsumed(left, right);
            await SendItemClosureAsync(live, new(2)); // Ended peer must not strand cancellation/refund ownership.
            AssertItemClosureSettled(left, right, 1_000, 2_000);
            AssertInventory(left, 10);
            AssertInventory(right, 20);
        });

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public Task PendingTradeCannotOpenAWindowAfterEitherEndpointEnds(bool acceptingEndpointEnded) =>
        RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_298_031, 1_000, 50, [Item(10)]);
            var right = AddItemClosurePlayer(1_298_032, 2_000, 60, [Item(20)]);
            await SendItemClosureAsync(left, new(0) { UID = right.Uid });
            var ended = acceptingEndpointEnded ? right : left;
            var live = acceptingEndpointEnded ? left : right;
            MarkEndedTradeEndpoint(ended);
            var party = AssertTradeParty(left);
            var beforeLeft = CaptureItemClosure(left);
            var beforeRight = CaptureItemClosure(right);

            await SendItemClosureAsync(right, new(1) { UID = left.Uid });

            AssertEndedTradeActionUnchanged(left, right, beforeLeft, beforeRight);
            Assert.False(Current(left).LegacyTradeActive);
            Assert.False(Current(right).LegacyTradeActive);
            Assert.False(party.WindowOpen);
            Assert.False(party.Other.WindowOpen);
            Assert.False(party.Confirmed);
            Assert.False(party.Other.Confirmed);
            await SendItemClosureAsync(live, new(2));
            AssertItemClosureSettled(left, right, 1_000, 2_000);
        });

    [Fact]
    public Task UnpairedEndedSessionCannotPublishAFinishedTradeAcknowledgment() => RunItemClosureAsync(async () =>
    {
        var player = AddItemClosurePlayer(1_298_041, 1_000, 50, [Item(10)]);
        MarkEndedTradeEndpoint(player);
        var before = CaptureItemClosure(player);
        await SendItemClosureAsync(player, new(9));
        AssertItemClosureUnchanged(player, before);
        Assert.Equal(before.SentCount, player.Sent.Count);
        Assert.False(trades.Contains(player.Connection));
        AssertPersisted(player);
    });

    private LegacyTradeParty AssertTradeParty(Player player)
    {
        Assert.True(trades.TryGet(player.Connection, out var party));
        return party;
    }
}
