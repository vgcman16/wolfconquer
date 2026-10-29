using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class CapricePairCommitOwnedTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CapricePairRetainedExactParticipantRejectsCompetingRecordOrRetirementBeforeAnyWriter(bool retire) =>
        F.CapricePairRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            var actor = f.Current(Actor); var peer = f.Current(Peer); var peerBinding = Binding(f, Peer);
            var draft = F.CapricePairDraft(f);
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(Actor), Actor, out var actorIdentity));
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(Peer), Peer, out var peerIdentity));
            if (retire) Assert.True(f.Store.TryRemove(f.Connection(Peer), out _));
            else f.Store.Set(f.Connection(Peer), peer with { ActiveNpcId = 17 });
            var newer = retire ? null : f.Current(Peer); var files = f.Data.Files();
            var actorFrames = f.Frames(Actor).Count; var peerFrames = f.Frames(Peer).Count;

            Assert.False(f.Store.TryCommitCapricePair(new(f.Connection(Actor), actor, actorIdentity), draft.Actor,
                new(f.Connection(Peer), peer, peerIdentity), draft.Peer, draft.Intent, out var receipt, out _));

            Assert.Null(receipt); Assert.Same(actor, f.Current(Actor)); Assert.Equal("40", f.Current(Actor).Character.Attributes["Stamina"]);
            if (retire) { Assert.True(peerBinding.IsRetired); Assert.False(f.Store.TryGet(f.Connection(Peer), out _)); }
            else { Assert.Same(newer, f.Current(Peer)); Assert.Equal(17U, f.Current(Peer).ActiveNpcId); Assert.Equal("80", f.Current(Peer).Character.Attributes["Stamina"]); }
            Assert.Equal(actorFrames, f.Frames(Actor).Count); Assert.Equal(peerFrames, f.Frames(Peer).Count);
            f.Data.AssertFiles(files); Assert.False(f.Recovery.IsBlocked);
        });
}
