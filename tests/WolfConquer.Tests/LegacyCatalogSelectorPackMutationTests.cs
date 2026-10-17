using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCatalogSelectorPackMutationTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const long CharacterUid = 1_293_600;
    private const uint BlackFridayAccessoryNpcId = 4_294_967_266;
    private const uint BlackFridayMountNpcId = 4_294_967_267;
    private const uint BlackFridayGarmentNpcId = 4_294_967_268;
    private const uint MountPacket3NpcId = 4_294_967_270;
    private const uint AccessoryPacket2NpcId = 4_294_967_271;
    private const uint GarmentPacket2NpcId = 4_294_967_272;
    private const uint AccessoryPacketNpcId = 4_294_967_273;
    private const uint MountPacket2NpcId = 4_294_967_274;
    private const uint MountPacketNpcId = 4_294_967_275;
    private const uint GarmentPacketNpcId = 4_294_967_276;
    private const uint BlackFridayAccessoryPackItemId = 3_100_053;
    private const uint BlackFridayMountPackItemId = 3_100_054;
    private const uint BlackFridayGarmentPackItemId = 3_100_055;
    private const uint FirstAccessoryItemId = 360_071;
    private const uint DuplicateAccessoryItemId = 360_150;
    private const uint LastAccessoryItemId = 360_203;
    private const uint FirstMountItemId = 200_021;
    private const uint LastMountItemId = 200_699;
    private const uint FirstGarmentItemId = 187_325;
    private const uint LastGarmentItemId = 196_675;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyCatalogSelectorPackMutationTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<uint, uint, uint, string, string> SelectionServiceCases => new()
    {
        { BlackFridayAccessoryNpcId, BlackFridayAccessoryPackItemId, FirstAccessoryItemId, "NpcHandler.BlackFridayAccesory", "Accessory" },
        { BlackFridayMountNpcId, BlackFridayMountPackItemId, FirstMountItemId, "NpcHandler.BlackFridayMountPack", "Mount" },
        { BlackFridayGarmentNpcId, BlackFridayGarmentPackItemId, FirstGarmentItemId, "NpcHandler.BlackFridayGarmentPack", "garment" },
        { AccessoryPacket2NpcId, 0, FirstAccessoryItemId, "NpcHandler.AccesoryPacket2", "Accessory" },
        { AccessoryPacketNpcId, 0, FirstAccessoryItemId, "NpcHandler.AccesoryPacket", "Accessory" },
        { MountPacket3NpcId, 0, FirstMountItemId, "NpcHandler.MountPacket3", "Mount" },
        { MountPacket2NpcId, 0, FirstMountItemId, "NpcHandler.MountPacket2", "Mount" },
        { MountPacketNpcId, 0, FirstMountItemId, "NpcHandler.MountPacket", "Mount" },
        { GarmentPacket2NpcId, 0, FirstGarmentItemId, "NpcHandler.GarmentPacket2", "garment" },
        { GarmentPacketNpcId, 0, FirstGarmentItemId, "NpcHandler.GarmentPacket", "garment" },
    };

    [Theory]
    [MemberData(nameof(SelectionServiceCases))]
    public async Task EverySourceHandlerRetainsItsSelectedItemAndSendsConfirmation(
        uint npcId,
        uint packItemId,
        uint expectedItemId,
        string source,
        string previewNoun)
    {
        var items = packItemId == 0
            ? PlayerItemSnapshot.Empty
            : Items(Item(50, packItemId));
        var result = await InvokeAsync(
            npcId,
            optionId: 100,
            items,
            SourceCatalog());

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.Equal(expectedItemId, result.UpdatedSession.LegacyCatalogSelectedItemId);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
            packet.Text.Contains("Are you sure you wanna buy", StringComparison.Ordinal));
        Assert.Contains(replies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 250 &&
            packet.Text == "Sure.");
        Assert.Contains(replies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 251 &&
            packet.Text == $"Show me the {previewNoun}.");
        Assert.Contains(replies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == byte.MaxValue &&
            packet.Text == "Let me check.");
        Assert.Contains("source=" + source, result.Log);
        Assert.Contains("selectedItemId=" + expectedItemId, result.Log);
    }

    public static TheoryData<uint, byte, uint> ExactCatalogIndexCases => new()
    {
        { AccessoryPacketNpcId, 122, DuplicateAccessoryItemId },
        { AccessoryPacketNpcId, 133, LastAccessoryItemId },
        { MountPacketNpcId, 101, LastMountItemId },
        { GarmentPacketNpcId, 101, LastGarmentItemId },
    };

    [Theory]
    [MemberData(nameof(ExactCatalogIndexCases))]
    public async Task SourceCatalogIndicesUseExactAccessoryAndCoatStorageOrder(
        uint npcId,
        byte optionId,
        uint expectedItemId)
    {
        var result = await InvokeAsync(
            npcId,
            optionId,
            PlayerItemSnapshot.Empty,
            SourceCatalog());

        Assert.True(result.Handled);
        Assert.Equal(expectedItemId, result.UpdatedSession.LegacyCatalogSelectedItemId);
        Assert.Contains($"catalogIndex={optionId - 100}", result.Log);
        Assert.Contains("selectedItemId=" + expectedItemId, result.Log);
    }

    [Fact]
    public async Task RootDialogsExposeSourceNamesStarsTypoAndOptionIds()
    {
        var accessory = await InvokeAsync(
            AccessoryPacketNpcId,
            optionId: 0,
            PlayerItemSnapshot.Empty,
            SourceCatalog());
        var accessoryReplies = ParseNpcReplies(accessory.Sent);
        Assert.Contains(accessoryReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
            packet.Text == "Select the Accesory you need.");
        Assert.Contains(accessoryReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 100 &&
            packet.Text == "CaribbeanFlag");
        Assert.Contains(accessoryReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 1 &&
            packet.Text == "Next Page");

        var mount = await InvokeAsync(
            MountPacketNpcId,
            optionId: 0,
            PlayerItemSnapshot.Empty,
            SourceCatalog());
        var mountReplies = ParseNpcReplies(mount.Sent);
        Assert.Contains(mountReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 100 &&
            packet.Text == "WingedFrostcatArmor(5Stars)");
        Assert.Contains(mountReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 101 &&
            packet.Text == "Shadow Leopard (Blood)(5Stars)");
        Assert.Contains(mountReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 1 &&
            packet.Text == "Next Page");
        Assert.DoesNotContain(mountReplies, packet => packet.Text.Contains("SnowFox", StringComparison.Ordinal));

        var garment = await InvokeAsync(
            GarmentPacketNpcId,
            optionId: 0,
            PlayerItemSnapshot.Empty,
            SourceCatalog());
        var garmentReplies = ParseNpcReplies(garment.Sent);
        Assert.Contains(garmentReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 100 &&
            packet.Text == "GotTalent(4Stars)");
        Assert.Contains(garmentReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 101 &&
            packet.Text == "Pistil Sprite (Glamour)(5Stars)");
        Assert.Contains(garmentReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 1 &&
            packet.Text == "Next Page");
        Assert.DoesNotContain(
            accessoryReplies.Concat(mountReplies).Concat(garmentReplies),
            packet => packet.Text.Contains('~'));
    }

    public static TheoryData<uint, uint, uint, byte> PackedGrantCases => new()
    {
        { BlackFridayAccessoryNpcId, BlackFridayAccessoryPackItemId, FirstAccessoryItemId, 0 },
        { BlackFridayMountNpcId, BlackFridayMountPackItemId, FirstMountItemId, 1 },
        { BlackFridayGarmentNpcId, BlackFridayGarmentPackItemId, FirstGarmentItemId, 1 },
    };

    [Theory]
    [MemberData(nameof(PackedGrantCases))]
    public async Task BlackFridayOption250PersistsThenRemovesAddsAndMessages(
        uint npcId,
        uint packItemId,
        uint rewardItemId,
        byte expectedBless)
    {
        var originalItems = Items(
            Item(10, 720_001),
            Item(50, packItemId));
        var result = await InvokeAsync(
            npcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: rewardItemId);

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 50);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(51U, reward.Uid);
        Assert.Equal((ushort)321, reward.Durability);
        Assert.Equal((ushort)321, reward.MaximumDurability);
        Assert.Equal(expectedBless, reward.Bless);
        Assert.Equal((byte)0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);
        Assert.Equal(0U, result.UpdatedSession.LegacyCatalogSelectedItemId);

        Assert.Collection(
            result.Sent,
            frame => AssertItemRemoval(frame, uid: 50),
            frame => AssertGrantedItem(frame, rewardItemId, expectedBless),
            frame => AssertMessageBox(frame, "You received 1 "));
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        AssertItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains("sourceOrder=RemovePack,AddItem,MessageBox", result.Log);
    }

    [Fact]
    public async Task UnboundPackGateThenBoundAgnosticRemovalConsumesFirstBoundMatch()
    {
        var originalItems = Items(
            Item(10, BlackFridayAccessoryPackItemId, bound: 1),
            Item(20, BlackFridayAccessoryPackItemId, bound: 0));
        var result = await InvokeAsync(
            BlackFridayAccessoryNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20 && item.Bound == 0);
        AssertItemRemoval(result.Sent[0], uid: 10);
    }

    [Fact]
    public async Task ZeroStackUnboundPackDoesNotSatisfySourceContainGate()
    {
        var originalItems = Items(Item(10, BlackFridayAccessoryPackItemId, stackSize: 0));
        var result = await InvokeAsync(
            BlackFridayAccessoryNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("WorldServer ignored LegacyNpc ItemPackServices no-op", result.Log);
    }

    [Fact]
    public async Task PositiveUnboundGateStillRemovesFirstMatchingZeroStackPack()
    {
        var originalItems = Items(
            Item(10, BlackFridayAccessoryPackItemId, stackSize: 0),
            Item(20, BlackFridayAccessoryPackItemId, stackSize: 1));
        var result = await InvokeAsync(
            BlackFridayAccessoryNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        Assert.Contains(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 20 && item.StackSize == 1);
        AssertItemRemoval(result.Sent[0], uid: 10);
    }

    [Fact]
    public async Task BoundOnlyBlackFridayPackDoesNotSatisfySourceContainGate()
    {
        var originalItems = Items(Item(10, BlackFridayAccessoryPackItemId, bound: 1));
        var result = await InvokeAsync(
            BlackFridayAccessoryNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("WorldServer ignored LegacyNpc ItemPackServices no-op", result.Log);
    }

    [Fact]
    public async Task FullBagIsRejectedBeforePackRemovalEvenThoughRemovalWouldFreeASlot()
    {
        var fullInventory = new List<PlayerItemSnapshotItem>
        {
            Item(1, BlackFridayAccessoryPackItemId),
        };
        fullInventory.AddRange(Enumerable.Range(0, 39)
            .Select(index => Item(checked((uint)(100 + index)), checked((uint)(700_000 + index)))));
        var originalItems = Items(fullInventory.ToArray());
        var result = await InvokeAsync(
            BlackFridayAccessoryNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId);

        AssertMutationFree(result, originalItems);
        Assert.Single(result.Sent);
        AssertMessageBox(result.Sent[0], "Please make 1 more space in your inventory.");
    }

    [Fact]
    public async Task BlackFridayAccessoryRootFullBagUsesSourceDialogWhileOtherRootsUseMessageBox()
    {
        var fullInventory = Enumerable.Range(0, 40)
            .Select(index => Item(
                checked((uint)(100 + index)),
                index == 0 ? BlackFridayAccessoryPackItemId : checked((uint)(700_000 + index))))
            .ToArray();

        var accessory = await InvokeAsync(
            BlackFridayAccessoryNpcId,
            optionId: 0,
            Items(fullInventory),
            SourceCatalog());
        var accessoryReplies = ParseNpcReplies(accessory.Sent);
        Assert.Contains(accessoryReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
            packet.Text == "Please make 1 more space in your inventory.");
        Assert.Contains(accessoryReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == byte.MaxValue &&
            packet.Text == "Let me check.");
        Assert.Contains(accessoryReplies, packet =>
            packet.InteractType == LegacyNpcReplyInteractType.Avatar && packet.InputMaxLength == 7);

        var mount = await InvokeAsync(
            MountPacketNpcId,
            optionId: 0,
            Items(fullInventory),
            SourceCatalog());
        Assert.Single(mount.Sent);
        AssertMessageBox(mount.Sent[0], "Please make 1 more space in your inventory.");
    }

    public static TheoryData<uint, uint, ulong> OneTimeClaimCases => new()
    {
        { AccessoryPacket2NpcId, FirstAccessoryItemId, 0x10_000 },
        { AccessoryPacketNpcId, FirstAccessoryItemId, 0x200 },
        { MountPacket3NpcId, FirstMountItemId, 0x40_000 },
        { MountPacket2NpcId, FirstMountItemId, 0x100 },
        { MountPacketNpcId, FirstMountItemId, 0x4 },
        { GarmentPacket2NpcId, FirstGarmentItemId, 0x400 },
        { GarmentPacketNpcId, FirstGarmentItemId, 0x1 },
    };

    [Theory]
    [MemberData(nameof(OneTimeClaimCases))]
    public async Task NoPackOption250PersistsExactOneTimeFlagAndSurvivesReload(
        uint npcId,
        uint rewardItemId,
        ulong claimFlag)
    {
        const ulong existingFlag = 0x20;
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            npcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: rewardItemId,
            rechargeProgress: existingFlag);

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        var expectedProgress = existingFlag | claimFlag;
        Assert.Equal(
            expectedProgress.ToString(CultureInfo.InvariantCulture),
            result.UpdatedSession.Character.Attributes["RechargeProgress"]);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(11U, reward.Uid);
        Assert.Equal((byte)0, reward.Bless);
        Assert.Equal((byte)0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);
        Assert.Equal(0U, result.UpdatedSession.LegacyCatalogSelectedItemId);

        Assert.Collection(
            result.Sent,
            frame => AssertGrantedItem(frame, rewardItemId, expectedBless: 0),
            frame => AssertMessageBox(frame, "You received 1 "));
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.CharacterPersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);

        var reloaded = new CharacterSnapshotLoader(result.UsersRoot, result.PlayerItemsRoot)
            .LoadByAccountId(AccountId);
        Assert.NotNull(reloaded);
        Assert.Equal(
            expectedProgress.ToString(CultureInfo.InvariantCulture),
            reloaded.Attributes["RechargeProgress"]);
        AssertItemsEqual(
            result.UpdatedSession.PlayerItems,
            new PlayerItemSnapshotLoader(result.PlayerItemsRoot).LoadByCharacterUid(CharacterUid));
        Assert.Contains($"claimFlag=0x{claimFlag:X}", result.Log);
        Assert.Contains("sourceOrder=AddRechargeFlag,AddItem,MessageBox", result.Log);
    }

    [Fact]
    public async Task AlreadyClaimedNoPackBranchSilentlyLeavesSelectionAndInventoryUntouched()
    {
        const ulong claimedFlag = 0x200;
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            AccessoryPacketNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId,
            rechargeProgress: claimedFlag);

        AssertMutationFree(result, originalItems);
        Assert.Equal(FirstAccessoryItemId, result.UpdatedSession.LegacyCatalogSelectedItemId);
        Assert.Empty(result.Sent);
        Assert.Contains("claim-already-used:flag=0x200", result.Log);
    }

    [Theory]
    [MemberData(nameof(SelectionServiceCases))]
    public async Task Option251SendsExactChatItemThenConfirmationWithoutMutation(
        uint npcId,
        uint packItemId,
        uint selectedItemId,
        string source,
        string previewNoun)
    {
        var originalItems = packItemId == 0
            ? Items(Item(10, 720_001))
            : Items(Item(10, packItemId));
        var result = await InvokeAsync(
            npcId,
            optionId: 251,
            originalItems,
            SourceCatalog(),
            selectedItemId: selectedItemId);

        AssertMutationFree(result, originalItems);
        Assert.Equal(selectedItemId, result.UpdatedSession.LegacyCatalogSelectedItemId);
        Assert.Equal(7, result.Sent.Count);
        AssertChatItem(result.Sent[0], selectedItemId);

        var itemName = selectedItemId switch
        {
            FirstAccessoryItemId => "CaribbeanFlag",
            FirstMountItemId => "WingedFrostcatArmor",
            FirstGarmentItemId => "GotTalent",
            _ => throw new InvalidOperationException("Unexpected preview fixture item."),
        };
        var replies = ParseNpcReplies(result.Sent.Skip(1));
        Assert.Collection(
            replies,
            packet =>
            {
                Assert.Equal(LegacyNpcReplyInteractType.Dialog, packet.InteractType);
                Assert.Equal($"Are you sure you wanna buy this {previewNoun} ({itemName})?!", packet.Text);
            },
            packet =>
            {
                Assert.Equal(LegacyNpcReplyInteractType.Option, packet.InteractType);
                Assert.Equal((byte)250, packet.OptionId);
                Assert.Equal("Sure.", packet.Text);
            },
            packet =>
            {
                Assert.Equal(LegacyNpcReplyInteractType.Option, packet.InteractType);
                Assert.Equal((byte)251, packet.OptionId);
                Assert.Equal($"Show me the {previewNoun}.", packet.Text);
            },
            packet =>
            {
                Assert.Equal(LegacyNpcReplyInteractType.Option, packet.InteractType);
                Assert.Equal(byte.MaxValue, packet.OptionId);
                Assert.Equal("Let me check.", packet.Text);
            },
            packet =>
            {
                Assert.Equal(LegacyNpcReplyInteractType.Avatar, packet.InteractType);
                Assert.Equal((ushort)7, packet.InputMaxLength);
            },
            packet => Assert.Equal(LegacyNpcReplyInteractType.Finish, packet.InteractType));
        Assert.Contains("source=" + source, result.Log);
        Assert.Contains("sourceOrder=FakeChatItem,ConfirmationDialog", result.Log);
    }

    [Fact]
    public async Task Option251WithMissingSelectedItemTypeSendsNothingAndDoesNotMutate()
    {
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            AccessoryPacketNpcId,
            optionId: 251,
            originalItems,
            Catalog(items: [], SourceCoatStorageRows()),
            selectedItemId: FirstAccessoryItemId);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("preview-itemtype-missing-or-invalid", result.Log);
    }

    [Theory]
    [InlineData(BlackFridayAccessoryNpcId, BlackFridayAccessoryPackItemId)]
    [InlineData(AccessoryPacketNpcId, 0U)]
    public async Task MissingAuthoritativeRewardDataDoesNotConsumePackOrSetClaim(
        uint npcId,
        uint packItemId)
    {
        var originalItems = packItemId == 0
            ? Items(Item(10, 720_001))
            : Items(Item(10, packItemId));
        var result = await InvokeAsync(
            npcId,
            optionId: 250,
            originalItems,
            Catalog(items: [], SourceCoatStorageRows()),
            selectedItemId: FirstAccessoryItemId);

        AssertMutationFree(result, originalItems);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["RechargeProgress"]);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing-or-invalid", result.Log);
    }

    public static TheoryData<string, string> MissingDependencyCases => new()
    {
        { "session-store", "session-store-missing" },
        { "session-registration", "session-registration-missing-or-stale" },
        { "player-items-root", "player-items-root-missing" },
        { "users-root", "users-root-missing" },
        { "character-uid", "character-uid-unavailable" },
        { "character-snapshot", "character-persistence-preflight" },
    };

    [Theory]
    [MemberData(nameof(MissingDependencyCases))]
    public async Task MissingMutationDependenciesDoNotSetFlagPersistItemCommitOrSend(
        string failure,
        string expectedReason)
    {
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            AccessoryPacketNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId,
            includeSessionStore: failure != "session-store",
            registerSession: failure != "session-registration",
            includePlayerItemsRoot: failure != "player-items-root",
            includeUsersRoot: failure != "users-root",
            characterUid: failure == "character-uid" ? 0 : CharacterUid,
            createCharacterSnapshot: failure != "character-snapshot");

        AssertMutationFree(result, originalItems, expectPersistedItems: failure != "player-items-root");
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["RechargeProgress"]);
        Assert.Empty(result.Sent);
        Assert.Contains(expectedReason, result.Log);
    }

    [Fact]
    public async Task ItemPersistenceFailureDoesNotConsumePackCommitOrSend()
    {
        Directory.CreateDirectory(_root);
        var blockedRoot = Path.Combine(_root, "blocked-items-root");
        File.WriteAllText(blockedRoot, "not a directory");
        var originalItems = Items(Item(10, BlackFridayAccessoryPackItemId));
        var result = await InvokeAsync(
            BlackFridayAccessoryNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId,
            playerItemsRootOverride: blockedRoot,
            seedPlayerItems: false);

        AssertMutationFree(result, originalItems, expectPersistedItems: false);
        Assert.Empty(result.Sent);
        Assert.Equal("not a directory", File.ReadAllText(blockedRoot));
        Assert.Contains("item-persistence-", result.Log);
    }

    [Fact]
    public async Task CharacterPersistenceFailureRollsBackNoPackItemAndCharacterBytes()
    {
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            AccessoryPacketNpcId,
            optionId: 250,
            originalItems,
            SourceCatalog(),
            selectedItemId: FirstAccessoryItemId,
            persistedCharacterUid: CharacterUid + 1);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        AssertItemsEqual(originalItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Equal("0", Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).Attributes["RechargeProgress"]);
        Assert.Contains("character-persistence-failed:uidMismatch", result.Log);
        Assert.Contains("itemRollback=complete", result.Log);
        Assert.Contains("characterRollback=complete", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        uint selectedItemId = 0,
        ulong rechargeProgress = 0,
        bool includeSessionStore = true,
        bool includePlayerItemsRoot = true,
        bool includeUsersRoot = true,
        long characterUid = CharacterUid,
        long? persistedCharacterUid = null,
        bool createCharacterSnapshot = true,
        string? playerItemsRootOverride = null,
        bool seedPlayerItems = true,
        bool registerSession = true)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = Path.Combine(usersRoot, AccountId + ".ini");
        if (createCharacterSnapshot)
        {
            WriteCharacterSnapshot(
                characterPath,
                persistedCharacterUid ?? characterUid,
                rechargeProgress);
        }

        var playerItemsRoot = playerItemsRootOverride ?? Path.Combine(invocationRoot, "PlayersItems");
        if (includePlayerItemsRoot && seedPlayerItems)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(characterUid, originalItems);
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RechargeProgress"] = rechargeProgress.ToString(CultureInfo.InvariantCulture),
        };
        var session = CreateSession(
            originalItems,
            attributes,
            includeUsersRoot ? usersRoot : null,
            characterUid,
            selectedItemId);
        var sent = new List<byte[]>();
        var commitCount = 0;
        var persistenceMatchedAtCommit = false;
        var characterPersistenceMatchedAtCommit = false;
        WorldSessionStore? sessions = includeSessionStore
            ? new WorldSessionStore((_, previous, current) =>
            {
                if (previous is null)
                {
                    return;
                }

                commitCount++;
                if (includePlayerItemsRoot && Directory.Exists(playerItemsRoot))
                {
                    var persisted = new PlayerItemSnapshotLoader(playerItemsRoot)
                        .LoadByCharacterUid(characterUid);
                    persistenceMatchedAtCommit = PlayerItemSnapshotLoader
                        .ToLegacyBytes(current.PlayerItems)
                        .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(persisted));
                }

                if (File.Exists(characterPath))
                {
                    var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
                    characterPersistenceMatchedAtCommit =
                        persisted.Attributes.TryGetValue("RechargeProgress", out var persistedProgress) &&
                        current.Character.Attributes.TryGetValue("RechargeProgress", out var currentProgress) &&
                        persistedProgress == currentProgress;
                }
            })
            : null;

        bool? sessionCommittedBeforeFirstSend = null;
        ConquerTcpConnection? connection = null;
        connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                if (!sessionCommittedBeforeFirstSend.HasValue)
                {
                    sessionCommittedBeforeFirstSend = sessions?.TryGet(connection!, out var current) == true &&
                        current is not null &&
                        !ReferenceEquals(current, session);
                }

                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        if (registerSession)
        {
            sessions?.Set(connection, session);
        }

        using var log = new StringWriter();
        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: includePlayerItemsRoot ? playerItemsRoot : null),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        var updatedSession = session;
        if (sessions?.TryGet(connection, out var storedSession) == true && storedSession is not null)
        {
            updatedSession = storedSession;
        }

        PlayerItemSnapshot? persistedItems = null;
        var itemsPath = Path.Combine(
            playerItemsRoot,
            characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        if (Directory.Exists(playerItemsRoot) && File.Exists(itemsPath))
        {
            persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(characterUid);
        }

        CharacterSnapshot? persistedCharacter = null;
        if (File.Exists(characterPath))
        {
            persistedCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
        }

        return new InvokeResult(
            handled,
            sent,
            log.ToString(),
            session,
            updatedSession,
            commitCount,
            persistenceMatchedAtCommit,
            characterPersistenceMatchedAtCommit,
            sessionCommittedBeforeFirstSend ?? false,
            persistedItems,
            persistedCharacter,
            usersRoot,
            playerItemsRoot);
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot items,
        IReadOnlyDictionary<string, string> attributes,
        string? usersRoot,
        long characterUid,
        uint selectedItemId)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "catalog-selector-test",
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: "CatalogTester",
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
                Y: 380,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 380,
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
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            LegacyCatalogSelectedItemId: selectedItemId);
    }

    private static void WriteCharacterSnapshot(string path, long uid, ulong rechargeProgress)
    {
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{uid}}
            Body=1003
            Face=0
            Name=CatalogTester
            Class=7051
            Map=1002
            X=430
            Y=380
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=1002
            PMapX=430
            PMapY=380
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
            RechargeProgress={{rechargeProgress}}
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
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem Item(
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
            StackSize = stackSize,
            Bound = bound,
        };
    }

    private static void AssertMutationFree(
        InvokeResult result,
        PlayerItemSnapshot expectedItems,
        bool expectPersistedItems = true)
    {
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        AssertItemsEqual(expectedItems, result.UpdatedSession.PlayerItems);
        if (expectPersistedItems)
        {
            AssertItemsEqual(expectedItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        }
    }

    private static void AssertItemRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(uid, packet.Id);
    }

    private static void AssertGrantedItem(byte[] bytes, uint itemId, byte expectedBless)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal((ushort)321, packet.Durability);
        Assert.Equal((ushort)321, packet.MaximumDurability);
        Assert.Equal(expectedBless, packet.Bless);
        Assert.Equal((byte)0, packet.Bound);
        Assert.Equal((ushort)1, packet.StackSize);
    }

    private static void AssertChatItem(byte[] bytes, uint itemId)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(1U, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal((ushort)321, packet.Durability);
        Assert.Equal((ushort)321, packet.MaximumDurability);
        Assert.Equal((ushort)9, LegacyGameItemPacket.ChatItemMode);
        Assert.Equal(LegacyGameItemPacket.ChatItemMode, packet.Mode);
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
        Assert.Equal(0U, packet.ProgressGreen);
        Assert.Equal((byte)0, packet.Suspicious);
        Assert.Equal((byte)0, packet.Locked);
        Assert.Equal(0U, packet.PlusProgress);
        Assert.Equal(0U, packet.RemainingTime);
        Assert.Equal(0U, packet.TimeLeftInMinutes);
        Assert.Equal((ushort)0, packet.StackSize);
        Assert.Equal(0U, packet.PerfectionLevel);
        Assert.Equal(0U, packet.PerfectionProgress);
        Assert.Equal(0U, packet.OwnerUid);
        Assert.Equal(string.Empty, packet.OwnerName);
        Assert.Equal(string.Empty, packet.Signature);
        Assert.Equal(0U, packet.RuneExperience);
        Assert.Equal(0U, packet.Attribute1);
        Assert.Equal(0U, packet.Attribute2);
        Assert.Equal(0U, packet.Attribute3);
        Assert.Equal(0U, packet.Attribute4);
        Assert.Equal(0U, packet.Attribute5);
        Assert.Equal(0U, packet.AnimaItemId);
        Assert.Equal(0U, packet.MythSoul);
        Assert.Equal(0U, packet.MythSoulPoints);
    }

    private static void AssertMessageBox(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, packet.InteractType);
        Assert.Contains(expectedText, packet.Text, StringComparison.Ordinal);
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var replies = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                replies.Add(packet);
            }
        }

        return replies;
    }

    private static void AssertItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
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

    private static WorldDataCatalog SourceCatalog()
    {
        return Catalog(
            [
                ItemType(FirstAccessoryItemId, "CaribbeanFlag"),
                ItemType(360_072, "Blackbeard`sFlag"),
                ItemType(DuplicateAccessoryItemId, "FloralHeart"),
                ItemType(LastAccessoryItemId, "BronzeDragonslayer(1-handed)"),
                ItemType(FirstMountItemId, "WingedFrostcatArmor"),
                ItemType(LastMountItemId, "Shadow Leopard (Blood)"),
                ItemType(FirstGarmentItemId, "GotTalent"),
                ItemType(LastGarmentItemId, "Pistil Sprite (Glamour)"),
            ],
            SourceCoatStorageRows());
    }

    private static CoatStorageType[] SourceCoatStorageRows()
    {
        return
        [
            CoatStorageType(200_475, garment: false, stars: 3, lineNumber: 1),
            CoatStorageType(FirstMountItemId, garment: false, stars: 5, lineNumber: 2),
            CoatStorageType(181_305, garment: true, stars: 1, lineNumber: 3),
            CoatStorageType(FirstGarmentItemId, garment: true, stars: 4, lineNumber: 4),
            CoatStorageType(LastMountItemId, garment: false, stars: 5, lineNumber: 5),
            CoatStorageType(LastGarmentItemId, garment: true, stars: 5, lineNumber: 6),
        ];
    }

    private static CoatStorageType CoatStorageType(
        uint itemId,
        bool garment,
        int stars,
        int lineNumber)
    {
        var kind = garment ? 1 : 2;
        var fields = new[]
        {
            itemId.ToString(CultureInfo.InvariantCulture),
            itemId.ToString(CultureInfo.InvariantCulture),
            kind.ToString(CultureInfo.InvariantCulture),
            "0",
            "0",
            "0",
            "0",
            stars.ToString(CultureInfo.InvariantCulture),
            string.Empty,
        };
        return new CoatStorageType(
            checked((int)itemId),
            StorageType: itemId,
            Parameters: [kind, 0, 0, 0, 0, stars],
            SourceFile: "coat_storage_type.txt",
            LineNumber: lineNumber,
            RawLine: string.Join("@@", fields),
            Fields: fields);
    }

    private static ItemType ItemType(uint itemId, string name)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] = "321";
        return new ItemType(checked((int)itemId), name, fields);
    }

    private static WorldDataCatalog Catalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<CoatStorageType> coatStorageTypes)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://catalog-selector-packs",
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
            coatStorageTypes: coatStorageTypes,
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

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        int CommitCount,
        bool PersistenceMatchedAtCommit,
        bool CharacterPersistenceMatchedAtCommit,
        bool SessionCommittedBeforeFirstSend,
        PlayerItemSnapshot? PersistedItems,
        CharacterSnapshot? PersistedCharacter,
        string UsersRoot,
        string PlayerItemsRoot);
}
