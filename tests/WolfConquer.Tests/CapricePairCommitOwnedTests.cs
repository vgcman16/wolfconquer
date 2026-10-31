using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class CapricePairCommitOwnedTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private static CharacterSnapshot Saved(OwnedEquipmentBehaviorFixture f, uint uid) =>
        Assert.IsType<CharacterSnapshot>(f.Data.Loader.TryLoadByAccountIdForAdmission(uid).Snapshot);
    private static LegacyOrdinarySpellBinding Binding(OwnedEquipmentBehaviorFixture f, uint uid)
    {
        var session = f.Current(uid);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(session.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired); return binding;
    }
    private static void SetRuntimeStamina(OwnedEquipmentBehaviorFixture f, uint uid, string value)
    {
        var current = f.Current(uid);
        var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Stamina"] = value };
        f.Store.Set(f.Connection(uid), current with { Character = current.Character with { Attributes = attributes } });
        Assert.Equal(ushort.Parse(value, CultureInfo.InvariantCulture), f.Current(uid).NativePlayer!.Stamina);
    }
    private static bool StaminaFrame(byte[] bytes, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
        update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Stamina && row.Value == value);
    private static void RequireDurablePair(OwnedEquipmentBehaviorFixture f, string actor, string peer)
    {
        Assert.Equal(actor, Saved(f, Actor).Attributes["Stamina"]); Assert.Equal(peer, Saved(f, Peer).Attributes["Stamina"]);
        Assert.Equal(493L, Saved(f, Actor).MinMana);
        foreach (var uid in new[] { Actor, Peer })
        {
            var current = f.Current(uid); var binding = Binding(f, uid);
            Assert.Equal(current.Character.MinMana, (long)binding.Player.Mana);
            Assert.Equal(current.Character.MinHitPoints, (long)binding.Player.HitPoints);
            Assert.Equal(current.Character.MinHitPoints, Saved(f, uid).MinHitPoints);
            Assert.Equal(LegacyStaminaState.RequireCurrent(current.Character), binding.Player.Stamina);
        }
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CapricePairCommitsNativeBalancesOrRefusesTheOriginalOutOfWidthSavedInputs(bool outOfWidthSaved) =>
        outOfWidthSaved ? F.CapricePairRejectedSavedBalancesAsync("70000", "80000") : F.CapricePairRunAsync(async (f, world) =>
        {
            await F.CapricePairPrepareQueriesAsync(f, world);
            File.AppendAllText(f.CharacterPath(Actor), "; CapricePairOpaque actor\r\n");
            File.AppendAllText(f.CharacterPath(Peer), "; CapricePairOpaque peer\r\n");
            var actor = Binding(f, Actor); var peer = Binding(f, Peer);
            const string actorAfter = "60", peerAfter = "60";
            bool observed = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (observed || uid != Actor || !StaminaFrame(bytes, ulong.Parse(actorAfter, CultureInfo.InvariantCulture))) return;
                observed = true; RequireDurablePair(f, actorAfter, peerAfter);
                Assert.Equal(actorAfter, f.Current(Actor).Character.Attributes["Stamina"]);
                Assert.Equal(peerAfter, f.Current(Peer).Character.Attributes["Stamina"]);
                Assert.Equal(f.Current(Actor).Character.Attributes["LegacyCapriceStampUtc"], Saved(f, Actor).Attributes["LegacyCapriceStampUtc"]);
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            };
            await F.CapricePairAttackAsync(f); f.BeforeSend = null;
            Assert.True(observed); RequireDurablePair(f, actorAfter, peerAfter);
            Assert.Contains(f.Frames(Peer), bytes => StaminaFrame(bytes, ulong.Parse(peerAfter, CultureInfo.InvariantCulture)));
            Assert.Contains("; CapricePairOpaque actor", File.ReadAllText(f.CharacterPath(Actor)), StringComparison.Ordinal);
            Assert.Contains("; CapricePairOpaque peer", File.ReadAllText(f.CharacterPath(Peer)), StringComparison.Ordinal);
            Assert.Equal((ushort)60, actor.Player.Stamina); Assert.Equal((ushort)60, peer.Player.Stamina);
            Assert.False(f.Recovery.IsBlocked);
        });
}
