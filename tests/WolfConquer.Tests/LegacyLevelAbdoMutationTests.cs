using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLevelAbdoMutationTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;
    private const uint LevelAbdoNpcId = 19_039;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyLevelAbdoMutationTests-" + Guid.NewGuid().ToString("N"));
    private readonly string _usersRoot;

    public LegacyLevelAbdoMutationTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(_usersRoot);
    }

    [Fact]
    public async Task Below140PersistsBeforeCommitPreservesExperienceHealsAndSendsSourceOrder()
    {
        var result = await InvokeAsync(
            level: 130,
            experience: 123_456,
            attributePoints: 7,
            minHitpoints: 5);

        Assert.Equal(140, result.UpdatedSession.Character.Level);
        Assert.Equal(123_456, result.UpdatedSession.Character.Experience);
        Assert.Equal(37, result.UpdatedSession.Character.AttributePoints);
        Assert.True(result.UpdatedSession.Character.MinHitPoints > 5);
        var persistedAtCommit = Assert.IsType<CharacterSnapshot>(result.PersistedAtCommit);
        AssertCharacterMutation(persistedAtCommit, result.UpdatedSession.Character);
        AssertCharacterMutation(result.Reloaded, result.UpdatedSession.Character);
        Assert.Contains("Sentinel=keep-me", result.PersistedText, StringComparison.Ordinal);
        Assert.Contains("Note=preserve-this-section", result.PersistedText, StringComparison.Ordinal);

        Assert.Equal(13, result.Sent.Count);
        AssertScalarUpdate(result.Sent[0], LegacyMsgUpdateDataType.Level, 140);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(result.Sent[1]), out var levelAction));
        Assert.Equal(CharacterUid, levelAction.ObjId);
        Assert.Equal((MsgActionType)92, levelAction.Type);
        Assert.Equal(140U, levelAction.PositionX);

        AssertScalarUpdate(result.Sent[2], LegacyMsgUpdateDataType.Atributes, 37);
        AssertHitpointPair(
            result.Sent,
            startIndex: 3,
            checked((uint)result.UpdatedSession.Character.MinHitPoints),
            expectedCurrent: 5);
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(result.Sent[5]), out _));
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(result.Sent[6]), out _));
        AssertHitpointPair(
            result.Sent,
            startIndex: 7,
            checked((uint)result.UpdatedSession.Character.MinHitPoints),
            checked((uint)result.UpdatedSession.Character.MinHitPoints));
        AssertCompletionDialog(result.Sent, startIndex: 9);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent).SelectMany(packet => packet.Updates),
            update => update.Type == LegacyMsgUpdateDataType.Experience);

        Assert.Contains("attributePointsAdded=30", result.Log);
        Assert.Contains("updateOrder=Level,Action92,StatsOrAtributes,Equipment,Hitpoints,Dialog", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task BelowSourceAutoStatCutoffSendsUnchangedStatsWithoutAddingAttributePoints()
    {
        var result = await InvokeAsync(
            level: 119,
            experience: 88,
            attributePoints: 7,
            strength: 11,
            agility: 12,
            vitality: 100,
            spirit: 14);

        Assert.Equal(140, result.Reloaded.Level);
        Assert.Equal(7, result.Reloaded.AttributePoints);
        Assert.Equal(11, result.Reloaded.Strength);
        Assert.Equal(12, result.Reloaded.Agility);
        Assert.Equal(100, result.Reloaded.Vitality);
        Assert.Equal(14, result.Reloaded.Spirit);

        AssertScalarUpdate(result.Sent[2], LegacyMsgUpdateDataType.Strength, 11);
        AssertScalarUpdate(result.Sent[3], LegacyMsgUpdateDataType.Agility, 12);
        AssertScalarUpdate(result.Sent[4], LegacyMsgUpdateDataType.Spirit, 14);
        AssertScalarUpdate(result.Sent[5], LegacyMsgUpdateDataType.Vitality, 100);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent).SelectMany(packet => packet.Updates),
            update => update.Type == LegacyMsgUpdateDataType.Atributes);
    }

    [Fact]
    public async Task Exactly140SendsOnlyExactSourceMessageBoxWithoutMutationOrAuthority()
    {
        var result = await InvokeAsync(
            level: 140,
            experience: 777,
            attributePoints: 9,
            includeWorldData: false);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(result.OriginalText, result.PersistedText);
        var message = Assert.Single(result.Sent);
        var reply = AssertNpcReply(message, LegacyNpcReplyInteractType.MessageBox);
        Assert.Equal("You already achieved the highest level possible.", reply.Text);
        Assert.Equal(140, result.Reloaded.Level);
        Assert.Equal(777, result.Reloaded.Experience);
        Assert.Equal(9, result.Reloaded.AttributePoints);
        Assert.Contains("reason=alreadyLevel140", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task Above140StillRunsUpdateLevel140WithoutGrantingAttributePoints()
    {
        var result = await InvokeAsync(
            level: 141,
            experience: 654_321,
            attributePoints: 19,
            minHitpoints: 7);

        Assert.Equal(140, result.UpdatedSession.Character.Level);
        Assert.Equal(654_321, result.UpdatedSession.Character.Experience);
        Assert.Equal(19, result.UpdatedSession.Character.AttributePoints);
        AssertCharacterMutation(result.Reloaded, result.UpdatedSession.Character);
        AssertScalarUpdate(result.Sent[0], LegacyMsgUpdateDataType.Level, 140);
        Assert.True(MsgActionPacket.TryParse(ReadFrame(result.Sent[1]), out var levelAction));
        Assert.Equal((MsgActionType)92, levelAction.Type);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent).SelectMany(packet => packet.Updates),
            update => update.Type is LegacyMsgUpdateDataType.Atributes or
                LegacyMsgUpdateDataType.Strength or
                LegacyMsgUpdateDataType.Agility or
                LegacyMsgUpdateDataType.Spirit or
                LegacyMsgUpdateDataType.Vitality or
                LegacyMsgUpdateDataType.Experience);
        AssertCompletionDialog(result.Sent, startIndex: 8);
        Assert.Contains("level=141->140", result.Log);
    }

    [Fact]
    public async Task MissingWorldDataFencesBeforePersistenceSessionCommitOrLevelPackets()
    {
        var result = await InvokeAsync(
            level: 130,
            experience: 55,
            attributePoints: 7,
            includeWorldData: false);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(result.OriginalText, result.PersistedText);
        Assert.Equal(130, result.Reloaded.Level);
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), packet =>
            packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.Level));
        Assert.DoesNotContain(result.Sent, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text.Contains("unavailable here", StringComparison.Ordinal));
        Assert.Contains("requires Sessions, WorldData, and UsersRoot", result.Log);
        Assert.Contains("PARITY-GAP LegacyNpc SocialCraftingServices", result.Log);
    }

    [Fact]
    public async Task PersistenceFailureFencesBeforeSessionCommitOrMutationPackets()
    {
        var result = await InvokeAsync(
            level: 130,
            experience: 55,
            attributePoints: 7,
            removeSnapshotBeforeHandle: true);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Null(result.ReloadedOrNull);
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), packet =>
            packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.Level));
        Assert.DoesNotContain(result.Sent, bytes => MsgActionPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text.Contains("unavailable here", StringComparison.Ordinal));
        Assert.Contains("persistence failed reason=snapshotMissing", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        long level,
        long experience,
        long attributePoints,
        long minHitpoints = 5,
        long strength = 11,
        long agility = 12,
        long vitality = 100,
        long spirit = 14,
        long classCode = 1_005,
        long reborn = 0,
        bool includeWorldData = true,
        bool removeSnapshotBeforeHandle = false)
    {
        WriteSnapshot(
            level,
            experience,
            attributePoints,
            minHitpoints,
            strength,
            agility,
            vitality,
            spirit,
            classCode,
            reborn);
        var snapshotPath = Path.Combine(_usersRoot, AccountId + ".ini");
        var originalText = File.ReadAllText(snapshotPath);
        var loader = new CharacterSnapshotLoader(_usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(AccountId));
        var session = new WorldSession(
            AccountId,
            "level-abdo-test",
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty,
            UsersRoot: _usersRoot);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        CharacterSnapshot? persistedAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is not null && current.Character.Level != previous.Character.Level)
            {
                persistedAtCommit = loader.LoadByAccountId(AccountId);
            }
        });
        sessions.Set(connection, session);
        if (removeSnapshotBeforeHandle)
        {
            File.Delete(snapshotPath);
        }

        using var log = new StringWriter();
        var handled = await WorldPacketHandlers.TryHandleLegacySocialCraftingServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: includeWorldData ? CreateWorldDataCatalog() : null,
                Sessions: sessions),
            session,
            LevelAbdoNpcId,
            optionId: 3,
            input: "",
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var reloaded = loader.LoadByAccountId(AccountId);
        var persistedText = File.Exists(snapshotPath) ? File.ReadAllText(snapshotPath) : "";
        return new InvokeResult(
            session,
            updatedSession,
            persistedAtCommit,
            reloaded,
            sent,
            log.ToString(),
            originalText,
            persistedText);
    }

    private void WriteSnapshot(
        long level,
        long experience,
        long attributePoints,
        long minHitpoints,
        long strength,
        long agility,
        long vitality,
        long spirit,
        long classCode,
        long reborn)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, AccountId + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=LevelAbdoTester
            Class={{classCode}}
            Map=1002
            X=430
            Y=378
            FirstClass={{classCode}}
            SecoundeClass=0
            Reborn={{reborn}}
            Level={{level}}
            Haire=0
            Experience={{experience}}
            PMap=1002
            PMapX=430
            PMapY=378
            Agility={{agility}}
            Strength={{strength}}
            Vitaliti={{vitality}}
            Spirit={{spirit}}
            Atributes={{attributePoints}}
            MinHitPoints={{minHitpoints}}
            MinMana=25
            MaxMana=25
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            Sentinel=keep-me

            [Other]
            Note=preserve-this-section
            """);
    }

    private static void AssertCharacterMutation(CharacterSnapshot actual, CharacterSnapshot expected)
    {
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Experience, actual.Experience);
        Assert.Equal(expected.Strength, actual.Strength);
        Assert.Equal(expected.Agility, actual.Agility);
        Assert.Equal(expected.Vitality, actual.Vitality);
        Assert.Equal(expected.Spirit, actual.Spirit);
        Assert.Equal(expected.AttributePoints, actual.AttributePoints);
        Assert.Equal(expected.MinHitPoints, actual.MinHitPoints);
    }

    private static void AssertHitpointPair(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        uint expectedMax,
        uint expectedCurrent)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[startIndex]), out var maxOnly));
        var maxUpdate = Assert.Single(maxOnly.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maxUpdate.Type);
        Assert.Equal((ulong)expectedMax, maxUpdate.Value);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[startIndex + 1]), out var pair));
        Assert.Collection(
            pair.Updates,
            update =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, update.Type);
                Assert.Equal((ulong)expectedMax, update.Value);
            },
            update =>
            {
                Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, update.Type);
                Assert.Equal((ulong)expectedCurrent, update.Value);
            });
    }

    private static void AssertCompletionDialog(IReadOnlyList<byte[]> sent, int startIndex)
    {
        var text = AssertNpcReply(sent[startIndex], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Okay, you have a Level 140! ", text.Text);

        var option = AssertNpcReply(sent[startIndex + 1], LegacyNpcReplyInteractType.Option);
        Assert.Equal("Thx ", option.Text);
        Assert.Equal(byte.MaxValue, option.OptionId);

        var avatar = AssertNpcReply(sent[startIndex + 2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal((ushort)15, avatar.InputMaxLength);
        AssertNpcReply(sent[startIndex + 3], LegacyNpcReplyInteractType.Finish);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
        Assert.Equal(expectedType, reply.InteractType);
        return reply;
    }

    private static void AssertScalarUpdate(
        byte[] bytes,
        LegacyMsgUpdateDataType expectedType,
        ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(CharacterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(expectedType, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        return sent
            .Select(ReadFrame)
            .Where(frame => LegacyMsgUpdatePacket.TryParse(frame, out _))
            .Select(frame =>
            {
                Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var packet));
                return packet;
            })
            .ToArray();
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        return sent
            .Select(ReadFrame)
            .Where(frame => LegacyNpcReplyPacket.TryParse(frame, out _))
            .Select(frame =>
            {
                Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
                return reply;
            })
            .ToArray();
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateWorldDataCatalog()
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z"),
                "fixture://level-abdo",
                new Dictionary<string, int>()),
            clientConfigs: [], items: [], itemAddBonuses: [], levelExperience: [], runeLevelExperience: [],
            runeStorageAttributes: [], itemRefineEffects: [], itemRefineEffectEx: [], itemRefineUpgrades: [],
            transformInfo: [], furniture: [], hairColorTypes: [], hairFaceStorageTypes: [], coatStorageTypes: [],
            coatStorageAttributes: [], coatColorRules: [], xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [], xuanbaoRandomAttributes: [], christmasNpcs: [], tutorTypes: [],
            tutorBattleLimitTypes: [], beastAttributes: [], fateExperience: [], spiritRates: [], hundredWeapons: [],
            gouyuTypes: [], gouyuImmortals: [], specialEffectAttributes: [], daoqiDictTypes: [],
            syndicateFormationTypes: [], syndicateSkillTypes: [], disdainRules: [], jiangHuAttributes: [],
            jiangHuCultivateStatuses: [], refineryBoxes: [], refinerySouls: [], refineryMaterials: [],
            stateCountFiles: [], binarySeedFiles: [], emptySeedFiles: [], backupSeedFiles: [], pokerTables: [],
            rouletteTables: [], npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, "", 0, []),
                [],
                "fixture://activity"),
            meltingTypeRules: [], lotteryPrizes: [], arenaRecords: [], elitePkRecords: [], teamElitePkRecords: [],
            skillTeamPkRecords: [], classPkWarRecords: [], combatGearRanks: [], prestigeRanks: [],
            innerPowerRanks: [], runeRankings: [], staticStatues: [], newPlayerRewards: [], votes: [],
            couplesPkRecords: [], socialAssociations: [], guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"), classLevelStats: [], professionLevelUps: [],
            professionLevelBenefits: [], professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [], questMissions: [], quizQuestions: [], magic: [], magicEffects: [],
            magicTypeOperations: [], titleTypes: [], combatGear: [], traps: [], npcs: [], sobNpcs: [], portals: [],
            maps: [], mapNames: [], monsters: [], monsterSpawns: [], lotteryDictionary: [], collectionRewardTiers: [],
            taskRewardTypes: [], taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                "",
                new Dictionary<string, string>()),
            neiGongSecrets: [], neiGongTypes: [], subProfessions: [], syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0), stageGoalStages: [], stageGoalTasks: [], shops: [], shopItems: [],
            exchangeShopGoods: [], exchangeShopGoodsEx: [], exchangeShopLevels: [], honorShopItems: [],
            honorShopRecommendations: [], racePointShopItems: [], racePointShopRecommendations: [],
            goldenLeagueShopItems: [], goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(), booths: [], boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record InvokeResult(
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        CharacterSnapshot? PersistedAtCommit,
        CharacterSnapshot? ReloadedOrNull,
        IReadOnlyList<byte[]> Sent,
        string Log,
        string OriginalText,
        string PersistedText)
    {
        public CharacterSnapshot Reloaded => Assert.IsType<CharacterSnapshot>(ReloadedOrNull);
    }
}
