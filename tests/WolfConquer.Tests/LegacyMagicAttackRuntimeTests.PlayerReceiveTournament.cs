using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int PlayerReceiveTournamentFreezeFlag = 0x3B;
    private const int PlayerReceiveTournamentDragonSwingFlag = 0xA0;
    private const uint PlayerReceiveFreezeWarSeconds = 2_592_000;
    private const uint PlayerReceiveDragonSwingSeconds = 2_592_000;

    [Fact]
    public async Task PlayerReceiveTournamentSkillTournamentWithRemainingLivesDecrementsLifeSendsCenterAndSkipsOrdinaryDamage()
    {
        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            CreatePlayerReceiveTournamentCasterAttributes("SkillTournament"),
            CreateSkillTournamentTargetAttributes(lifes: 2));

        AssertSkillTournamentLifes(result.TargetSession, expected: 1);
        Assert.Equal(5_000, result.TargetSession.Character.MinHitPoints);

        Assert.Contains(ParseLegacyMessages(result.TargetSent), message =>
            message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            message.Content.Count > 3 &&
            message.Content[3] == "You have 1 more life's left.");
        Assert.DoesNotContain(ParseDeathAttacks(result.TargetSent), attack =>
            attack.Query.OpponentUid == TargetPlayerUid);
    }

    [Fact]
    public async Task PlayerReceiveTournamentSkillTournamentWithNoLivesIncrementsKillsSendsAccessionAndRunsDeath()
    {
        var casterAttributes = CreatePlayerReceiveTournamentCasterAttributes("SkillTournament");
        casterAttributes["TournamentKills"] = "3";
        casterAttributes["LegacyTournamentKills"] = "3";

        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            casterAttributes,
            CreateSkillTournamentTargetAttributes(lifes: 0));

        AssertTournamentKills(result.CasterSession, expected: 4);
        Assert.Equal(0, result.TargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));

        Assert.Contains(ParseLegacyStrings(result.TargetSent), packet =>
            packet.Uid == TargetPlayerUid &&
            packet.Id == LegacyStringId.Effect &&
            packet.Strings.SequenceEqual(["accession1"]));

        var death = Assert.Single(ParseDeathAttacks(result.TargetSent));
        Assert.Equal(PlayerUid, death.Query.Uid);
        Assert.Equal(TargetPlayerUid, death.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Death, death.Query.AttackType);
    }

    [Fact]
    public async Task PlayerReceiveTournamentFreezeWarDifferentGarmentsAppliesFreezeAndSkipsOrdinaryDamage()
    {
        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            CreatePlayerReceiveTournamentCasterAttributes("FreezeWar", garmentId: 181_325),
            CreateFreezeWarTargetAttributes(garmentId: 182_525));

        Assert.Equal(5_000, result.TargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, PlayerReceiveTournamentFreezeFlag));

        Assert.True(result.TargetSession.Character.Attributes.TryGetValue(
            "LegacyFreezeWarExpiresAtUtc",
            out var rawExpiresAt));
        Assert.True(DateTimeOffset.TryParse(
            rawExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt));
        var remainingSeconds = (expiresAt - DateTimeOffset.UtcNow).TotalSeconds;
        Assert.InRange(
            remainingSeconds,
            PlayerReceiveFreezeWarSeconds - 5,
            PlayerReceiveFreezeWarSeconds + 5);

        Assert.Contains(ParseMsgUpdates(result.TargetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
    }

    [Fact]
    public async Task PlayerReceiveTournamentFreezeWarSameGarmentsRemovesExistingFreezeAndSkipsOrdinaryDamage()
    {
        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            CreatePlayerReceiveTournamentCasterAttributes("FreezeWar", garmentId: 181_325),
            CreateFreezeWarTargetAttributes(
                garmentId: 181_325,
                statusFlags: BuildStatusFlags(PlayerReceiveTournamentFreezeFlag)));

        Assert.Equal(5_000, result.TargetSession.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, PlayerReceiveTournamentFreezeFlag));
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarNoSwingSkipsOrdinaryDamage()
    {
        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            CreatePlayerReceiveTournamentCasterAttributes("DragonWar"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(5_000, result.TargetSession.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, PlayerReceiveTournamentDragonSwingFlag));
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, PlayerReceiveTournamentDragonSwingFlag));
        Assert.DoesNotContain(ParseDeathAttacks(result.TargetSent), attack =>
            attack.Query.OpponentUid == TargetPlayerUid);
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarTargetSwingWithHitsDecrementsHitsAddsScoreAndSkipsDamage()
    {
        var casterAttributes = CreatePlayerReceiveTournamentCasterAttributes("DragonWar");
        casterAttributes["LegacyDragonWarScore"] = "7";

        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            casterAttributes,
            CreateDragonWarTargetAttributes(hits: 3, dragonSwing: true));

        Assert.Equal(5_000, result.TargetSession.Character.MinHitPoints);
        AssertDragonWarScore(result.CasterSession, expected: 12);
        AssertDragonWarHits(result.TargetSession, expected: 2);
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, PlayerReceiveTournamentDragonSwingFlag));
        Assert.DoesNotContain(ParseDeathAttacks(result.TargetSent), attack =>
            attack.Query.OpponentUid == TargetPlayerUid);
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarTargetSwingPreservesExistingLegacyKeys()
    {
        var casterAttributes = CreatePlayerReceiveTournamentCasterAttributes("DragonWar");
        casterAttributes["LegacyDragonWarScore"] = "11";
        var targetAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyDragonWarHits"] = "4",
            ["StatusFlags"] = BuildStatusFlags(PlayerReceiveTournamentDragonSwingFlag),
        };

        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            casterAttributes,
            targetAttributes);

        Assert.Equal(16UL, Assert.Single(ReadUInt64AttributeValues(
            result.CasterSession.Character.Attributes,
            "LegacyDragonWarScore")));
        Assert.False(result.CasterSession.Character.Attributes.ContainsKey("DragonWarScore"));
        Assert.Equal(3UL, Assert.Single(ReadUInt64AttributeValues(
            result.TargetSession.Character.Attributes,
            "LegacyDragonWarHits")));
        Assert.False(result.TargetSession.Character.Attributes.ContainsKey("DragonWarHits"));
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarTargetSwingWithNoHitsKillsTransfersSwingAndSetsAttackerHits()
    {
        var casterAttributes = CreatePlayerReceiveTournamentCasterAttributes("DragonWar");
        casterAttributes["LegacyDragonWarScore"] = "1";
        casterAttributes["LegacyDragonWarHits"] = "2";

        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            casterAttributes,
            CreateDragonWarTargetAttributes(hits: 0, dragonSwing: true));

        Assert.Equal(0, result.TargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, PlayerReceiveTournamentDragonSwingFlag));
        AssertDragonWarScore(result.CasterSession, expected: 21);
        AssertDragonWarHits(result.CasterSession, expected: 5);
        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, PlayerReceiveTournamentDragonSwingFlag));

        Assert.True(result.CasterSession.Character.Attributes.TryGetValue(
            "LegacyDragonSwingExpiresAtUtc",
            out var rawExpiresAt));
        Assert.True(DateTimeOffset.TryParse(
            rawExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt));
        var remainingSeconds = (expiresAt - DateTimeOffset.UtcNow).TotalSeconds;
        Assert.InRange(
            remainingSeconds,
            PlayerReceiveDragonSwingSeconds - 5,
            PlayerReceiveDragonSwingSeconds + 5);

        var death = Assert.Single(ParseDeathAttacks(result.TargetSent));
        Assert.Equal(PlayerUid, death.Query.Uid);
        Assert.Equal(TargetPlayerUid, death.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Death, death.Query.AttackType);

        var updates = ParseMsgUpdates(result.CasterSent.Concat(result.TargetSent));
        AssertStatusFlagUpdate(updates, PlayerUid, PlayerReceiveTournamentDragonSwingFlag, expectedSet: true);
        AssertStatusFlagUpdate(updates, TargetPlayerUid, PlayerReceiveTournamentDragonSwingFlag, expectedSet: false);
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarTargetSwingWithNoHitsMarksKillSystemDeadForCasterUid()
    {
        var casterAttributes = CreatePlayerReceiveTournamentCasterAttributes("DragonWar");
        casterAttributes["LegacyDragonWarKillSystemUid"] = PlayerUid.ToString(CultureInfo.InvariantCulture);

        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            casterAttributes,
            CreateDragonWarTargetAttributes(hits: 0, dragonSwing: true));

        AssertDragonWarKillSystemDead(result.CasterSession, expected: true);
        Assert.Equal(0, result.CasterSession.Character.ConquerPoints);
        Assert.DoesNotContain(ParseLegacyStrings(result.CasterSent.Concat(result.TargetSent)), packet =>
            packet.Id == LegacyStringId.Sound);
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarAttackerSwingKillsTargetAndAddsScore()
    {
        var casterAttributes = CreatePlayerReceiveTournamentCasterAttributes("DragonWar");
        casterAttributes["StatusFlags"] = BuildStatusFlags(PlayerReceiveTournamentDragonSwingFlag);
        casterAttributes["DragonWarScore"] = "4";

        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            casterAttributes,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(0, result.TargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.TargetSession.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        AssertDragonWarScore(result.CasterSession, expected: 19);
        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, PlayerReceiveTournamentDragonSwingFlag));

        var death = Assert.Single(ParseDeathAttacks(result.TargetSent));
        Assert.Equal(PlayerUid, death.Query.Uid);
        Assert.Equal(TargetPlayerUid, death.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Death, death.Query.AttackType);
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarAttackerSwingUpdatesKillSystemFirstBloodRewardAndBroadcast()
    {
        var casterAttributes = CreatePlayerReceiveTournamentCasterAttributes("DragonWar");
        casterAttributes["StatusFlags"] = BuildStatusFlags(PlayerReceiveTournamentDragonSwingFlag);

        var result = await RunPlayerReceiveTournamentMagicAttackAsync(
            casterAttributes,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        AssertDragonWarKillSystemUid(result.CasterSession, PlayerUid);
        AssertDragonWarKillSystemFlag(result.CasterSession, "FirstBloodKill", expectedNumericValue: 1);
        Assert.Equal(10_000, result.CasterSession.Character.ConquerPoints);

        Assert.Contains(ParseLegacyStrings(result.CasterSent.Concat(result.TargetSent)), packet =>
            packet.Id == LegacyStringId.Sound &&
            packet.Strings.SequenceEqual(["sound/fc1.wav", "1"]));

        Assert.Contains(ParseLegacyMessages(result.CasterSent.Concat(result.TargetSent)), message =>
            message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            message.Content.Any(content =>
                content.Contains("[FirstBloodKill]", StringComparison.Ordinal) &&
                content.Contains("10000 ConquerPoints", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarAttackerSwingKillSystemRewardPersistsConquerPointsAndLogsSource()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-dragonwar-killsystem-cp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            WriteCharacterSnapshot(usersRoot, PlayerUid, conquerPoints: 0);
            var casterAttributes = CreatePlayerReceiveTournamentCasterAttributes("DragonWar");
            casterAttributes["StatusFlags"] = BuildStatusFlags(PlayerReceiveTournamentDragonSwingFlag);

            var result = await RunPlayerReceiveTournamentMagicAttackAsync(
                casterAttributes,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                usersRoot,
                persistScalarChanges: true);

            Assert.Equal(10_000, result.CasterSession.Character.ConquerPoints);
            var persisted = CharacterSnapshotParser.ParseText(
                File.ReadAllText(Path.Combine(usersRoot, PlayerUid.ToString(CultureInfo.InvariantCulture) + ".ini")));
            Assert.Equal(10_000, persisted.ConquerPoints);

            Assert.Contains("WorldServer applied legacy DragonWar KillerSystem reward", result.Output);
            Assert.Contains("source=KillerSystem.Update/Send.", result.Output);
            Assert.Contains("conquerPoints=10000", result.Output);
        }
        finally
        {
            if (Directory.Exists(usersRoot))
            {
                Directory.Delete(usersRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PlayerReceiveTournamentDragonWarDragonSwingRuntimeExpiryClearsFlagAndSendsStatusUpdate()
    {
        var now = DateTimeOffset.UtcNow;
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            CreateMagicCatalog(CreateMonsterFamily(), []),
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(1))),
            log);
        var session = CreateSession(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(PlayerReceiveTournamentDragonSwingFlag),
                ["LegacyDragonSwingExpiresAtUtc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute] = "30",
                ["DragonSwingChance"] = "30",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);
            await WaitUntilAsync(
                () =>
                    GetHandlerSessions(handler).TryGet(connection, out var updated) &&
                    updated is not null &&
                    !HasLegacyStatusFlag(updated.Character.Attributes, PlayerReceiveTournamentDragonSwingFlag),
                TimeSpan.FromSeconds(3));

            var updatedSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, PlayerReceiveTournamentDragonSwingFlag));
            Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyDragonSwingExpiresAtUtc"));
            Assert.False(updatedSession.Character.Attributes.ContainsKey(WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute));
            Assert.False(updatedSession.Character.Attributes.ContainsKey("DragonSwingChance"));
            AssertStatusFlagUpdate(
                ParseMsgUpdates(sent),
                PlayerUid,
                PlayerReceiveTournamentDragonSwingFlag,
                expectedSet: false);
            Assert.Contains("expired legacy DragonWar DragonSwing flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static async Task<PlayerReceiveTournamentHitResult> RunPlayerReceiveTournamentMagicAttackAsync(
        IReadOnlyDictionary<string, string> casterAttributes,
        IReadOnlyDictionary<string, string> targetAttributes,
        string? usersRoot = null,
        bool persistScalarChanges = false)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = persistScalarChanges
            ? new WorldSessionStore((connection, previous, current) =>
            {
                _ = connection;
                if (previous is not null &&
                    !string.IsNullOrWhiteSpace(current.UsersRoot) &&
                    CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(previous.Character, current.Character))
                {
                    Assert.True(
                        CharacterSnapshotScalarPersistence.TryPersist(current.UsersRoot, current.Character, out _, out var reason),
                        reason);
                }
            })
            : new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(attributes: casterAttributes);
        if (!string.IsNullOrWhiteSpace(usersRoot))
        {
            casterSession = casterSession with { UsersRoot = usersRoot };
        }

        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: targetAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        return new PlayerReceiveTournamentHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static Dictionary<string, string> CreatePlayerReceiveTournamentCasterAttributes(
        string tournamentType,
        uint garmentId = 0)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyTournamentInTournament"] = "1",
            ["LegacyTournamentType"] = tournamentType,
            ["LegacyTournamentProcess"] = "Alive",
        };

        if (garmentId != 0)
        {
            attributes["GarmentId"] = garmentId.ToString(CultureInfo.InvariantCulture);
            attributes["SpecialGarment"] = garmentId.ToString(CultureInfo.InvariantCulture);
        }

        return attributes;
    }

    private static Dictionary<string, string> CreateSkillTournamentTargetAttributes(uint lifes)
    {
        var rawLifes = lifes.ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SkillTournamentLifes"] = rawLifes,
            ["LegacySkillTournamentLifes"] = rawLifes,
        };
    }

    private static Dictionary<string, string> CreateFreezeWarTargetAttributes(
        uint garmentId,
        string? statusFlags = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GarmentId"] = garmentId.ToString(CultureInfo.InvariantCulture),
            ["SpecialGarment"] = garmentId.ToString(CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(statusFlags))
        {
            attributes["StatusFlags"] = statusFlags;
        }

        return attributes;
    }

    private static Dictionary<string, string> CreateDragonWarTargetAttributes(
        uint hits,
        bool dragonSwing)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DragonWarHits"] = hits.ToString(CultureInfo.InvariantCulture),
        };

        if (dragonSwing)
        {
            attributes["StatusFlags"] = BuildStatusFlags(PlayerReceiveTournamentDragonSwingFlag);
        }

        return attributes;
    }

    private static void AssertSkillTournamentLifes(WorldSession session, ulong expected)
    {
        Assert.Contains(
            expected,
            ReadUInt64AttributeValues(
                session.Character.Attributes,
                "SkillTournamentLifes",
                "LegacySkillTournamentLifes"));
    }

    private static void AssertTournamentKills(WorldSession session, ulong expected)
    {
        Assert.Contains(
            expected,
            ReadUInt64AttributeValues(
                session.Character.Attributes,
                "TournamentKills",
                "LegacyTournamentKills"));
    }

    private static void AssertDragonWarScore(WorldSession session, ulong expected)
    {
        Assert.Contains(
            expected,
            ReadUInt64AttributeValues(
                session.Character.Attributes,
                "DragonWarScore",
                "LegacyDragonWarScore"));
    }

    private static void AssertDragonWarHits(WorldSession session, ulong expected)
    {
        Assert.Contains(
            expected,
            ReadUInt64AttributeValues(
                session.Character.Attributes,
                "DragonWarHits",
                "LegacyDragonWarHits"));
    }

    private static void AssertDragonWarKillSystemUid(WorldSession session, ulong expected)
    {
        Assert.Contains(
            expected,
            ReadUInt64AttributeValues(
                session.Character.Attributes,
                "DragonWarKillSystemUid",
                "LegacyDragonWarKillSystemUid"));
    }

    private static void AssertDragonWarKillSystemFlag(
        WorldSession session,
        string expected,
        ulong expectedNumericValue)
    {
        Assert.Contains(session.Character.Attributes, pair =>
            (string.Equals(pair.Key, "DragonWarKillSystemFlag", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "LegacyDragonWarKillSystemFlag", StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(pair.Value, expected, StringComparison.OrdinalIgnoreCase) ||
                (ulong.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
                    value == expectedNumericValue)));
    }

    private static void AssertDragonWarKillSystemDead(WorldSession session, bool expected)
    {
        Assert.Contains(session.Character.Attributes, pair =>
            (string.Equals(pair.Key, "DragonWarKillSystemDead", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "LegacyDragonWarKillSystemDead", StringComparison.OrdinalIgnoreCase)) &&
            TryReadBooleanAttribute(pair.Value, out var actual) &&
            actual == expected);
    }

    private static bool TryReadBooleanAttribute(string raw, out bool value)
    {
        if (bool.TryParse(raw, out value))
        {
            return true;
        }

        if (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            value = numeric != 0;
            return true;
        }

        value = false;
        return false;
    }

    private static IReadOnlyList<ulong> ReadUInt64AttributeValues(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        var values = new List<ulong>();
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private sealed record PlayerReceiveTournamentHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
