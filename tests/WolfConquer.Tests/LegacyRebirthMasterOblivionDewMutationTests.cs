using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRebirthMasterOblivionDewMutationTests : IDisposable
{
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_800;
    private const uint RebirthMasterNpcId = 9_072;
    private const byte OblivionDewOptionId = 67;
    private const uint OblivionDewItemId = 711_083;
    private const ushort OblivionDewDurability = 321;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyRebirthMasterOblivionDewMutationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PurchasePersistsDebitAndGrantBeforeCpThenItemPackets()
    {
        var originalItems = Items(InventoryItem(10, 700_010));
        var result = await InvokeAsync(conquerPoints: 2, originalItems);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(1, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal("1", result.UpdatedSession.Character.Attributes["ConquerPoints"]);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == OblivionDewItemId);
        Assert.Equal(11U, reward.Uid);
        Assert.Equal(OblivionDewDurability, reward.Durability);
        Assert.Equal(OblivionDewDurability, reward.MaximumDurability);
        Assert.Equal((ushort)0, reward.Position);
        Assert.Equal((ushort)1, reward.StackSize);
        Assert.Equal((byte)0, reward.Bound);
        Assert.Equal((byte)0, reward.Plus);
        Assert.Equal((byte)0, reward.Bless);
        Assert.Equal((byte)0, reward.Enchant);
        Assert.InRange(reward.Color, 3U, 8U);

        Assert.Equal(2, result.Sent.Count);
        AssertConquerPointsUpdate(result.Sent[0], expectedValue: 1);
        var itemPacket = AssertGameItem(result.Sent[1]);
        Assert.Equal(reward.Uid, itemPacket.Uid);
        Assert.Equal(OblivionDewItemId, itemPacket.ItemId);
        Assert.Equal(OblivionDewDurability, itemPacket.Durability);
        Assert.Equal(OblivionDewDurability, itemPacket.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Mode);
        Assert.Equal((byte)0, itemPacket.Position);
        Assert.Equal((ushort)1, itemPacket.StackSize);
        Assert.Equal((byte)0, itemPacket.Bound);
        Assert.Equal((byte)0, itemPacket.Plus);
        Assert.Equal((byte)0, itemPacket.Bless);
        Assert.Equal((byte)0, itemPacket.Enchant);

        Assert.Equal(2, result.Exposures.Count);
        Assert.All(result.Exposures, exposure =>
        {
            Assert.Equal(1, exposure.Session.Character.ConquerPoints);
            Assert.Equal(1, exposure.PersistedCharacter.ConquerPoints);
            AssertItemsEqual(result.UpdatedSession.PlayerItems, exposure.PersistedItems);
        });
        Assert.Equal(1, ReloadCharacter(result.CharacterPath).ConquerPoints);
        AssertPersistedItems(result, result.UpdatedSession.PlayerItems);
        Assert.Contains("sourceLines=40405-40419", result.Log);
        Assert.Contains("sourceOrder=conquer-points+add-item", result.Log);
        Assert.Contains("no-success-dialog=true", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc JailGuildServices RebirthMaster OblivionDew", result.Log);
    }

    [Fact]
    public async Task ZeroCpSendsExactSourceMessageBoxAndMutatesNothing()
    {
        var originalItems = Items(InventoryItem(10, 700_010));
        var result = await InvokeAsync(conquerPoints: 0, originalItems);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        AssertItemsEqual(originalItems, result.UpdatedSession.PlayerItems);

        var reply = AssertNpcReply(Assert.Single(result.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("sorry you not have enough ConquerPoints  .", reply.Text);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        AssertPersistedItems(result, originalItems);
        var exposure = Assert.Single(result.Exposures);
        Assert.Same(result.OriginalSession, exposure.Session);
        Assert.Equal(0, exposure.PersistedCharacter.ConquerPoints);
        AssertItemsEqual(originalItems, exposure.PersistedItems);
    }

    [Fact]
    public async Task FullFortySlotInventoryPersistsDebitAndSendsOnlyCpUpdate()
    {
        var originalItems = Items(
            Enumerable.Range(1, 40)
                .Select(index => InventoryItem(
                    checked((uint)index),
                    checked((uint)(700_000 + index))))
                .ToArray());
        var result = await InvokeAsync(conquerPoints: 10, originalItems);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(9, result.UpdatedSession.Character.ConquerPoints);
        AssertItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        AssertConquerPointsUpdate(Assert.Single(result.Sent), expectedValue: 9);
        Assert.Equal(9, ReloadCharacter(result.CharacterPath).ConquerPoints);
        AssertPersistedItems(result, originalItems);

        var exposure = Assert.Single(result.Exposures);
        Assert.Equal(9, exposure.Session.Character.ConquerPoints);
        Assert.Equal(9, exposure.PersistedCharacter.ConquerPoints);
        AssertItemsEqual(originalItems, exposure.PersistedItems);
        Assert.Contains("itemGranted=False", result.Log);
        Assert.Contains("no-success-dialog=true", result.Log);
    }

    [Fact]
    public async Task ItemCommitFailureRollsBackCharacterAndExposesNoPartialSuccess()
    {
        var result = await InvokeAsync(
            conquerPoints: 10,
            PlayerItemSnapshot.Empty,
            blockItemDestinationWithDirectory: true);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(10, result.UpdatedSession.Character.ConquerPoints);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Empty(result.Sent);
        Assert.Empty(result.Exposures);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(10, ReloadCharacter(result.CharacterPath).ConquerPoints);
        Assert.True(Directory.Exists(result.PlayerItemsPath));
        Assert.Contains("reason=transactionWriteFailed", result.Log);
        Assert.Empty(Directory.EnumerateFiles(result.UsersRoot, "*.reward-points-*"));
        Assert.Empty(Directory.EnumerateFiles(result.PlayerItemsRoot, "*.reward-points-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        long conquerPoints,
        PlayerItemSnapshot originalItems,
        bool blockItemDestinationWithDirectory = false)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        var playerItemsRoot = Path.Combine(invocationRoot, "PlayersItems");
        var playerItemsPath = Path.Combine(
            playerItemsRoot,
            CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        Directory.CreateDirectory(usersRoot);

        if (blockItemDestinationWithDirectory)
        {
            Directory.CreateDirectory(playerItemsPath);
        }
        else
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        File.WriteAllText(characterPath, CreateCharacterText(conquerPoints));
        var originalCharacterBytes = File.ReadAllBytes(characterPath);

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var exposures = new List<ExposureSnapshot>();
        var sessions = new WorldSessionStore();
        ConquerTcpConnection? connection = null;
        connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                if (!sessions.TryGet(connection!, out var visibleSession) || visibleSession is null)
                {
                    throw new InvalidOperationException("Session was not available when a packet was exposed.");
                }

                exposures.Add(
                    new ExposureSnapshot(
                        visibleSession,
                        ReloadCharacter(characterPath),
                        new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid)));
                return ValueTask.CompletedTask;
            });

        var session = CreateSession(conquerPoints, originalItems, usersRoot);
        sessions.Set(connection, session);
        var handled = await WorldPacketHandlers.TryHandleLegacyJailGuildServicesNpcAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: session,
                    WorldData: CreateWorldDataCatalog(CreateOblivionDewItemType()),
                    Sessions: sessions,
                    PlayerItemsRoot: playerItemsRoot),
                session,
                RebirthMasterNpcId,
                OblivionDewOptionId,
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            handled,
            sent,
            exposures,
            log.ToString(),
            session,
            updatedSession,
            usersRoot,
            playerItemsRoot,
            playerItemsPath,
            characterPath,
            originalCharacterBytes);
    }

    private static WorldSession CreateSession(
        long conquerPoints,
        PlayerItemSnapshot playerItems,
        string usersRoot)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
        };

        return new WorldSession(
            AccountId,
            Username: "legacy-rebirth-master-oblivion-dew-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "RebirthHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
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
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static string CreateCharacterText(long conquerPoints)
    {
        return
            $"[Character]{Environment.NewLine}" +
            $"UID={CharacterUid}{Environment.NewLine}" +
            $"Body=1003{Environment.NewLine}" +
            $"Face=0{Environment.NewLine}" +
            $"Name=RebirthHero{Environment.NewLine}" +
            $"Class=7051{Environment.NewLine}" +
            $"Map=1002{Environment.NewLine}" +
            $"X=430{Environment.NewLine}" +
            $"Y=378{Environment.NewLine}" +
            $"FirstClass=7051{Environment.NewLine}" +
            $"SecoundeClass=0{Environment.NewLine}" +
            $"Reborn=0{Environment.NewLine}" +
            $"Level=140{Environment.NewLine}" +
            $"Haire=0{Environment.NewLine}" +
            $"Experience=0{Environment.NewLine}" +
            $"PMap=1002{Environment.NewLine}" +
            $"PMapX=430{Environment.NewLine}" +
            $"PMapY=378{Environment.NewLine}" +
            $"Agility=0{Environment.NewLine}" +
            $"Strength=0{Environment.NewLine}" +
            $"Vitaliti=0{Environment.NewLine}" +
            $"Spirit=0{Environment.NewLine}" +
            $"Atributes=0{Environment.NewLine}" +
            $"MinHitPoints=1{Environment.NewLine}" +
            $"MinMana=0{Environment.NewLine}" +
            $"ConquerPoints={conquerPoints.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}" +
            $"BankCps=0{Environment.NewLine}" +
            $"BoundConquerPoints=0{Environment.NewLine}" +
            $"Money=0{Environment.NewLine}" +
            $"PkPoints=0{Environment.NewLine}" +
            $"Online=0{Environment.NewLine}";
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem InventoryItem(uint uid, uint itemId)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 1,
            Bound = 0,
            Color = 5,
        };
    }

    private static ItemType CreateOblivionDewItemType()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = OblivionDewItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OblivionDew";
        fields[ItemType.DurabilityFieldIndex] = OblivionDewDurability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)OblivionDewItemId), "OblivionDew", fields);
    }

    private static void AssertConquerPointsUpdate(byte[] bytes, ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal((uint)CharacterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot ReloadCharacter(string path)
    {
        return CharacterSnapshotParser.ParseText(File.ReadAllText(path), path);
    }

    private static void AssertPersistedItems(InvokeResult result, PlayerItemSnapshot expected)
    {
        AssertItemsEqual(
            expected,
            new PlayerItemSnapshotLoader(result.PlayerItemsRoot).LoadByCharacterUid(CharacterUid));
    }

    private static void AssertItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://rebirth-master-oblivion-dew",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record ExposureSnapshot(
        WorldSession Session,
        CharacterSnapshot PersistedCharacter,
        PlayerItemSnapshot PersistedItems);

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        IReadOnlyList<ExposureSnapshot> Exposures,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        string UsersRoot,
        string PlayerItemsRoot,
        string PlayerItemsPath,
        string CharacterPath,
        byte[] OriginalCharacterBytes);
}
