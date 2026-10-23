using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ActiveTitleStorageSafetyTests
{
    public enum SourceChange { ReplaceSession, RemoveSession, Teleport }

    [Theory]
    [InlineData(SourceChange.ReplaceSession)]
    [InlineData(SourceChange.RemoveSession)]
    [InlineData(SourceChange.Teleport)]
    public async Task SourceChangeAfterAcknowledgementStopsStaleUseTitleAndPeerSpawn(SourceChange change)
    {
        using var fixture = new Fixture { VerifyPersistenceBeforePublication = true };
        WorldSession? later = null;
        fixture.AfterOwnerFrameSent = () =>
        {
            Assert.Single(fixture.Sent);
            var committed = fixture.Current();
            Assert.Equal(PackedWing, committed.Character.Attributes["SpecialWingID"]);
            Assert.Equal(PackedWing, fixture.ReadPersisted().Attributes["SpecialWingID"]);
            fixture.AfterOwnerFrameSent = null;
            if (change == SourceChange.RemoveSession)
            {
                Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out var removed));
                Assert.Same(committed, removed);
                return;
            }

            later = change == SourceChange.Teleport
                ? committed with { Character = committed.Character with { MapId = 1015, X = 100, Y = 100 } }
                : committed with { };
            Assert.NotSame(committed, later);
            fixture.Sessions.Set(fixture.Connection, later);
        };

        await fixture.DispatchAsync();

        Assert.Equal(LegacyTitleStorageAction.Equip, ReadTitlePacket(Assert.Single(fixture.Sent)).Action);
        Assert.Empty(fixture.PeerSent);
        var persisted = fixture.ReadPersisted();
        Assert.Equal(PackedWing, persisted.Attributes["SpecialWingID"]);
        Assert.Equal(ActiveWingTitles, persisted.Attributes["SpecialTitles"]);
        if (change == SourceChange.RemoveSession)
            Assert.False(fixture.Sessions.TryGet(fixture.Connection, out _));
        else
            Assert.Same(later, fixture.Current());
    }
}
