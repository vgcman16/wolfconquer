using WolfConquer.Core.World;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class ClassicTargetAdmissionOwnedTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private static Task Cast(OwnedEquipmentBehaviorFixture f, ushort id, uint target) => f.SendAsync(Actor,
        new MsgAttackPacket(new MsgAttackQuery(Actor, target, 836, 584, MsgAttackType.Magic,
            0, 0, 0, MsgAttackEffect.None, 0, SpellId: id)).ToFrameBytes());
    private static MonsterRuntimeStateStore States(OwnedEquipmentBehaviorFixture f) =>
        Assert.IsType<MonsterRuntimeStateStore>(f.Handler.CreateMonsterRuntimeContext(new(f.Connection(Actor), f.Current())).MonsterStates);
    private static bool Flag(CharacterSnapshot character, int flag) => LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag);
    private static LegacyOrdinarySpellBinding Owner(OwnedEquipmentBehaviorFixture f)
    {
        var owner = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(owner)); Assert.False(owner.IsRetired);
        Assert.False(f.Recovery.IsBlocked); return owner;
    }
    private static void Rejected(OwnedEquipmentBehaviorFixture f, WorldSession actor, WorldSession peer, Dictionary<string, byte[]> files)
    {
        Assert.Same(actor, f.Current()); Assert.Same(peer, f.Current(Peer)); Owner(f);
        Assert.Equal(500L, actor.Character.MinMana); Assert.Equal(500U, actor.NativePlayer!.Mana);
        Assert.Equal("100", actor.Character.Attributes["Stamina"]); Assert.Null(actor.MagicCooldowns);
        Assert.Equal((ushort)0, actor.LegacyRandomSpell); Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(Peer));
        f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
    private static LegacySpellAnimationPacket[] Animations(OwnedEquipmentBehaviorFixture f) => f.ActorFrames
        .Select(OwnedEquipmentBehaviorFixture.ReadFrame).Where(frame => LegacySpellAnimationPacket.TryParse(frame, out _))
        .Select(frame => { Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var packet)); return packet; }).ToArray();
}
