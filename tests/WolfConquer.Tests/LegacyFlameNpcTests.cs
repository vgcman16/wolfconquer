using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFlameNpcTests
{
    private const uint Flame1NpcId = 657;
    private const uint Flame2NpcId = 658;
    private const uint Flame3NpcId = 659;
    private const uint Flame10NpcId = 666;
    private const uint FlameTaoistNpcId = 681;

    [Fact]
    public async Task FlameTaoistSendsTranslatedSourceMenu()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            FlameTaoistNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("There are 10 Flame Stones", StringComparison.Ordinal));
        AssertOption(replies, "Rewards? Tell me more!", 1);
        AssertOption(replies, "Can you give me a rune?", 4);
        AssertOption(replies, "I have a rune. Give me the reward!", 5);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 67);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.FlameTaoist", log.ToString());
    }

    [Fact]
    public async Task FlameStoneConsumesRuneAndGrantsNextRuneWhenGuildWarIsActiveAndPersistenceIsAvailable()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var temp = new TemporaryDirectory();
        var session = CreateSession([Item(20, 729_961)]);
        var sessions = new WorldSessionStore();
        var guildWar = new LegacyGuildWarRuntimeStateStore();
        guildWar.Start();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                GuildWarStates: guildWar,
                PlayerItemsRoot: temp.Path),
            session,
            Flame2NpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_961U);
        var reward = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_962U);
        Assert.Equal(1U, reward.Uid);

        var persisted = new PlayerItemSnapshotLoader(temp.Path).LoadByCharacterUid(updated.Character.Uid);
        Assert.DoesNotContain(persisted.InventoryItems, item => item.ItemId == 729_961U);
        Assert.Contains(persisted.InventoryItems, item => item.ItemId == 729_962U);
        Assert.Contains(ParseGameItems(sent), item => item.ItemId == 729_962U && item.Mode == LegacyGameItemPacket.AddItemMode);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Congratulations! You lit the Flame Stone (236,291) near the bridge in the Guild Arena.");
        Assert.Contains("WorldServer applied LegacyNpc Flame rune exchange", log.ToString());
        Assert.Contains("source=NpcHandler.Flame2", log.ToString());
        Assert.DoesNotContain("PARITY-GAP LegacyNpc FlameWarEvent", log.ToString());
    }

    [Fact]
    public async Task FlameStoneSendsSystemTalkWhenGuildWarStateIsKnownInactive()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(20, 729_961)]);
        var sessions = new WorldSessionStore();
        var guildWar = new LegacyGuildWarRuntimeStateStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                GuildWarStates: guildWar),
            session,
            Flame2NpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_961U);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_962U);
        Assert.Empty(ParseNpcReplies(sent));
        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == 729_962U);

        var message = Assert.Single(ParseMessages(sent));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Equal("It's not the right time. You can't light up the Flame Stone.", message.Content[3]);
        Assert.Contains("WorldServer sent LegacyNpc Flame inactive guild-war message", log.ToString());
        Assert.Contains("source=NpcHandler.Flame2", log.ToString());
        Assert.DoesNotContain("PARITY-GAP LegacyNpc FlameWarEvent", log.ToString());
    }

    [Fact]
    public async Task FlameStoneRejectsMissingRuneWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(30, 729_961)]);
        var sessions = new WorldSessionStore();
        var guildWar = new LegacyGuildWarRuntimeStateStore();
        guildWar.Start();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions, GuildWarStates: guildWar),
            session,
            Flame3NpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text.Contains("proper rune", StringComparison.Ordinal));
        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == 729_963U);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc FlameWarEvent", log.ToString());
    }

    [Fact]
    public async Task FlameStoneFencesRuneMutationWithoutPersistenceRoot()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(10, 729_960)]);
        var sessions = new WorldSessionStore();
        var guildWar = new LegacyGuildWarRuntimeStateStore();
        guildWar.Start();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions, GuildWarStates: guildWar),
            session,
            Flame1NpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_960U);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_961U);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "The Flame Stone rune exchange is not available from this runtime yet.");
        Assert.Contains("PARITY-GAP LegacyNpc FlameWarEvent", log.ToString());
        Assert.Contains("source=NpcHandler.Flame1", log.ToString());
        Assert.Contains("PlayerItemsRoot is missing", log.ToString());
    }

    [Fact]
    public async Task FlameStoneFencesRuneMutationWhenGuildWarStateIsUnavailable()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var temp = new TemporaryDirectory();
        var session = CreateSession([Item(10, 729_960)]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: temp.Path),
            session,
            Flame1NpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_960U);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_961U);
        Assert.Empty(ParseMessages(sent));
        Assert.DoesNotContain(ParseGameItems(sent), item => item.ItemId == 729_961U);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "It is not the right time. You cannot light this Flame Stone yet.");
        Assert.Contains("PARITY-GAP LegacyNpc FlameWarEvent", log.ToString());
        Assert.Contains("source=NpcHandler.Flame1", log.ToString());
        Assert.Contains("process state is unavailable", log.ToString());
    }

    [Fact]
    public async Task FlameTaoistRewardIsFencedWhenRewardRuneExists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(40, 729_970)]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            FlameTaoistNpcId,
            optionId: 5,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_970U);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "The Flame Taoist experience reward is not available from this runtime yet.");
        Assert.Contains("PARITY-GAP LegacyNpc FlameWarEvent", log.ToString());
        Assert.Contains("source=NpcHandler.FlameTaoist option=5", log.ToString());
        Assert.Contains("GainExpBall(7200)", log.ToString());
    }

    [Fact]
    public async Task FlameTaoistFinalRuneRewardPersistsConsumedRuneAndExperience()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(playerItemsRoot);
        WriteSnapshot(usersRoot, level: 139, experience: 0);

        var session = CreateSession(
            [Item(40, 729_970)],
            level: 139,
            experience: 0,
            usersRoot: usersRoot);
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(session.Character.Uid, session.PlayerItems);

        var sessions = new WorldSessionStore();
        var guildWar = new LegacyGuildWarRuntimeStateStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                GuildWarStates: guildWar,
                WorldData: CreateWorldDataCatalog(LevelExperienceRow(139, experience: 100_000, upLevTime: 10_000)),
                PlayerItemsRoot: playerItemsRoot),
            session,
            FlameTaoistNpcId,
            optionId: 5,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_970U);
        Assert.Equal(139, updated.Character.Level);
        Assert.Equal(72_000, updated.Character.Experience);

        var persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(updated.Character.Uid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == 729_970U);

        var persistedCharacter = new CharacterSnapshotLoader(usersRoot).LoadByAccountId(updated.Character.Uid);
        Assert.NotNull(persistedCharacter);
        Assert.Equal(139, persistedCharacter.Level);
        Assert.Equal(72_000, persistedCharacter.Experience);

        Assert.Contains(
            ParseMessages(sent),
            message => message.Content[3] == "You have gained experience worth 12 exp ball(s).");
        Assert.Contains("WorldServer applied LegacyNpc FlameTaoist final rune reward", log.ToString());
        Assert.Contains("WorldServer persisted LegacyNpc FlameTaoist character reward", log.ToString());
        Assert.DoesNotContain("PARITY-GAP LegacyNpc FlameWarEvent", log.ToString());
    }

    [Fact]
    public async Task Flame10PreservesSourceEmptyOption()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            Flame10NpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.Contains("source=NpcHandler.Flame10", log.ToString());
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
    }

    private static WorldSession CreateSession(
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null,
        long level = 140,
        long experience = 0,
        string? usersRoot = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "flame-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "FlameRunner",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: level,
                Experience: experience,
                MapId: 1_038,
                X: 350,
                Y: 321,
                PreviousMapId: 1_038,
                PreviousMapX: 350,
                PreviousMapY: 321,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: new PlayerItemSnapshot(inventoryItems ?? [], [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = 0,
            StackSize = 1,
        };
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMessages(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private static void WriteSnapshot(string usersRoot, long level, long experience)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, "1293600.ini"),
            $$"""
            [Character]
            UID=1293600
            Body=1003
            Face=0
            Name=FlameRunner
            Class=7051
            Map=1038
            X=350
            Y=321
            FirstClass=7051
            SecoundeClass=0
            Conquer=0
            Reborn=0
            Level={{level}}
            Haire=0
            Experience={{experience}}
            PMap=1038
            PMapX=350
            PMapY=321
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            MaxMana=0
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0

            [PaidNobility]
            Rank=Serf
            Identifier=0
            """);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params LevelExperience[] levelExperience)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://flame-war",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
            itemAddBonuses: [],
            levelExperience: levelExperience,
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static LevelExperience LevelExperienceRow(int level, long experience, int upLevTime)
    {
        return new LevelExperience(
            Type: 0,
            Level: level,
            Experience: experience,
            UpLevTime: upLevTime,
            MentorUpLevTime: upLevTime,
            SourceLine: 1,
            RawLine: $"0 {level.ToString(CultureInfo.InvariantCulture)} {experience.ToString(CultureInfo.InvariantCulture)} {upLevTime.ToString(CultureInfo.InvariantCulture)} {upLevTime.ToString(CultureInfo.InvariantCulture)}",
            Fields:
            [
                "0",
                level.ToString(CultureInfo.InvariantCulture),
                experience.ToString(CultureInfo.InvariantCulture),
                upLevTime.ToString(CultureInfo.InvariantCulture),
                upLevTime.ToString(CultureInfo.InvariantCulture),
            ]);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wolf-flame-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
