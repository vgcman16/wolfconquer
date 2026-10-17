using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRewardPointsExchangeTests : IDisposable
{
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;
    private const uint MysteryFruitItemId = 3_001_044;
    private const uint LifeFruitBasketItemId = 723_725;

    private static readonly DateTime TestNow = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Unspecified);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyRewardPointsExchangeTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public async Task OptionsCheckSourceHaveSpaceGateBeforePoints(byte optionId)
    {
        var inventory = Enumerable.Range(1, 39)
            .Select(index => InventoryItem(checked((uint)index), checked((uint)(400_000 + index))))
            .Append(InventoryItem(40, LifeFruitBasketItemId, stackSize: 2))
            .ToArray();
        var originalItems = Items(inventory);
        var result = await InvokeAsync(
            optionId,
            rewardPoints: 0,
            originalItems,
            CreateWorldDataCatalog(
                ItemType(MysteryFruitItemId, "MysteryFruit", stackLimit: 1),
                ItemType(LifeFruitBasketItemId, "LifeFruitBasket", stackLimit: 10)));

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        var messageBox = AssertNpcReply(Assert.Single(result.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal("Please make 1 more space in your inventory.", messageBox.Text);
        Assert.Contains("reason=inventoryFull", result.Log);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        AssertPersistedItems(result, originalItems);
    }

    [Theory]
    [InlineData((byte)1, 0UL, 1UL)]
    [InlineData((byte)2, 9UL, 10UL)]
    [InlineData((byte)3, 4UL, 5UL)]
    public async Task OptionsCheckPointsAfterSpaceWithoutMutation(
        byte optionId,
        ulong rewardPoints,
        ulong expectedCost)
    {
        var originalItems = Items(InventoryItem(10, 400_010));
        var result = await InvokeAsync(
            optionId,
            rewardPoints,
            originalItems,
            CreateWorldDataCatalog(
                ItemType(MysteryFruitItemId, "MysteryFruit", stackLimit: 1),
                ItemType(LifeFruitBasketItemId, "LifeFruitBasket", stackLimit: 10)));

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Equal(4, result.Sent.Count);

        var dialog = AssertNpcReply(result.Sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Contains($"You need {expectedCost.ToString(CultureInfo.InvariantCulture)} reward point(s).", dialog.Text);
        var option = AssertNpcReply(result.Sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal("Thanks!", option.Text);
        var avatar = AssertNpcReply(result.Sent[2]);
        Assert.Equal(LegacyNpcReplyInteractType.Avatar, avatar.InteractType);
        Assert.Equal((ushort)166, avatar.InputMaxLength);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, AssertNpcReply(result.Sent[3]).InteractType);

        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        AssertPersistedItems(result, originalItems);
    }

    [Fact]
    public async Task Option1SpendsOneAndDurablyGrantsUnboundMysteryFruit()
    {
        var originalItems = Items(InventoryItem(10, 400_010));
        var result = await InvokeAsync(
            optionId: 1,
            rewardPoints: 3,
            originalItems,
            CreateWorldDataCatalog(ItemType(MysteryFruitItemId, "MysteryFruit", stackLimit: 1)));

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal("2", result.UpdatedSession.Character.Attributes["RewardPoints"]);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == MysteryFruitItemId);
        Assert.Equal((uint)11, reward.Uid);
        Assert.Equal((byte)0, reward.Bound);
        Assert.Equal((ushort)1, reward.StackSize);
        Assert.InRange(reward.Color, 3U, 8U);

        var grant = AssertGameItem(Assert.Single(result.Sent));
        Assert.Equal(MysteryFruitItemId, grant.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, grant.Mode);
        Assert.Equal((byte)0, grant.Bound);
        Assert.Equal((ushort)1, grant.StackSize);

        var reloadedCharacter = ReloadCharacter(result.CharacterPath);
        Assert.Equal("2", reloadedCharacter.Attributes["RewardPoints"]);
        AssertPersistedItems(result, result.UpdatedSession.PlayerItems);
        Assert.Contains("itemMode=add", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc RewardPoints", result.Log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Option2UsesSourceExpiryBaseAndVipPacketOrder(bool activeVip)
    {
        var originalItems = Items(InventoryItem(10, 400_010));
        var originalExpiry = activeVip ? TestNow.AddHours(2) : TestNow.AddMinutes(-1);
        var expectedExpiry = (activeVip ? originalExpiry : TestNow).AddHours(1);
        var result = await InvokeAsync(
            optionId: 2,
            rewardPoints: 20,
            originalItems,
            CreateWorldDataCatalog(),
            vipLevel: activeVip ? 4UL : 0UL,
            vipExpiry: originalExpiry);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal("10", result.UpdatedSession.Character.Attributes["RewardPoints"]);
        Assert.Equal("6", result.UpdatedSession.Character.Attributes["VipLevel"]);
        Assert.Equal(
            expectedExpiry,
            DateTime.FromBinary(long.Parse(
                result.UpdatedSession.Character.Attributes["VipTime"],
                CultureInfo.InvariantCulture)));
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);

        Assert.Equal(4, result.Sent.Count);
        AssertVipLevelUpdate(result.Sent[0], 6);
        AssertVipLevelUpdate(result.Sent[1], 6);
        Assert.True(LegacyVipStatusPacket.TryParse(ReadFrame(result.Sent[2]), out var vipStatus));
        Assert.Equal(LegacyVipStatusFlags.FullVip, vipStatus.Flags);
        var messageBox = AssertNpcReply(result.Sent[3]);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal("You`ve received VIP6 (1 Hours).", messageBox.Text);

        var reloadedCharacter = ReloadCharacter(result.CharacterPath);
        Assert.Equal("10", reloadedCharacter.Attributes["RewardPoints"]);
        Assert.Equal("6", reloadedCharacter.Attributes["VipLevel"]);
        Assert.Equal(
            expectedExpiry,
            DateTime.FromBinary(long.Parse(reloadedCharacter.Attributes["VipTime"], CultureInfo.InvariantCulture)));
        AssertPersistedItems(result, originalItems);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc RewardPoints", result.Log);
    }

    [Fact]
    public async Task Option3StacksUnboundBasketThenSendsSourceMessage()
    {
        var originalItems = Items(
            InventoryItem(10, LifeFruitBasketItemId, stackSize: 3),
            InventoryItem(11, LifeFruitBasketItemId, stackSize: 2, bound: 1),
            InventoryItem(12, 400_012));
        var result = await InvokeAsync(
            optionId: 3,
            rewardPoints: 9,
            originalItems,
            CreateWorldDataCatalog(ItemType(LifeFruitBasketItemId, "LifeFruitBasket", stackLimit: 10)));

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal("4", result.UpdatedSession.Character.Attributes["RewardPoints"]);
        var unbound = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == LifeFruitBasketItemId && item.Bound == 0);
        Assert.Equal((uint)10, unbound.Uid);
        Assert.Equal((ushort)4, unbound.StackSize);
        var bound = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == LifeFruitBasketItemId && item.Bound == 1);
        Assert.Equal((ushort)2, bound.StackSize);

        Assert.Equal(2, result.Sent.Count);
        var stackUpdate = AssertGameItem(result.Sent[0]);
        Assert.Equal((uint)10, stackUpdate.Uid);
        Assert.Equal(LifeFruitBasketItemId, stackUpdate.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, stackUpdate.Mode);
        Assert.Equal((byte)0, stackUpdate.Bound);
        Assert.Equal((ushort)4, stackUpdate.StackSize);
        var messageBox = AssertNpcReply(result.Sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal("You`ve received (LifeFruitBasket).", messageBox.Text);

        Assert.Equal("4", ReloadCharacter(result.CharacterPath).Attributes["RewardPoints"]);
        AssertPersistedItems(result, result.UpdatedSession.PlayerItems);
        Assert.Contains("itemMode=update", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc RewardPoints", result.Log);
    }

    [Theory]
    [InlineData("sessions", "sessionStoreMissing")]
    [InlineData("users-root", "usersRootMissing")]
    [InlineData("items-root", "playerItemsRootMissing")]
    public async Task MissingCommitWiringFencesBeforeMutation(string missing, string expectedReason)
    {
        var originalItems = Items(InventoryItem(10, 400_010));
        var result = await InvokeAsync(
            optionId: 1,
            rewardPoints: 3,
            originalItems,
            CreateWorldDataCatalog(ItemType(MysteryFruitItemId, "MysteryFruit", stackLimit: 1)),
            includeSessionStore: missing != "sessions",
            includeUsersRoot: missing != "users-root",
            includePlayerItemsRoot: missing != "items-root");

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Contains($"reason={expectedReason}", result.Log);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        AssertPersistedItems(result, originalItems);
    }

    [Theory]
    [InlineData((byte)1, 1UL)]
    [InlineData((byte)3, 5UL)]
    public async Task MissingItemTypeFencesWithoutSpending(byte optionId, ulong cost)
    {
        var originalItems = Items(InventoryItem(10, 400_010));
        var result = await InvokeAsync(
            optionId,
            rewardPoints: cost,
            originalItems,
            CreateWorldDataCatalog());

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=itemtypeMissing", result.Log);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        AssertPersistedItems(result, originalItems);
    }

    [Theory]
    [InlineData((byte)1, 1UL)]
    [InlineData((byte)3, 5UL)]
    public async Task ItemUidExhaustionFencesWithoutSpending(byte optionId, ulong cost)
    {
        var originalItems = Items(InventoryItem(uint.MaxValue, 400_010));
        var result = await InvokeAsync(
            optionId,
            rewardPoints: cost,
            originalItems,
            CreateWorldDataCatalog(
                ItemType(MysteryFruitItemId, "MysteryFruit", stackLimit: 1),
                ItemType(LifeFruitBasketItemId, "LifeFruitBasket", stackLimit: 10)));

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=itemUidExhausted", result.Log);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        AssertPersistedItems(result, originalItems);
    }

    [Fact]
    public async Task SecondSnapshotWriteFailureRollsBackCharacterBeforeCommit()
    {
        var result = await InvokeAsync(
            optionId: 1,
            rewardPoints: 3,
            PlayerItemSnapshot.Empty,
            CreateWorldDataCatalog(ItemType(MysteryFruitItemId, "MysteryFruit", stackLimit: 1)),
            blockItemDestinationWithDirectory: true);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Equal("3", result.UpdatedSession.Character.Attributes["RewardPoints"]);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Contains("reason=transactionWriteFailed", result.Log);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal("3", ReloadCharacter(result.CharacterPath).Attributes["RewardPoints"]);
        Assert.True(Directory.Exists(result.PlayerItemsPath));
        AssertPersistedItems(result, PlayerItemSnapshot.Empty);
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
        byte optionId,
        ulong rewardPoints,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog worldData,
        ulong vipLevel = 0,
        DateTime? vipExpiry = null,
        bool includeSessionStore = true,
        bool includeUsersRoot = true,
        bool includePlayerItemsRoot = true,
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

        var vipTime = (vipExpiry ?? default).Ticks.ToString(CultureInfo.InvariantCulture);
        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        File.WriteAllText(
            characterPath,
            $"[Character]{Environment.NewLine}" +
            $"UID={CharacterUid}{Environment.NewLine}" +
            $"Body=1003{Environment.NewLine}" +
            $"Face=0{Environment.NewLine}" +
            $"Name=RewardPointHero{Environment.NewLine}" +
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
            $"RewardPoints={rewardPoints.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}" +
            $"VipLevel={vipLevel.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}" +
            $"VipTime={vipTime}{Environment.NewLine}");
        var originalCharacterBytes = File.ReadAllBytes(characterPath);

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            rewardPoints,
            vipLevel,
            vipTime,
            originalItems,
            includeUsersRoot ? usersRoot : null);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var handled = await WorldPacketHandlers.TryHandleLegacyRewardPointsAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: includePlayerItemsRoot ? playerItemsRoot : null),
            session,
            optionId,
            TestNow,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            handled,
            sent,
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
        ulong rewardPoints,
        ulong vipLevel,
        string vipTime,
        PlayerItemSnapshot playerItems,
        string? usersRoot)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RewardPoints"] = rewardPoints.ToString(CultureInfo.InvariantCulture),
            ["VipLevel"] = vipLevel.ToString(CultureInfo.InvariantCulture),
            ["VipTime"] = vipTime,
        };

        return new WorldSession(
            AccountId,
            Username: "legacy-reward-points-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "RewardPointHero",
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
                Attributes: attributes,
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

    private static PlayerItemSnapshotItem InventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
            Color = 5,
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

    private static CharacterSnapshot ReloadCharacter(string path)
    {
        return CharacterSnapshotParser.ParseText(File.ReadAllText(path), path);
    }

    private static void AssertPersistedItems(InvokeResult result, PlayerItemSnapshot expected)
    {
        var persisted = new PlayerItemSnapshotLoader(result.PlayerItemsRoot)
            .LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(expected, persisted);
    }

    private static void AssertVipLevelUpdate(byte[] bytes, ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.VIPLevel, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
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

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://reward-points-exchange",
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

    private static ItemType ItemType(uint itemTypeId, string name, ushort stackLimit)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] = "1";
        fields[46] = stackLimit.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        string UsersRoot,
        string PlayerItemsRoot,
        string PlayerItemsPath,
        string CharacterPath,
        byte[] OriginalCharacterBytes);
}
