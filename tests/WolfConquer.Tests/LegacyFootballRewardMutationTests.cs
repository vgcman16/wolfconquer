using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFootballRewardMutationTests : IDisposable
{
    private const uint CharacterUid = 1_294_001;
    private const long AccountId = CharacterUid;
    private const uint FootballNpcId = 52_001;
    private const ushort CatalogDurability = 321;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyFootballRewardMutationTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, uint, uint, string> RewardCases => new()
    {
        { 3, 30, 4_200_009, "EuroSilveTrophy" },
        { 4, 60, 4_200_010, "EuroGoldTrophy" },
    };

    public static TheoryData<byte, uint> PointBranchCases => new()
    {
        { 3, 30 },
        { 4, 60 },
    };

    [Theory]
    [MemberData(nameof(RewardCases))]
    public async Task ExactThresholdPersistsBeforeExposureThenAddsItemAndBroadcastsToAllActiveSessions(
        byte optionId,
        uint cost,
        uint rewardItemId,
        string rewardName)
    {
        var originalItems = Items(InventoryItem(41, 400_041));
        var result = await InvokeAsync(
            optionId,
            cost.ToString(CultureInfo.InvariantCulture),
            originalItems,
            CreateWorldDataCatalog(ItemType(rewardItemId)),
            observerCount: 2);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(0U, FootballPoints(result.UpdatedSession.Character));

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(42U, reward.Uid);
        AssertRewardItem(reward, rewardItemId);

        Assert.NotNull(result.PersistedAtCommit);
        Assert.Equal(0U, FootballPoints(result.PersistedAtCommit!.Character));
        var committedReward = Assert.Single(
            result.PersistedAtCommit.Items.InventoryItems,
            item => item.ItemId == rewardItemId);
        AssertRewardItem(committedReward, rewardItemId);
        Assert.NotNull(result.ReloadedCharacter);
        Assert.Equal(0U, FootballPoints(result.ReloadedCharacter!));
        Assert.NotNull(result.PersistedItems);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, result.PersistedItems!);
        Assert.Contains(
            "MyFootBallPoints=0",
            File.ReadAllLines(result.CharacterPath),
            StringComparer.Ordinal);

        Assert.Equal("commit", result.ExposureOrder[0]);
        Assert.Equal("claimant:2116", result.ExposureOrder[1]);
        Assert.Equal(5, result.ExposureOrder.Count);

        Assert.Equal(2, result.ClaimantSent.Count);
        var itemFrame = ReadFrame(result.ClaimantSent[0]);
        Assert.Equal((ushort)2_116, itemFrame.Header.Opcode);
        Assert.Equal(LegacyGameItemPacket.Opcode, itemFrame.Header.Opcode);
        Assert.True(LegacyGameItemPacket.TryParse(itemFrame, out var itemPacket));
        Assert.Equal(reward.Uid, itemPacket.Uid);
        Assert.Equal(rewardItemId, itemPacket.ItemId);
        Assert.Equal(CatalogDurability, itemPacket.Durability);
        Assert.Equal(CatalogDurability, itemPacket.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Mode);
        Assert.Equal((byte)0, itemPacket.Position);
        Assert.Equal((ushort)1, itemPacket.StackSize);
        Assert.Equal((byte)0, itemPacket.Bound);
        Assert.Equal((byte)0, itemPacket.Plus);
        Assert.Equal((byte)0, itemPacket.Bless);
        Assert.Equal((byte)0, itemPacket.Enchant);
        Assert.Equal((byte)0, itemPacket.SocketOne);
        Assert.Equal((byte)0, itemPacket.SocketTwo);
        Assert.Equal(0U, itemPacket.SocketProgress);
        Assert.Equal((byte)0, itemPacket.Effect);
        Assert.Equal(0U, itemPacket.SpellId);
        Assert.Equal(0U, itemPacket.PerfectionLevel);
        Assert.Equal(0U, itemPacket.PerfectionProgress);

        var expectedTalk =
            $"FootballRewardHero has claimed {rewardName} from Football Tournament npc";
        AssertExactGlobalTalk(result.ClaimantSent[1], expectedTalk);
        Assert.All(
            result.ObserverSent,
            observerFrames => AssertExactGlobalTalk(Assert.Single(observerFrames), expectedTalk));
        Assert.Equal(2, result.ObserverSent.Count);
        Assert.DoesNotContain(
            result.ClaimantSent,
            bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(
            result.ClaimantSent,
            bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains("packetOrder=GameItemAdd,GlobalTalk", result.Log);
    }

    [Theory]
    [MemberData(nameof(PointBranchCases))]
    public async Task InsufficientPointsWithoutSuccessInfrastructureSendsExactDialogAndMutatesNothing(
        byte optionId,
        uint cost)
    {
        var originalItems = Items(InventoryItem(41, 400_041));
        var result = await InvokeAsync(
            optionId,
            checked(cost - 1).ToString(CultureInfo.InvariantCulture),
            originalItems,
            worldData: null,
            includeSessions: false,
            includeUsersRoot: false,
            includePlayerItemsRoot: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(cost - 1, FootballPoints(result.UpdatedSession.Character));
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(result.OriginalItemBytes, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(
            ["claimant:2255", "claimant:2255", "claimant:2255"],
            result.ExposureOrder);

        Assert.Equal(3, result.ClaimantSent.Count);
        Assert.All(result.ClaimantSent, bytes => Assert.Equal((ushort)2_255, ReadFrame(bytes).Header.Opcode));
        var replies = ParseNpcReplies(result.ClaimantSent);
        Assert.Equal(
            [
                LegacyNpcReplyInteractType.Dialog,
                LegacyNpcReplyInteractType.Option,
                LegacyNpcReplyInteractType.Finish,
            ],
            replies.Select(reply => reply.InteractType).ToArray());
        Assert.Equal("Sorry, you do not have enough Football points.", replies[0].Text);
        Assert.Equal("I understand.", replies[1].Text);
        Assert.Equal(byte.MaxValue, replies[1].OptionId);
        Assert.All(replies, AssertAsciiReply);
        Assert.DoesNotContain(result.ClaimantSent, bytes => ReadFrame(bytes).Header.Opcode == LegacyGameItemPacket.Opcode);
        Assert.DoesNotContain(result.ClaimantSent, bytes => ReadFrame(bytes).Header.Opcode == LegacyMsgMessagePacket.Opcode);
    }

    [Theory]
    [MemberData(nameof(PointBranchCases))]
    public async Task FullBagWinsOverInsufficientPointsAndNeedsNoSuccessInfrastructure(
        byte optionId,
        uint cost)
    {
        var fullInventory = Items(
            Enumerable.Range(1, 40)
                .Select(index => InventoryItem(checked((uint)index), checked((uint)(400_000 + index))))
                .ToArray());
        var result = await InvokeAsync(
            optionId,
            checked(cost - 1).ToString(CultureInfo.InvariantCulture),
            fullInventory,
            worldData: null,
            includeSessions: false,
            includeUsersRoot: false,
            includePlayerItemsRoot: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(cost - 1, FootballPoints(result.UpdatedSession.Character));
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(result.OriginalItemBytes, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Null(result.PersistedAtCommit);
        Assert.Equal(["claimant:2255"], result.ExposureOrder);

        var frame = ReadFrame(Assert.Single(result.ClaimantSent));
        Assert.Equal((ushort)2_255, frame.Header.Opcode);
        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("Please make 1 more space in your inventory.", reply.Text);
        AssertAsciiReply(reply);
    }

    [Fact]
    public async Task FootballPointsAliasIsReadButCanonicalSourceKeyIsAddedAndPersisted()
    {
        const uint cost = 30;
        var result = await InvokeAsync(
            optionId: 3,
            footballPoints: cost.ToString(CultureInfo.InvariantCulture),
            originalItems: Items(InventoryItem(41, 400_041)),
            worldData: CreateWorldDataCatalog(ItemType(4_200_009)),
            pointKey: "FootballPoints");

        Assert.True(result.Handled);
        Assert.Equal(0U, FootballPoints(result.UpdatedSession.Character));
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["MyFootBallPoints"]);
        Assert.Equal("30", result.UpdatedSession.Character.Attributes["FootballPoints"]);
        Assert.NotNull(result.ReloadedCharacter);
        Assert.Equal("0", result.ReloadedCharacter!.Attributes["MyFootBallPoints"]);
        Assert.Equal("30", result.ReloadedCharacter.Attributes["FootballPoints"]);
        var persistedLines = File.ReadAllLines(result.CharacterPath);
        Assert.Contains("FootballPoints=30", persistedLines, StringComparer.Ordinal);
        Assert.Contains("MyFootBallPoints=0", persistedLines, StringComparer.Ordinal);
    }

    [Fact]
    public async Task UInt32MaximumFootballPointsIsAcceptedAndSafelyDebited()
    {
        var result = await InvokeAsync(
            optionId: 4,
            footballPoints: uint.MaxValue.ToString(CultureInfo.InvariantCulture),
            originalItems: Items(InventoryItem(41, 400_041)),
            worldData: CreateWorldDataCatalog(ItemType(4_200_010)));

        var expected = checked(uint.MaxValue - 60U);
        Assert.True(result.Handled);
        Assert.Equal(expected, FootballPoints(result.UpdatedSession.Character));
        Assert.NotNull(result.ReloadedCharacter);
        Assert.Equal(expected, FootballPoints(result.ReloadedCharacter!));
        Assert.Equal(2, result.ClaimantSent.Count);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("4294967296")]
    [InlineData("-1")]
    public async Task MalformedOrOutOfRangeSourcePointsFailClosed(string storedValue)
    {
        var result = await InvokeAsync(
            optionId: 3,
            footballPoints: storedValue,
            originalItems: Items(InventoryItem(41, 400_041)),
            worldData: CreateWorldDataCatalog(ItemType(4_200_009)),
            additionalAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FootballPoints"] = "60",
            });

        AssertEligibleFailure(result, "football-points-invalid");
    }

    [Theory]
    [InlineData("sessions", "session-store-missing")]
    [InlineData("world-data", "world-data-missing")]
    [InlineData("metadata", "itemtype-missing")]
    [InlineData("users-root", "usersRootMissing")]
    [InlineData("player-items-root", "playerItemsRootMissing")]
    public async Task MissingSuccessInfrastructureFailsClosed(string missing, string expectedReason)
    {
        var worldData = missing switch
        {
            "world-data" => null,
            "metadata" => CreateWorldDataCatalog(),
            _ => CreateWorldDataCatalog(ItemType(4_200_009)),
        };
        var result = await InvokeAsync(
            optionId: 3,
            footballPoints: "30",
            originalItems: Items(InventoryItem(41, 400_041)),
            worldData,
            includeSessions: missing != "sessions",
            includeUsersRoot: missing != "users-root",
            includePlayerItemsRoot: missing != "player-items-root",
            observerCount: 1);

        AssertEligibleFailure(result, expectedReason);
    }

    [Fact]
    public async Task ItemUidExhaustionFailsClosed()
    {
        var result = await InvokeAsync(
            optionId: 4,
            footballPoints: "60",
            originalItems: Items(InventoryItem(uint.MaxValue, 400_041)),
            worldData: CreateWorldDataCatalog(ItemType(4_200_010)),
            observerCount: 1);

        AssertEligibleFailure(result, "item-uid-exhausted");
    }

    [Theory]
    [InlineData(PersistenceLock.Character)]
    [InlineData(PersistenceLock.Items)]
    public async Task ForcedCharacterOrItemCommitFailureRollsBackBothSnapshots(PersistenceLock lockedFile)
    {
        var originalItems = Items(InventoryItem(41, 400_041));
        var result = await InvokeAsync(
            optionId: 3,
            footballPoints: "30",
            originalItems,
            worldData: CreateWorldDataCatalog(ItemType(4_200_009)),
            observerCount: 1,
            persistenceLock: lockedFile);

        AssertEligibleFailure(result, "transactionWriteFailed");
        Assert.NotNull(result.ReloadedCharacter);
        Assert.Equal(30U, FootballPoints(result.ReloadedCharacter!));
        Assert.NotNull(result.PersistedItems);
        AssertPlayerItemsEqual(originalItems, result.PersistedItems!);
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
        string footballPoints,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        string pointKey = "MyFootBallPoints",
        IReadOnlyDictionary<string, string>? additionalAttributes = null,
        bool includeSessions = true,
        bool includeUsersRoot = true,
        bool includePlayerItemsRoot = true,
        int observerCount = 0,
        PersistenceLock persistenceLock = PersistenceLock.None)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        var playerItemsRoot = Path.Combine(invocationRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);

        var characterPath = Path.Combine(
            usersRoot,
            AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        WriteCharacterSnapshot(characterPath, pointKey, footballPoints, additionalAttributes);
        var playerItemsPath = Path.Combine(
            playerItemsRoot,
            CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var originalCharacterBytes = File.ReadAllBytes(characterPath);
        var originalItemBytes = File.ReadAllBytes(playerItemsPath);

        using var log = new StringWriter();
        var claimantSent = new List<byte[]>();
        var observerSent = new List<IReadOnlyList<byte[]>>();
        // WorldSessionStore holds weak keys; the fixture owns observer lifetimes.
        var observerConnections = new List<ConquerTcpConnection>();
        var exposureOrder = new List<string>();
        var claimantConnection = CreateSendingConnection(claimantSent, exposureOrder, "claimant", 9_958);
        var session = CreateSession(
            pointKey,
            footballPoints,
            originalItems,
            includeUsersRoot ? usersRoot : null,
            additionalAttributes);
        var characterLoader = new CharacterSnapshotLoader(usersRoot);
        var itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
        PersistedState? persistedAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is null || ReferenceEquals(previous, current))
            {
                return;
            }

            persistedAtCommit = new PersistedState(
                Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
                itemLoader.LoadByCharacterUid(CharacterUid));
            exposureOrder.Add("commit");
        });
        sessions.Set(claimantConnection, session);

        for (var index = 0; index < observerCount; index++)
        {
            var sent = new List<byte[]>();
            var observerConnection = CreateSendingConnection(
                sent,
                exposureOrder,
                "observer" + index.ToString(CultureInfo.InvariantCulture),
                9_959 + index);
            var observer = session with
            {
                AccountId = AccountId + index + 1,
                Character = session.Character with
                {
                    Uid = CharacterUid + checked((uint)index) + 1,
                    Name = "FootballObserver" + index.ToString(CultureInfo.InvariantCulture),
                },
                UsersRoot = null,
            };
            sessions.Set(observerConnection, observer);
            observerConnections.Add(observerConnection);
            observerSent.Add(sent);
        }

        FileStream? lockedStream = persistenceLock switch
        {
            PersistenceLock.Character => OpenPersistenceLock(characterPath),
            PersistenceLock.Items => OpenPersistenceLock(playerItemsPath),
            _ => null,
        };

        bool handled;
        try
        {
            handled = await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
                new WorldPacketHandlerContext(
                    claimantConnection,
                    log,
                    Session: session,
                    WorldData: worldData,
                    Sessions: includeSessions ? sessions : null,
                    PlayerItemsRoot: includePlayerItemsRoot ? playerItemsRoot : null),
                session,
                FootballNpcId,
                optionId,
                CancellationToken.None);
        }
        finally
        {
            lockedStream?.Dispose();
            GC.KeepAlive(observerConnections);
        }

        var updatedSession = session;
        if (includeSessions)
        {
            Assert.True(sessions.TryGet(claimantConnection, out var storedSession));
            updatedSession = Assert.IsType<WorldSession>(storedSession);
        }

        return new InvokeResult(
            handled,
            session,
            updatedSession,
            persistedAtCommit,
            Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
            itemLoader.LoadByCharacterUid(CharacterUid),
            claimantSent,
            observerSent,
            exposureOrder,
            log.ToString(),
            usersRoot,
            playerItemsRoot,
            characterPath,
            playerItemsPath,
            originalCharacterBytes,
            originalItemBytes);
    }

    private static FileStream OpenPersistenceLock(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static void WriteCharacterSnapshot(
        string path,
        string pointKey,
        string footballPoints,
        IReadOnlyDictionary<string, string>? additionalAttributes)
    {
        var extraLines = additionalAttributes is null
            ? string.Empty
            : string.Concat(
                additionalAttributes
                    .Where(pair => !string.Equals(pair.Key, pointKey, StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.Key + "=" + pair.Value + Environment.NewLine));
        File.WriteAllText(
            path,
            $"[Character]{Environment.NewLine}" +
            $"UID={CharacterUid}{Environment.NewLine}" +
            $"Body=1003{Environment.NewLine}" +
            $"Face=0{Environment.NewLine}" +
            $"Name=FootballRewardHero{Environment.NewLine}" +
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
            $"{pointKey}={footballPoints}{Environment.NewLine}" +
            extraLines +
            $"Sentinel=keep-me{Environment.NewLine}");
    }

    private static WorldSession CreateSession(
        string pointKey,
        string footballPoints,
        PlayerItemSnapshot playerItems,
        string? usersRoot,
        IReadOnlyDictionary<string, string>? additionalAttributes)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [pointKey] = footballPoints,
        };
        if (additionalAttributes is not null)
        {
            foreach (var (key, value) in additionalAttributes)
            {
                attributes[key] = value;
            }
        }

        return new WorldSession(
            AccountId,
            Username: "legacy-football-reward-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "FootballRewardHero",
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

    private static uint FootballPoints(CharacterSnapshot character)
    {
        foreach (var key in new[] { "MyFootBallPoints", "FootballPoints" })
        {
            if (character.Attributes.TryGetValue(key, out var raw) &&
                uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
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
        Assert.Equal(0U, item.PerfectionLevel);
        Assert.Equal(0U, item.PerfectionProgress);
        Assert.InRange(item.Color, 3U, 8U);
    }

    private static void AssertExactGlobalTalk(byte[] bytes, string expectedText)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal((ushort)2_179, frame.Header.Opcode);
        Assert.Equal(LegacyMsgMessagePacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        Assert.Equal(
            ["SYSTEM", "ALL", string.Empty, expectedText, string.Empty, string.Empty, string.Empty],
            packet.Content);
    }

    private static void AssertEligibleFailure(InvokeResult result, string expectedReason)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.ClaimantSent);
        Assert.All(result.ObserverSent, Assert.Empty);
        Assert.Empty(result.ExposureOrder);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.Equal(result.OriginalItemBytes, File.ReadAllBytes(result.PlayerItemsPath));
        Assert.Contains("reason=" + expectedReason, result.Log, StringComparison.Ordinal);
        Assert.Contains("no Football points, items, session state, or packets were committed", result.Log);
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var replies = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply));
            replies.Add(reply);
        }

        return replies;
    }

    private static void AssertAsciiReply(LegacyNpcReplyPacket reply)
    {
        Assert.All(reply.Text, character => Assert.InRange((int)character, 0, 127));
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static ConquerTcpConnection CreateSendingConnection(
        List<byte[]> sent,
        List<string> exposureOrder,
        string label,
        int port)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
        {
            var bytes = frame.ToArray();
            sent.Add(bytes);
            exposureOrder.Add(
                label + ":" + ReadFrame(bytes).Header.Opcode.ToString(CultureInfo.InvariantCulture));
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
                "fixture://football-rewards",
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
        fields[1] = "FootballReward";
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] =
            CatalogDurability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), "FootballReward", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    public enum PersistenceLock
    {
        None,
        Character,
        Items,
    }

    private sealed record PersistedState(CharacterSnapshot Character, PlayerItemSnapshot Items);

    private sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        PersistedState? PersistedAtCommit,
        CharacterSnapshot? ReloadedCharacter,
        PlayerItemSnapshot? PersistedItems,
        IReadOnlyList<byte[]> ClaimantSent,
        IReadOnlyList<IReadOnlyList<byte[]>> ObserverSent,
        IReadOnlyList<string> ExposureOrder,
        string Log,
        string UsersRoot,
        string PlayerItemsRoot,
        string CharacterPath,
        string PlayerItemsPath,
        byte[] OriginalCharacterBytes,
        byte[] OriginalItemBytes);
}
