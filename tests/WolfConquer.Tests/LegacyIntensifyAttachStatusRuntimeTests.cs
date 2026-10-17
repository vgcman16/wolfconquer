using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort IntensifyAttachStatusSpellId = 9_000;
    private const ushort SpiritFocusAttachStatusSpellId = 11_670;
    private const byte IntensifyAttachStatusLearnedLevel = 1;
    private const uint IntensifyAttachStatusDamage = 77;
    private const uint IntensifyAttachStatusDuration = 45;
    private const int FocusedStatusFlag = 0x15;

    [Theory]
    [InlineData(IntensifyAttachStatusSpellId, "Intensify")]
    [InlineData(SpiritFocusAttachStatusSpellId, "SpiritFocus")]
    public async Task HandlerIntensifyAttachStatusSelfCastStoresFocusStateSendsRewrittenInteractionAndProgressNoAnimation(
        int spellIdValue,
        string spellName)
    {
        var spellId = checked((ushort)spellIdValue);
        var worldData = CreateIntensifyAttachStatusCatalog(spellId);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var watcherSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var watcherConnection = CreateSendingConnection(watcherSent);
        var casterSession = CreateIntensifyAttachStatusSession(spellId);
        var watcherSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Watcher",
            x: 832,
            y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(watcherConnection, watcherSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var beforeCast = DateTime.Now;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateIntensifyAttachStatusQuery(spellId)).ToFrameBytes()));

        var afterCast = DateTime.Now;
        Assert.True(sessions.TryGet(casterConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        AssertIntensifyFocusState(updatedSession, spellId, beforeCast, afterCast);

        var casterInteraction = Assert.Single(ParseIntensifyAttachStatusInteractions(casterSent, spellId));
        AssertIntensifyInteraction(casterInteraction, spellId);
        var watcherInteraction = Assert.Single(ParseIntensifyAttachStatusInteractions(watcherSent, spellId));
        AssertIntensifyInteraction(watcherInteraction, spellId);

        var spellUpdate = Assert.Single(ParseLegacySpellPackets(casterSent));
        Assert.Equal(spellId, spellUpdate.Id);
        Assert.Equal(IntensifyAttachStatusLearnedLevel, spellUpdate.Level);
        Assert.Equal(checked((int)IntensifyAttachStatusDuration), spellUpdate.Experience);
        Assert.Empty(ParseLegacySpellPackets(watcherSent));
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(watcherSent));

        var output = log.ToString();
        Assert.Contains($"WorldServer handled legacy {spellName} AttachStatus magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("source=AttachStatus.cs case 9000/11670", output);
    }

    [Fact]
    public async Task RuntimeLoopPromotesArmedIntensifyFocusAfterFiveSeconds()
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
                new AnchoredRuntimeTimeProvider(now.AddSeconds(6))),
            log: log);
        var session = CreateIntensifyAttachStatusSession(
            IntensifyAttachStatusSpellId,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute] = "true",
                [WorldPacketHandlers.RuntimeLegacyIntensifyStampAttribute] =
                    now.AddSeconds(-6).ToLocalTime().DateTime.ToString("O", CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyIntensifyStampUtcAttribute] =
                    now.AddSeconds(-6).ToString("O", CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyIntensifyDamageAttribute] =
                    IntensifyAttachStatusDamage.ToString(CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyFocusClientSpellIdAttribute] =
                    IntensifyAttachStatusSpellId.ToString(CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyFocusClientSpellLevelAttribute] =
                    IntensifyAttachStatusLearnedLevel.ToString(CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyFocusClientSpellUseSpellSoulAttribute] = "0",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return HasLegacyStatusFlag(current.Character.Attributes, FocusedStatusFlag) &&
                        current.Character.Attributes.TryGetValue(
                            WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute,
                            out var inUse) &&
                        string.Equals(inUse, "false", StringComparison.OrdinalIgnoreCase);
                },
                TimeSpan.FromSeconds(1));
            var afterPromotion = DateTimeOffset.UtcNow.AddSeconds(6);

            var promotedSession = AssertHandlerSession(handler, connection);
            Assert.True(HasLegacyStatusFlag(promotedSession.Character.Attributes, FocusedStatusFlag));
            Assert.Equal("false", promotedSession.Character.Attributes[WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute]);
            Assert.Equal("true", promotedSession.Character.Attributes["LegacyFocusedRemoveOnDead"]);
            AssertLegacyXpSkillExpiresAround(
                promotedSession.Character.Attributes,
                WorldPacketHandlers.RuntimeLegacyFocusedExpiresAtUtcAttribute,
                now.AddSeconds(6),
                afterPromotion,
                expectedDurationSeconds: 60);

            var animation = Assert.Single(ParseSpellAnimations(sent));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(PlayerUid, animation.AttackedUid);
            Assert.Equal(0, animation.X);
            Assert.Equal(0, animation.Y);
            Assert.Equal(IntensifyAttachStatusSpellId, animation.SpellId);
            Assert.Equal(IntensifyAttachStatusLearnedLevel, animation.SpellLevel);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);

            Assert.Contains(ParseMsgUpdates(sent), update =>
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, FocusedStatusFlag)));
            Assert.Contains("WorldServer processed legacy Intensify focus runtime updates", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerMsgMovementCancelsArmedIntensifyFocusBeforePromotion()
    {
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateIntensifyAttachStatusSession(
            IntensifyAttachStatusSpellId,
            CreateArmedIntensifyFocusAttributes(IntensifyAttachStatusSpellId));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgMovementPacket
            {
                Direction = 6,
                Uid = 0,
                Running = MsgMovementMode.Walk,
                Timestamp = 123,
            }.ToFrameBytes()));

        var updatedSession = AssertStoredSession(sessions, connection);
        Assert.Equal(
            "false",
            updatedSession.Character.Attributes[WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, FocusedStatusFlag));

        var cancelAction = Assert.Single(
            ParseMsgActions(sent),
            action => action.Type == MsgActionType.CancelFocusedSpell);
        Assert.Equal(PlayerUid, cancelAction.ObjId);
        Assert.Equal(IntensifyAttachStatusSpellId, cancelAction.TargetUid);
        Assert.Contains("WorldServer sent MsgMovement focused cancel", log.ToString());
    }

    [Fact]
    public async Task HandlerJumpCancelsArmedIntensifyFocusBeforePromotion()
    {
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateIntensifyAttachStatusSession(
            IntensifyAttachStatusSpellId,
            CreateArmedIntensifyFocusAttributes(IntensifyAttachStatusSpellId));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgActionPacket
            {
                ObjId = PlayerUid,
                TargetPositionX = 831,
                TargetPositionY = 584,
                Timestamp = 456,
                Type = MsgActionType.Jump,
                Facing = 6,
            }.ToFrameBytes()));

        var updatedSession = AssertStoredSession(sessions, connection);
        Assert.Equal(
            "false",
            updatedSession.Character.Attributes[WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, FocusedStatusFlag));

        var cancelAction = Assert.Single(
            ParseMsgActions(sent),
            action => action.Type == MsgActionType.CancelFocusedSpell);
        Assert.Equal(PlayerUid, cancelAction.ObjId);
        Assert.Equal(IntensifyAttachStatusSpellId, cancelAction.TargetUid);
        Assert.Contains("WorldServer sent MsgMovement focused cancel", log.ToString());
    }

    [Fact]
    public async Task HandlerMsgAttackCancelsArmedIntensifyFocusBeforePromotion()
    {
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateIntensifyAttachStatusSession(
            IntensifyAttachStatusSpellId,
            CreateArmedIntensifyFocusAttributes(IntensifyAttachStatusSpellId));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 831,
                Y: 584,
                AttackType: MsgAttackType.Physical,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: 0)).ToFrameBytes()));

        var updatedSession = AssertStoredSession(sessions, connection);
        Assert.Equal(
            "false",
            updatedSession.Character.Attributes[WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, FocusedStatusFlag));

        var cancelAction = Assert.Single(
            ParseMsgActions(sent),
            action => action.Type == MsgActionType.CancelFocusedSpell);
        Assert.Equal(PlayerUid, cancelAction.ObjId);
        Assert.Equal(IntensifyAttachStatusSpellId, cancelAction.TargetUid);
        Assert.Contains("WorldServer sent MsgAttack focused cancel", log.ToString());
    }

    [Fact]
    public async Task HandlerMsgAttackConsumesFocusedAndClearsDoFocusForOneAttackAttempt()
    {
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateIntensifyAttachStatusSession(
            IntensifyAttachStatusSpellId,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(FocusedStatusFlag),
                [WorldPacketHandlers.RuntimeLegacyIntensifyDamageAttribute] =
                    IntensifyAttachStatusDamage.ToString(CultureInfo.InvariantCulture),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 831,
                Y: 584,
                AttackType: MsgAttackType.Physical,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: 0)).ToFrameBytes()));

        var updatedSession = AssertStoredSession(sessions, connection);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, FocusedStatusFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("doFocus"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("DoFocus"));
        Assert.Contains("WorldServer consumed legacy Focused attack", log.ToString());
    }

    private static MsgAttackQuery CreateIntensifyAttachStatusQuery(ushort spellId)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: PlayerUid,
            X: 830,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static WorldDataCatalog CreateIntensifyAttachStatusCatalog(ushort spellId)
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                spellId,
                sort: 6,
                level: 0,
                experience: 1_000,
                duration: IntensifyAttachStatusDuration,
                coldTime: 0),
            CreateMagicRow(
                spellId,
                sort: 6,
                level: IntensifyAttachStatusLearnedLevel,
                useMana: 0,
                damage: IntensifyAttachStatusDamage,
                experience: 1_000,
                useStamina: 0,
                duration: IntensifyAttachStatusDuration,
                coldTime: 0),
            CreateMagicRow(
                spellId,
                sort: 6,
                level: IntensifyAttachStatusLearnedLevel + 1,
                useMana: 0,
                damage: IntensifyAttachStatusDamage,
                experience: 2_000,
                useStamina: 0,
                duration: IntensifyAttachStatusDuration,
                coldTime: 0));
    }

    private static WorldSession CreateIntensifyAttachStatusSession(
        ushort spellId,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return CreateSession(
            spellEntries:
            [
                CreatePlayerSpell(spellId, IntensifyAttachStatusLearnedLevel),
            ],
            attributes: attributes);
    }

    private static IReadOnlyDictionary<string, string> CreateArmedIntensifyFocusAttributes(ushort spellId)
    {
        var now = DateTimeOffset.UtcNow;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute] = "true",
            [WorldPacketHandlers.RuntimeLegacyIntensifyStampAttribute] =
                now.ToLocalTime().DateTime.ToString("O", CultureInfo.InvariantCulture),
            [WorldPacketHandlers.RuntimeLegacyIntensifyStampUtcAttribute] =
                now.ToString("O", CultureInfo.InvariantCulture),
            [WorldPacketHandlers.RuntimeLegacyIntensifyDamageAttribute] =
                IntensifyAttachStatusDamage.ToString(CultureInfo.InvariantCulture),
            [WorldPacketHandlers.RuntimeLegacyFocusClientSpellIdAttribute] =
                spellId.ToString(CultureInfo.InvariantCulture),
            [WorldPacketHandlers.RuntimeLegacyFocusClientSpellLevelAttribute] =
                IntensifyAttachStatusLearnedLevel.ToString(CultureInfo.InvariantCulture),
            [WorldPacketHandlers.RuntimeLegacyFocusClientSpellUseSpellSoulAttribute] = "0",
        };
    }

    private static WorldSession AssertStoredSession(WorldSessionStore sessions, ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static void AssertIntensifyFocusState(
        WorldSession session,
        ushort spellId,
        DateTime beforeCast,
        DateTime afterCast)
    {
        AssertTruthyAttribute(session.Character.Attributes, "InUseIntensify");
        Assert.Equal(
            IntensifyAttachStatusDamage.ToString(CultureInfo.InvariantCulture),
            session.Character.Attributes["IntensifyDamage"]);
        Assert.Equal(
            spellId.ToString(CultureInfo.InvariantCulture),
            session.Character.Attributes["FocusClientSpellId"]);
        Assert.Equal(
            IntensifyAttachStatusLearnedLevel.ToString(CultureInfo.InvariantCulture),
            session.Character.Attributes["FocusClientSpellLevel"]);
        AssertDateTimeAttributeAround(session.Character.Attributes, "IntensifyStamp", beforeCast, afterCast);
    }

    private static void AssertIntensifyInteraction(MsgAttackPacket packet, ushort spellId)
    {
        Assert.Equal(PlayerUid, packet.Query.Uid);
        Assert.Equal(PlayerUid, packet.Query.OpponentUid);
        Assert.Equal(830, packet.Query.X);
        Assert.Equal(584, packet.Query.Y);
        Assert.Equal(MsgAttackType.Magic, packet.Query.AttackType);
        Assert.Equal(0, packet.Query.SpellId);
        Assert.Equal(spellId, packet.Query.KilledMonster);
        Assert.Equal(IntensifyAttachStatusLearnedLevel, packet.Query.SpellLevel);
    }

    private static void AssertTruthyAttribute(IReadOnlyDictionary<string, string> attributes, string key)
    {
        Assert.True(attributes.TryGetValue(key, out var rawValue));
        Assert.True(
            string.Equals(rawValue, "1", StringComparison.Ordinal) ||
                (bool.TryParse(rawValue, out var value) && value),
            $"Expected {key} to be truthy, but was '{rawValue}'.");
    }

    private static void AssertDateTimeAttributeAround(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        DateTime beforeCast,
        DateTime afterCast)
    {
        Assert.True(attributes.TryGetValue(key, out var rawValue));
        Assert.True(
            DateTime.TryParse(
                rawValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var stamp),
            $"Expected {key} to parse as a DateTime, but was '{rawValue}'.");
        Assert.InRange(stamp, beforeCast.AddSeconds(-1), afterCast.AddSeconds(1));
    }

    private static IReadOnlyList<MsgAttackPacket> ParseIntensifyAttachStatusInteractions(
        IEnumerable<byte[]> frames,
        ushort spellId)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == 0 &&
                packet.Query.KilledMonster == spellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
