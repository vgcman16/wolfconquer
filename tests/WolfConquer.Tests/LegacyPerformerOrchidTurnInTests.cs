using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPerformerOrchidTurnInTests : IDisposable
{
    private const long CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;
    private const uint PerformerNpcId = 7_776;
    private const uint PerformerCardItemId = 711_679;
    private const string OrchidCountAttribute = "GiveFlowersToPerformer";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPerformerOrchidTurnInTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(26, 753_001U, 1)]
    [InlineData(28, 753_003U, 3)]
    [InlineData(29, 753_009U, 9)]
    public async Task EachTierConsumesOneExactUnboundItemPersistsIncrementAndOrdersRemovalBeforeDialog(
        byte optionId,
        uint itemId,
        int expectedCount)
    {
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, itemId, bound: 1),
            CreateInventoryItem(20, itemId),
            CreateInventoryItem(30, 720_001));
        var result = await InvokeAsync(
            optionId,
            counter: 0,
            originalItems: originalItems);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(expectedCount.ToString(CultureInfo.InvariantCulture),
            result.UpdatedSession.Character.Attributes[OrchidCountAttribute]);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10 && item.Bound == 1);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == PerformerCardItemId);

        AssertRemoval(result.Sent[0], uid: 20);
        AssertExactDialog(
            result.Sent,
            offset: 1,
            $"You`ve handed in {expectedCount} Orchids. As there are 15 Orchids, you can get a Performer Card.",
            (LegacyNpcReplyPacket.ClosedOptionId, "Oh."));
        AssertPersistedState(result, expectedCount, result.UpdatedSession.PlayerItems);
        Assert.Contains($"option={optionId}", result.Log);
        Assert.Contains($"orchidItemId={itemId}", result.Log);
        Assert.Contains($"increment={expectedCount}", result.Log);
        Assert.Contains("packetOrder=remove+dialog", result.Log);
    }

    [Theory]
    [InlineData(26, 753_001U, "You don`t have an Orchid to give me.")]
    [InlineData(28, 753_003U, " You don`t have 3 Orchids to give.")]
    [InlineData(29, 753_009U, "You don`t have 9 Orchids to give.")]
    public async Task BoundOnlyTierItemsDoNotSatisfyTheSourceContainGate(
        byte optionId,
        uint itemId,
        string expectedText)
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, itemId, bound: 1));
        var result = await InvokeAsync(
            optionId,
            counter: 4,
            originalItems: originalItems);

        AssertMutationFree(result, originalItems, expectedCounter: 4);
        AssertExactDialog(
            result.Sent,
            offset: 0,
            expectedText,
            (LegacyNpcReplyPacket.ClosedOptionId, "Oh."));
        Assert.DoesNotContain(result.Sent, IsItemMutation);
        AssertPersistedState(result, expectedCounter: 4, originalItems);
    }

    [Fact]
    public async Task FullInventoryAtThresholdUsesTheConsumedCellGrantsUnboundCardAndSendsRemoveAddDialog()
    {
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(50, 753_009),
        };
        inventory.AddRange(
            Enumerable.Range(0, 39)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var result = await InvokeAsync(
            optionId: 29,
            counter: 6,
            originalItems: originalItems,
            worldData: CreateWorldDataCatalog(
                CreateItemType(PerformerCardItemId, "PerformerCard", durability: 321)));

        Assert.Equal(40, originalItems.InventoryItems.Count);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes[OrchidCountAttribute]);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 50);
        var card = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == PerformerCardItemId);
        Assert.Equal(139U, card.Uid);
        Assert.Equal(0, card.Bound);
        Assert.Equal(321, card.Durability);
        Assert.Equal(321, card.MaximumDurability);

        AssertRemoval(result.Sent[0], uid: 50);
        var cardPacket = AssertGameItem(result.Sent[1]);
        Assert.Equal(139U, cardPacket.Uid);
        Assert.Equal(PerformerCardItemId, cardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, cardPacket.Mode);
        Assert.Equal(0, cardPacket.Bound);
        AssertExactDialog(
            result.Sent,
            offset: 2,
            "Hmmm, I`ve accepted the 15 Orchids you just handed in and here is your Performer Card. Well, if you`ve reached level 70, you will become a Performer immediately. Are you sure you want to join us?",
            (27, "Sure."),
            (LegacyNpcReplyPacket.ClosedOptionId, "I`ve~changed~my~mind."));
        AssertPersistedState(result, expectedCounter: 0, result.UpdatedSession.PlayerItems);
        Assert.Contains("packetOrder=remove+add-card+dialog", result.Log);
    }

    [Fact]
    public async Task OverThresholdSubtractsFifteenExactlyOnceAndCarriesTheRemainder()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(20, 753_003));
        var result = await InvokeAsync(
            optionId: 28,
            counter: 14,
            originalItems: originalItems,
            worldData: CreateWorldDataCatalog(CreateItemType(PerformerCardItemId, "PerformerCard")));

        Assert.Equal("2", result.UpdatedSession.Character.Attributes[OrchidCountAttribute]);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == PerformerCardItemId);
        AssertRemoval(result.Sent[0], uid: 20);
        Assert.Equal(PerformerCardItemId, AssertGameItem(result.Sent[1]).ItemId);
        AssertExactDialog(
            result.Sent,
            offset: 2,
            "Hmmm, I`ve accepted the 15 Orchids you just handed in and here is your Performer Card. Well, if you`ve reached level 70, you will become a Performer immediately. Are you sure you want to join us?",
            (27, "Sure."),
            (LegacyNpcReplyPacket.ClosedOptionId, "I`ve~changed~my~mind."));
        AssertPersistedState(result, expectedCounter: 2, result.UpdatedSession.PlayerItems);
    }

    [Theory]
    [InlineData(0, "You haven`t handed in any Orchids yet. It charges 15 Orchids for a Performer Card.")]
    [InlineData(7, "You`ve handed in 7 Orchids. As you meet the requirement, you`ll get a Performer Card.")]
    public async Task OptionThirtyReportsTheExactSourceCounterDialogWithoutMutation(
        int counter,
        string expectedText)
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(20, 720_001));
        var result = await InvokeAsync(
            optionId: 30,
            counter: counter,
            originalItems: originalItems);

        AssertMutationFree(result, originalItems, counter);
        AssertExactDialog(
            result.Sent,
            offset: 0,
            expectedText,
            (LegacyNpcReplyPacket.ClosedOptionId, "Oh."));
        AssertPersistedState(result, counter, originalItems);
    }

    [Theory]
    [InlineData(false, true, true, "session-store-missing")]
    [InlineData(true, false, true, "player-items-root-missing")]
    [InlineData(true, true, false, "users-root-missing")]
    public async Task MissingMutationDependenciesArePacketFreeAndMutationFree(
        bool includeSessionStore,
        bool includePlayerItemsRoot,
        bool includeUsersRoot,
        string expectedReason)
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(20, 753_001));
        var result = await InvokeAsync(
            optionId: 26,
            counter: 0,
            originalItems: originalItems,
            includeSessionStore: includeSessionStore,
            includePlayerItemsRoot: includePlayerItemsRoot,
            includeUsersRoot: includeUsersRoot);

        AssertMutationFree(result, originalItems, expectedCounter: 0);
        Assert.Empty(result.Sent);
        Assert.Contains(expectedReason, result.Log);
        AssertPersistedState(result, expectedCounter: 0, originalItems);
    }

    [Fact]
    public async Task MissingCardItemTypeAtThresholdDoesNotConsumePersistCommitOrReply()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(20, 753_001));
        var result = await InvokeAsync(
            optionId: 26,
            counter: 14,
            originalItems: originalItems,
            worldData: CreateWorldDataCatalog());

        AssertMutationFree(result, originalItems, expectedCounter: 14);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing itemId=711679", result.Log);
        AssertPersistedState(result, expectedCounter: 14, originalItems);
    }

    [Fact]
    public async Task InvalidCardDurabilityAtThresholdDoesNotConsumePersistCommitOrReply()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(20, 753_001));
        var result = await InvokeAsync(
            optionId: 26,
            counter: 14,
            originalItems: originalItems,
            worldData: CreateWorldDataCatalog(
                CreateItemType(PerformerCardItemId, "PerformerCard", durability: 70_000)));

        AssertMutationFree(result, originalItems, expectedCounter: 14);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-invalid itemId=711679", result.Log);
        AssertPersistedState(result, expectedCounter: 14, originalItems);
    }

    [Fact]
    public async Task ExhaustedItemUidAtThresholdDoesNotConsumePersistCommitOrReply()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(uint.MaxValue, 753_001));
        var result = await InvokeAsync(
            optionId: 26,
            counter: 14,
            originalItems: originalItems,
            worldData: CreateWorldDataCatalog(CreateItemType(PerformerCardItemId, "PerformerCard")));

        AssertMutationFree(result, originalItems, expectedCounter: 14);
        Assert.Empty(result.Sent);
        Assert.Contains("item-uid-exhausted", result.Log);
        AssertPersistedState(result, expectedCounter: 14, originalItems);
    }

    [Fact]
    public async Task ItemSnapshotWriteFailureLeavesCharacterSessionAndBlockedRootUntouched()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = CreatePlayerItems(CreateInventoryItem(20, 753_001));
        var result = await InvokeAsync(
            optionId: 26,
            counter: 0,
            originalItems: originalItems,
            includePlayerItemsRoot: true,
            playerItemsRootOverride: blockedPlayerItemsRoot,
            seedPlayerItems: false);

        AssertMutationFree(result, originalItems, expectedCounter: 0);
        Assert.Empty(result.Sent);
        Assert.Contains("item-persistence-write-failed", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
        AssertPersistedCharacter(result, expectedCounter: 0);
    }

    [Fact]
    public async Task CharacterUidMismatchFailsPreflightBeforeAnyItemWriteOrCommit()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(20, 753_001));
        var result = await InvokeAsync(
            optionId: 26,
            counter: 0,
            originalItems: originalItems,
            persistedCharacterUid: CharacterUid + 1);

        AssertMutationFree(result, originalItems, expectedCounter: 0);
        Assert.Empty(result.Sent);
        Assert.Contains("character-persistence-preflight-failed reason=uidMismatch", result.Log);
        AssertPersistedItems(result, originalItems);
        Assert.Contains("GiveFlowersToPerformer=0", File.ReadAllText(result.CharacterPath!));
    }

    [Fact]
    public async Task CharacterWriteFailureRollsBackTheAlreadyWrittenItemSnapshotBeforeReturning()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(20, 753_001));
        var result = await InvokeAsync(
            optionId: 26,
            counter: 0,
            originalItems: originalItems,
            lockCharacterFile: true);

        AssertMutationFree(result, originalItems, expectedCounter: 0);
        Assert.Empty(result.Sent);
        Assert.Contains("character-persistence-write-failed", result.Log);
        Assert.Contains("item-rollback=complete", result.Log);
        AssertPersistedState(result, expectedCounter: 0, originalItems);
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
        int counter,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData = null,
        bool includeSessionStore = true,
        bool includePlayerItemsRoot = true,
        bool includeUsersRoot = true,
        string? playerItemsRootOverride = null,
        long persistedCharacterUid = CharacterUid,
        bool seedPlayerItems = true,
        bool lockCharacterFile = false)
    {
        var usersRoot = includeUsersRoot ? CreateUsersRoot(counter, persistedCharacterUid) : null;
        var characterPath = usersRoot is null
            ? null
            : Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var playerItemsRoot = includePlayerItemsRoot
            ? playerItemsRootOverride ?? Path.Combine(_root, "PlayerItems-" + Guid.NewGuid().ToString("N"))
            : null;
        if (playerItemsRoot is not null && seedPlayerItems)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(usersRoot, counter, originalItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        FileStream? characterLock = null;
        if (lockCharacterFile)
        {
            characterLock = new FileStream(
                characterPath!,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }

        bool handled;
        try
        {
            handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: session,
                    WorldData: worldData,
                    Sessions: includeSessionStore ? sessions : null,
                    PlayerItemsRoot: playerItemsRoot),
                session,
                PerformerNpcId,
                optionId,
                CancellationToken.None);
        }
        finally
        {
            characterLock?.Dispose();
        }

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        return new InvokeResult(
            handled,
            sent,
            log.ToString(),
            session,
            Assert.IsType<WorldSession>(updatedSession),
            usersRoot,
            characterPath,
            playerItemsRoot);
    }

    private string CreateUsersRoot(int counter, long persistedCharacterUid)
    {
        var usersRoot = Path.Combine(_root, "Users-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $"""
            [Character]
            UID={persistedCharacterUid}
            Body=1003
            Face=0
            Name=PerformerOrchidTester
            Class=7051
            Map=1002
            X=430
            Y=378
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=70
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
            GiveFlowersToPerformer={counter}

            [PaidNobility]
            """);
        return usersRoot;
    }

    private static WorldSession CreateSession(
        string? usersRoot,
        int counter,
        PlayerItemSnapshot playerItems)
    {
        return new WorldSession(
            AccountId,
            Username: "performer-orchid-turn-in-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PerformerOrchidTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 70,
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
                    [OrchidCountAttribute] = counter.ToString(CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
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

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertMutationFree(
        InvokeResult result,
        PlayerItemSnapshot originalItems,
        int expectedCounter)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(
            expectedCounter.ToString(CultureInfo.InvariantCulture),
            result.UpdatedSession.Character.Attributes[OrchidCountAttribute]);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
    }

    private static void AssertPersistedState(
        InvokeResult result,
        int expectedCounter,
        PlayerItemSnapshot expectedItems)
    {
        AssertPersistedCharacter(result, expectedCounter);
        AssertPersistedItems(result, expectedItems);
    }

    private static void AssertPersistedCharacter(InvokeResult result, int expectedCounter)
    {
        if (result.UsersRoot is null)
        {
            return;
        }

        var reloaded = new CharacterSnapshotLoader(result.UsersRoot).LoadByAccountId(AccountId);
        Assert.NotNull(reloaded);
        Assert.Equal(
            expectedCounter.ToString(CultureInfo.InvariantCulture),
            reloaded.Attributes[OrchidCountAttribute]);
    }

    private static void AssertPersistedItems(InvokeResult result, PlayerItemSnapshot expectedItems)
    {
        if (result.PlayerItemsRoot is null || !Directory.Exists(result.PlayerItemsRoot))
        {
            return;
        }

        var reloaded = new PlayerItemSnapshotLoader(result.PlayerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(expectedItems, reloaded);
    }

    private static void AssertRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(0UL, packet.DwParam);
        Assert.Equal(0U, packet.DwParam2);
        Assert.Equal(0U, packet.DwParam3);
        Assert.Equal(0U, packet.DwParam4);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        int offset,
        string expectedText,
        params (byte OptionId, string Text)[] expectedOptions)
    {
        Assert.Equal(offset + expectedOptions.Length + 3, sent.Count);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[offset]), out var dialog));
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(expectedText, dialog.Text);

        for (var index = 0; index < expectedOptions.Length; index++)
        {
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[offset + index + 1]), out var option));
            Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
            Assert.Equal(expectedOptions[index].OptionId, option.OptionId);
            Assert.Equal(expectedOptions[index].Text, option.Text);
        }

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[^2]), out var avatar));
        Assert.Equal(LegacyNpcReplyInteractType.Avatar, avatar.InteractType);
        Assert.Equal(110, avatar.InputMaxLength);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[^1]), out var finish));
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
    }

    private static bool IsItemMutation(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        return MsgItemUsagePacket.TryParse(frame, out _) || LegacyGameItemPacket.TryParse(frame, out _);
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
                "fixture://performer-orchid-turn-in",
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

    private static ItemType CreateItemType(
        uint itemTypeId,
        string name,
        uint durability = 1)
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

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        string? UsersRoot,
        string? CharacterPath,
        string? PlayerItemsRoot);
}
