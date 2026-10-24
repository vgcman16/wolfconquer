using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Theory]
    [InlineData(true, 7U)]
    [InlineData(true, 12U)]
    [InlineData(false, 7U)]
    [InlineData(false, 12U)]
    public Task ItemClosureMalformedOwnOrPartnerGraphCannotAcceptNewCurrencyEscrow(
        bool corruptOfferingOwner, uint action) => RunItemClosureAsync(async () =>
        {
            var left = AddItemClosurePlayer(1_297_111, 1_000, 50, new[] { Item(10) });
            var right = AddItemClosurePlayer(1_297_112, 2_000, 60, new[] { Item(20) });
            await OpenItemClosureAsync(left, right);
            var malformedOwner = corruptOfferingOwner ? left : right;
            var current = Current(malformedOwner);
            sessions.Set(malformedOwner.Connection, current with
            {
                PlayerItems = current.PlayerItems with
                {
                    // The offered inventory roots are ordinary, but a foreign
                    // seven-store tail is not structurally/save-valid authority.
                    WarehouseItems = current.PlayerItems.WarehouseItems
                        .Concat(new PlayerItemSnapshotItem[] { null! }).ToArray(),
                },
            });
            var leftBefore = CaptureItemClosure(left);
            var rightBefore = CaptureItemClosure(right);
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.Empty(party.Items);
            Assert.Empty(party.Other.Items);
            Assert.Equal(0L, party.Money);
            Assert.Equal(0U, party.ConquerPoints);
            Assert.Equal(0L, party.Other.Money);
            Assert.Equal(0U, party.Other.ConquerPoints);
            Assert.True(LegacyTradeEscrowClaims.TryRead(Current(left).Character.Attributes,
                out var leftClaimBefore, out var leftReason), leftReason);
            Assert.True(LegacyTradeEscrowClaims.TryRead(Current(right).Character.Attributes,
                out var rightClaimBefore, out var rightReason), rightReason);
            Assert.Null(leftClaimBefore);
            Assert.Null(rightClaimBefore);

            await SendItemClosureAsync(left, new(action)
            {
                Money = action == 7 ? 300 : 0,
                CPS = action == 12 ? 10U : 0U,
            });

            AssertItemClosureUnchanged(left, leftBefore);
            AssertItemClosureUnchanged(right, rightBefore);
            AssertWallet(left, 1_000, 50);
            AssertWallet(right, 2_000, 60);
            Assert.Equal(leftBefore.SentCount, left.Sent.Count);
            Assert.Equal(rightBefore.SentCount, right.Sent.Count);
            Assert.True(trades.TryGet(left.Connection, out var retained));
            Assert.Same(party, retained);
            Assert.True(Current(left).LegacyTradeActive);
            Assert.True(Current(right).LegacyTradeActive);
            Assert.Empty(party.Items);
            Assert.Empty(party.Other.Items);
            Assert.Equal(0L, party.Money);
            Assert.Equal(0U, party.ConquerPoints);
            Assert.Equal(0L, party.Other.Money);
            Assert.Equal(0U, party.Other.ConquerPoints);
            Assert.False(party.Confirmed);
            Assert.False(party.Other.Confirmed);
            Assert.True(LegacyTradeEscrowClaims.TryRead(Current(left).Character.Attributes,
                out var leftClaimAfter, out leftReason), leftReason);
            Assert.True(LegacyTradeEscrowClaims.TryRead(Current(right).Character.Attributes,
                out var rightClaimAfter, out rightReason), rightReason);
            Assert.Null(leftClaimAfter);
            Assert.Null(rightClaimAfter);
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
            // No Close/refund attempt with the malformed graph. There was never
            // new escrow to refund, and all actual registered work is terminal.
        });
}
