using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndedConnectionOrActualOpenTradeRefusesWithoutChangingThePartyOrEitherSession(bool trading)
    {
        await WithDurableClassAsync(async fixture =>
        {
            var otherConnection = fixture.NewConnection();
            var other = fixture.Original with { AccountId = fixture.Original.AccountId + 1, Username = "other-trade-owner",
                PlayerItems = PlayerItemSnapshot.Empty,
                Character = fixture.Original.Character with { Uid = CharacterUid + 1, Name = "OtherTradeOwner" } };
            fixture.Store.Set(otherConnection, other);
            LegacyTradeParty? party = null;
            if (trading)
            {
                lock (fixture.Trades.SyncRoot)
                {
                    party = fixture.Trades.Create(fixture.Connection, CharacterUid, otherConnection, CharacterUid + 1);
                    party.WindowOpen = true; party.Other.WindowOpen = true;
                }
            }
            else fixture.Trades.MarkConnectionEnded(fixture.Connection);
            var originalBefore = new DurableClassImage(fixture.Original);
            var otherBefore = new DurableClassImage(other);
            var counterpart = party?.Other;
            var tradeId = party?.TradeId;
            var offers = party?.Items;
            var otherOffers = counterpart?.Items;
            var handled = false;
            var error = await Record.ExceptionAsync(async () =>
            { handled = await fixture.InvokeAsync(fixture.Original); });

            fixture.RequireFilesUnchanged();
            originalBefore.RequireUnchanged(); otherBefore.RequireUnchanged();
            Assert.True(fixture.Store.TryGet(fixture.Connection, out var current));
            Assert.Same(fixture.Original, current);
            Assert.True(fixture.Store.TryGet(otherConnection, out var otherCurrent));
            Assert.Same(other, otherCurrent);
            lock (fixture.Trades.SyncRoot)
            {
                Assert.Equal(!trading, fixture.Trades.HasConnectionEnded(fixture.Connection));
                if (trading)
                {
                    Assert.True(fixture.Trades.TryGet(fixture.Connection, out var actual));
                    Assert.Same(party, actual);
                    Assert.True(fixture.Trades.TryGet(otherConnection, out var actualOther));
                    Assert.Same(counterpart, actualOther);
                    Assert.Same(counterpart, actual.Other); Assert.Same(actual, actualOther.Other);
                    Assert.Equal(tradeId, actual.TradeId); Assert.Equal(tradeId, actualOther.TradeId);
                    Assert.True(actual.WindowOpen); Assert.True(actualOther.WindowOpen);
                    Assert.True(actual.IsRequester); Assert.False(actualOther.IsRequester);
                    Assert.False(actual.Confirmed); Assert.False(actualOther.Confirmed);
                    Assert.Equal(0, actual.Money); Assert.Equal(0U, actual.ConquerPoints);
                    Assert.Equal(0, actualOther.Money); Assert.Equal(0U, actualOther.ConquerPoints);
                    Assert.Same(offers, actual.Items); Assert.Same(otherOffers, actualOther.Items);
                    Assert.Empty(actual.Items); Assert.Empty(actualOther.Items);
                }
                else Assert.False(fixture.Trades.Contains(fixture.Connection));
            }
            Assert.Empty(fixture.Sent);
            Assert.False(fixture.Disconnected);
            Assert.True(handled);
            Assert.Null(error);
        });
    }
}
