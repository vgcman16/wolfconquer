using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyCommittedPairPublicationFailureTests
{
    [Fact]
    public Task PairWriterCommittedExceptionRetainsCauseAndRetiresBothRealOwners()
    {
        var catalog = LegacyItemWorldDataFixture.Create([], []);
        OwnedEquipmentBehaviorInput Player(uint uid, string name) => new(
            OwnedEquipmentBehaviorFixture.Character(uid, name, classCode: 1000, level: 100,
                strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000), PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(Player(OwnedEquipmentBehaviorFixture.Actor, "PairActor"), catalog, f =>
        {
            var actorConnection = f.Connection(OwnedEquipmentBehaviorFixture.Actor);
            var peerConnection = f.Connection(OwnedEquipmentBehaviorFixture.Peer);
            var actor = f.Current(); var peer = f.Current(OwnedEquipmentBehaviorFixture.Peer);
            var actorBinding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
            var peerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(peer.NativeGameClient!.OrdinarySpellBinding);
            Assert.NotSame(actorBinding, peerBinding); Assert.Same(f.Recovery, actorBinding.Recovery);
            Assert.Same(f.Recovery, peerBinding.Recovery);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(actorBinding)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
            var actorSpells = actorBinding.Storage.ClientSpells; var peerSpells = peerBinding.Storage.ClientSpells;
            var actorSpellBytes = PlayerSpellSnapshotLoader.ToLegacyBytes(actor.PlayerSpells);
            var peerSpellBytes = PlayerSpellSnapshotLoader.ToLegacyBytes(peer.PlayerSpells);
            var actorItems = PlayerItemSnapshotLoader.ToLegacyBytes(actor.PlayerItems);
            var peerItems = PlayerItemSnapshotLoader.ToLegacyBytes(peer.PlayerItems);
            var actorHp = actorBinding.Player.HitPoints; var peerHp = peerBinding.Player.HitPoints;
            var actorStamp = actorBinding.Player.AttackStamp; var peerStamp = peerBinding.Player.AttackStamp;
            var files = f.Data.Files(); f.ActorFrames.Clear(); f.Frames(OwnedEquipmentBehaviorFixture.Peer).Clear();
            var cause = CaptureDeclaredCause();
            var primary = new LegacyCommittedSpellPublicationException(cause);
            int calls = 0; WorldSession? first = null, second = null;
            var observed = Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                f.Store.TryCommitPair(actorConnection, actor, actor, peerConnection, peer, peer,
                    () => { calls++; return ThrowDeclaredCommittedFailure(primary); }, out first, out second));
            Assert.Equal(1, calls); Assert.Same(primary, observed); Assert.Same(cause, observed.InnerException);
            Assert.Contains(nameof(ThrowDeclaredCommittedFailure), observed.StackTrace, StringComparison.Ordinal);
            Assert.Contains(nameof(CaptureDeclaredCause), cause.StackTrace, StringComparison.Ordinal);
            Assert.Null(first); Assert.Null(second); Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(actorConnection, out _)); Assert.False(f.Store.TryGet(peerConnection, out _));
            Assert.True(actorBinding.IsRetired); Assert.True(peerBinding.IsRetired);
            Assert.Same(actorSpells, actorBinding.Storage.ClientSpells); Assert.Same(peerSpells, peerBinding.Storage.ClientSpells);
            Assert.Equal(actorSpellBytes, PlayerSpellSnapshotLoader.ToLegacyBytes(actor.PlayerSpells));
            Assert.Equal(peerSpellBytes, PlayerSpellSnapshotLoader.ToLegacyBytes(peer.PlayerSpells));
            Assert.Equal(actorItems, PlayerItemSnapshotLoader.ToLegacyBytes(actor.PlayerItems));
            Assert.Equal(peerItems, PlayerItemSnapshotLoader.ToLegacyBytes(peer.PlayerItems));
            Assert.Equal(actorHp, actorBinding.Player.HitPoints); Assert.Equal(peerHp, peerBinding.Player.HitPoints);
            Assert.Equal(actorStamp, actorBinding.Player.AttackStamp); Assert.Equal(peerStamp, peerBinding.Player.AttackStamp);
            Assert.False(actorBinding.Client.OnAutoAttack); Assert.False(peerBinding.Client.OnAutoAttack);
            Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(OwnedEquipmentBehaviorFixture.Peer)); f.Data.AssertFiles(files);
            return Task.CompletedTask;
        }, Player(OwnedEquipmentBehaviorFixture.Peer, "PairPeer"));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ThrowDeclaredCommittedFailure(LegacyCommittedSpellPublicationException failure) => throw failure;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IOException CaptureDeclaredCause()
    {
        try { throw new IOException("declared pair persistence cause"); }
        catch (IOException cause) { return cause; }
    }
}
