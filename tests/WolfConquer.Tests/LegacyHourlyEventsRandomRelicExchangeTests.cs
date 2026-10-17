using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyHourlyEventsRandomRelicExchangeTests : IDisposable
{
    private const uint EventsInHourNpcId = 5_595;
    private const byte RandomRelicOptionId = 106;
    private const uint ArrestTokenItemId = 711_624;
    private const uint ArrestTokenUid = 7_116_240;
    private const uint BoundArrestTokenUid = 7_116_241;
    private const uint RelicItemId = 4_100_001;
    private const string RelicName = "SaturnRelic";
    private const long CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-hourly-random-relic-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option106ConsumesThreeUnitsPersistsThenCommitsDeterministicSourceRelicAndDialog()
    {
        var result = await InvokeAsync(
            Items(Token(ArrestTokenUid, stackSize: 3)),
            CreateWorldDataCatalog());

        Assert.True(result.Handled);
        Assert.NotSame(result.InitialSession, result.UpdatedSession);
        Assert.Equal(1, result.CommitCount);

        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == ArrestTokenItemId);
        var relic = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == RelicItemId);
        Assert.Equal(1U, relic.Uid);
        Assert.Equal((ushort)1_099, relic.Durability);
        Assert.Equal((ushort)1_099, relic.MaximumDurability);
        Assert.Equal((ushort)0, relic.Position);
        Assert.Equal((ushort)1, relic.StackSize);
        Assert.Equal((byte)0, relic.Bound);
        Assert.Equal(200_103U, relic.Attribute1);
        Assert.Equal(100_104U, relic.Attribute2);
        Assert.Equal(900_108U, relic.Attribute3);
        Assert.Equal(0U, relic.Attribute4);
        Assert.Equal(0U, relic.Attribute5);

        var itemsAtCommit = Assert.IsType<PlayerItemSnapshot>(result.ItemsAtCommit);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, itemsAtCommit);
        var persistedItems = Assert.IsType<PlayerItemSnapshot>(result.PersistedItems);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persistedItems);
        Assert.Equal(CharacterUid, result.PersistedCharacter.Uid);

        Assert.Equal(7, result.Sent.Count);
        AssertTokenUpdate(result.Sent[0], stackSize: 2, bound: 0);
        AssertTokenUpdate(result.Sent[1], stackSize: 1, bound: 0);
        AssertTokenRemoval(result.Sent[2], ArrestTokenUid);
        var rewardPacket = AssertGameItem(result.Sent[3]);
        Assert.Equal(relic.Uid, rewardPacket.Uid);
        Assert.Equal(RelicItemId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal((ushort)1_099, rewardPacket.Durability);
        Assert.Equal((ushort)1_099, rewardPacket.MaximumDurability);
        Assert.Equal((ushort)1, rewardPacket.StackSize);
        Assert.Equal((byte)0, rewardPacket.Bound);
        Assert.Equal(200_103U, rewardPacket.Attribute1);
        Assert.Equal(100_104U, rewardPacket.Attribute2);
        Assert.Equal(900_108U, rewardPacket.Attribute3);
        AssertNpcReply(
            result.Sent[4],
            LegacyNpcReplyInteractType.Dialog,
            $"Congratulations! You received 1 {RelicName}.");
        AssertNpcReply(
            result.Sent[5],
            LegacyNpcReplyInteractType.Option,
            "Thank you.",
            optionId: byte.MaxValue);
        AssertNpcReply(result.Sent[6], LegacyNpcReplyInteractType.Finish, "");

        Assert.Contains(
            "source=NpcHandler.EventsInHour option=106 itemId=711624 consumed=3",
            result.Log);
        Assert.Contains($"rewardItemId={RelicItemId} rewardItemUid={relic.Uid}", result.Log);
        Assert.Contains($"rewardName=\"{RelicName}\" bound=0", result.Log);
        Assert.Contains("persisted LegacyNpc HourlyEvents Saturn random relic exchange items", result.Log);
        Assert.DoesNotContain(
            "PARITY-GAP LegacyNpc HourlyEvents Saturn random relic exchange",
            result.Log);
    }

    [Fact]
    public async Task Option106RequiresExactlyThreeUnitsFromOneSourceBoundClass()
    {
        var twoTokens = await InvokeAsync(
            Items(Token(ArrestTokenUid, stackSize: 2)),
            CreateWorldDataCatalog());

        Assert.True(twoTokens.Handled);
        Assert.Same(twoTokens.InitialSession, twoTokens.UpdatedSession);
        Assert.Equal(0, twoTokens.CommitCount);
        AssertExactFailureDialog(twoTokens.Sent);
        AssertPlayerItemsEqual(
            twoTokens.InitialSession.PlayerItems,
            Assert.IsType<PlayerItemSnapshot>(twoTokens.PersistedItems));

        var mixedBoundClasses = await InvokeAsync(
            Items(
                Token(ArrestTokenUid, stackSize: 2, bound: 0),
                Token(BoundArrestTokenUid, stackSize: 1, bound: 1)),
            CreateWorldDataCatalog());

        Assert.True(mixedBoundClasses.Handled);
        Assert.Same(mixedBoundClasses.InitialSession, mixedBoundClasses.UpdatedSession);
        Assert.Equal(0, mixedBoundClasses.CommitCount);
        AssertExactFailureDialog(mixedBoundClasses.Sent);
        AssertPlayerItemsEqual(
            mixedBoundClasses.InitialSession.PlayerItems,
            Assert.IsType<PlayerItemSnapshot>(mixedBoundClasses.PersistedItems));
        Assert.Contains("neither the unbound nor bound source inventory class contains three tokens", mixedBoundClasses.Log);
    }

    [Fact]
    public async Task Option106BoundGatePassesButSourceRemovalDoesNotFilterByBound()
    {
        var result = await InvokeAsync(
            Items(
                Token(ArrestTokenUid, stackSize: 1, bound: 0),
                Token(BoundArrestTokenUid, stackSize: 3, bound: 1)),
            CreateWorldDataCatalog());

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == ArrestTokenUid);
        var remainingBoundToken = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == ArrestTokenItemId);
        Assert.Equal(BoundArrestTokenUid, remainingBoundToken.Uid);
        Assert.Equal((byte)1, remainingBoundToken.Bound);
        Assert.Equal((ushort)1, remainingBoundToken.StackSize);
        Assert.Equal((byte)0, Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == RelicItemId).Bound);

        AssertTokenRemoval(result.Sent[0], ArrestTokenUid);
        AssertTokenUpdate(result.Sent[1], BoundArrestTokenUid, stackSize: 2, bound: 1);
        AssertTokenUpdate(result.Sent[2], BoundArrestTokenUid, stackSize: 1, bound: 1);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, AssertGameItem(result.Sent[3]).Mode);
        AssertPlayerItemsEqual(
            result.UpdatedSession.PlayerItems,
            Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Theory]
    [InlineData("world-data", "randomRelicWorldDataMissing")]
    [InlineData("random-data", "randomRelicDataUnavailable")]
    [InlineData("item-type", "randomRelicItemTypeUnavailable")]
    [InlineData("session-store", "sessionUnavailable")]
    [InlineData("context-session", "sessionUnavailable")]
    [InlineData("session-registration", "sessionUnavailable")]
    [InlineData("root", "playerItemsRootMissing")]
    [InlineData("character-uid", "characterUidUnavailable")]
    [InlineData("item-uid", "itemUidUnavailable")]
    [InlineData("persistence", "itemPersistenceUnavailable")]
    public async Task Option106DependencyFailuresDoNotConsumePersistCommitOrSend(
        string failure,
        string expectedReason)
    {
        var initialItems = failure == "item-uid"
            ? Items(
                Token(ArrestTokenUid, stackSize: 3),
                new PlayerItemSnapshotItem
                {
                    Uid = uint.MaxValue,
                    ItemId = 729_999,
                    Position = 0,
                    StackSize = 1,
                })
            : Items(Token(ArrestTokenUid, stackSize: 3));
        WorldDataCatalog? worldData = failure switch
        {
            "world-data" => null,
            "random-data" => CreateWorldDataCatalog(includeRandomAttributes: false),
            "item-type" => CreateWorldDataCatalog(includeItemType: false),
            _ => CreateWorldDataCatalog(),
        };

        string? itemsRootOverride = null;
        var seedPlayerItems = true;
        if (failure == "persistence")
        {
            Directory.CreateDirectory(_root);
            itemsRootOverride = Path.Combine(_root, "blocked-items-root");
            File.WriteAllText(itemsRootOverride, "not a directory");
            seedPlayerItems = false;
        }

        var result = await InvokeAsync(
            initialItems,
            worldData,
            includeSessionStore: failure != "session-store",
            includeContextSession: failure != "context-session",
            registerSession: failure != "session-registration",
            includePlayerItemsRoot: failure != "root",
            playerItemsRootOverride: itemsRootOverride,
            characterUid: failure == "character-uid" ? 0 : CharacterUid,
            seedPlayerItems: seedPlayerItems);

        Assert.True(result.Handled);
        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        Assert.Empty(result.Sent);
        AssertPlayerItemsEqual(initialItems, result.UpdatedSession.PlayerItems);
        if (result.PersistedItems is not null)
        {
            AssertPlayerItemsEqual(initialItems, result.PersistedItems);
        }

        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == RelicItemId);
        Assert.Contains($"reason={expectedReason}", result.Log);
        Assert.Contains("no token, reward, session, persistence, or packet mutation was committed", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        PlayerItemSnapshot initialItems,
        WorldDataCatalog? worldData,
        bool includeSessionStore = true,
        bool includeContextSession = true,
        bool registerSession = true,
        bool includePlayerItemsRoot = true,
        string? playerItemsRootOverride = null,
        long characterUid = CharacterUid,
        bool seedPlayerItems = true)
    {
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = WriteCharacterSnapshot(usersRoot, characterUid);
        var itemsRoot = playerItemsRootOverride ?? Path.Combine(_root, "PlayersItems");
        if (includePlayerItemsRoot && seedPlayerItems)
        {
            new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(characterUid, initialItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(initialItems, usersRoot, characterUid);
        var commitCount = 0;
        PlayerItemSnapshot? itemsAtCommit = null;
        WorldSessionStore? sessions = includeSessionStore
            ? new WorldSessionStore((_, previous, _) =>
            {
                if (previous is null)
                {
                    return;
                }

                commitCount++;
                if (includePlayerItemsRoot && Directory.Exists(itemsRoot))
                {
                    itemsAtCommit = new PlayerItemSnapshotLoader(itemsRoot)
                        .LoadByCharacterUid(characterUid);
                }
            })
            : null;
        if (sessions is not null && registerSession)
        {
            sessions.Set(connection, session);
        }

        var handled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: includeContextSession ? session : null,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: includePlayerItemsRoot ? itemsRoot : null),
            session,
            EventsInHourNpcId,
            RandomRelicOptionId,
            CancellationToken.None);

        var updatedSession = session;
        if (sessions?.TryGet(connection, out var storedSession) == true && storedSession is not null)
        {
            updatedSession = storedSession;
        }

        PlayerItemSnapshot? persistedItems = null;
        if (includePlayerItemsRoot &&
            Directory.Exists(itemsRoot) &&
            File.Exists(Path.Combine(
                itemsRoot,
                characterUid.ToString(CultureInfo.InvariantCulture) + ".bin")))
        {
            persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(characterUid);
        }

        return new InvokeResult(
            handled,
            session,
            updatedSession,
            commitCount,
            itemsAtCommit,
            persistedItems,
            CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath)),
            sent,
            log.ToString());
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot items,
        string usersRoot,
        long characterUid)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "hourly-random-relic-test",
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: "SaturnTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LegacyBoss10250RelicItemIndex"] = "0",
                    ["LegacyBoss10250RelicDurabilityRoll"] = "10",
                    ["LegacyBoss10250RelicAttributeCount"] = "3",
                    ["LegacyBoss10250RelicAttributeIndexes"] = "0,0,0",
                    ["LegacyBoss10250RelicAttributeValues"] = "200,100,900",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventory)
    {
        return new PlayerItemSnapshot(inventory, [], [], [], []);
    }

    private static PlayerItemSnapshotItem Token(uint uid, ushort stackSize, byte bound = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = ArrestTokenItemId,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
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

    private static void AssertExactFailureDialog(IReadOnlyList<byte[]> sent)
    {
        Assert.Equal(3, sent.Count);
        AssertNpcReply(
            sent[0],
            LegacyNpcReplyInteractType.Dialog,
            "You don't have enough Arrest Tokens!");
        AssertNpcReply(
            sent[1],
            LegacyNpcReplyInteractType.Option,
            "I understand.",
            optionId: byte.MaxValue);
        AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Finish, "");
    }

    private static void AssertTokenUpdate(byte[] bytes, ushort stackSize, byte bound)
    {
        AssertTokenUpdate(bytes, ArrestTokenUid, stackSize, bound);
    }

    private static void AssertTokenUpdate(byte[] bytes, uint uid, ushort stackSize, byte bound)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(ArrestTokenItemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        Assert.Equal(stackSize, packet.StackSize);
        Assert.Equal(bound, packet.Bound);
    }

    private static void AssertTokenRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType interactType,
        string text,
        byte optionId = 0)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(interactType, packet.InteractType);
        Assert.Equal(text, packet.Text);
        Assert.Equal(optionId, packet.OptionId);
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static string WriteCharacterSnapshot(string usersRoot, long characterUid)
    {
        var path = Path.Combine(usersRoot, AccountId + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{characterUid}}
            Body=1003
            Face=0
            Name=SaturnTester
            Class=7051
            Map=1002
            X=430
            Y=378
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=1002
            PMapX=430
            PMapY=378
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0

            [PaidNobility]
            Identifier=0
            LastNobilityDonation=0
            PaidRank=0
            PeriodTime=0
            IsActive=False
            Rank=Serf
            Position=-1
            """);
        return path;
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        bool includeItemType = true,
        bool includeRandomAttributes = true)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://hourly-random-relic",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: includeItemType ? [CreateItemType(RelicItemId, RelicName)] : [],
            itemAddBonuses: [],
            levelExperience: [],
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
            xuanbaoRandomAttributes: includeRandomAttributes ? CreateRelicRandomAttributes() : [],
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
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, string.Empty, 0, []),
                [],
                "fixture://activity"),
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
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                string.Empty,
                new Dictionary<string, string>()),
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

    private static ItemType CreateItemType(uint itemId, string name)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        return new ItemType(checked((int)itemId), name, fields);
    }

    private static IReadOnlyList<XuanbaoRandomAttribute> CreateRelicRandomAttributes()
    {
        return
        [
            CreateRelicRandomAttribute(LegacyChiAttributeType.Immunity, min: 110, max: 201),
            CreateRelicRandomAttribute(LegacyChiAttributeType.Breakthrough, min: 30, max: 101),
            CreateRelicRandomAttribute(LegacyChiAttributeType.AddMagicAttack, min: 600, max: 901),
        ];
    }

    private static XuanbaoRandomAttribute CreateRelicRandomAttribute(
        LegacyChiAttributeType attributeType,
        int min,
        int max)
    {
        var raw = FormattableString.Invariant($"{RelicItemId}@@{(byte)attributeType}@@{min}@@{max}@@");
        return new XuanbaoRandomAttribute(
            checked((int)RelicItemId),
            (byte)attributeType,
            min,
            max,
            "fixture://xuanbao_rand_attr.txt",
            checked((int)RelicItemId),
            raw,
            raw.Split("@@"));
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData(
            "fixture://" + name,
            [],
            [],
            [],
            info: null,
            pole: null,
            conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        int CommitCount,
        PlayerItemSnapshot? ItemsAtCommit,
        PlayerItemSnapshot? PersistedItems,
        CharacterSnapshot PersistedCharacter,
        IReadOnlyList<byte[]> Sent,
        string Log);
}
