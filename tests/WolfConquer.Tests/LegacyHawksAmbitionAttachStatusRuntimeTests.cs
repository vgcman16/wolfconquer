using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort HawksAmbitionAttachStatusSpellId = 15_920;
    private const int HawksAmbitionStatusFlag = 0x100;

    [Fact]
    public async Task HandlerHawksAmbition15920SelfStatusSendsSourceInteractionArchiveAnimationEquipmentAndProgress()
    {
        const uint duration = 33;
        const uint power = 35;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                HawksAmbitionAttachStatusSpellId,
                sort: 6,
                level: 0,
                experience: 50,
                damage: power,
                duration: duration,
                coldTime: 0),
            CreateMagicRow(
                HawksAmbitionAttachStatusSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                damage: power,
                duration: duration,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [HawksAmbitionAttachStatusSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: 0,
                Y: 0,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: HawksAmbitionAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, HawksAmbitionStatusFlag));
        Assert.Equal(power.ToString(), updatedSession.Character.Attributes["HawksAmbitionPower"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyHawksAmbitionRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyHawksAmbitionExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: checked((int)duration));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(HawksAmbitionAttachStatusSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == HawksAmbitionAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, HawksAmbitionStatusFlag)));
        var archiveIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == HawksAmbitionStatusFlag &&
                    entry.Time == duration &&
                    entry.Damage == 0 &&
                    entry.SpellLevel == 0));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == HawksAmbitionAttachStatusSpellId);
        var equipmentIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < archiveIndex);
        Assert.True(archiveIndex < animationIndex);
        Assert.True(animationIndex < equipmentIndex);

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(0, spell.X);
        Assert.Equal(0, spell.Y);
        Assert.Equal(HawksAmbitionAttachStatusSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);
        var spellUpdate = Assert.Single(
            sentSnapshot
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(HawksAmbitionAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy HawksAmbition AttachStatus magic", output);
        Assert.Contains("source=AttachStatus.cs case 15920", output);
        Assert.Contains("appliedExperience=500", output);
    }
}
