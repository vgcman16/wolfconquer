using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort RevengeTailAttachStatusSpellId = 13_030;
    private const int RevengeTailStatusFlag = 0xCA;
    private const uint RevengeGaleRuneItemId = 4_039_009;

    [Fact]
    public async Task HandlerRevengeTail13030SelfCastAppliesStatusRunePowerAndSourceAnimation()
    {
        var worldData = CreateMagicCatalogWithItems(
            CreateMonsterFamily(),
            [],
            [CreateItemType((int)RevengeGaleRuneItemId, "RevengeGale(+9)")],
            CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, level: 0, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [RevengeTailAttachStatusSpellId],
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = RevengeGaleRuneItemId,
                Position = 101,
            }));
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
                X: 833,
                Y: 586,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: RevengeTailAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, RevengeTailStatusFlag));
        Assert.Equal("100000", updatedSession.Character.Attributes["LegacyRevengeTailChange"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyRevengeTailRemoveOnDead"]);
        Assert.Equal("20", updatedSession.Character.Attributes["LegacyRevengeTailStampSeconds"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyRevengeTailExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: 10);

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == RevengeTailAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, RevengeTailStatusFlag)));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == RevengeTailAttachStatusSpellId);
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < animationIndex);

        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill));
        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(833, spell.X);
        Assert.Equal(586, spell.Y);
        Assert.Equal(RevengeTailAttachStatusSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy RevengeTail AttachStatus magic", output);
        Assert.Contains("source=AttachStatus.cs case 13030", output);
        Assert.Contains("revengeTailChange=100000", output);
    }

    [Fact]
    public async Task HandlerRevengeTail13030ActiveCastRemovesStatusWithoutAnimation()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, level: 0, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [RevengeTailAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(RevengeTailStatusFlag),
                ["LegacyRevengeTailChange"] = "5",
                ["LegacyRevengeTailExpiresAtUtc"] = DateTimeOffset.UtcNow.AddSeconds(10).ToString("O"),
                ["LegacyRevengeTailRemoveOnDead"] = "true",
                ["LegacyRevengeTailStampSeconds"] = "20",
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
                X: 833,
                Y: 586,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: RevengeTailAttachStatusSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, RevengeTailStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyRevengeTailExpiresAtUtc"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyRevengeTailRemoveOnDead"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyRevengeTailStampSeconds"));

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == RevengeTailAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, RevengeTailStatusFlag)));
        Assert.True(interactionIndex < statusIndex);
        Assert.Empty(ParseSpellAnimations(sentSnapshot));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy RevengeTail AttachStatus toggle", output);
        Assert.Contains("removedStatusFlag=202", output);
        Assert.Contains("source=AttachStatus.cs case 13030", output);
    }

    [Fact]
    public async Task HandlerRevengeTail13030NonSelfTargetSendsTargetlessAnimationOnly()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, level: 0, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [RevengeTailAttachStatusSpellId]);
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
                OpponentUid: TargetPlayerUid,
                X: 840,
                Y: 590,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: RevengeTailAttachStatusSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, RevengeTailStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyRevengeTailChange"));

        var sentSnapshot = sent.ToArray();
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(840, spell.X);
        Assert.Equal(590, spell.Y);
        Assert.Equal(RevengeTailAttachStatusSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy RevengeTail AttachStatus magic", output);
        Assert.Contains("selfTarget=False", output);
        Assert.Contains("source=AttachStatus.cs case 13030", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresRevengeTailFlagAndClearsCounterAttributes()
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
            spellIds: [RevengeTailAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(RevengeTailStatusFlag),
                ["LegacyRevengeTailChange"] = "5",
                ["LegacyRevengeTailExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyRevengeTailRemoveOnDead"] = "true",
                ["LegacyRevengeTailStampSeconds"] = "20",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, RevengeTailStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("LegacyRevengeTailChange") &&
                        !current.Character.Attributes.ContainsKey("LegacyRevengeTailExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyRevengeTailRemoveOnDead") &&
                        !current.Character.Attributes.ContainsKey("LegacyRevengeTailStampSeconds");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, RevengeTailStatusFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyRevengeTailChange"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyRevengeTailExpiresAtUtc"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyRevengeTailRemoveOnDead"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyRevengeTailStampSeconds"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, RevengeTailStatusFlag)));
            Assert.Contains("WorldServer expired legacy RevengeTail flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }
}
