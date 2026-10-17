using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyP7SacredTreasuresMutationTests : IDisposable
{
    private const uint NpcId = 16_849;
    private const uint SavageBoneItemId = 3_004_181;
    private const uint WeaponSoulPackItemId = 3_004_247;
    private const uint EquipmentSoulPackItemId = 3_004_248;
    private const uint SacredRefineryPackItemId = 3_004_249;
    private const uint SmallMoneyBagItemId = 3_001_318;
    private const uint LargeMoneyBagItemId = 3_001_322;
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyP7SacredTreasuresMutationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EnglishMenusPreserveSourceFramesAndCpFormatting()
    {
        var root = await InvokeAsync(0, Items(), Catalog());
        var rootReplies = root.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(
            [
                "There`s a mysterious treasure sealed since the ancient times. With a Savage Bone, you can explore and have a chance to get Sacred refinery materials, rare",
                "~weapon accessories, and even the legendary P7 Dragon Soul.",
            ],
            rootReplies.Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Dialog).Select(packet => packet.Text));
        Assert.Contains(rootReplies, packet => packet.OptionId == 1 && packet.Text == "I~wanna~explore.");

        var paidMenu = await InvokeAsync(2, Items(), Catalog());
        var paidReplies = paidMenu.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(
            [
                "If you don`t have a Savage Bone, you can also pay 3700 or 17.000 CPs to explore. Of course, the more you pay, the better you get.",
                "~How much would you like to pay?",
            ],
            paidReplies.Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Dialog).Select(packet => packet.Text));
        Assert.Contains(paidReplies, packet => packet.OptionId == 3 && packet.Text == "3700~CPs.");
        Assert.Contains(paidReplies, packet => packet.OptionId == 5 && packet.Text == "17.000~CPs.");

        var smallConfirm = await InvokeAsync(3, Items(), Catalog());
        Assert.Contains(
            smallConfirm.Sent.Select(AssertNpcReply),
            packet => packet.Text == "Are you sure you want to use 3700 CPs to explored the P7 Sacred Treasures?");
        var largeConfirm = await InvokeAsync(5, Items(), Catalog());
        Assert.Contains(
            largeConfirm.Sent.Select(AssertNpcReply),
            packet => packet.Text == "Are you sure you want to use 17.000 CPs to explored the P7 Sacred Treasures?");
    }

    [Theory]
    [InlineData(1U, WeaponSoulPackItemId, "You received a P7 Weapon Soul Pack.")]
    [InlineData(199U, WeaponSoulPackItemId, "You received a P7 Weapon Soul Pack.")]
    [InlineData(200U, SacredRefineryPackItemId, "You received a SacredRefineryPack.")]
    [InlineData(600U, SacredRefineryPackItemId, "You received a SacredRefineryPack.")]
    [InlineData(601U, EquipmentSoulPackItemId, "You received a P7 Equipment Soul Pack.")]
    [InlineData(699U, EquipmentSoulPackItemId, "You received a P7 Equipment Soul Pack.")]
    [InlineData(700U, SacredRefineryPackItemId, "You received a SacredRefineryPack.")]
    [InlineData(999U, SacredRefineryPackItemId, "You received a SacredRefineryPack.")]
    public async Task SavageBoneRollBoundariesPersistExactPackAndSourcePacketOrder(
        uint treasureRoll,
        uint rewardItemId,
        string expectedMessage)
    {
        var originalItems = Items(Item(20, SavageBoneItemId));
        var result = await InvokeAsync(
            optionId: 10,
            originalItems,
            Catalog(ItemType(rewardItemId, durability: 321)),
            treasureRoll: treasureRoll);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == SavageBoneItemId);
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(21U, reward.Uid);
        Assert.Equal(rewardItemId, reward.ItemId);
        Assert.Equal(321, reward.Durability);
        Assert.Equal(0, reward.Bound);
        Assert.Equal(1, reward.StackSize);
        Assert.InRange(reward.Color, 3U, 8U);

        Assert.Equal(3, result.Sent.Count);
        AssertRemoval(result.Sent[0], 20);
        Assert.Equal(expectedMessage, AssertMessageBox(result.Sent[1]).Text);
        var rewardPacket = AssertGameItem(result.Sent[2]);
        Assert.Equal(rewardItemId, rewardPacket.ItemId);
        Assert.Equal(21U, rewardPacket.Uid);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);

        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains($"roll={treasureRoll}", result.Log);
        Assert.Contains("sourceOrder=remove-item+message-box+add-item", result.Log);
    }

    [Fact]
    public async Task SavageBoneGateIsUnboundButRemovalConsumesFirstBoundMatch()
    {
        var originalItems = Items(
            Item(10, SavageBoneItemId, stackSize: 2, bound: 1),
            Item(20, SavageBoneItemId));
        var result = await InvokeAsync(
            optionId: 10,
            originalItems,
            Catalog(ItemType(WeaponSoulPackItemId)),
            treasureRoll: 199);

        var bones = result.UpdatedSession.PlayerItems.InventoryItems
            .Where(item => item.ItemId == SavageBoneItemId)
            .OrderBy(item => item.Uid)
            .ToArray();
        Assert.Equal(2, bones.Length);
        Assert.Equal(10U, bones[0].Uid);
        Assert.Equal(1, bones[0].Bound);
        Assert.Equal(1, bones[0].StackSize);
        Assert.Equal(20U, bones[1].Uid);
        Assert.Equal(0, bones[1].Bound);
        Assert.Equal(1, bones[1].StackSize);
        var update = AssertGameItem(result.Sent[0]);
        Assert.Equal(10U, update.Uid);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, update.Mode);
    }

    [Fact]
    public async Task BoundAgnosticRemovalCanTakeZeroStackRecordBeforeGateItem()
    {
        var originalItems = Items(
            Item(5, SavageBoneItemId, stackSize: 0, bound: 1),
            Item(20, SavageBoneItemId));
        var result = await InvokeAsync(
            optionId: 10,
            originalItems,
            Catalog(ItemType(WeaponSoulPackItemId)),
            treasureRoll: 1);

        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 5);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20);
        AssertRemoval(result.Sent[0], 5);
    }

    [Fact]
    public async Task BoundOnlySavageBoneUsesExactSourceDialogWithoutMutation()
    {
        var originalItems = Items(Item(10, SavageBoneItemId, bound: 1));
        var result = await InvokeAsync(
            optionId: 10,
            originalItems,
            Catalog(ItemType(WeaponSoulPackItemId)),
            treasureRoll: 1);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        AssertExactDialog(
            result.Sent,
            "Sorry, you don`t have a Savage Bone.",
            "My~bad.",
            avatarId: 200);
    }

    [Fact]
    public async Task FullInventorySingletonBoneFreesSlotAndSucceeds()
    {
        var inventory = new List<PlayerItemSnapshotItem> { Item(50, SavageBoneItemId) };
        inventory.AddRange(Enumerable.Range(1, 39).Select(index => Item(
            checked((uint)index),
            checked((uint)(800_000 + index)))));
        var result = await InvokeAsync(
            optionId: 10,
            Items(inventory.ToArray()),
            Catalog(ItemType(SacredRefineryPackItemId)),
            treasureRoll: 200);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 50);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == SacredRefineryPackItemId);
    }

    [Fact]
    public async Task FullInventoryStackedBoneFencesWholeExchangeWithoutLoss()
    {
        var inventory = new List<PlayerItemSnapshotItem> { Item(50, SavageBoneItemId, stackSize: 2) };
        inventory.AddRange(Enumerable.Range(1, 39).Select(index => Item(
            checked((uint)index),
            checked((uint)(810_000 + index)))));
        var originalItems = Items(inventory.ToArray());
        var result = await InvokeAsync(
            optionId: 10,
            originalItems,
            Catalog(ItemType(SacredRefineryPackItemId)),
            treasureRoll: 200);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("inventory-full-after-planned-Savage-Bone-removal", result.Log);
    }

    public static TheoryData<byte, long, uint, bool, uint, int> PaidCases => new()
    {
        { 4, 3_700, 3, true, SavageBoneItemId, 2 },
        { 4, 3_700, 4, false, SmallMoneyBagItemId, 3 },
        { 6, 17_000, 19, true, SavageBoneItemId, 2 },
        { 6, 17_000, 20, false, LargeMoneyBagItemId, 3 },
    };

    [Theory]
    [MemberData(nameof(PaidCases))]
    public async Task PaidRateBoundariesPersistWalletAndExactRewardOrder(
        byte optionId,
        long cost,
        uint rateRoll,
        bool rateHit,
        uint rewardItemId,
        int expectedPacketCount)
    {
        const long startingCp = 20_000;
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog(ItemType(rewardItemId, durability: 77)),
            conquerPoints: startingCp,
            rateRoll: rateRoll);

        Assert.True(result.Handled);
        Assert.Equal(startingCp - cost, result.UpdatedSession.Character.ConquerPoints);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(11U, reward.Uid);
        Assert.Equal(77, reward.Durability);
        Assert.Equal(0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);
        Assert.Equal(expectedPacketCount, result.Sent.Count);

        var update = AssertScalarUpdate(result.Sent[0]);
        var cpEntry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, cpEntry.Type);
        Assert.Equal(checked((ulong)(startingCp - cost)), cpEntry.Value);
        var itemIndex = rateHit ? 1 : 2;
        if (!rateHit)
        {
            Assert.Equal("You~received~a~Money~Bag.", AssertMessageBox(result.Sent[1]).Text);
        }

        Assert.Equal(rewardItemId, AssertGameItem(result.Sent[itemIndex]).ItemId);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Equal(startingCp - cost, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
        Assert.Contains($"Rate({(optionId == 4 ? 4 : 20)})={rateHit.ToString().ToLowerInvariant()}", result.Log);
    }

    [Theory]
    [InlineData((byte)4, 3_699L)]
    [InlineData((byte)6, 16_999L)]
    public async Task InsufficientCpUsesExactSourceDialog(byte optionId, long conquerPoints)
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog(ItemType(SmallMoneyBagItemId)),
            conquerPoints: conquerPoints);

        AssertMutationFree(result, originalItems, conquerPoints);
        AssertExactDialog(
            result.Sent,
            "Sorry, but you don`t have enough CPs to buy that.",
            "Alright.",
            avatarId: 200);
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)6)]
    public async Task InTradePaidRequestIsSilentAndMutationFree(byte optionId)
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog(ItemType(SavageBoneItemId)),
            conquerPoints: 20_000,
            legacyTradeActive: true);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=in-trade; source branch is silent", result.Log);
    }

    [Theory]
    [InlineData((byte)4, 4)]
    [InlineData((byte)6, 3)]
    public async Task FullInventoryPaidRequestUsesSourceAvatarShape(byte optionId, int expectedFrames)
    {
        var inventory = Enumerable.Range(1, 40)
            .Select(index => Item(checked((uint)index), checked((uint)(820_000 + index))))
            .ToArray();
        var originalItems = Items(inventory);
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog(ItemType(SavageBoneItemId)),
            conquerPoints: 20_000);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Equal(expectedFrames, result.Sent.Count);
        AssertExactDialog(
            result.Sent,
            "Please make 1 space in your inventory.",
            "Ah~yes.",
            avatarId: optionId == 4 ? (ushort?)200 : null);
    }

    [Theory]
    [InlineData((byte)10)]
    [InlineData((byte)4)]
    [InlineData((byte)6)]
    public async Task MissingRewardDataDoesNotMutate(byte optionId)
    {
        var originalItems = optionId == 10 ? Items(Item(10, SavageBoneItemId)) : Items();
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog(),
            conquerPoints: 20_000,
            treasureRoll: 1,
            rateRoll: 0);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing", result.Log);
    }

    [Fact]
    public async Task PaidTransactionFailureRollsBackWalletAndItems()
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId: 4,
            originalItems,
            Catalog(ItemType(SavageBoneItemId)),
            conquerPoints: 20_000,
            rateRoll: 0,
            persistedCharacterUid: CharacterUid + 1);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Equal(20_000, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
        AssertPlayerItemsEqual(originalItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains("uidMismatch", result.Log);
    }

    [Fact]
    public async Task ItemPersistenceFailureDoesNotCommitOrSend()
    {
        Directory.CreateDirectory(_root);
        var blockedRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedRoot, "not a directory");
        var originalItems = Items(Item(10, SavageBoneItemId));
        var result = await InvokeAsync(
            optionId: 10,
            originalItems,
            Catalog(ItemType(WeaponSoulPackItemId)),
            treasureRoll: 1,
            playerItemsRootOverride: blockedRoot,
            seedPlayerItems: false);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("item-persistence-", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedRoot));
    }

    [Fact]
    public async Task ExhaustedRewardUidDoesNotConsumeOrCharge()
    {
        var originalItems = Items(Item(uint.MaxValue, SavageBoneItemId));
        var result = await InvokeAsync(
            optionId: 10,
            originalItems,
            Catalog(ItemType(WeaponSoulPackItemId)),
            treasureRoll: 1);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("item-uid-exhausted", result.Log);
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
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        long conquerPoints = 20_000,
        uint treasureRoll = 1,
        uint rateRoll = 0,
        bool legacyTradeActive = false,
        bool includeSessionStore = true,
        bool includePlayerItemsRoot = true,
        bool includeUsersRoot = true,
        string? playerItemsRootOverride = null,
        bool seedPlayerItems = true,
        long? persistedCharacterUid = null)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = WriteCharacterSnapshot(
            usersRoot,
            persistedCharacterUid ?? CharacterUid,
            conquerPoints);
        var playerItemsRoot = playerItemsRootOverride ?? Path.Combine(invocationRoot, "PlayersItems");
        if (includePlayerItemsRoot && seedPlayerItems)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyP7SacredTreasureRoll"] = treasureRoll.ToString(CultureInfo.InvariantCulture),
            ["LegacyP7SacredRateRoll"] = rateRoll.ToString(CultureInfo.InvariantCulture),
        };
        var session = CreateSession(
            originalItems,
            conquerPoints,
            attributes,
            includeUsersRoot ? usersRoot : null,
            legacyTradeActive);
        var sessions = includeSessionStore ? new WorldSessionStore() : null;
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        sessions?.Set(connection, session);

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
            NpcId,
            optionId,
            CancellationToken.None);

        var updatedSession = session;
        if (sessions?.TryGet(connection, out var stored) == true && stored is not null)
        {
            updatedSession = stored;
        }

        PlayerItemSnapshot? persistedItems = null;
        var itemPath = Path.Combine(playerItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        if (includePlayerItemsRoot && Directory.Exists(playerItemsRoot) && File.Exists(itemPath))
        {
            persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
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
            persistedItems,
            persistedCharacter);
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot items,
        long conquerPoints,
        IReadOnlyDictionary<string, string> attributes,
        string? usersRoot,
        bool legacyTradeActive)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "p7-sacred-treasures-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "SacredExplorer",
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
                ConquerPoints: conquerPoints,
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
            LegacyTradeActive: legacyTradeActive);
    }

    private static string WriteCharacterSnapshot(string usersRoot, long uid, long conquerPoints)
    {
        var path = Path.Combine(usersRoot, AccountId + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{uid}}
            Body=1003
            Face=0
            Name=SacredExplorer
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
            ConquerPoints={{conquerPoints}}
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            """);
        return path;
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
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
        };
    }

    private static ItemType ItemType(uint itemId, ushort durability = 1)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "P7Reward";
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), "P7Reward", fields);
    }

    private static void AssertMutationFree(InvokeResult result, PlayerItemSnapshot originalItems, long conquerPoints)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(conquerPoints, result.UpdatedSession.Character.ConquerPoints);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
    }

    private static void AssertRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static LegacyNpcReplyPacket AssertMessageBox(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, packet.InteractType);
        return packet;
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

    private static LegacyMsgUpdatePacket AssertScalarUpdate(byte[] bytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string text,
        string optionText,
        ushort? avatarId)
    {
        var expectedCount = avatarId.HasValue ? 4 : 3;
        Assert.Equal(expectedCount, sent.Count);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[0]), out var dialog));
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(text, dialog.Text);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[1]), out var option));
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(optionText, option.Text);
        if (avatarId.HasValue)
        {
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[2]), out var avatar));
            Assert.Equal(LegacyNpcReplyInteractType.Avatar, avatar.InteractType);
            Assert.Equal(avatarId.Value, avatar.InputMaxLength);
        }

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[^1]), out var finish));
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
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

    private static WorldDataCatalog Catalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://p7-sacred-treasures",
                new Dictionary<string, int>()),
            clientConfigs: [], items: items, itemAddBonuses: [], levelExperience: [], runeLevelExperience: [],
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
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, string.Empty, 0, []), [], "fixture://activity"),
            meltingTypeRules: [], lotteryPrizes: [], arenaRecords: [], elitePkRecords: [], teamElitePkRecords: [],
            skillTeamPkRecords: [], classPkWarRecords: [], combatGearRanks: [], prestigeRanks: [], innerPowerRanks: [],
            runeRankings: [], staticStatues: [], newPlayerRewards: [], votes: [], couplesPkRecords: [],
            socialAssociations: [], guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"), classLevelStats: [], professionLevelUps: [],
            professionLevelBenefits: [], professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [], questMissions: [], quizQuestions: [], magic: [], magicEffects: [],
            magicTypeOperations: [], titleTypes: [], combatGear: [], traps: [], npcs: [], sobNpcs: [], portals: [],
            maps: [], mapNames: [], monsters: [], monsterSpawns: [], lotteryDictionary: [], collectionRewardTiers: [],
            taskRewardTypes: [], taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, string.Empty, new Dictionary<string, string>()),
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

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        PlayerItemSnapshot? PersistedItems,
        CharacterSnapshot? PersistedCharacter);
}
