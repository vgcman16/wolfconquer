using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyArthurVipQuestPersistenceTests : IDisposable
{
    private const uint ArthurNpcId = 3_601;
    private const uint MossItemId = 722_723;
    private const uint SoulAromaItemId = 722_724;
    private const uint DreamGrassItemId = 722_725;
    private const uint GhostHornItemId = 722_722;
    private const uint EvilToothItemId = 722_721;
    private const uint ImpureVigorItemId = 722_730;
    private const uint VipTokenItemId = 780_001;
    private const long CharacterUid = 1_293_600;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Registry =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyArthurVipQuestPersistenceTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(
        (byte)1,
        GhostHornItemId,
        "You successfully exchange the (Moss,SoulAroma and DreamGras) for an GhostHorn!",
        false)]
    [InlineData(
        (byte)2,
        EvilToothItemId,
        "You successfully exchange the GhostHorn for an EvilTooth!",
        false)]
    [InlineData(
        (byte)3,
        ImpureVigorItemId,
        "You successfully exchange the EvilTooth for an ImpureVigor!",
        false)]
    [InlineData((byte)4, VipTokenItemId, null, true)]
    public async Task AllFourExchangeChainsPersistBeforeCommitAndPreserveSourcePacketOrder(
        byte optionId,
        uint rewardItemId,
        string? successText,
        bool broadcastsCompletion)
    {
        var costs = GetCosts(optionId);
        var originalItems = CreatePlayerItems(
            costs.Select((cost, index) => CreateInventoryItem(
                    checked((uint)(10 + index * 10)),
                    cost.ItemId,
                    cost.Count))
                .ToArray());
        var playerItemsRoot = CreatePlayerItemsRoot();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(rewardItemId, "ArthurReward", durability: 321)),
            playerItemsRoot,
            optionId);

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        foreach (var cost in costs)
        {
            Assert.DoesNotContain(
                result.UpdatedSession.PlayerItems.InventoryItems,
                item => item.ItemId == cost.ItemId);
        }

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(checked(originalItems.InventoryItems.Max(item => item.Uid) + 1), reward.Uid);
        Assert.Equal(321, reward.Durability);
        Assert.Equal(321, reward.MaximumDurability);
        Assert.Equal(1, reward.StackSize);
        Assert.Equal(0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);

        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        AssertSourceOrderedSuccessFrames(
            result,
            costs,
            reward,
            successText,
            broadcastsCompletion);
        Assert.Contains("source=NpcHandler.Arthur", result.Log);
        Assert.Contains($"option={optionId}", result.Log);
        Assert.Contains($"rewardItemId={rewardItemId}", result.Log);
    }

    [Fact]
    public async Task UnboundContainGateDoesNotAcceptBoundOnlyMaterials()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, GhostHornItemId, stackSize: 3, bound: 1));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(EvilToothItemId, "EvilTooth")),
            playerItemsRoot,
            optionId: 2);

        AssertMutationFree(result, originalItems);
        AssertNoItemMutationFrames(result.Sent);
        AssertExactDialog(
            result.Sent,
            "I'm sorry, you need at least 3 GhostHorn in order for me to help you.",
            "I see.");
        Assert.Contains("reason=missing-materials", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task RemoveAfterUnboundGatePreservesLowerUidBoundMaterial()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, GhostHornItemId, bound: 1),
            CreateInventoryItem(20, GhostHornItemId, stackSize: 3));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(EvilToothItemId, "EvilTooth", durability: 77)),
            playerItemsRoot,
            optionId: 2);

        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var bound = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        Assert.Equal(1, bound.StackSize);
        Assert.Equal(1, bound.Bound);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == EvilToothItemId);

        AssertMaterialUpdate(result.Sent[0], uid: 20, itemId: GhostHornItemId, stackSize: 2);
        AssertMaterialUpdate(result.Sent[1], uid: 20, itemId: GhostHornItemId, stackSize: 1);
        AssertMaterialRemoval(result.Sent[2], uid: 20);
        var rewardPacket = AssertGameItem(result.Sent[3]);
        Assert.Equal(EvilToothItemId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Fact]
    public async Task FullInventoryWinsBeforeMaterialAndDependencyChecks()
    {
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, GhostHornItemId, stackSize: 3),
        };
        inventory.AddRange(
            Enumerable.Range(0, 39)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var playerItemsRoot = CreatePlayerItemsRoot();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(EvilToothItemId, "EvilTooth")),
            playerItemsRoot,
            optionId: 2);

        AssertMutationFree(result, originalItems);
        var messageBox = AssertNpcReply(Assert.Single(result.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal("Please make 1 more space in your inventory.", messageBox.Text);
        AssertNoItemMutationFrames(result.Sent);
        Assert.Contains("reason=inventory-full", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingRewardItemDataDoesNotConsumeMaterials()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreateStageTwoMaterials();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(),
            playerItemsRoot,
            optionId: 2);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        AssertNoItemMutationFrames(result.Sent);
        Assert.Contains("reason=reward-itemtype-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingSessionStoreDoesNotPersistCommitOrSendMutationPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreateStageTwoMaterials();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(EvilToothItemId, "EvilTooth")),
            playerItemsRoot,
            optionId: 2,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        AssertNoItemMutationFrames(result.Sent);
        Assert.Contains("reason=session-store-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingPlayerItemsRootDoesNotCreateSessionOnlyGrant()
    {
        var originalItems = CreateStageTwoMaterials();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(EvilToothItemId, "EvilTooth")),
            playerItemsRoot: null,
            optionId: 2,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        AssertNoItemMutationFrames(result.Sent);
        Assert.Contains("reason=player-items-root-missing", result.Log);
    }

    [Fact]
    public async Task PersistenceWriteFailureDoesNotCommitOrSendMutationPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = CreateStageTwoMaterials();
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(EvilToothItemId, "EvilTooth")),
            blockedPlayerItemsRoot,
            optionId: 2,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        AssertNoItemMutationFrames(result.Sent);
        Assert.Contains("Arthur VipQuest exchange persistence", result.Log);
        Assert.Contains("no session mutation was committed", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
    }

    [Fact]
    public async Task ExhaustedItemUidDoesNotConsumeMaterialsOrCommit()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(uint.MaxValue, GhostHornItemId, stackSize: 3));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(EvilToothItemId, "EvilTooth")),
            playerItemsRoot,
            optionId: 2);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        AssertNoItemMutationFrames(result.Sent);
        Assert.Contains("reason=item-uid-exhausted", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        string? playerItemsRoot,
        byte optionId,
        bool seedPersistence = true,
        bool includeSessionStore = true)
    {
        if (seedPersistence && playerItemsRoot is not null)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var observerSent = new List<byte[]>();
        var session = CreateSession(originalItems, CharacterUid, "VipSeeker");
        var persistenceMatchedAtCommit = false;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is null || ReferenceEquals(previous, current) || playerItemsRoot is null)
            {
                return;
            }

            try
            {
                var persisted = new PlayerItemSnapshotLoader(playerItemsRoot)
                    .LoadByCharacterUid(CharacterUid);
                persistenceMatchedAtCommit = PlayerItemSnapshotLoader
                    .ToLegacyBytes(current.PlayerItems)
                    .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(persisted));
            }
            catch (Exception)
            {
                persistenceMatchedAtCommit = false;
            }
        });

        bool? sessionCommittedBeforeFirstSend = null;
        ConquerTcpConnection? connection = null;
        connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                if (!sessionCommittedBeforeFirstSend.HasValue)
                {
                    sessionCommittedBeforeFirstSend = sessions.TryGet(connection!, out var current) &&
                        !ReferenceEquals(current, session);
                }

                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        sessions.Set(connection, session);

        var observerConnection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9959),
            (frame, _) =>
            {
                observerSent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        sessions.Set(
            observerConnection,
            CreateSession(PlayerItemSnapshot.Empty, CharacterUid + 1, "Observer"));

        await Registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: playerItemsRoot),
            ReadFrame(LegacyNpcServerRequestPacket.Script(
                    ArthurNpcId,
                    optionId,
                    interactType: 1)
                .ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            sent,
            observerSent,
            log.ToString(),
            session,
            updatedSession,
            persistenceMatchedAtCommit,
            sessionCommittedBeforeFirstSend ?? false);
    }

    private static void AssertSourceOrderedSuccessFrames(
        InvokeResult result,
        IReadOnlyList<ArthurItemCost> costs,
        PlayerItemSnapshotItem reward,
        string? successText,
        bool broadcastsCompletion,
        IReadOnlyList<uint>? costUids = null)
    {
        var offset = 0;
        for (var costIndex = 0; costIndex < costs.Count; costIndex++)
        {
            var cost = costs[costIndex];
            var uid = costUids?[costIndex] ?? checked((uint)(10 + costIndex * 10));
            for (var remaining = cost.Count - 1; remaining >= 0; remaining--)
            {
                if (remaining == 0)
                {
                    AssertMaterialRemoval(result.Sent[offset], uid);
                }
                else
                {
                    AssertMaterialUpdate(
                        result.Sent[offset],
                        uid,
                        cost.ItemId,
                        checked((ushort)remaining));
                }

                offset++;
            }
        }

        var rewardPacket = AssertGameItem(result.Sent[offset++]);
        Assert.Equal(reward.Uid, rewardPacket.Uid);
        Assert.Equal(reward.ItemId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal(reward.Bound, rewardPacket.Bound);

        if (broadcastsCompletion)
        {
            var broadcast = AssertMessage(result.Sent[offset++]);
            Assert.Equal(LegacyMsgMessagePacket.RedColor, broadcast.Color);
            Assert.Equal(LegacyMsgMessagePacket.TalkChatType, broadcast.ChatType);
            Assert.Equal("Congratulation! VipSeeker has finished the Vip6 Quest.", broadcast.Content[3]);

            var observerBroadcast = AssertMessage(Assert.Single(result.ObserverSent));
            Assert.Equal(broadcast.Color, observerBroadcast.Color);
            Assert.Equal(broadcast.ChatType, observerBroadcast.ChatType);
            Assert.Equal(broadcast.Content.ToArray(), observerBroadcast.Content.ToArray());
        }
        else
        {
            Assert.Empty(result.ObserverSent);
            var dialog = AssertNpcReply(result.Sent[offset++]);
            Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
            Assert.Equal(successText, dialog.Text);

            var option = AssertNpcReply(result.Sent[offset++]);
            Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
            Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
            Assert.Equal("Thank you.", option.Text);

            var finish = AssertNpcReply(result.Sent[offset++]);
            Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
        }

        Assert.Equal(offset, result.Sent.Count);
    }

    private static IReadOnlyList<ArthurItemCost> GetCosts(byte optionId)
    {
        return optionId switch
        {
            1 =>
            [
                new ArthurItemCost(MossItemId, 3),
                new ArthurItemCost(SoulAromaItemId, 3),
                new ArthurItemCost(DreamGrassItemId, 3),
            ],
            2 => [new ArthurItemCost(GhostHornItemId, 3)],
            3 => [new ArthurItemCost(EvilToothItemId, 4)],
            4 => [new ArthurItemCost(ImpureVigorItemId, 5)],
            _ => throw new ArgumentOutOfRangeException(nameof(optionId), optionId, null),
        };
    }

    private static PlayerItemSnapshot CreateStageTwoMaterials()
    {
        return CreatePlayerItems(CreateInventoryItem(10, GhostHornItemId, stackSize: 3));
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        long characterUid,
        string characterName)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "arthur-vip-quest-test",
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: characterName,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 11_127,
                X: 208,
                Y: 255,
                PreviousMapId: 11_127,
                PreviousMapX: 208,
                PreviousMapY: 255,
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
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            ActiveNpcId: ArthurNpcId);
    }

    private static PlayerItemSnapshot CreatePlayerItems(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(
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
        };
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static void AssertMutationFree(InvokeResult result, PlayerItemSnapshot originalItems)
    {
        Assert.Same(result.OriginalSession.Character, result.UpdatedSession.Character);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
    }

    private static void AssertPersistedEquals(string playerItemsRoot, PlayerItemSnapshot expected)
    {
        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(expected, persisted);
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string expectedText,
        string expectedOption)
    {
        Assert.Equal(3, sent.Count);
        var dialog = AssertNpcReply(sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(expectedText, dialog.Text);

        var option = AssertNpcReply(sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(expectedOption, option.Text);

        var finish = AssertNpcReply(sent[2]);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
    }

    private static void AssertMaterialUpdate(
        byte[] bytes,
        uint uid,
        uint itemId,
        ushort stackSize)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        Assert.Equal(stackSize, packet.StackSize);
    }

    private static void AssertMaterialRemoval(byte[] bytes, uint uid)
    {
        var packet = AssertItemUsage(bytes);
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static void AssertNoItemMutationFrames(IReadOnlyList<byte[]> sent)
    {
        Assert.DoesNotContain(sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(sent, bytes => MsgItemUsagePacket.TryParse(ReadFrame(bytes), out _));
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

    private static MsgItemUsagePacket AssertItemUsage(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyMsgMessagePacket AssertMessage(byte[] bytes)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://arthur-vip-quest",
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
            npcs: [new Npc(checked((int)ArthurNpcId), 0, 0, 11_127, 208, 255, null)],
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

    private static ItemType CreateItemType(uint itemTypeId, string name, uint durability = 1)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private readonly record struct ArthurItemCost(uint ItemId, ushort Count);

    private sealed record InvokeResult(
        IReadOnlyList<byte[]> Sent,
        IReadOnlyList<byte[]> ObserverSent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        bool PersistenceMatchedAtCommit,
        bool SessionCommittedBeforeFirstSend);
}
