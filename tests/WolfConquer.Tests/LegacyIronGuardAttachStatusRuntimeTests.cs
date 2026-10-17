using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort IronGuardAttachStatusSpellId = 15_470;
    private const int IronGuardStatusFlag = 0xE4;

    [Fact]
    public async Task HandlerIronGuard15470SelfCastAppliesStatusSkillCountdownAnimationAndEquipment()
    {
        const uint duration = 35;
        const uint furyStrikeHp = 1_200;
        const uint furyStrikeAttack = 17;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                IronGuardAttachStatusSpellId,
                sort: 6,
                level: 0,
                experience: 50,
                damage: furyStrikeAttack,
                damageOnHuman: checked((int)furyStrikeHp),
                duration: duration,
                coldTime: 0),
            CreateMagicRow(
                IronGuardAttachStatusSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                damage: furyStrikeAttack,
                damageOnHuman: checked((int)furyStrikeHp),
                duration: duration,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [IronGuardAttachStatusSpellId]);
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
                X: 846,
                Y: 591,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: IronGuardAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, IronGuardStatusFlag));
        Assert.Equal(furyStrikeHp.ToString(), updatedSession.Character.Attributes["FuryStrikeHP"]);
        Assert.Equal(furyStrikeAttack.ToString(), updatedSession.Character.Attributes["FuryStrikeAttack"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyIronGuardRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyIronGuardExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)duration));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(IronGuardAttachStatusSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == IronGuardAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, IronGuardStatusFlag)));
        var countdownIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.SkillCountdown));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == IronGuardAttachStatusSpellId);
        var equipmentIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < countdownIndex);
        Assert.True(countdownIndex < animationIndex);
        Assert.True(animationIndex < equipmentIndex);
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill));
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));

        var countdownEntry = Assert.Single(
            ParseMsgUpdates([sentSnapshot[countdownIndex]]).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.SkillCountdown);
        Assert.Equal((uint)IronGuardStatusFlag, countdownEntry.Flag);
        Assert.Equal(duration, countdownEntry.Time);
        Assert.Equal(0U, countdownEntry.Damage);
        Assert.Equal(0U, countdownEntry.SpellLevel);

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(846, spell.X);
        Assert.Equal(591, spell.Y);
        Assert.Equal(IronGuardAttachStatusSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy IronGuard AttachStatus magic", output);
        Assert.Contains("statusFlag=228", output);
        Assert.Contains("furyStrikeHP=1200", output);
        Assert.Contains("furyStrikeAttack=17", output);
        Assert.Contains("source=AttachStatus.cs case 15470", output);
        Assert.DoesNotContain("appliedExperience=500", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresIronGuard15470FlagAndFuryStrikeAttributes()
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
            spellIds: [IronGuardAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(IronGuardStatusFlag),
                ["FuryStrikeHP"] = "1200",
                ["FuryStrikeAttack"] = "17",
                ["LegacyIronGuardExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyIronGuardRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, IronGuardStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("FuryStrikeHP") &&
                        !current.Character.Attributes.ContainsKey("FuryStrikeAttack") &&
                        !current.Character.Attributes.ContainsKey("LegacyIronGuardExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyIronGuardRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, IronGuardStatusFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("FuryStrikeHP"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("FuryStrikeAttack"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyIronGuardExpiresAtUtc"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyIronGuardRemoveOnDead"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, IronGuardStatusFlag)));
            Assert.Contains("WorldServer expired legacy IronGuard flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }
}
