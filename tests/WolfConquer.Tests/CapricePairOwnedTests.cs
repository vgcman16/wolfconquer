using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class CapricePairOwnedTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;
    private const uint Peer = OwnedEquipmentBehaviorFixture.Peer;

    // OPEN OLD-API regression: the old implementation publishes the actor's
    // +20/stamp before the genuine target HP/Character writer is attempted.
    [WindowsTradeJournalFailureFact]
    public Task CapricePairRealTargetCharacterRefusalCannotPublishOnlyTheAttackerGain() =>
        F.CapricePairRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            var actor = f.Current(Actor); var peer = f.Current(Peer);
            var actorBinding = Binding(f, Actor); var peerBinding = Binding(f, Peer);
            var actorPhysicalStamina = actorBinding.Player.Stamina;
            var peerPhysicalStamina = peerBinding.Player.Stamina;
            var stamp = actor.Character.Attributes["LegacyCapriceStampUtc"];
            Dictionary<string, byte[]>? acceptedFiles = null;
            f.BeforeSend = (uid, bytes) =>
            {
                if (acceptedFiles is not null || uid != Actor || !IsMana493(bytes)) return;
                AssertAcceptedOuterCost(f); Assert.Equal("40", f.Current(Actor).Character.Attributes["Stamina"]);
                Assert.Equal("80", f.Current(Peer).Character.Attributes["Stamina"]);
                acceptedFiles = f.Data.Files();
            };

            // Actual Windows sharing rule: reads remain available, replacement
            // is refused. The actor's separate real Mana write remains possible.
            using (var denied = new FileStream(f.CharacterPath(Peer), FileMode.Open, FileAccess.Read, FileShare.Read))
                await F.CapricePairAttackAsync(f);
            f.BeforeSend = null;

            Assert.NotNull(acceptedFiles); AssertAcceptedOuterCost(f);
            Assert.Equal(new[] { "40", "80" }, new[] {
                f.Current(Actor).Character.Attributes["Stamina"], f.Current(Peer).Character.Attributes["Stamina"] });
            Assert.Equal(stamp, f.Current(Actor).Character.Attributes["LegacyCapriceStampUtc"]);
            Assert.Same(actorBinding, Binding(f, Actor)); Assert.Same(peerBinding, Binding(f, Peer));
            Assert.Equal(peer.Character.MinHitPoints, f.Current(Peer).Character.MinHitPoints);
            Assert.Equal(peer.Character.MinHitPoints, peerBinding.Player.HitPoints);
            Assert.Equal(actorPhysicalStamina, actorBinding.Player.Stamina);
            Assert.Equal(peerPhysicalStamina, peerBinding.Player.Stamina);
            f.Data.AssertFiles(acceptedFiles!);
            AssertNoTransferOutput(f); Assert.False(f.Recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });

    // Genuine accepted-cost sender retirement, before receive/transfer capture.
    // This control preserves the existing independent outer accounting boundary.
    [Fact]
    public Task CapricePairPeerRetirementAfterAcceptedManaCannotPublishEitherTransferHalf() =>
        F.CapricePairRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            var actor = f.Current(Actor); var peer = f.Current(Peer);
            var actorBinding = Binding(f, Actor); var peerBinding = Binding(f, Peer);
            var actorPhysicalStamina = actorBinding.Player.Stamina;
            var peerPhysicalStamina = peerBinding.Player.Stamina;
            var stamp = actor.Character.Attributes["LegacyCapriceStampUtc"];
            Dictionary<string, byte[]>? acceptedFiles = null; bool retired = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (retired || uid != Actor || !IsMana493(bytes)) return;
                AssertAcceptedOuterCost(f);
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.True(f.Store.TryRemove(f.Connection(Peer), out var removed)); Assert.Same(peer.NativePlayer, removed!.NativePlayer);
                retired = true; Assert.True(peerBinding.IsRetired);
                acceptedFiles = f.Data.Files();
            };

            await F.CapricePairAttackAsync(f); f.BeforeSend = null;

            Assert.True(retired); Assert.NotNull(acceptedFiles); AssertAcceptedOuterCost(f);
            Assert.False(f.Store.TryGet(f.Connection(Peer), out _));
            Assert.Equal("40", f.Current(Actor).Character.Attributes["Stamina"]);
            Assert.Equal(stamp, f.Current(Actor).Character.Attributes["LegacyCapriceStampUtc"]);
            Assert.Same(actorBinding, Binding(f, Actor)); Assert.True(peerBinding.IsRetired);
            Assert.Equal(actorPhysicalStamina, actorBinding.Player.Stamina);
            Assert.Equal(peerPhysicalStamina, peerBinding.Player.Stamina);
            Assert.Equal(peer.Character.MinHitPoints, peerBinding.Player.HitPoints);
            f.Data.AssertFiles(acceptedFiles!); AssertNoTransferOutput(f);
            Assert.Equal("80", f.Data.Loader.TryLoadByAccountIdForAdmission(Peer).Snapshot!.Attributes["Stamina"]);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });

    private static LegacyOrdinarySpellBinding Binding(OwnedEquipmentBehaviorFixture f, uint uid)
    {
        var current = f.Current(uid);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired);
        Assert.Same(current.NativePlayer, binding.Player); return binding;
    }

    private static bool IsMana493(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Mana && row.Value == 493);

    private static void AssertAcceptedOuterCost(OwnedEquipmentBehaviorFixture f)
    {
        Assert.Equal(493L, f.Current(Actor).Character.MinMana);
        Assert.Equal(493U, f.Current(Actor).NativePlayer!.Mana);
        Assert.Equal(493L, f.Data.Loader.TryLoadByAccountIdForAdmission(Actor).Snapshot!.MinMana);
    }

    private static void AssertNoTransferOutput(OwnedEquipmentBehaviorFixture f)
    {
        foreach (var bytes in f.Frames(Actor).Concat(f.Frames(Peer)))
        {
            if (!LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update)) continue;
            Assert.DoesNotContain(update.Updates, row => row.Type == LegacyMsgUpdateDataType.Stamina);
        }
        Assert.DoesNotContain("applied legacy Caprice player-hit stamina transfer", f.Log.ToString(), StringComparison.Ordinal);
    }
}
