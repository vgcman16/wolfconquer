using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPerformerDanceLearningTests
{
    [Theory]
    [InlineData("stale")]
    [InlineData("equal-clone")]
    [InlineData("different-uid")]
    [InlineData("retired")]
    [InlineData("different-store")]
    public async Task LearningRequiresTheExactCurrentSessionInTheSuppliedStore(string scenario)
    {
        await WithDurableDanceAsync(async fixture =>
        {
            var connection = fixture.Connection();
            var owningStore = fixture.Store();
            var suppliedStore = owningStore;
            var original = fixture.Session();
            owningStore.Set(connection, original);
            fixture.Seed(CharacterUid, original.PlayerSpells);
            WorldSession? winner = original;
            WolfConquer.Core.Networking.ConquerTcpConnection? otherConnection = null;
            WorldSession? otherSession = null;

            switch (scenario)
            {
                case "stale":
                    winner = original with { Character = original.Character with { Money = 777777, ConquerPoints = 888 } };
                    owningStore.Set(connection, winner);
                    break;
                case "equal-clone":
                    winner = original with { };
                    Assert.Equal(original, winner);
                    Assert.NotSame(original, winner);
                    owningStore.Set(connection, winner);
                    break;
                case "different-uid":
                    winner = fixture.Session(CharacterUid + 1) with { AccountId = 101, Username = "performer-current" };
                    owningStore.Set(connection, winner);
                    fixture.Seed(winner.Character.Uid, winner.PlayerSpells);
                    break;
                case "retired":
                    Assert.True(owningStore.TryRemove(connection, out var removed));
                    Assert.Same(original, removed);
                    winner = null;
                    break;
                case "different-store":
                    suppliedStore = fixture.Store();
                    otherConnection = fixture.Connection();
                    otherSession = fixture.Session(CharacterUid + 1) with { AccountId = 101, Username = "other-connection" };
                    suppliedStore.Set(otherConnection, otherSession);
                    fixture.Seed(otherSession.Character.Uid, otherSession.PlayerSpells);
                    break;
            }

            var before = new DurableSessionImage(original);
            var winnerBefore = winner is null ? null : new DurableSessionImage(winner);
            var otherBefore = otherSession is null ? null : new DurableSessionImage(otherSession);
            var handled = false;
            var error = await Record.ExceptionAsync(async () =>
            { handled = await fixture.InvokeAsync(connection, suppliedStore, original); });

            fixture.RequireFilesUnchanged();
            before.RequireUnchanged();
            winnerBefore?.RequireUnchanged();
            otherBefore?.RequireUnchanged();
            if (winner is null) Assert.False(owningStore.TryGet(connection, out _));
            else
            {
                Assert.True(owningStore.TryGet(connection, out var current));
                Assert.Same(winner, current);
            }
            if (otherConnection is not null)
            {
                Assert.False(suppliedStore.TryGet(connection, out _));
                Assert.True(suppliedStore.TryGet(otherConnection, out var otherCurrent));
                Assert.Same(otherSession, otherCurrent);
            }
            Assert.Empty(fixture.Sent);
            Assert.True(handled);
            Assert.Null(error);
            Assert.Contains("reason=sessionChanged", fixture.Log.ToString());
        });
    }
}
