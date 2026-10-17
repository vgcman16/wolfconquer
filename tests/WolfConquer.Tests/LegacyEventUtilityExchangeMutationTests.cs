using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyEventUtilityExchangeMutationTests : IDisposable
{
    private const uint SwapperStarryNpcId = 832_663;
    private const uint ElementalPoolNpcId = 832_664;
    private const uint RedFlowerItemId = 722_732;
    private const uint WhiteFlowerItemId = 722_736;
    private const uint YinYangSealItemId = 3_004_462;
    private const uint StardustItemId = 3_004_465;
    private const uint ShadowScrollItemId = 3_004_459;
    private const uint TwilightScrollItemId = 3_004_460;
    private const uint FatalScrollItemId = 3_004_461;
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyEventUtilityExchangeMutationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FlowerRateHitConsumesRedThenWhiteBoundAgnosticallyAndGrantsSeal()
    {
        var originalItems = Items(
            Item(10, RedFlowerItemId, stackSize: 2, bound: 1),
            Item(20, RedFlowerItemId, stackSize: 4),
            Item(30, WhiteFlowerItemId, bound: 1),
            Item(40, WhiteFlowerItemId, stackSize: 4));
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 3,
            originalItems,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal", durability: 321)),
            rateRoll: 29);

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal((ushort)2, Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 20).StackSize);
        Assert.Equal((ushort)1, Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 40).StackSize);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid is 10 or 30);

        var seal = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == YinYangSealItemId);
        Assert.Equal((uint)41, seal.Uid);
        Assert.Equal((ushort)321, seal.Durability);
        Assert.Equal((ushort)321, seal.MaximumDurability);
        Assert.Equal((byte)0, seal.Bound);
        Assert.InRange(seal.Color, 3U, 8U);

        Assert.Collection(
            result.Sent,
            frame => AssertItemUpdate(frame, 10, RedFlowerItemId, stackSize: 1, bound: 1),
            frame => AssertItemRemoval(frame, 10),
            frame => AssertItemUpdate(frame, 20, RedFlowerItemId, stackSize: 3, bound: 0),
            frame => AssertItemUpdate(frame, 20, RedFlowerItemId, stackSize: 2, bound: 0),
            frame => AssertItemRemoval(frame, 30),
            frame => AssertItemUpdate(frame, 40, WhiteFlowerItemId, stackSize: 3, bound: 0),
            frame => AssertItemUpdate(frame, 40, WhiteFlowerItemId, stackSize: 2, bound: 0),
            frame => AssertItemUpdate(frame, 40, WhiteFlowerItemId, stackSize: 1, bound: 0),
            frame => AssertGrant(frame, 41, YinYangSealItemId, durability: 321));
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains("Rate(30)=true", result.Log);
        Assert.Contains("rateRoll=29", result.Log);
        Assert.Contains("sourceOrder=RemoveRed,RemoveWhite,Rate,RewardOrFailureDialog", result.Log);
    }

    [Fact]
    public async Task FlowerRateBoundaryMissConsumesBothSetsBeforeExactFailureDialog()
    {
        var originalItems = Items(
            Item(10, RedFlowerItemId, stackSize: 4),
            Item(20, WhiteFlowerItemId, stackSize: 4));
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 3,
            originalItems,
            Catalog(),
            rateRoll: 30);

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(12, result.Sent.Count);
        AssertItemUpdate(result.Sent[0], 10, RedFlowerItemId, 3, 0);
        AssertItemUpdate(result.Sent[1], 10, RedFlowerItemId, 2, 0);
        AssertItemUpdate(result.Sent[2], 10, RedFlowerItemId, 1, 0);
        AssertItemRemoval(result.Sent[3], 10);
        AssertItemUpdate(result.Sent[4], 20, WhiteFlowerItemId, 3, 0);
        AssertItemUpdate(result.Sent[5], 20, WhiteFlowerItemId, 2, 0);
        AssertItemUpdate(result.Sent[6], 20, WhiteFlowerItemId, 1, 0);
        AssertItemRemoval(result.Sent[7], 20);
        AssertExactDialog(
            result.Sent.Skip(8).ToArray(),
            "You wasn`t very lucky today .. I hope you will have a better luck next time ^^.",
            "Ah~OK",
            avatarId: 203);
        Assert.DoesNotContain(result.Sent, IsItemGrant);
        Assert.Contains("Rate(30)=false", result.Log);
        Assert.Contains("rateRoll=30", result.Log);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Fact]
    public async Task FlowerRewardUidAdvancesPastConsumedHighestUid()
    {
        var originalItems = Items(
            Item(10, RedFlowerItemId, stackSize: 4),
            Item(20, WhiteFlowerItemId, stackSize: 4));
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 3,
            originalItems,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal")),
            rateRoll: 29);

        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(YinYangSealItemId, reward.ItemId);
        Assert.Equal((uint)21, reward.Uid);
        AssertGrant(result.Sent[^1], 21, YinYangSealItemId, durability: 1);
    }

    [Fact]
    public async Task FlowerContainGateRequiresFourUnboundUnitsPerFlowerClass()
    {
        var originalItems = Items(
            Item(10, RedFlowerItemId, stackSize: 2),
            Item(11, RedFlowerItemId, stackSize: 2, bound: 1),
            Item(20, WhiteFlowerItemId, stackSize: 4));
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 3,
            originalItems,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal")),
            rateRoll: 29);

        AssertMutationFree(result, originalItems);
        AssertExactDialog(
            result.Sent,
            "Please come back when you will have at inventory 4 Red Flowers and 4 White Flowers.",
            "Ah~OK",
            avatarId: 203);
    }

    [Fact]
    public async Task SealPurchaseAtExactCostPersistsBothFilesBeforeCommitAndSendsWalletThenItem()
    {
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 12,
            PlayerItemSnapshot.Empty,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal", durability: 456)),
            conquerPoints: 1_000);

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.Equal(0, result.UpdatedSession.Character.ConquerPoints);
        var seal = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(YinYangSealItemId, seal.ItemId);
        Assert.Equal((ushort)456, seal.Durability);
        Assert.Equal((byte)0, seal.Bound);
        Assert.InRange(seal.Color, 3U, 8U);

        Assert.Collection(
            result.Sent,
            frame => AssertConquerPointsUpdate(frame, expected: 0),
            frame => AssertGrant(frame, seal.Uid, YinYangSealItemId, durability: 456));
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.CharacterPersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        Assert.Equal(0, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains("sourceOrder=ConquerPoints,AddItem", result.Log);
    }

    [Fact]
    public async Task SealPurchaseInTradeSilentlyNoOps()
    {
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 12,
            PlayerItemSnapshot.Empty,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal")),
            conquerPoints: 1_000,
            legacyTradeActive: true);

        AssertMutationFree(result, PlayerItemSnapshot.Empty);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=in-trade; source branch is silent", result.Log);
    }

    [Fact]
    public async Task SealPurchaseInsufficientCpUsesExactAvatarSevenDialog()
    {
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 12,
            PlayerItemSnapshot.Empty,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal")),
            conquerPoints: 999);

        AssertMutationFree(result, PlayerItemSnapshot.Empty);
        AssertExactDialog(
            result.Sent,
            "Sorry, but you don`t have enough CPs to buy that.",
            "Alright.",
            avatarId: 7);
    }

    [Fact]
    public async Task SealPurchaseFullInventoryUsesExactAvatar203Dialog()
    {
        var fullItems = Items(Enumerable.Range(0, 40)
            .Select(index => Item(checked((uint)(100 + index)), checked((uint)(410_000 + index))))
            .ToArray());
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 12,
            fullItems,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal")),
            conquerPoints: 1_000);

        AssertMutationFree(result, fullItems);
        AssertExactDialog(
            result.Sent,
            "Please make 1 more space in your inventory",
            "Let me check.",
            avatarId: 203);
    }

    [Theory]
    [InlineData((byte)10, ShadowScrollItemId)]
    [InlineData((byte)11, TwilightScrollItemId)]
    [InlineData((byte)12, FatalScrollItemId)]
    public async Task StardustOptionsMapToExactScrollAndAdvancePastConsumedHighestUid(
        byte optionId,
        uint rewardItemId)
    {
        var originalItems = Items(Item(10, StardustItemId));
        var result = await InvokeAsync(
            ElementalPoolNpcId,
            optionId,
            originalItems,
            Catalog(ItemType(rewardItemId, "Scroll-" + rewardItemId, durability: 222)));

        Assert.True(result.Handled);
        Assert.Equal(1, result.CommitCount);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == StardustItemId);
        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(rewardItemId, reward.ItemId);
        Assert.Equal((uint)11, reward.Uid);
        Assert.Equal((byte)0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);
        Assert.Collection(
            result.Sent,
            frame => AssertItemRemoval(frame, 10),
            frame => AssertGrant(frame, 11, rewardItemId, durability: 222));
        Assert.DoesNotContain(result.Sent, IsNpcReply);
        Assert.True(result.PersistenceMatchedAtCommit);
        Assert.True(result.SessionCommittedBeforeFirstSend);
        Assert.Contains("sourceOrder=Remove,Add", result.Log);
    }

    [Fact]
    public async Task StardustUnboundGatePassesButRemoveConsumesLowerUidBoundItem()
    {
        var originalItems = Items(
            Item(10, StardustItemId, stackSize: 2, bound: 1),
            Item(20, StardustItemId));
        var result = await InvokeAsync(
            ElementalPoolNpcId,
            optionId: 10,
            originalItems,
            Catalog(ItemType(ShadowScrollItemId, "ShadowScroll")));

        var boundDust = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 10);
        Assert.Equal((ushort)1, boundDust.StackSize);
        Assert.Equal((byte)1, boundDust.Bound);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20 && item.Bound == 0);
        AssertItemUpdate(result.Sent[0], 10, StardustItemId, stackSize: 1, bound: 1);
        Assert.Equal(ShadowScrollItemId, AssertGameItem(result.Sent[1]).ItemId);
    }

    [Fact]
    public async Task BoundOnlyStardustDoesNotSatisfySourceContainGate()
    {
        var originalItems = Items(Item(10, StardustItemId, bound: 1));
        var result = await InvokeAsync(
            ElementalPoolNpcId,
            optionId: 10,
            originalItems,
            Catalog(ItemType(ShadowScrollItemId, "ShadowScroll")));

        AssertMutationFree(result, originalItems);
        AssertExactDialog(
            result.Sent,
            "Sorry, you not have the Stardust in your inventory.",
            "Okay.",
            avatarId: 203);
    }

    [Theory]
    [InlineData(SwapperStarryNpcId, (byte)3)]
    [InlineData(SwapperStarryNpcId, (byte)12)]
    [InlineData(ElementalPoolNpcId, (byte)10)]
    public async Task MissingRewardDataLeavesEveryExchangeMutationFree(uint npcId, byte optionId)
    {
        var originalItems = optionId switch
        {
            3 => Items(Item(10, RedFlowerItemId, 4), Item(20, WhiteFlowerItemId, 4)),
            10 => Items(Item(10, StardustItemId)),
            _ => PlayerItemSnapshot.Empty,
        };
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            Catalog(),
            conquerPoints: optionId == 12 ? 1_000 : 0,
            rateRoll: 29);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing", result.Log);
    }

    [Fact]
    public async Task ExhaustedItemUidDoesNotConsumeStardust()
    {
        var originalItems = Items(
            Item(10, StardustItemId),
            Item(uint.MaxValue, 729_999));
        var result = await InvokeAsync(
            ElementalPoolNpcId,
            optionId: 10,
            originalItems,
            Catalog(ItemType(ShadowScrollItemId, "ShadowScroll")));

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("item-uid-exhausted", result.Log);
    }

    [Fact]
    public async Task FullInventoryAfterBoundAgnosticStardustUpdateDoesNotConsume()
    {
        var inventory = new List<PlayerItemSnapshotItem>
        {
            Item(10, StardustItemId, stackSize: 2, bound: 1),
            Item(20, StardustItemId),
        };
        inventory.AddRange(Enumerable.Range(0, 38)
            .Select(index => Item(checked((uint)(100 + index)), checked((uint)(410_000 + index)))));
        var originalItems = Items(inventory.ToArray());
        var result = await InvokeAsync(
            ElementalPoolNpcId,
            optionId: 10,
            originalItems,
            Catalog(ItemType(ShadowScrollItemId, "ShadowScroll")));

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("inventory is full; no Stardust was consumed", result.Log);
    }

    [Theory]
    [InlineData("session-store", "session-store-missing")]
    [InlineData("session-registration", "session-registration-missing")]
    [InlineData("player-items-root", "player-items-root-missing")]
    [InlineData("character-uid", "character-uid-unavailable")]
    [InlineData("users-root", "users-root-missing")]
    public async Task MissingMutationDependenciesDoNotPersistCommitOrSend(
        string failure,
        string expectedReason)
    {
        var purchase = failure == "users-root";
        var originalItems = purchase
            ? PlayerItemSnapshot.Empty
            : Items(Item(10, StardustItemId));
        var result = await InvokeAsync(
            purchase ? SwapperStarryNpcId : ElementalPoolNpcId,
            optionId: purchase ? (byte)12 : (byte)10,
            originalItems,
            Catalog(ItemType(
                purchase ? YinYangSealItemId : ShadowScrollItemId,
                purchase ? "YinYangSeal" : "ShadowScroll")),
            conquerPoints: purchase ? 1_000 : 0,
            includeSessionStore: failure != "session-store",
            registerSession: failure != "session-registration",
            includePlayerItemsRoot: failure != "player-items-root",
            includeUsersRoot: failure != "users-root",
            characterUid: failure == "character-uid" ? 0 : CharacterUid);

        AssertMutationFree(result, originalItems, expectPersistedItems: failure != "player-items-root");
        Assert.Empty(result.Sent);
        Assert.Contains(expectedReason, result.Log);
    }

    [Fact]
    public async Task ItemPersistenceFailureDoesNotCommitOrSendMutationFrames()
    {
        Directory.CreateDirectory(_root);
        var blockedRoot = Path.Combine(_root, "blocked-items-root");
        File.WriteAllText(blockedRoot, "not a directory");
        var originalItems = Items(Item(10, StardustItemId));
        var result = await InvokeAsync(
            ElementalPoolNpcId,
            optionId: 10,
            originalItems,
            Catalog(ItemType(ShadowScrollItemId, "ShadowScroll")),
            playerItemsRootOverride: blockedRoot,
            seedPlayerItems: false);

        AssertMutationFree(result, originalItems, expectPersistedItems: false);
        Assert.Empty(result.Sent);
        Assert.Contains("Stardust exchange persistence failed", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedRoot));
    }

    [Fact]
    public async Task CharacterPersistenceFailureRollsBackPurchasedItemFile()
    {
        var originalItems = PlayerItemSnapshot.Empty;
        var result = await InvokeAsync(
            SwapperStarryNpcId,
            optionId: 12,
            originalItems,
            Catalog(ItemType(YinYangSealItemId, "YinYangSeal")),
            conquerPoints: 1_000,
            persistedCharacterUid: CharacterUid + 1);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        AssertPlayerItemsEqual(originalItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Equal(1_000, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
        Assert.Contains("reason=uidMismatch", result.Log);
        Assert.Contains("itemRollback=complete", result.Log);
        Assert.Contains("characterRollback=complete", result.Log);
    }

    [Theory]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public async Task ElementalMonsterSpawnOptionsRemainFenced(byte optionId)
    {
        var originalItems = Items(Item(10, optionId == 2 ? 3_004_463U : YinYangSealItemId));
        var result = await InvokeAsync(
            ElementalPoolNpcId,
            optionId,
            originalItems,
            Catalog());

        AssertMutationFree(result, originalItems);
        AssertExactDialog(
            result.Sent,
            "Elemental monster opening is unavailable here.",
            "I understand.",
            avatarId: 203);
        Assert.Contains("PARITY-GAP LegacyNpc EventUtilityNpcs", result.Log);
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
        long conquerPoints = 0,
        uint rateRoll = 29,
        bool legacyTradeActive = false,
        bool includeSessionStore = true,
        bool registerSession = true,
        bool includePlayerItemsRoot = true,
        bool includeUsersRoot = true,
        string? playerItemsRootOverride = null,
        bool seedPlayerItems = true,
        long characterUid = CharacterUid,
        long? persistedCharacterUid = null)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = WriteCharacterSnapshot(
            usersRoot,
            persistedCharacterUid ?? characterUid,
            conquerPoints);
        var playerItemsRoot = playerItemsRootOverride ?? Path.Combine(invocationRoot, "PlayersItems");
        if (includePlayerItemsRoot && seedPlayerItems)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(characterUid, originalItems);
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacySwapperStarryRateRoll"] = rateRoll.ToString(CultureInfo.InvariantCulture),
        };
        var session = CreateSession(
            originalItems,
            conquerPoints,
            attributes,
            includeUsersRoot ? usersRoot : null,
            characterUid,
            legacyTradeActive);
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

                if (includeUsersRoot && File.Exists(characterPath))
                {
                    var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
                    characterPersistenceMatchedAtCommit =
                        persisted.ConquerPoints == current.Character.ConquerPoints;
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
        if (sessions is not null && registerSession)
        {
            sessions.Set(connection, session);
        }

        using var log = new StringWriter();
        var handled = await WorldPacketHandlers.TryHandleLegacyEventUtilityNpcAsync(
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
        if (includePlayerItemsRoot && Directory.Exists(playerItemsRoot) && File.Exists(itemsPath))
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
            persistedCharacter);
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot items,
        long conquerPoints,
        IReadOnlyDictionary<string, string> attributes,
        string? usersRoot,
        long characterUid,
        bool legacyTradeActive)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "event-utility-exchange-test",
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: "UtilityExchangeHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 3_851,
                X: 27,
                Y: 40,
                PreviousMapId: 3_851,
                PreviousMapX: 27,
                PreviousMapY: 40,
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

    private static string WriteCharacterSnapshot(
        string usersRoot,
        long persistedCharacterUid,
        long conquerPoints)
    {
        var path = Path.Combine(usersRoot, AccountId + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{persistedCharacterUid}}
            Body=1003
            Face=0
            Name=UtilityExchangeHero
            Class=7051
            Map=3851
            X=27
            Y=40
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=3851
            PMapX=27
            PMapY=40
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

    private static void AssertMutationFree(
        InvokeResult result,
        PlayerItemSnapshot expectedItems,
        bool expectPersistedItems = true)
    {
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        AssertPlayerItemsEqual(expectedItems, result.UpdatedSession.PlayerItems);
        Assert.Equal(
            result.OriginalSession.Character.ConquerPoints,
            result.UpdatedSession.Character.ConquerPoints);
        if (expectPersistedItems)
        {
            AssertPlayerItemsEqual(expectedItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        }
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string expectedText,
        string expectedOption,
        ushort avatarId)
    {
        Assert.Equal(4, sent.Count);
        var text = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(expectedText, text.Text);

        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(expectedOption, option.Text);
        Assert.Equal(byte.MaxValue, option.OptionId);

        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(avatarId, avatar.InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static void AssertItemUpdate(
        byte[] bytes,
        uint uid,
        uint itemId,
        ushort stackSize,
        byte bound)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        Assert.Equal(stackSize, packet.StackSize);
        Assert.Equal(bound, packet.Bound);
    }

    private static void AssertItemRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(uid, packet.Id);
    }

    private static void AssertGrant(
        byte[] bytes,
        uint uid,
        uint itemId,
        ushort durability)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal(durability, packet.Durability);
        Assert.Equal(durability, packet.MaximumDurability);
        Assert.Equal((byte)0, packet.Bound);
        Assert.Equal((ushort)1, packet.StackSize);
    }

    private static void AssertConquerPointsUpdate(byte[] bytes, ulong expected)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(CharacterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(expected, update.Value);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static bool IsItemGrant(byte[] bytes)
    {
        return LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Mode == LegacyGameItemPacket.AddItemMode;
    }

    private static bool IsNpcReply(byte[] bytes)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _);
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
                "fixture://event-utility-exchanges",
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

    private static ItemType ItemType(uint itemId, string name, ushort durability = 1)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] =
            durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), name, fields);
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
        CharacterSnapshot? PersistedCharacter);
}
