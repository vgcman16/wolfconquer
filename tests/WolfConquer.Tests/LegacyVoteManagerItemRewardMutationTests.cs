using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVoteManagerItemRewardMutationTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;
    private const uint VoteManagerNpcId = 52_300;
    private const ushort CatalogDurability = 321;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyVoteManagerItemRewardMutationTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ulong, uint> RewardCases => new()
    {
        { 43, 10, 4_200_013 },
        { 44, 2, 3_321_806 },
        { 45, 3, 729_463 },
        { 46, 2, 3_307_212 },
    };

    [Theory]
    [MemberData(nameof(RewardCases))]
    public async Task ItemRewardOptionsPersistBeforeExposureAndSendOnlyAddItem(
        byte optionId,
        ulong cost,
        uint rewardItemId)
    {
        var originalItems = Items(InventoryItem(41, 400_041));
        var result = await InvokeAsync(
            optionId,
            votePoints: cost + 7,
            originalItems,
            CreateWorldDataCatalog(ItemType(rewardItemId)));

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(7UL, VotePoints(result.UpdatedSession.Character));

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(42U, reward.Uid);
        AssertRewardItem(reward, rewardItemId);

        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(7UL, VotePoints(result.PersistedAtCommit!.Character));
        var committedReward = Assert.Single(
            result.PersistedAtCommit.Items.InventoryItems,
            item => item.ItemId == rewardItemId);
        AssertRewardItem(committedReward, rewardItemId);
        Assert.NotNull(result.ReloadedCharacter);
        Assert.Equal(7UL, VotePoints(result.ReloadedCharacter!));
        Assert.NotNull(result.PersistedItems);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, result.PersistedItems!);

        Assert.Equal(["commit", "packet"], result.ExposureOrder);
        var frame = ReadFrame(Assert.Single(result.Sent));
        Assert.Equal(LegacyGameItemPacket.Opcode, frame.Header.Opcode);
        Assert.False(LegacyMsgUpdatePacket.TryParse(frame, out _));
        Assert.True(LegacyGameItemPacket.TryParse(frame, out var packet));
        Assert.Equal(reward.Uid, packet.Uid);
        Assert.Equal(rewardItemId, packet.ItemId);
        Assert.Equal(CatalogDurability, packet.Durability);
        Assert.Equal(CatalogDurability, packet.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal((byte)0, packet.Position);
        Assert.Equal(0U, packet.SocketProgress);
        Assert.Equal((byte)0, packet.SocketOne);
        Assert.Equal((byte)0, packet.SocketTwo);
        Assert.Equal(0U, packet.SpellId);
        Assert.Equal((byte)0, packet.Effect);
        Assert.Equal((byte)0, packet.Plus);
        Assert.Equal((byte)0, packet.Bless);
        Assert.Equal((byte)0, packet.Bound);
        Assert.Equal((byte)0, packet.Enchant);
        Assert.Equal((ushort)1, packet.StackSize);
        Assert.Contains("packetOrder=GameItemAdd", result.Log);
    }

    [Theory]
    [MemberData(nameof(RewardCases))]
    public async Task InsufficientVotePointsIsSilentAndMutationFree(
        byte optionId,
        ulong cost,
        uint rewardItemId)
    {
        var originalItems = Items(InventoryItem(41, 400_041));
        var result = await InvokeAsync(
            optionId,
            votePoints: cost - 1,
            originalItems,
            CreateWorldDataCatalog(ItemType(rewardItemId)));

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(cost - 1, VotePoints(result.UpdatedSession.Character));
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.ExposureOrder);
        Assert.Empty(result.Sent);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.NotNull(result.PersistedItems);
        AssertPlayerItemsEqual(originalItems, result.PersistedItems!);
        Assert.DoesNotContain("VoteManager item reward", result.Log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FullInventoryPersistsDebitOnlyAndSendsNothing()
    {
        var originalItems = Items(Enumerable.Range(1, 40)
            .Select(index => InventoryItem(checked((uint)index), checked((uint)(400_000 + index))))
            .ToArray());
        var result = await InvokeAsync(
            optionId: 43,
            votePoints: 15,
            originalItems,
            CreateWorldDataCatalog(ItemType(4_200_013)));

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(5UL, VotePoints(result.UpdatedSession.Character));
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(5UL, VotePoints(result.PersistedAtCommit!.Character));
        AssertPlayerItemsEqual(originalItems, result.PersistedAtCommit.Items);
        Assert.NotNull(result.PersistedItems);
        AssertPlayerItemsEqual(originalItems, result.PersistedItems!);
        Assert.Equal(result.OriginalItemBytes, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Equal(["commit"], result.ExposureOrder);
        Assert.Empty(result.Sent);
        Assert.Contains("inventoryFull=True packetOrder=none", result.Log);
    }

    [Fact]
    public async Task CombinedPersistenceFailureRollsBackDebitAndExposesNothing()
    {
        var result = await InvokeAsync(
            optionId: 44,
            votePoints: 2,
            PlayerItemSnapshot.Empty,
            CreateWorldDataCatalog(ItemType(3_321_806)),
            blockItemDestinationWithDirectory: true);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(2UL, VotePoints(result.UpdatedSession.Character));
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.ExposureOrder);
        Assert.Empty(result.Sent);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.NotNull(result.ReloadedCharacter);
        Assert.Equal(2UL, VotePoints(result.ReloadedCharacter!));
        Assert.True(Directory.Exists(result.PlayerItemsPath));
        Assert.Null(result.PersistedItems);
        Assert.Contains("reason=transactionWriteFailed", result.Log);
        Assert.Contains("no session mutation or packets were committed", result.Log);
        Assert.Empty(Directory.EnumerateFiles(result.UsersRoot, "*.reward-points-*"));
        Assert.Empty(Directory.EnumerateFiles(result.PlayerItemsRoot, "*.reward-points-*"));
    }

    [Fact]
    public async Task MissingMetadataUsesDocumentedSafetyFenceWithoutDebit()
    {
        var originalItems = Items(InventoryItem(41, 400_041));
        var result = await InvokeAsync(
            optionId: 45,
            votePoints: 3,
            originalItems,
            CreateWorldDataCatalog());

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(3UL, VotePoints(result.UpdatedSession.Character));
        Assert.Empty(result.Sent);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Contains("reason=itemtype-missing", result.Log);
        Assert.Contains("safetyDifference=", result.Log);
    }

    [Fact]
    public async Task ItemUidExhaustionUsesDocumentedSafetyFenceWithoutDebit()
    {
        var originalItems = Items(InventoryItem(uint.MaxValue, 400_041));
        var result = await InvokeAsync(
            optionId: 46,
            votePoints: 2,
            originalItems,
            CreateWorldDataCatalog(ItemType(3_307_212)));

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(2UL, VotePoints(result.UpdatedSession.Character));
        Assert.Empty(result.Sent);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Contains("reason=item-uid-exhausted", result.Log);
        Assert.Contains("safetyDifference=", result.Log);
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
        ulong votePoints,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog worldData,
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
        WriteCharacterSnapshot(characterPath, votePoints);
        var originalCharacterBytes = File.ReadAllBytes(characterPath);
        var originalItemBytes = File.Exists(playerItemsPath) ? File.ReadAllBytes(playerItemsPath) : null;

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var exposureOrder = new List<string>();
        var connection = CreateSendingConnection(sent, exposureOrder);
        var session = CreateSession(votePoints, originalItems, usersRoot);
        var characterLoader = new CharacterSnapshotLoader(usersRoot);
        var itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
        PersistedState? persistedAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is not null && !ReferenceEquals(previous, current))
            {
                persistedAtCommit = new PersistedState(
                    Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
                    itemLoader.LoadByCharacterUid(CharacterUid));
                exposureOrder.Add("commit");
            }
        });
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            VoteManagerNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var reloadedCharacter = File.Exists(characterPath)
            ? characterLoader.LoadByAccountId(AccountId)
            : null;
        var persistedItems = File.Exists(playerItemsPath)
            ? itemLoader.LoadByCharacterUid(CharacterUid)
            : null;
        return new InvokeResult(
            handled,
            session,
            updatedSession,
            persistedAtCommit,
            reloadedCharacter,
            persistedItems,
            sent,
            exposureOrder,
            log.ToString(),
            usersRoot,
            playerItemsRoot,
            playerItemsPath,
            characterPath,
            originalCharacterBytes,
            originalItemBytes);
    }

    private static void WriteCharacterSnapshot(string path, ulong votePoints)
    {
        File.WriteAllText(
            path,
            $"[Character]{Environment.NewLine}" +
            $"UID={CharacterUid}{Environment.NewLine}" +
            $"Body=1003{Environment.NewLine}" +
            $"Face=0{Environment.NewLine}" +
            $"Name=VoteItemRewardHero{Environment.NewLine}" +
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
            $"ConquerPoints=0{Environment.NewLine}" +
            $"BankCps=0{Environment.NewLine}" +
            $"BoundConquerPoints=0{Environment.NewLine}" +
            $"Money=0{Environment.NewLine}" +
            $"PkPoints=0{Environment.NewLine}" +
            $"Online=0{Environment.NewLine}" +
            $"VotePoints={votePoints.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}" +
            $"Sentinel=keep-me{Environment.NewLine}");
    }

    private static WorldSession CreateSession(
        ulong votePoints,
        PlayerItemSnapshot playerItems,
        string usersRoot)
    {
        return new WorldSession(
            AccountId,
            Username: "legacy-vote-item-reward-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "VoteItemRewardHero",
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["VotePoints"] = votePoints.ToString(CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
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
            Color = 5,
        };
    }

    private static ulong VotePoints(CharacterSnapshot character)
    {
        return character.Attributes.TryGetValue("VotePoints", out var raw) &&
            ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
    }

    private static void AssertRewardItem(PlayerItemSnapshotItem item, uint expectedItemId)
    {
        Assert.Equal(expectedItemId, item.ItemId);
        Assert.Equal(CatalogDurability, item.Durability);
        Assert.Equal(CatalogDurability, item.MaximumDurability);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Equal((byte)0, item.Bound);
        Assert.Equal((byte)0, item.Plus);
        Assert.Equal((byte)0, item.Bless);
        Assert.Equal((byte)0, item.Enchant);
        Assert.Equal((byte)0, item.SocketOne);
        Assert.Equal((byte)0, item.SocketTwo);
        Assert.Equal(0U, item.SocketProgress);
        Assert.Equal(0U, item.Effect);
        Assert.InRange(item.Color, 3U, 8U);
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static ConquerTcpConnection CreateSendingConnection(
        List<byte[]> sent,
        List<string> exposureOrder)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9_958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            exposureOrder.Add("packet");
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://vote-manager-item-rewards",
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

    private static ItemType ItemType(uint itemTypeId)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "VoteReward";
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] =
            CatalogDurability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), "VoteReward", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record PersistedState(CharacterSnapshot Character, PlayerItemSnapshot Items);

    private sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        PersistedState? PersistedAtCommit,
        CharacterSnapshot? ReloadedCharacter,
        PlayerItemSnapshot? PersistedItems,
        IReadOnlyList<byte[]> Sent,
        IReadOnlyList<string> ExposureOrder,
        string Log,
        string UsersRoot,
        string PlayerItemsRoot,
        string PlayerItemsPath,
        string CharacterPath,
        byte[] OriginalCharacterBytes,
        byte[]? OriginalItemBytes);
}
