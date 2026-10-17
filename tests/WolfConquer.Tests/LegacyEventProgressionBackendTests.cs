using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyEventProgressionBackendTests
{
    private const uint FlameTaoistNpcId = 681;
    private const uint FlameRewardRuneItemId = 729_970;
    private const long CharacterUid = 1_293_600;

    [Fact]
    public async Task FlameTaoistFinalRewardConsumesRunePersistsStateAndAppliesExpBallRewardWhenGuildWarEnded()
    {
        using var log = new StringWriter();
        using var fixture = new TemporaryDirectory();
        var usersRoot = Path.Combine(fixture.Path, "Users");
        var itemsRoot = Path.Combine(fixture.Path, "Items");
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid + ".ini"),
            """
            [Character]
            UID=1293600
            Level=139
            Experience=0
            """);

        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var guildWar = new LegacyGuildWarRuntimeStateStore();
        guildWar.Stop();
        var session = CreateSession(
            level: 139,
            experience: 0,
            usersRoot: usersRoot,
            inventoryItems: [Item(40, FlameRewardRuneItemId)]);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateLevelExperienceCatalog(),
                Sessions: sessions,
                GuildWarStates: guildWar,
                PlayerItemsRoot: itemsRoot),
            session,
            FlameTaoistNpcId,
            optionId: 5,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == FlameRewardRuneItemId);
        Assert.Equal(140, updated.Character.Level);
        Assert.Equal(0, updated.Character.Experience);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == FlameRewardRuneItemId);
        var characterFile = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Level=140", characterFile);
        Assert.Contains("Experience=0", characterFile);

        var updates = ParseMsgUpdates(sent).SelectMany(packet => packet.Updates).ToArray();
        Assert.Contains(updates, update => update.Type == LegacyMsgUpdateDataType.Level && update.Value == 140);
        Assert.Contains(updates, update => update.Type == LegacyMsgUpdateDataType.Experience && update.Value == 0);
        Assert.Contains(ParseStrings(sent), packet => packet.Strings.SequenceEqual(["angelwing"]));
        Assert.Contains(
            ParseMessages(sent),
            packet => packet.Content[3] == "You have gained experience worth 12 exp ball(s).");

        var output = log.ToString();
        Assert.Contains("WorldServer applied LegacyNpc FlameTaoist final rune reward", output);
        Assert.Contains("GainExpBall(7200)", output);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc FlameWarEvent", output);
    }

    [Fact]
    public async Task FlameTaoistFinalRewardRejectsActiveGuildWarWithoutConsumingRune()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var guildWar = new LegacyGuildWarRuntimeStateStore();
        guildWar.Start();
        var session = CreateSession(inventoryItems: [Item(40, FlameRewardRuneItemId)]);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyFlameWarEventNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                GuildWarStates: guildWar),
            session,
            FlameTaoistNpcId,
            optionId: 5,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == FlameRewardRuneItemId);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "You cannot claim experience while guild war.");
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseStrings(sent));
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    private static WorldSession CreateSession(
        long level = 140,
        long experience = 0,
        string? usersRoot = null,
        IReadOnlyList<PlayerItemSnapshotItem>? inventoryItems = null)
    {
        return new WorldSession(
            AccountId: CharacterUid,
            Username: "event-progress-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "FlameReward",
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

    private static WorldDataCatalog CreateLevelExperienceCatalog()
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-20T00:00:00Z"),
                "fixture://event-progression",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
            itemAddBonuses: [],
            levelExperience:
            [
                new LevelExperience(
                    Type: 0,
                    Level: 139,
                    Experience: 1_200,
                    UpLevTime: 600,
                    MentorUpLevTime: 600,
                    SourceLine: 1,
                    RawLine: "0 139 1200 600 600",
                    Fields: ["0", "139", "1200", "600", "600"]),
            ],
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static IReadOnlyList<LegacyStringPacket> ParseStrings(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyStringPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet))
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wolf-event-progression-" + Guid.NewGuid().ToString("N"));
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
