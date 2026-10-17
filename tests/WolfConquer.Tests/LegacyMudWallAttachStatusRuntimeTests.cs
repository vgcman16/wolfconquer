using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort MudWallAttachStatusSpellId = 16_440;
    private const int MudWallStatusFlag = 0x112;

    [Theory]
    [InlineData(16_440)]
    [InlineData(16_450)]
    public async Task HandlerMudWallSelfStatusSendsArchiveAnimationAndEquipmentRefresh(ushort spellId)
    {
        const uint duration = 37;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                spellId,
                sort: 6,
                useMana: 0,
                coldTime: 0,
                duration: duration));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [spellId]);
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
            ReadFrame(new MsgAttackPacket(CreateSelfStatusSetterQuery(spellId, session)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, MudWallStatusFlag));
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyMudWallRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyMudWallExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: checked((int)duration));

        var sentSnapshot = sent.ToArray();
        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, MudWallStatusFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(sentSnapshot).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == MudWallStatusFlag);
        Assert.Equal(duration, archiveEntry.Time);
        Assert.Equal(0U, archiveEntry.Damage);
        Assert.Equal(0U, archiveEntry.SpellLevel);

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(0, spell.X);
        Assert.Equal(0, spell.Y);
        Assert.Equal(spellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);

        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, MudWallStatusFlag)));
        var archiveIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == MudWallStatusFlag));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == spellId);
        var equipmentIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(statusIndex < archiveIndex);
        Assert.True(archiveIndex < animationIndex);
        Assert.True(animationIndex < equipmentIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy MudWall AttachStatus magic", output);
        Assert.Contains("source=AttachStatus.cs case 16440/16450", output);
    }
}
