using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class CapriceNestedContinuationOwnedTests
{
    [Theory]
    [InlineData((ushort)10950)] [InlineData((ushort)20360)]
    public Task ActualNestedWrapperPreservesNewerPeerWhenLaterTransformContinuationRefuses(ushort proc) =>
        F.CapriceNestedRunAsync(proc, async (f, _catalog) =>
        {
            await PrepareQueries(f);
            var actorBinding = Binding(f, Actor); var peerBinding = Binding(f, Peer);
            WorldSession? newer = null; byte[]? pairedPeerImage = null;
            DateTimeOffset? acceptedDeadline = null; bool observed = false;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (observed || recipient != Actor || !LegacyShowEquipmentPacket.TryParse(
                        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
                observed = true;
                // This is an actual post-pair transform query output. No test
                // supplies an exception or a replacement receive/proc body.
                PairSaved(f); Assert.Equal("60", f.Current(Actor).Character.Attributes["Stamina"]);
                var peer = f.Current(Peer); Assert.Equal("60", peer.Character.Attributes["Stamina"]);
                Assert.Equal(1L, peer.Character.MinHitPoints);
                pairedPeerImage = File.ReadAllBytes(f.CharacterPath(Peer));
                acceptedDeadline = f.Current(Actor).MagicCooldowns![proc];
                Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(Peer), Peer, out var identity));
                var attributes = new Dictionary<string, string>(peer.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                    { ["Stamina"] = "70" };
                f.Store.Set(f.Connection(Peer), peer with { Character = peer.Character with { Attributes = attributes } });
                newer = f.Current(Peer);
                Assert.True(LegacyNativeClientPublication.TryCurrent(f.Store, f.Connection(Peer), Peer, identity, out var current));
                Assert.Same(newer, current); PairSaved(f);
            };
            Exception? failure;
            try
            {
                using var shared = await Operations(f).EnterSharedAsync();
                failure = await Record.ExceptionAsync(async () => { await InvokeWrapper(f, proc); });
            }
            finally { f.BeforeSend = null; }

            Assert.True(observed, f.Log.ToString()); Assert.NotNull(newer);
            Assert.Same(newer, f.Current(Peer)); Assert.Equal("70", newer.Character.Attributes["Stamina"]);
            Assert.Equal(1L, newer.Character.MinHitPoints); Assert.Equal(1, peerBinding.Player.HitPoints);
            Assert.Same(actorBinding, Binding(f, Actor)); Assert.Same(peerBinding, Binding(f, Peer));
            Assert.False(f.Recovery.IsBlocked); PairSaved(f);
            Assert.NotNull(failure); Assert.Equal("LegacyCapricePairContinuationRefusalException", failure.GetType().Name);
            Assert.Equal("capricePairCommittedContinuationRefused:targetRecordChanged", failure.Message);
            var actorHistory = Historical(failure, "PublishedAttacker"); var peerHistory = Historical(failure, "PublishedTarget");
            Assert.Equal((long)Actor, actorHistory.Character.Uid); Assert.Equal((long)Peer, peerHistory.Character.Uid);
            Assert.Equal("60", actorHistory.Character.Attributes["Stamina"]);
            Assert.Equal("60", peerHistory.Character.Attributes["Stamina"]); Assert.Equal(493L, actorHistory.Character.MinMana);
            Assert.Same(actorBinding.Client, actorHistory.NativeGameClient);
            Assert.Same(peerBinding.Player, peerHistory.NativePlayer);
            Assert.Equal(acceptedDeadline, f.Current(Actor).MagicCooldowns![proc]);
            Assert.Equal("0", f.Current(Actor).Character.Attributes["TransformationID"]);
            Assert.Contains(f.ActorFrames, bytes => Finish(bytes, Actor));
            Assert.DoesNotContain(f.Frames(Peer), bytes => Finish(bytes, Peer));
            Assert.DoesNotContain(f.Frames(Peer), bytes => Stamina(bytes, 60));
            Assert.DoesNotContain(f.ActorFrames, bytes => Spell(bytes, proc));
            Assert.Equal(1L, Saved(f, Peer).MinHitPoints);
            Assert.Equal(pairedPeerImage, File.ReadAllBytes(f.CharacterPath(Peer)));
        });

    [Theory]
    [InlineData((ushort)10950)] [InlineData((ushort)20360)]
    public Task SameActualNestedWrapperWithoutInterleaveCompletesTransformDamageAndPairReplies(ushort proc) =>
        F.CapriceNestedRunAsync(proc, async (f, _catalog) =>
        {
            await PrepareQueries(f); bool observed = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (observed || uid != Actor || !LegacyShowEquipmentPacket.TryParse(
                        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
                observed = true; PairSaved(f);
                Assert.Equal("60", f.Current(Peer).Character.Attributes["Stamina"]);
                Assert.Equal(1L, f.Current(Peer).Character.MinHitPoints);
            };
            try
            {
                using var shared = await Operations(f).EnterSharedAsync();
                await InvokeWrapper(f, proc);
            }
            finally { f.BeforeSend = null; }
            Assert.True(observed, f.Log.ToString()); PairSaved(f); Assert.False(f.Recovery.IsBlocked);
            Assert.Equal(0L, f.Current(Peer).Character.MinHitPoints); Assert.Equal(0L, Saved(f, Peer).MinHitPoints);
            Assert.Equal(0, Binding(f, Peer).Player.HitPoints);
            Assert.Equal("0", f.Current(Actor).Character.Attributes["TransformationID"]);
            Assert.Equal("0", f.Current(Peer).Character.Attributes["TransformationID"]);
            Assert.Contains(f.ActorFrames, bytes => Finish(bytes, Actor));
            Assert.Contains(f.Frames(Peer), bytes => Finish(bytes, Peer));
            Assert.Contains(f.ActorFrames, bytes => Spell(bytes, proc));
            Assert.Contains(f.ActorFrames, bytes => bytes.SequenceEqual(Convert.FromHexString("0E00800808A0FA4E12040808103C")));
            Assert.Contains(f.Frames(Peer), bytes => bytes.SequenceEqual(Convert.FromHexString("0E00800808A1FA4E12040808103C")));
        });
}
