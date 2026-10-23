using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ActiveTitleStorageSafetyTests
{
    public enum AdditionalPeerChange
    {
        EqualValueReplacement,
        Removal,
        Teleport,
        VisibleDifferentUid,
        RetireAndReAddSameReference,
    }

    [Theory]
    [InlineData(AdditionalPeerChange.EqualValueReplacement)]
    [InlineData(AdditionalPeerChange.Removal)]
    [InlineData(AdditionalPeerChange.Teleport)]
    [InlineData(AdditionalPeerChange.VisibleDifferentUid)]
    [InlineData(AdditionalPeerChange.RetireAndReAddSameReference)]
    public async Task PeerChangedAfterOwnerAcknowledgementReceivesNoPreparedSpawn(
        AdditionalPeerChange change)
    {
        using var fixture = new Fixture { VerifyPersistenceBeforePublication = true };
        Assert.True(fixture.Sessions.TryGet(fixture.PeerConnection, out var capturedPeer));
        var originalPeer = Assert.IsType<WorldSession>(capturedPeer);
        var capturedIdentity = CaptureAdditionalPeerIdentity(fixture, fixture.PeerConnection,
            originalPeer.Character.Uid);
        WorldSession? laterPeer = null;
        var observedCommit = false;
        fixture.AfterOwnerFrameSent = () =>
        {
            Assert.Single(fixture.Sent);
            Assert.Equal(PackedWing, fixture.Current().Character.Attributes["SpecialWingID"]);
            Assert.Equal(PackedWing, fixture.ReadPersisted().Attributes["SpecialWingID"]);
            observedCommit = true;
            fixture.AfterOwnerFrameSent = null;
            switch (change)
            {
                case AdditionalPeerChange.Removal:
                    Assert.True(fixture.Sessions.TryRemove(fixture.PeerConnection, out var removed));
                    Assert.Same(originalPeer, removed);
                    break;
                case AdditionalPeerChange.RetireAndReAddSameReference:
                    Assert.True(fixture.Sessions.TryRemove(fixture.PeerConnection, out var retired));
                    Assert.Same(originalPeer, retired);
                    laterPeer = originalPeer;
                    fixture.Sessions.Set(fixture.PeerConnection, laterPeer);
                    Assert.NotSame(capturedIdentity, CaptureAdditionalPeerIdentity(fixture,
                        fixture.PeerConnection, laterPeer.Character.Uid));
                    break;
                default:
                    laterPeer = change switch
                    {
                        AdditionalPeerChange.EqualValueReplacement => originalPeer with { },
                        AdditionalPeerChange.Teleport => originalPeer with
                        { Character = originalPeer.Character with { MapId = 1015, X = 100, Y = 100 } },
                        AdditionalPeerChange.VisibleDifferentUid => originalPeer with
                        {
                            AccountId = originalPeer.Character.Uid + 99,
                            Username = "synthetic-replacement-peer",
                            Character = originalPeer.Character with { Uid = originalPeer.Character.Uid + 99, Name = "NewVisiblePeer" },
                        },
                        _ => throw new InvalidOperationException("Unsupported finite peer control."),
                    };
                    Assert.NotSame(originalPeer, laterPeer);
                    fixture.Sessions.Set(fixture.PeerConnection, laterPeer);
                    if (change == AdditionalPeerChange.EqualValueReplacement)
                    {
                        Assert.Equal(originalPeer, laterPeer);
                        Assert.Same(capturedIdentity, CaptureAdditionalPeerIdentity(fixture,
                            fixture.PeerConnection, laterPeer.Character.Uid));
                    }
                    break;
            }
        };

        await fixture.DispatchAsync();

        Assert.True(observedCommit);
        Assert.NotSame(fixture.Original, fixture.Current());
        Assert.Equal(PackedWing, fixture.Current().Character.Attributes["SpecialWingID"]);
        Assert.Equal(ActiveWingTitles, fixture.ReadPersisted().Attributes["SpecialTitles"]);
        AssertTitleAttributesEqual(fixture.Current().Character, fixture.ReadPersisted());
        Assert.Equal(new[] { LegacyTitleStorageAction.Equip, LegacyTitleStorageAction.UseTitle },
            fixture.Sent.Select(frame => ReadTitlePacket(frame).Action));
        Assert.Empty(fixture.PeerSent);
        Assert.False(fixture.Recovery.IsBlocked);
        if (change == AdditionalPeerChange.Removal)
            Assert.False(fixture.Sessions.TryGet(fixture.PeerConnection, out _));
        else
        {
            Assert.True(fixture.Sessions.TryGet(fixture.PeerConnection, out var currentPeer));
            Assert.Same(laterPeer, currentPeer);
        }
    }

    private static object CaptureAdditionalPeerIdentity(Fixture fixture, ConquerTcpConnection connection, long uid)
    {
        object? identity = null;
        Assert.True(fixture.Sessions.WithCurrentNativeClientIdentity(connection, uid, current => identity = current));
        Assert.NotNull(identity);
        return identity!;
    }
}
