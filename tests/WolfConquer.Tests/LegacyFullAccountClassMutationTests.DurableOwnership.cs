using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyFullAccountClassMutationTests
{
    [Theory]
    [InlineData("stale")]
    [InlineData("equal-clone")]
    [InlineData("different-account")]
    [InlineData("different-uid")]
    [InlineData("retired")]
    [InlineData("different-store")]
    public async Task ClassResetRequiresTheExactCurrentSessionBeforeEitherSavedFileOrExposure(string scenario)
    {
        await WithDurableClassAsync(async fixture =>
        {
            var original = fixture.Original;
            var supplied = fixture.Store;
            WorldSession? winner = original;
            WolfConquer.Core.Networking.ConquerTcpConnection? otherConnection = null;
            WorldSession? otherSession = null;
            switch (scenario)
            {
                case "stale":
                    winner = original with { Character = original.Character with { Money = 777777, ConquerPoints = 888 } };
                    fixture.Store.Set(fixture.Connection, winner);
                    break;
                case "equal-clone":
                    winner = original with { };
                    Assert.Equal(original, winner);
                    Assert.NotSame(original, winner);
                    fixture.Store.Set(fixture.Connection, winner);
                    break;
                case "different-account":
                    winner = original with { AccountId = original.AccountId + 1, Username = "current-other-account" };
                    fixture.Store.Set(fixture.Connection, winner);
                    break;
                case "different-uid":
                    winner = original with { Character = original.Character with { Uid = CharacterUid + 1, Name = "CurrentOtherCharacter" } };
                    fixture.Store.Set(fixture.Connection, winner);
                    break;
                case "retired":
                    Assert.True(fixture.Store.TryRemove(fixture.Connection, out var removed));
                    Assert.Same(original, removed);
                    winner = null;
                    break;
                case "different-store":
                    supplied = fixture.NewStore();
                    otherConnection = fixture.NewConnection();
                    otherSession = original with { AccountId = original.AccountId + 1,
                        Character = original.Character with { Uid = CharacterUid + 1, Name = "DifferentStoreCharacter" } };
                    supplied.Set(otherConnection, otherSession);
                    break;
            }
            var before = new DurableClassImage(original);
            var winnerBefore = winner is null ? null : new DurableClassImage(winner);
            var otherBefore = otherSession is null ? null : new DurableClassImage(otherSession);
            var handled = false;
            var error = await Record.ExceptionAsync(async () =>
            { handled = await fixture.InvokeAsync(original, supplied); });

            fixture.RequireFilesUnchanged();
            before.RequireUnchanged();
            winnerBefore?.RequireUnchanged();
            otherBefore?.RequireUnchanged();
            if (winner is null) Assert.False(fixture.Store.TryGet(fixture.Connection, out _));
            else
            {
                Assert.True(fixture.Store.TryGet(fixture.Connection, out var current));
                Assert.Same(winner, current);
            }
            if (otherConnection is not null)
            {
                Assert.False(supplied.TryGet(fixture.Connection, out _));
                Assert.True(supplied.TryGet(otherConnection, out var otherCurrent));
                Assert.Same(otherSession, otherCurrent);
            }
            Assert.Empty(fixture.Sent);
            Assert.False(fixture.Disconnected);
            Assert.True(handled);
            Assert.Null(error);
            Assert.DoesNotContain("WorldServer applied LegacyNpc AccountSecurity FullAccount class reset", fixture.Log.ToString());
        });
    }
}
