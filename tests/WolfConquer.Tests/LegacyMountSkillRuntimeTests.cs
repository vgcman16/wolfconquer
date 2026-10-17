using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort MountControlRidingSpellId = 7_001;
    private const ushort MountControlSpookSpellId = 7_002;
    private const ushort MountControlWarCrySpellId = 7_003;
    private const int MountControlRideFlag = 0x32;
    private const int MountControlRideDurationSeconds = 2_592_000;
    private const uint MountControlSecondTargetUid = 1_293_602;
    private const uint MountControlThirdTargetUid = 1_293_603;
    private const uint MountControlFarTargetUid = 1_293_604;

    [Fact]
    public async Task MountControlRiding7001SpendsResourcesTogglesRideRestoresVigorAndDoesNotProgress()
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [MountControlRidingSpellId],
            attributes: new Dictionary<string, string>
            {
                ["MaxVigor"] = "2000",
            },
            playerItems: CreateMountControlItems(plus: 6, plusProgress: 1_000));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var query = CreateMountControlQuery(MountControlRidingSpellId);
        var beforeCast = DateTimeOffset.UtcNow;

        Assert.True(await InvokeMountControlHandlerAsync(context, session, query));

        var afterCast = DateTimeOffset.UtcNow;
        var mountedSession = AssertMountControlSession(sessions, connection);
        Assert.Equal("80", mountedSession.Character.Attributes["Stamina"]);
        Assert.Equal("2000", mountedSession.Character.Attributes["Vigor"]);
        Assert.True(HasLegacyStatusFlag(mountedSession.Character.Attributes, MountControlRideFlag));
        Assert.Equal("true", mountedSession.Character.Attributes[WorldPacketHandlers.RuntimeLegacyRideRemoveOnDeadAttribute]);
        Assert.Equal("1", mountedSession.Character.Attributes[WorldPacketHandlers.RuntimeLegacyRideStampSecondsAttribute]);
        AssertLegacyXpSkillExpiresAround(
            mountedSession.Character.Attributes,
            WorldPacketHandlers.RuntimeLegacyRideExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            MountControlRideDurationSeconds);

        var firstCastFrames = sent.ToArray();
        Assert.Contains(ParseMsgUpdates(firstCastFrames), update =>
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 80)]));
        Assert.Contains(ParseMsgUpdates(firstCastFrames), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, MountControlRideFlag)));
        Assert.Equal(2_000U, Assert.Single(ParseMountControlServerInfoPackets(firstCastFrames)).Vigor);
        AssertMountControlSelfAnimation(
            Assert.Single(ParseSpellAnimations(firstCastFrames)),
            MountControlRidingSpellId,
            query.X,
            query.Y);
        AssertMountControlNoSpellProgress(firstCastFrames);

        var secondCastStart = sent.Count;
        Assert.True(await InvokeMountControlHandlerAsync(
            context with { Session = mountedSession },
            mountedSession,
            query));

        var dismountedSession = AssertMountControlSession(sessions, connection);
        Assert.Equal("60", dismountedSession.Character.Attributes["Stamina"]);
        Assert.Equal("2000", dismountedSession.Character.Attributes["Vigor"]);
        Assert.False(HasLegacyStatusFlag(dismountedSession.Character.Attributes, MountControlRideFlag));
        Assert.False(dismountedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyRideExpiresAtUtcAttribute));
        Assert.False(dismountedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyRideRemoveOnDeadAttribute));
        Assert.False(dismountedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyRideStampSecondsAttribute));

        var secondCastFrames = sent.Skip(secondCastStart).ToArray();
        Assert.Contains(ParseMsgUpdates(secondCastFrames), update =>
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 60)]));
        Assert.Contains(ParseMsgUpdates(secondCastFrames), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, MountControlRideFlag)));
        Assert.Empty(ParseMountControlServerInfoPackets(secondCastFrames));
        AssertMountControlSelfAnimation(
            Assert.Single(ParseSpellAnimations(secondCastFrames)),
            MountControlRidingSpellId,
            query.X,
            query.Y);
        AssertMountControlNoSpellProgress(secondCastFrames);
        Assert.Equal(0, Assert.Single(dismountedSession.PlayerSpells.Spells).Experience);

        var output = log.ToString();
        Assert.Contains("source=Riding.cs", output);
        Assert.Contains("toggledOn=True", output);
        Assert.Contains("toggledOn=False", output);
    }

    [Fact]
    public async Task MountControlRiding7001BlockedMapStillSpendsStaminaAndOnlySendsTopLeftMessage()
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [MountControlRidingSpellId],
            mapId: 1_004,
            playerItems: CreateMountControlItems(plus: 6, plusProgress: 1_000));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        Assert.True(await InvokeMountControlHandlerAsync(
            context,
            session,
            CreateMountControlQuery(MountControlRidingSpellId)));

        var updatedSession = AssertMountControlSession(sessions, connection);
        Assert.Equal("80", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, MountControlRideFlag));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseMountControlServerInfoPackets(sent));
        AssertMountControlNoSpellProgress(sent);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));

        var message = Assert.Single(ParseLegacyMessages(sent));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, message.ChatType);
        Assert.Contains("You can't use this skill on this map.", message.Content);
        Assert.Contains("resourcesSpentBeforeGate=True", log.ToString());
    }

    [Theory]
    [InlineData("StatusFlags", "134217728")]
    [InlineData("TransformationID", "1")]
    [InlineData("LegacySteedRaceInSteedRace", "true")]
    public async Task MountControlRiding7001StateAndSteedRaceGatesSpendStaminaBeforeBlocking(
        string attribute,
        string value)
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var attributes = new Dictionary<string, string>
        {
            [attribute] = value,
        };
        var session = CreateSession(
            stamina: 100,
            spellIds: [MountControlRidingSpellId],
            attributes: attributes,
            playerItems: CreateMountControlItems(plus: 6, plusProgress: 1_000));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        Assert.True(await InvokeMountControlHandlerAsync(
            context,
            session,
            CreateMountControlQuery(MountControlRidingSpellId)));

        var updatedSession = AssertMountControlSession(sessions, connection);
        Assert.Equal("80", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, MountControlRideFlag));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseMountControlServerInfoPackets(sent));
        var message = Assert.Single(ParseLegacyMessages(sent));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, message.ChatType);
        Assert.Contains("You can`t use this skill right now !", message.Content);
        Assert.Contains("resourcesSpentBeforeGate=True", log.ToString());
    }

    [Fact]
    public async Task MountControlRiding7001TournamentGateSpendsStaminaBeforeBlocking()
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [MountControlRidingSpellId],
            attributes: new Dictionary<string, string>
            {
                ["LegacyTournamentInTournament"] = "true",
            },
            playerItems: CreateMountControlItems(plus: 6, plusProgress: 1_000));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        Assert.True(await InvokeMountControlHandlerAsync(
            context,
            session,
            CreateMountControlQuery(MountControlRidingSpellId)));

        var updatedSession = AssertMountControlSession(sessions, connection);
        Assert.Equal("80", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, MountControlRideFlag));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseMountControlServerInfoPackets(sent));
        var message = Assert.Single(ParseLegacyMessages(sent));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, message.ChatType);
        Assert.Contains("You can't use this skill on this map.", message.Content);
        Assert.Contains("resourcesSpentBeforeGate=True", log.ToString());
    }

    [Fact]
    public async Task MountControlRiding7001FencesVigorUpdateWithoutLocalMaximum()
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [MountControlRidingSpellId],
            playerItems: CreateMountControlItems(plus: 6, plusProgress: 1_000));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        Assert.True(await InvokeMountControlHandlerAsync(
            context,
            session,
            CreateMountControlQuery(MountControlRidingSpellId)));

        var updatedSession = AssertMountControlSession(sessions, connection);
        Assert.Equal("80", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, MountControlRideFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("Vigor"));
        Assert.Empty(ParseMountControlServerInfoPackets(sent));
        AssertMountControlSelfAnimation(
            Assert.Single(ParseSpellAnimations(sent)),
            MountControlRidingSpellId,
            832,
            588);
        Assert.Contains("restoredVigor=unchanged", log.ToString());
    }

    [Fact]
    public async Task MountControlSpook7002UsesVisibleTargetAndPerfectionFallbackBeyondDbRange()
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellIds: [MountControlSpookSpellId],
            playerItems: CreateMountControlItems(
                plus: 3,
                plusProgress: 10,
                perfectionLevel: 10,
                perfectionProgress: 20));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "SpookTarget",
            x: 847,
            y: 584,
            stamina: 75,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(MountControlRideFlag),
            },
            playerItems: CreateMountControlItems(
                plus: 5,
                plusProgress: 999,
                perfectionLevel: 9,
                perfectionProgress: 999));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var query = CreateMountControlQuery(
            MountControlSpookSpellId,
            opponentUid: TargetPlayerUid,
            x: 900,
            y: 901);

        Assert.True(await InvokeMountControlHandlerAsync(context, casterSession, query));

        var updatedTarget = AssertMountControlSession(sessions, targetConnection);
        Assert.False(HasLegacyStatusFlag(updatedTarget.Character.Attributes, MountControlRideFlag));
        Assert.Equal("0", updatedTarget.Character.Attributes["Stamina"]);
        Assert.Contains(ParseMsgUpdates(casterSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, MountControlRideFlag)));
        Assert.DoesNotContain(
            ParseMsgUpdates(targetSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));

        var interaction = Assert.Single(ParseMountControlInteractions(casterSent, MountControlSpookSpellId));
        Assert.Equal(TargetPlayerUid, interaction.Query.OpponentUid);
        Assert.Equal(847, interaction.Query.X);
        Assert.Equal(584, interaction.Query.Y);
        Assert.Equal(MsgAttackType.Magic, interaction.Query.AttackType);

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(900, animation.X);
        Assert.Equal(901, animation.Y);
        Assert.Equal(MountControlSpookSpellId, animation.SpellId);
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, animationTarget.Uid);
        Assert.Equal(0U, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Equal(MsgAttackEffect.None, animationTarget.Effect);
        AssertMountControlNoSpellProgress(casterSent);
        Assert.Equal(0, Assert.Single(AssertMountControlSession(sessions, casterConnection).PlayerSpells.Spells).Experience);

        var output = log.ToString();
        Assert.Contains("source=Spook.cs", output);
        Assert.Contains("spellRange=10", output);
        Assert.Contains("dismountedTarget=True", output);
    }

    [Fact]
    public async Task MountControlSpook7002LosingComparisonKeepsRideAndSendsEmptyAnimation()
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            spellIds: [MountControlSpookSpellId],
            playerItems: CreateMountControlItems(plus: 4, plusProgress: 100, perfectionLevel: 1, perfectionProgress: 1));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "StrongerMount",
            x: 840,
            y: 584,
            stamina: 70,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(MountControlRideFlag),
            },
            playerItems: CreateMountControlItems(plus: 5, plusProgress: 1, perfectionLevel: 2, perfectionProgress: 2));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        Assert.True(await InvokeMountControlHandlerAsync(
            context,
            casterSession,
            CreateMountControlQuery(MountControlSpookSpellId, TargetPlayerUid)));

        var unchangedTarget = AssertMountControlSession(sessions, targetConnection);
        Assert.True(HasLegacyStatusFlag(unchangedTarget.Character.Attributes, MountControlRideFlag));
        Assert.Equal("70", unchangedTarget.Character.Attributes["Stamina"]);
        Assert.Empty(ParseMountControlInteractions(casterSent, MountControlSpookSpellId));
        Assert.Empty(ParseMsgUpdates(targetSent));
        Assert.Empty(Assert.Single(ParseSpellAnimations(casterSent)).Targets);
        AssertMountControlNoSpellProgress(casterSent);
        Assert.Contains("dismountedTarget=False", log.ToString());
    }

    [Fact]
    public async Task MountControlSpook7002VisibleUnmountedTargetReturnsWithoutAnimation()
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            spellIds: [MountControlSpookSpellId],
            playerItems: CreateMountControlItems(plus: 12, plusProgress: 999));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "UnmountedTarget",
            x: 840,
            y: 584,
            stamina: 70,
            spellIds: [],
            playerItems: CreateMountControlItems(plus: 1, plusProgress: 0));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        Assert.True(await InvokeMountControlHandlerAsync(
            context,
            casterSession,
            CreateMountControlQuery(MountControlSpookSpellId, TargetPlayerUid)));

        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseMountControlInteractions(casterSent, MountControlSpookSpellId));
        Assert.Empty(ParseMsgUpdates(targetSent));
        Assert.Equal("70", AssertMountControlSession(sessions, targetConnection).Character.Attributes["Stamina"]);
        AssertMountControlNoSpellProgress(casterSent);
        Assert.Contains("returnedBeforeAnimation=True", log.ToString());
    }

    [Fact]
    public async Task MountControlWarCry7003UsesAllVisibleMountedPlayersAndOnlyPlusProgressComparison()
    {
        var worldData = CreateMountControlCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var firstTargetSent = new List<byte[]>();
        var secondTargetSent = new List<byte[]>();
        var thirdTargetSent = new List<byte[]>();
        var farTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var firstTargetConnection = CreateSendingConnection(firstTargetSent);
        var secondTargetConnection = CreateSendingConnection(secondTargetSent);
        var thirdTargetConnection = CreateSendingConnection(thirdTargetSent);
        var farTargetConnection = CreateSendingConnection(farTargetSent);
        var casterSession = CreateSession(
            spellIds: [MountControlWarCrySpellId],
            playerItems: CreateMountControlItems(
                plus: 5,
                plusProgress: 100,
                perfectionLevel: 100,
                perfectionProgress: 100));
        var firstTargetSession = CreateMountControlTargetSession(
            TargetPlayerUid,
            "WarCryMissingSteedUsesZeroStats",
            x: 815,
            y: 584,
            stamina: 81,
            plus: 4,
            plusProgress: 999,
            perfectionLevel: 200,
            perfectionProgress: 200) with
        {
            PlayerItems = new PlayerItemSnapshot([], [], [], [], []),
        };
        var secondTargetSession = CreateMountControlTargetSession(
            MountControlSecondTargetUid,
            "WarCryLowerProgress",
            x: 830,
            y: 600,
            stamina: 82,
            plus: 5,
            plusProgress: 99);
        var thirdTargetSession = CreateMountControlTargetSession(
            MountControlThirdTargetUid,
            "WarCryPerfectionIgnored",
            x: 845,
            y: 584,
            stamina: 83,
            plus: 5,
            plusProgress: 101,
            perfectionLevel: 0,
            perfectionProgress: 0);
        var farTargetSession = CreateMountControlTargetSession(
            MountControlFarTargetUid,
            "WarCryOutsideView",
            x: 849,
            y: 584,
            stamina: 84,
            plus: 1,
            plusProgress: 0);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(firstTargetConnection, firstTargetSession);
        sessions.Set(secondTargetConnection, secondTargetSession);
        sessions.Set(thirdTargetConnection, thirdTargetSession);
        sessions.Set(farTargetConnection, farTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var query = CreateMountControlQuery(
            MountControlWarCrySpellId,
            opponentUid: 0,
            x: 840,
            y: 584);

        Assert.True(await InvokeMountControlHandlerAsync(context, casterSession, query));

        AssertMountControlDismounted(sessions, firstTargetConnection, expectedStamina: "0");
        AssertMountControlDismounted(sessions, secondTargetConnection, expectedStamina: "0");
        AssertMountControlStillMounted(sessions, thirdTargetConnection, expectedStamina: "83");
        AssertMountControlStillMounted(sessions, farTargetConnection, expectedStamina: "84");

        var interactions = ParseMountControlInteractions(casterSent, MountControlWarCrySpellId);
        Assert.Equal(2, interactions.Count);
        Assert.Equal(
            new[] { TargetPlayerUid, MountControlSecondTargetUid },
            interactions.Select(packet => packet.Query.OpponentUid).Order().ToArray());
        Assert.Contains(interactions, packet =>
            packet.Query.OpponentUid == TargetPlayerUid &&
            packet.Query.X == 815 &&
            packet.Query.Y == 584);
        Assert.Contains(interactions, packet =>
            packet.Query.OpponentUid == MountControlSecondTargetUid &&
            packet.Query.X == 830 &&
            packet.Query.Y == 600);

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(840, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(MountControlWarCrySpellId, animation.SpellId);
        Assert.Equal(
            new[] { TargetPlayerUid, MountControlSecondTargetUid },
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.All(animation.Targets, target =>
        {
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });
        Assert.Empty(ParseSpellAnimations(farTargetSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(firstTargetSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
        Assert.DoesNotContain(
            ParseMsgUpdates(secondTargetSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
        AssertMountControlNoSpellProgress(casterSent);
        Assert.Equal(0, Assert.Single(AssertMountControlSession(sessions, casterConnection).PlayerSpells.Spells).Experience);

        var output = log.ToString();
        Assert.Contains("source=WarCry.cs", output);
        Assert.Contains("mountedVisibleTargets=3", output);
        Assert.Contains("dismountedTargets=2", output);
        Assert.Contains("constructedSectorUnused=True", output);
    }

    private static async ValueTask<bool> InvokeMountControlHandlerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        Assert.NotNull(context.WorldData);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            context.WorldData,
            query.SpellId,
            out var playerSpell,
            out var spell));
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerMountControlMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = method.Invoke(
            null,
            [context with { Session = session }, query, playerSpell, spell, session, CancellationToken.None]);
        var pending = Assert.IsType<ValueTask<bool>>(invocation);
        return await pending;
    }

    private static WorldDataCatalog CreateMountControlCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            MagicTypeParser.ParseLine(
                "10301@@7001@@32@@Riding@@0@@0@@0@@2@@0@@0@@1@@0@@100@@1000000@@0@@0@@47@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@20@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@"),
            MagicTypeParser.ParseLine(
                "10302@@7002@@30@@Spook@@0@@0@@0@@0@@0@@0@@0@@0@@100@@0@@10@@10@@-1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@"),
            MagicTypeParser.ParseLine(
                "10303@@7003@@31@@WarCry@@0@@0@@0@@4@@0@@0@@0@@0@@100@@0@@10@@10@@-1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@"));
    }

    private static MsgAttackQuery CreateMountControlQuery(
        ushort spellId,
        uint opponentUid = 0,
        ushort x = 832,
        ushort y = 588)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static PlayerItemSnapshot CreateMountControlItems(
        byte plus,
        uint plusProgress,
        uint perfectionLevel = 0,
        uint perfectionProgress = 0)
    {
        var equipment = new List<PlayerItemSnapshotItem>
        {
            new()
            {
                Uid = 700_100,
                ItemId = 300_000,
                Durability = 1,
                MaximumDurability = 1,
                Position = 12,
                Plus = plus,
                PlusProgress = plusProgress,
                PerfectionLevel = perfectionLevel,
                PerfectionProgress = perfectionProgress,
            },
        };
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: equipment,
            RuneItems: []);
    }

    private static WorldSession CreateMountControlTargetSession(
        uint uid,
        string name,
        long x,
        long y,
        ulong stamina,
        byte plus,
        uint plusProgress,
        uint perfectionLevel = 0,
        uint perfectionProgress = 0)
    {
        return CreateSession(
            uid: uid,
            name: name,
            x: x,
            y: y,
            stamina: stamina,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(MountControlRideFlag),
            },
            playerItems: CreateMountControlItems(
                plus,
                plusProgress,
                perfectionLevel,
                perfectionProgress));
    }

    private static WorldSession AssertMountControlSession(
        WorldSessionStore sessions,
        WolfConquer.Core.Networking.ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static void AssertMountControlDismounted(
        WorldSessionStore sessions,
        WolfConquer.Core.Networking.ConquerTcpConnection connection,
        string expectedStamina)
    {
        var session = AssertMountControlSession(sessions, connection);
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, MountControlRideFlag));
        Assert.Equal(expectedStamina, session.Character.Attributes["Stamina"]);
    }

    private static void AssertMountControlStillMounted(
        WorldSessionStore sessions,
        WolfConquer.Core.Networking.ConquerTcpConnection connection,
        string expectedStamina)
    {
        var session = AssertMountControlSession(sessions, connection);
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, MountControlRideFlag));
        Assert.Equal(expectedStamina, session.Character.Attributes["Stamina"]);
    }

    private static void AssertMountControlSelfAnimation(
        LegacySpellAnimationPacket animation,
        ushort spellId,
        ushort x,
        ushort y)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(x, animation.X);
        Assert.Equal(y, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
    }

    private static void AssertMountControlNoSpellProgress(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            frames,
            frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));
    }

    private static IReadOnlyList<LegacyServerInfoPacket> ParseMountControlServerInfoPackets(
        IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyServerInfoPacket>();
        foreach (var frame in frames)
        {
            if (LegacyServerInfoPacket.TryParse(ReadFrame(frame), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgAttackPacket> ParseMountControlInteractions(
        IEnumerable<byte[]> frames,
        ushort spellId)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var frame in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.Query.SpellId == spellId)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
