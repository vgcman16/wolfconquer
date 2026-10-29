using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class CapricePairContinuationOwnedTests
{
    [Fact]
    public Task ReturnedRealReceivePreparationCannotOverwriteNewerTargetStaminaBeforeDamagePublication() =>
        F.CapricePairRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            using var shared = await Operations(f).EnterSharedAsync();
            var phases = await PrepareActualReceive(f, world);
            var pairedActor = f.Current(Actor); var pairedPeer = f.Current(Peer);
            var actorBinding = Binding(f, Actor); var peerBinding = Binding(f, Peer);
            var peerHp = pairedPeer.Character.MinHitPoints;
            Assert.Equal(peerHp, Saved(f, Peer).MinHitPoints);
            Assert.Equal("60", pairedActor.Character.Attributes["Stamina"]);
            Assert.Equal("60", pairedPeer.Character.Attributes["Stamina"]);
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(Peer), Peer, out var identity));
            var attributes = new Dictionary<string, string>(pairedPeer.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["Stamina"] = "70" };
            f.Store.Set(f.Connection(Peer), pairedPeer with { Character = pairedPeer.Character with { Attributes = attributes } });
            var newer = f.Current(Peer);
            Assert.True(LegacyNativeClientPublication.TryCurrent(f.Store, f.Connection(Peer), Peer, identity, out var sameIssuance));
            Assert.Same(newer, sameIssuance); Assert.NotSame(pairedPeer, newer);
            DurablePair(f); Assert.Equal("70", newer.Character.Attributes["Stamina"]);
            var files = f.Data.Files(); int a = f.ActorFrames.Count, p = f.Frames(Peer).Count;

            object? committed = null;
            var failure = Record.Exception(() => { committed = Invoke("CommitLegacyPreparedPlayerReceive", phases.Context,
                phases.Prepared, Member(phases.Result, "UpdatedSession")); });

            // Old C25 overwrites70 with60 here. A real continuation refusal must
            // retain the new record and the separately accepted pair/history.
            Assert.Same(newer, f.Current(Peer)); Assert.Equal("70", f.Current(Peer).Character.Attributes["Stamina"]);
            Assert.Equal(peerHp, f.Current(Peer).Character.MinHitPoints);
            Assert.Equal(peerHp, (long)peerBinding.Player.HitPoints); Assert.Null(committed);
            Assert.NotNull(failure);
            Assert.Equal("LegacyCapricePairContinuationRefusalException", failure.GetType().Name);
            Assert.Equal("capricePairCommittedContinuationRefused:targetRecordChanged", failure.Message);
            Assert.Same(Member(phases.Receipt, "Attacker"), Member(failure, "PublishedAttacker"));
            Assert.Same(Member(phases.Receipt, "Target"), Member(failure, "PublishedTarget"));
            Assert.Same(pairedActor, f.Current(Actor)); Assert.Same(actorBinding, Binding(f, Actor)); Assert.Same(peerBinding, Binding(f, Peer));
            Assert.Equal(a, f.ActorFrames.Count); Assert.Equal(p, f.Frames(Peer).Count); f.Data.AssertFiles(files);
            DurablePair(f); Assert.False(f.Recovery.IsBlocked);

            // Exercise the real retained receipt as a separate output component;
            // current target70 may never receive its obsolete prepared60 reply.
            Assert.False(await SendActualReceipt(phases.Receipt));
            Assert.DoesNotContain(f.Frames(Peer).Skip(p), bytes => Stamina(bytes, 60));
            Assert.Same(newer, f.Current(Peer)); Assert.Equal("70", f.Current(Peer).Character.Attributes["Stamina"]);
            DurablePair(f); f.Data.AssertFiles(files); Assert.False(f.Recovery.IsBlocked);
        });
}
