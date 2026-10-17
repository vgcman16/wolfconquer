using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyReallotPointsMutationTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint ReallotPointsNpcId = 2_542;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyReallotPointsMutationTests-" + Guid.NewGuid().ToString("N"));
    private readonly string _usersRoot;

    public LegacyReallotPointsMutationTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(_usersRoot);
    }

    [Theory]
    [InlineData(1, LegacyMsgUpdateDataType.Vitality, "Vitaliti", 11, 12, 18, 14)]
    [InlineData(2, LegacyMsgUpdateDataType.Agility, "Agility", 11, 17, 13, 14)]
    [InlineData(3, LegacyMsgUpdateDataType.Strength, "Strength", 16, 12, 13, 14)]
    [InlineData(4, LegacyMsgUpdateDataType.Spirit, "Spirit", 11, 12, 13, 19)]
    public async Task AllocationOptionsPersistBeforeCommitAndRefreshEquipmentRuntimeWithoutAppearance(
        byte optionId,
        LegacyMsgUpdateDataType expectedStatType,
        string expectedStatKey,
        long expectedStrength,
        long expectedAgility,
        long expectedVitality,
        long expectedSpirit)
    {
        var result = await InvokeAsync(optionId, "5");

        AssertStats(
            result.UpdatedSession.Character,
            expectedStrength,
            expectedAgility,
            expectedVitality,
            expectedSpirit,
            attributePoints: 15);
        Assert.NotNull(result.PersistedAtCommit);
        AssertStats(
            result.PersistedAtCommit,
            expectedStrength,
            expectedAgility,
            expectedVitality,
            expectedSpirit,
            attributePoints: 15);
        AssertStats(
            result.Reloaded,
            expectedStrength,
            expectedAgility,
            expectedVitality,
            expectedSpirit,
            attributePoints: 15);
        Assert.Equal(
            ReadStat(result.Reloaded, expectedStatKey).ToString(),
            result.Reloaded.Attributes[expectedStatKey]);
        Assert.Equal("15", result.Reloaded.Attributes["Atributes"]);
        Assert.Contains("Sentinel=keep-me", result.PersistedText, StringComparison.Ordinal);
        Assert.Contains("Note=preserve-this-section", result.PersistedText, StringComparison.Ordinal);

        Assert.Equal(optionId == 4 ? 7 : 6, result.Sent.Count);
        AssertScalarUpdate(result.Sent[0], expectedStatType, ReadStat(result.Reloaded, expectedStatKey));
        AssertScalarUpdate(result.Sent[1], LegacyMsgUpdateDataType.Atributes, 15);
        AssertEquipmentRuntimeRefresh(result.Sent, startIndex: 2);
        Assert.DoesNotContain(
            result.Sent,
            bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        if (optionId == 4)
        {
            AssertMessageBox(result.Sent[6], "You do not have: 5 attribute points.");
        }

        Assert.Contains("WorldServer applied LegacyNpc ReallotPoints allocation", result.Log);
        Assert.Contains("equipmentRuntimeRefresh=true callItems=false", result.Log);
        Assert.Contains("WorldServer sent MsgItemUsage equipment stat refresh", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task AllocationUsesDefaultUshortParsingForSignedWhitespace()
    {
        var result = await InvokeAsync(optionId: 1, input: " +5 ");

        Assert.Equal(18, result.Reloaded.Vitality);
        Assert.Equal(15, result.Reloaded.AttributePoints);
        AssertScalarUpdate(result.Sent[0], LegacyMsgUpdateDataType.Vitality, 18);
        AssertScalarUpdate(result.Sent[1], LegacyMsgUpdateDataType.Atributes, 15);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("1.0")]
    public async Task InvalidAmountsLeaveSessionAndSnapshotUnchanged(string input)
    {
        var result = await InvokeAsync(optionId: 1, input);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(result.OriginalText, result.PersistedText);
        Assert.Empty(result.Sent);
        AssertStats(result.Reloaded, 11, 12, 13, 14, attributePoints: 20);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task InsufficientAmountsOnlySendSourceMessageBox(byte optionId)
    {
        const string input = " 21 ";

        var result = await InvokeAsync(optionId, input);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(result.OriginalText, result.PersistedText);
        var message = Assert.Single(result.Sent);
        AssertMessageBox(message, "You do not have:  21  attribute points.");
        AssertStats(result.Reloaded, 11, 12, 13, 14, attributePoints: 20);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotCommitOrSendScalarUpdates()
    {
        var result = await InvokeAsync(optionId: 3, input: "5", removeSnapshotBeforeHandle: true);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.Sent);
        Assert.Null(result.ReloadedOrNull);
        Assert.Contains("reason=snapshotMissing", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        byte optionId,
        string input,
        bool removeSnapshotBeforeHandle = false)
    {
        WriteSnapshot();
        var snapshotPath = Path.Combine(_usersRoot, AccountId + ".ini");
        var originalText = File.ReadAllText(snapshotPath);
        var loader = new CharacterSnapshotLoader(_usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(AccountId));
        var session = new WorldSession(
            AccountId,
            "reallot-points-test",
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            UsersRoot: _usersRoot);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        CharacterSnapshot? persistedAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is not null && current.Character.AttributePoints != previous.Character.AttributePoints)
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
        var handled = await WorldPacketHandlers.TryHandleLegacyUtilityNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(),
                Sessions: sessions),
            session,
            ReallotPointsNpcId,
            optionId,
            input,
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

    private void WriteSnapshot()
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, AccountId + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=ReallotTester
            Class=15
            Map=1002
            X=430
            Y=378
            FirstClass=15
            SecoundeClass=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=1002
            PMapX=430
            PMapY=378
            Agility=12
            Strength=11
            Vitaliti=13
            Spirit=14
            Atributes=20
            MinHitPoints=1
            MinMana=0
            MaxMana=0
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

    private static WorldDataCatalog CreateWorldDataCatalog()
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z"),
                "fixture://reallot-points",
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

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertStats(
        CharacterSnapshot snapshot,
        long strength,
        long agility,
        long vitality,
        long spirit,
        long attributePoints)
    {
        Assert.Equal(strength, snapshot.Strength);
        Assert.Equal(agility, snapshot.Agility);
        Assert.Equal(vitality, snapshot.Vitality);
        Assert.Equal(spirit, snapshot.Spirit);
        Assert.Equal(attributePoints, snapshot.AttributePoints);
    }

    private static ulong ReadStat(CharacterSnapshot snapshot, string key)
    {
        return key switch
        {
            "Vitaliti" => checked((ulong)snapshot.Vitality),
            "Agility" => checked((ulong)snapshot.Agility),
            "Strength" => checked((ulong)snapshot.Strength),
            "Spirit" => checked((ulong)snapshot.Spirit),
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
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

    private static void AssertEquipmentRuntimeRefresh(IReadOnlyList<byte[]> sent, int startIndex)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[startIndex]), out var maxHitpointsOnly));
        Assert.Equal(CharacterUid, maxHitpointsOnly.Uid);
        var maxHitpoints = Assert.Single(maxHitpointsOnly.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maxHitpoints.Type);

        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[startIndex + 1]), out var hitpointPair));
        Assert.Equal(CharacterUid, hitpointPair.Uid);
        Assert.Collection(
            hitpointPair.Updates,
            update => Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, update.Type),
            update => Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, update.Type));

        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(sent[startIndex + 2]), out var status));
        Assert.Equal(CharacterUid, status.Uid);
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(sent[startIndex + 3]), out var refine));
        Assert.Equal(CharacterUid, refine.Uid);
    }

    private static void AssertMessageBox(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, packet.InteractType);
        Assert.Equal(expectedText, packet.Text);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
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
