using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlExperienceOverlapTests
{
    [Theory]
    [InlineData((ushort)17_490, 1_500U)]
    [InlineData((ushort)17_650, 1_125U)]
    public Task LethalMonsterCompletesTeamLevelQueryBeforeSameCastLethallyReceivesTheLeveledPeer(
        ushort spellId, uint playerDamage) => Run(spellId, async (f, monster, monsters, clock) =>
    {
        var actorBefore = f.Current(); var peerBefore = f.Current(Peer);
        var actorBinding = Assert.IsType<LegacyOrdinarySpellBinding>(actorBefore.NativeGameClient!.OrdinarySpellBinding);
        var peerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(peerBefore.NativeGameClient!.OrdinarySpellBinding);
        var files = ExpectedFiles(f);
        var wire = new List<(uint Recipient, byte[] Bytes)>();
        int terminalExperienceOutputs = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            wire.Add((recipient, bytes.ToArray()));
            if (recipient == Peer && bytes.AsSpan().SequenceEqual(PeerExperienceNine))
            {
                terminalExperienceOutputs++;
                Assert.False(monsters.TryGetVisibleInstance(monster, clock.GetUtcNow(), out _));
                AssertCurrentPeer(f, peerBinding, hitpoints: 540);
                AssertSavedPeer(f, hitpoints: 540);
                Assert.Equal(actorBefore.Character.ConquerPoints + 5, f.Current().Character.ConquerPoints);
            }
            if (!LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) ||
                animation.SpellId != spellId) return;
            Assert.True(terminalExperienceOutputs == 1, $"Expected one terminal experience output; actual={terminalExperienceOutputs}.\n{BoundedLog(f)}");
            AssertCurrentPeer(f, peerBinding, hitpoints: 0);
            AssertSavedPeer(f, hitpoints: 0);
            Assert.False(monsters.TryGetVisibleInstance(monster, clock.GetUtcNow(), out _));
        };
        try { await f.SendAsync(Actor, Physical(monster)); }
        finally { f.BeforeSend = null; }

        Assert.True(f.Store.TryGet(f.Connection(Actor), out _), BoundedLog(f));
        Assert.True(f.Store.TryGet(f.Connection(Peer), out _), BoundedLog(f));
        Assert.True(terminalExperienceOutputs == 1, $"Expected one terminal experience output; actual={terminalExperienceOutputs}.\n{BoundedLog(f)}");
        AssertCurrentPeer(f, peerBinding, hitpoints: 0); AssertSavedPeer(f, hitpoints: 0);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Peer).Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Same(actorBinding, f.Current().NativeGameClient!.OrdinarySpellBinding);
        Assert.Same(peerBinding, f.Current(Peer).NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(actorBinding));
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
        Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal(493U, actorBinding.Player.Mana);
        Assert.Equal(95, (int)actorBinding.Player.Stamina);
        Assert.Equal(actorBefore.Character.ConquerPoints + 5, f.Current().Character.ConquerPoints);
        Assert.Equal(0L, f.Current().Character.Experience);
        Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells, row => row.Id == spellId).Experience);
        Assert.Equal("actor-seed", f.Current().Character.Attributes["OpaqueOverlap"]);
        Assert.Equal("peer-seed", f.Current(Peer).Character.Attributes["OpaqueOverlap"]);
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        f.Data.AssertFiles(files);
        AssertWireOrder(wire, monster.Uid, spellId, playerDamage);

        // A real status request after death must expose the last complete
        // rebuilt caches, not the level10 Spirit/HP/mana image.
        int start = f.ActorFrames.Count;
        await f.SendAsync(Actor, new LegacyPlayerStatusQueryPacket(Peer).ToFrameBytes());
        var response = Assert.Single(f.ActorFrames.Skip(start));
        Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(response), out var status));
        Assert.Equal(540U, status.MaxHP); Assert.Equal(600U, status.MaxMana); Assert.Equal(21U, status.MagicAttack);
        f.Data.AssertFiles(files);
    });

    private static void AssertCurrentPeer(OwnedEquipmentBehaviorFixture f, LegacyOrdinarySpellBinding binding, int hitpoints)
    {
        var current = f.Current(Peer);
        Assert.Equal(11L, current.Character.Level); Assert.Equal(9L, current.Character.Experience);
        Assert.Equal(20L, current.Character.Vitality); Assert.Equal(20L, current.Character.Spirit);
        Assert.Equal(12L, current.Character.AttributePoints);
        Assert.Equal((long)hitpoints, current.Character.MinHitPoints);
        Assert.Equal((ushort)11, binding.Player.Level); Assert.Equal(9UL, binding.Player.Experience);
        Assert.Equal((ushort)20, binding.Player.Vitality); Assert.Equal((ushort)20, binding.Player.Spirit);
        Assert.Equal((ushort)12, binding.Player.Atributes); Assert.Equal(hitpoints, binding.Player.HitPoints);
        Assert.Equal(20L, current.Character.MinMana); Assert.Equal(450L, current.Character.MaxMana);
        Assert.Equal(20U, binding.Player.Mana);
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(current, out var maximum));
        Assert.Equal(540U, maximum); Assert.Equal(600U, LegacyPhysicalStatusOverlay.ForOwner(current).MaxMana);
        Assert.Equal(540U, binding.Client.Status.MaxHitpoints); Assert.Equal(600U, binding.Client.Status.MaxMana);
        Assert.True(LegacyNativeMagicQueryClient.TryRead(f.Store, f.Connection(Peer), Peer, out var magic, binding.NativeIdentity));
        Assert.Equal(21U, magic.MagicAttack);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId));
    }

    private static void AssertSavedPeer(OwnedEquipmentBehaviorFixture f, int hitpoints)
    {
        var bytes = File.ReadAllBytes(f.CharacterPath(Peer));
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _));
        Assert.Equal(11L, saved.Level); Assert.Equal(9L, saved.Experience);
        Assert.Equal(20L, saved.Vitality); Assert.Equal(20L, saved.Spirit);
        Assert.Equal((long)hitpoints, saved.MinHitPoints); Assert.Equal(20L, saved.MinMana); Assert.Equal(450L, saved.MaxMana);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(bytes, Peer, Peer));
    }

    private static string BoundedLog(OwnedEquipmentBehaviorFixture f)
    {
        string log = f.Log.ToString();
        return log.Length <= 12_000 ? log : log[^12_000..];
    }
}
