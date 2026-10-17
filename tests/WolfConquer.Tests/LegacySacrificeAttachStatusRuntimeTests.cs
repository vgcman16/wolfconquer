using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort SacrificeAttachStatusSpellId = 14_320;
    private const int SacrificeStatusFlag = 0xD3;
    private const int SacrificeDurationSeconds = 2_592_000;

    [Fact]
    public async Task HandlerSacrifice14320SelfCastAppliesStatusAnimationEquipmentAndProgress()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                SacrificeAttachStatusSpellId,
                sort: 6,
                level: 0,
                experience: 50,
                duration: 17,
                coldTime: 0),
            CreateMagicRow(
                SacrificeAttachStatusSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                duration: 17,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [SacrificeAttachStatusSpellId]);
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
                X: 844,
                Y: 589,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: SacrificeAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, SacrificeStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacySacrificeRemoveOnDead"));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacySacrificeExpiresAtUtc",
            beforeCast,
            afterCast,
            SacrificeDurationSeconds);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(SacrificeAttachStatusSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == SacrificeAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, SacrificeStatusFlag)));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == SacrificeAttachStatusSpellId);
        var equipmentIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        var spellUpdateIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == SacrificeAttachStatusSpellId);
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < animationIndex);
        Assert.True(animationIndex < equipmentIndex);
        Assert.True(equipmentIndex < spellUpdateIndex);
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill));

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(844, spell.X);
        Assert.Equal(589, spell.Y);
        Assert.Equal(SacrificeAttachStatusSpellId, spell.SpellId);
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
        Assert.Equal(SacrificeAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Sacrifice AttachStatus magic", output);
        Assert.Contains("duration=2592000", output);
        Assert.Contains("removeOnDead=False", output);
        Assert.Contains("source=AttachStatus.cs case 14320", output);
        Assert.Contains("appliedExperience=500", output);
    }

    [Fact]
    public async Task HandlerSacrifice14320ActiveCastRemovesStatusRefreshesEquipmentWithoutAnimationOrProgress()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                SacrificeAttachStatusSpellId,
                sort: 6,
                level: 0,
                experience: 50,
                duration: 17,
                coldTime: 0),
            CreateMagicRow(
                SacrificeAttachStatusSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                duration: 17,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [SacrificeAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(SacrificeStatusFlag),
                ["LegacySacrificeExpiresAtUtc"] = DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: 844,
                Y: 589,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: SacrificeAttachStatusSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, SacrificeStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacySacrificeExpiresAtUtc"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacySacrificeRemoveOnDead"));

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == SacrificeAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, SacrificeStatusFlag)));
        var equipmentIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < equipmentIndex);
        Assert.Empty(ParseSpellAnimations(sentSnapshot));
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Sacrifice AttachStatus toggle", output);
        Assert.Contains("removedStatusFlag=211", output);
        Assert.Contains("source=AttachStatus.cs case 14320", output);
        Assert.DoesNotContain("appliedExperience=500", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresSacrifice14320FlagWithoutDeathRemovalAttribute()
    {
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var now = DateTimeOffset.UtcNow;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(10))),
            log: log);
        var session = CreateSession(
            spellIds: [SacrificeAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(SacrificeStatusFlag),
                ["LegacySacrificeExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, SacrificeStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacySacrificeExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacySacrificeRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, SacrificeStatusFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacySacrificeExpiresAtUtc"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacySacrificeRemoveOnDead"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, SacrificeStatusFlag)));
            Assert.Contains("WorldServer expired legacy Sacrifice flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }
}
