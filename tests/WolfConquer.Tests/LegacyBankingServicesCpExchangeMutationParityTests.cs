using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBankingServicesCpExchangeMutationParityTests : IDisposable
{
    private const uint MarketCpAdminNpcId = 2_071;
    private const uint JailCpAdminNpcId = 1_000;
    private const uint DragonBallItemId = 1_088_000;
    private const uint DragonBallScrollItemId = 720_028;
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_700;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-CpExchangeMutation-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(MarketCpAdminNpcId, 1, DragonBallScrollItemId, 2_150, "Congratulations!You~have~got~2150~CPs~in~your~inventory")]
    [InlineData(JailCpAdminNpcId, 1, DragonBallScrollItemId, 2_150, "Congratulations!You~have~got~2150~CPs~in~your~inventory")]
    [InlineData(MarketCpAdminNpcId, 2, DragonBallItemId, 215, "Congratulations!You~have~got~215~CPs~in~your~inventory")]
    [InlineData(JailCpAdminNpcId, 2, DragonBallItemId, 215, "Congratulations!You~have~got~215~CPs~in~your~inventory")]
    public async Task ItemToConquerPointsConsumesOnePersistsAndSendsExactSourceFrames(
        uint npcId,
        byte optionId,
        uint itemId,
        long reward,
        string successMessage)
    {
        var sourceItem = CreateInventoryItem(10, itemId);
        var sentinel = CreateInventoryItem(11, 3_303_123);
        var originalItems = CreatePlayerItems(sourceItem, sentinel);

        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            conquerPoints: 500,
            money: 700,
            chiPoints: 9);

        Assert.True(result.Handled);
        Assert.True(
            result.UpdatedSession.Character.ConquerPoints == 500 + reward,
            result.Log);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == sourceItem.Uid);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == sentinel.Uid);

        Assert.Equal(3, result.Sent.Count);
        var removal = AssertItemUsage(result.Sent[0]);
        Assert.Equal(sourceItem.Uid, removal.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, removal.Action);
        AssertUpdate(result.Sent[1], LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)(500 + reward)));
        AssertMessageBox(result.Sent[2], successMessage);

        AssertPersistedItems(result, result.UpdatedSession.PlayerItems);
        var persistedCharacter = File.ReadAllText(result.CharacterPath);
        Assert.Contains($"ConquerPoints={500 + reward}", persistedCharacter, StringComparison.Ordinal);
        Assert.Contains("Money=700", persistedCharacter, StringComparison.Ordinal);
        Assert.Contains("Note=keep-me", persistedCharacter, StringComparison.Ordinal);
        Assert.Contains("applied LegacyNpc CpExchangeAdmin item mutation", result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CpExchangeAdmin", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(9, DragonBallItemId, 800, "Congratulations!You~have~got~800~ChiPoints.")]
    [InlineData(10, DragonBallScrollItemId, 8_000, "Congratulations!You~have~got~8000~ChiPoints.")]
    public async Task ItemToChiConsumesOnePersistsAndUsesSourceMessageWithoutChiUpdate(
        byte optionId,
        uint itemId,
        int reward,
        string successMessage)
    {
        var sourceItem = CreateInventoryItem(20, itemId);
        var result = await InvokeAsync(
            MarketCpAdminNpcId,
            optionId,
            CreatePlayerItems(sourceItem),
            conquerPoints: 30,
            money: 40,
            chiPoints: 50);

        Assert.True(result.Handled);
        Assert.Equal(50 + reward, LegacyChiState.FromSnapshot(result.UpdatedSession.Character).ChiPoints);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(2, result.Sent.Count);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, AssertItemUsage(result.Sent[0]).Action);
        AssertMessageBox(result.Sent[1], successMessage);
        Assert.DoesNotContain(result.Sent, IsMsgUpdate);

        AssertPersistedItems(result, result.UpdatedSession.PlayerItems);
        Assert.Contains(
            $"ChiPoints={50 + reward}",
            File.ReadAllText(result.CharacterPath),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MarketCpAdminNpcId, 4, 2_000_000, 7, 1_000_000, 100_000_007, "ConquerPoints,Money,ConquerPoints", "Congratulations!You~have~got~100m~Money~in~your~inventory")]
    [InlineData(MarketCpAdminNpcId, 5, 150_000_000, 7, 50_000_000, 1_000_000_007, "ConquerPoints,Money,ConquerPoints", "Congratulations!You~have~got~1kkk~Money~in~your~inventory")]
    [InlineData(JailCpAdminNpcId, 4, 7, 20_000_000, 107, 10_000_000, "Money,ConquerPoints,Money", "Congratulations!You~have~got~100~CPs~in~your~inventory")]
    [InlineData(JailCpAdminNpcId, 5, 7, 150_000_000, 1_007, 50_000_000, "Money,ConquerPoints,Money", "Congratulations!You~have~got~1000~CPs~in~your~inventory")]
    public async Task CurrencyExchangePersistsExactAmountsAndSourceUpdateOrder(
        uint npcId,
        byte optionId,
        long conquerPoints,
        long money,
        long expectedConquerPoints,
        long expectedMoney,
        string expectedUpdateOrder,
        string successMessage)
    {
        var result = await InvokeAsync(
            npcId,
            optionId,
            PlayerItemSnapshot.Empty,
            conquerPoints,
            money,
            chiPoints: 60);

        Assert.True(result.Handled);
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(expectedMoney, result.UpdatedSession.Character.Money);
        Assert.Equal(4, result.Sent.Count);

        var updates = result.Sent.Take(3).Select(AssertSingleUpdate).ToArray();
        Assert.Equal(
            expectedUpdateOrder.Split(',').Select(Enum.Parse<LegacyMsgUpdateDataType>).ToArray(),
            updates.Select(update => update.Type).ToArray());
        foreach (var update in updates)
        {
            var expectedValue = update.Type == LegacyMsgUpdateDataType.ConquerPoints
                ? checked((ulong)expectedConquerPoints)
                : checked((ulong)expectedMoney);
            Assert.Equal(expectedValue, update.Value);
        }

        AssertMessageBox(result.Sent[3], successMessage);
        var persistedCharacter = File.ReadAllText(result.CharacterPath);
        Assert.Contains($"ConquerPoints={expectedConquerPoints}", persistedCharacter, StringComparison.Ordinal);
        Assert.Contains($"Money={expectedMoney}", persistedCharacter, StringComparison.Ordinal);
        Assert.Contains("Note=keep-me", persistedCharacter, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MarketCpAdminNpcId, 1, "Where~is~your~DBScroll?", "Er,~sorry.~I'm not have DBScroll.")]
    [InlineData(JailCpAdminNpcId, 1, "Where~is~your~DBScroll?", "Er,~sorry.~I'm not have DBScroll.")]
    [InlineData(MarketCpAdminNpcId, 2, "Where~is~your~DragonBall?", "Er,~sorry.~I'm not have Dragon Ball.")]
    [InlineData(JailCpAdminNpcId, 2, "Where~is~your~DragonBall?", "Er,~sorry.~I'm not have Dragon Ball.")]
    [InlineData(MarketCpAdminNpcId, 9, "Where~is~your~DBScroll?", "Er,~sorry.~I'm not have DBScroll.")]
    [InlineData(MarketCpAdminNpcId, 10, "Where~is~your~DBScroll?", "Er,~sorry.~I'm not have DBScroll.")]
    public async Task MissingItemsUseExactSourceDialogsWithoutMutation(
        uint npcId,
        byte optionId,
        string expectedText,
        string expectedOption)
    {
        var result = await InvokeAsync(
            npcId,
            optionId,
            PlayerItemSnapshot.Empty,
            conquerPoints: 100,
            money: 200,
            chiPoints: 300);

        AssertUnchanged(result, conquerPoints: 100, money: 200, chiPoints: 300);
        AssertReply(result.Sent[0], LegacyNpcReplyInteractType.Dialog, expectedText);
        AssertReply(result.Sent[1], LegacyNpcReplyInteractType.Option, expectedOption, byte.MaxValue);
        Assert.DoesNotContain(result.Sent, IsMutationFrame);
        AssertPersistedItems(result, PlayerItemSnapshot.Empty);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
    }

    [Theory]
    [InlineData(MarketCpAdminNpcId, 4, 999_999, 1, "You not have 1m cps with you.")]
    [InlineData(MarketCpAdminNpcId, 5, 99_999_999, 1, "You not have 100m cps with you.")]
    [InlineData(JailCpAdminNpcId, 4, 1, 9_999_999, "You do not have 10.000.000 Money with you.")]
    [InlineData(JailCpAdminNpcId, 5, 1, 99_999_999, "You do not have 100.000.000 Money with you.")]
    public async Task InsufficientCurrencyUsesExactSourceDialogWithoutMutation(
        uint npcId,
        byte optionId,
        long conquerPoints,
        long money,
        string expectedText)
    {
        var result = await InvokeAsync(
            npcId,
            optionId,
            PlayerItemSnapshot.Empty,
            conquerPoints,
            money,
            chiPoints: 3);

        AssertUnchanged(result, conquerPoints, money, chiPoints: 3);
        AssertReply(result.Sent[0], LegacyNpcReplyInteractType.Dialog, expectedText);
        AssertReply(result.Sent[1], LegacyNpcReplyInteractType.Option, "I~see", byte.MaxValue);
        Assert.DoesNotContain(result.Sent, IsMutationFrame);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
    }

    [Fact]
    public async Task ItemPersistenceFailureDoesNotCommitSessionCharacterOrInventory()
    {
        var sourceItem = CreateInventoryItem(30, DragonBallItemId);
        var result = await InvokeAsync(
            MarketCpAdminNpcId,
            optionId: 2,
            CreatePlayerItems(sourceItem),
            conquerPoints: 400,
            money: 500,
            chiPoints: 600,
            blockPlayerItemsRoot: true);

        AssertUnchanged(result, conquerPoints: 400, money: 500, chiPoints: 600);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Contains("playerItemsSnapshotMissing", result.Log, StringComparison.Ordinal);
        Assert.Contains("PARITY-GAP LegacyNpc CpExchangeAdmin", result.Log, StringComparison.Ordinal);
        Assert.Equal("blocked", File.ReadAllText(result.PlayerItemsRoot));
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.DoesNotContain(result.Sent, IsMutationFrame);
    }

    [Fact]
    public async Task CharacterPersistenceFailureRollsBackPersistedItemAndDoesNotCommitSession()
    {
        var sourceItem = CreateInventoryItem(40, DragonBallScrollItemId);
        var originalItems = CreatePlayerItems(sourceItem);
        var result = await InvokeAsync(
            JailCpAdminNpcId,
            optionId: 1,
            originalItems,
            conquerPoints: 700,
            money: 800,
            chiPoints: 900,
            persistedCharacterUid: CharacterUid + 1);

        AssertUnchanged(result, conquerPoints: 700, money: 800, chiPoints: 900);
        Assert.Contains("characterPersistenceFailed:uidMismatch", result.Log, StringComparison.Ordinal);
        Assert.Contains("itemRollback=none", result.Log, StringComparison.Ordinal);
        AssertPersistedItems(result, originalItems);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        Assert.DoesNotContain(result.Sent, IsMutationFrame);
    }

    [Fact]
    public async Task StaleItemExchangeReplayCannotDuplicateReward()
    {
        var harness = CreateHarness(
            MarketCpAdminNpcId,
            optionId: 2,
            CreatePlayerItems(CreateInventoryItem(50, DragonBallItemId)),
            conquerPoints: 1_000,
            money: 2_000,
            chiPoints: 3_000);

        Assert.True(await InvokeHarnessAsync(harness, harness.OriginalSession));
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var committed));
        Assert.Equal(1_215, committed!.Character.ConquerPoints);

        Assert.True(await InvokeHarnessAsync(harness, harness.OriginalSession));
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var afterReplay));
        Assert.Equal(1_215, afterReplay!.Character.ConquerPoints);
        Assert.Empty(afterReplay.PlayerItems.InventoryItems);
        Assert.Equal(
            1,
            ParseReplies(harness.Sent).Count(reply =>
                reply.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                reply.Text == "Congratulations!You~have~got~215~CPs~in~your~inventory"));
        Assert.Contains("playerItemsBaselineMismatch", harness.Log.ToString(), StringComparison.Ordinal);

        Assert.Empty(new PlayerItemSnapshotLoader(harness.PlayerItemsRoot).LoadByCharacterUid(CharacterUid).InventoryItems);
        Assert.Contains("ConquerPoints=1215", File.ReadAllText(harness.CharacterPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaleCurrencyExchangeReplayCannotDuplicateReward()
    {
        var harness = CreateHarness(
            JailCpAdminNpcId,
            optionId: 4,
            PlayerItemSnapshot.Empty,
            conquerPoints: 1_000,
            money: 20_000_000,
            chiPoints: 30);

        Assert.True(await InvokeHarnessAsync(harness, harness.OriginalSession));
        Assert.True(await InvokeHarnessAsync(harness, harness.OriginalSession));
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var afterReplay));
        Assert.Equal(1_100, afterReplay!.Character.ConquerPoints);
        Assert.Equal(10_000_000, afterReplay.Character.Money);
        Assert.Equal(
            1,
            ParseReplies(harness.Sent).Count(reply =>
                reply.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                reply.Text == "Congratulations!You~have~got~100~CPs~in~your~inventory"));
        Assert.Contains("characterBaselineMismatch:ConquerPoints", harness.Log.ToString(), StringComparison.Ordinal);
        var persisted = File.ReadAllText(harness.CharacterPath);
        Assert.Contains("ConquerPoints=1100", persisted, StringComparison.Ordinal);
        Assert.Contains("Money=10000000", persisted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MarketCpAdminNpcId, 2, 2_000_000_000, 1, DragonBallItemId, "conquerPointsOverflowSetterBranchNotAudited")]
    [InlineData(MarketCpAdminNpcId, 4, 1_000_000, 500_000_000_000, 0, "warehouseMoneyOverflowSetterBranchNotAudited")]
    [InlineData(JailCpAdminNpcId, 4, 2_000_000_000, 10_000_000, 0, "conquerPointsOverflowSetterBranchNotAudited")]
    public async Task SeparateSourceOverflowSetterBranchesRemainFencedWithoutMutation(
        uint npcId,
        byte optionId,
        long conquerPoints,
        long money,
        uint itemId,
        string expectedReason)
    {
        var items = itemId == 0
            ? PlayerItemSnapshot.Empty
            : CreatePlayerItems(CreateInventoryItem(60, itemId));
        var result = await InvokeAsync(
            npcId,
            optionId,
            items,
            conquerPoints,
            money,
            chiPoints: 10);

        AssertUnchanged(result, conquerPoints, money, chiPoints: 10);
        Assert.Contains(expectedReason, result.Log, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Sent, IsMutationFrame);
        AssertPersistedItems(result, items);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvocationResult> InvokeAsync(
        uint npcId,
        byte optionId,
        PlayerItemSnapshot playerItems,
        long conquerPoints,
        long money,
        int chiPoints,
        bool blockPlayerItemsRoot = false,
        long persistedCharacterUid = CharacterUid)
    {
        var harness = CreateHarness(
            npcId,
            optionId,
            playerItems,
            conquerPoints,
            money,
            chiPoints,
            blockPlayerItemsRoot,
            persistedCharacterUid);
        var handled = await InvokeHarnessAsync(harness, harness.OriginalSession);
        Assert.True(harness.Sessions.TryGet(harness.Connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvocationResult(
            handled,
            harness.Sent,
            harness.Log.ToString(),
            harness.OriginalSession,
            updatedSession,
            harness.PlayerItemsRoot,
            harness.CharacterPath,
            harness.OriginalCharacterBytes);
    }

    private Harness CreateHarness(
        uint npcId,
        byte optionId,
        PlayerItemSnapshot playerItems,
        long conquerPoints,
        long money,
        int chiPoints,
        bool blockPlayerItemsRoot = false,
        long persistedCharacterUid = CharacterUid)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        var playerItemsRoot = Path.Combine(invocationRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);

        if (blockPlayerItemsRoot)
        {
            Directory.CreateDirectory(invocationRoot);
            File.WriteAllText(playerItemsRoot, "blocked");
        }
        else
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, playerItems);
        }

        var characterPath = Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        File.WriteAllText(
            characterPath,
            $"[Character]{Environment.NewLine}" +
            $"UID={persistedCharacterUid}{Environment.NewLine}" +
            $"Name=Banker{Environment.NewLine}" +
            $"Money={money}{Environment.NewLine}" +
            $"ConquerPoints={conquerPoints}{Environment.NewLine}" +
            $"BankCps=0{Environment.NewLine}" +
            $"ChiPoints={chiPoints}{Environment.NewLine}" +
            $"{Environment.NewLine}[Custom]{Environment.NewLine}" +
            $"Note=keep-me{Environment.NewLine}");

        var originalCharacterBytes = File.ReadAllBytes(characterPath);
        var sent = new List<byte[]>();
        var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(playerItems, conquerPoints, money, chiPoints, usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions,
            PlayerItemsRoot: playerItemsRoot);
        return new Harness(
            npcId,
            optionId,
            sent,
            log,
            connection,
            sessions,
            context,
            session,
            playerItemsRoot,
            characterPath,
            originalCharacterBytes);
    }

    private static ValueTask<bool> InvokeHarnessAsync(Harness harness, WorldSession session)
    {
        return WorldPacketHandlers.TryHandleLegacyBankingServicesNpcAsync(
            harness.Context with { Session = session },
            session,
            harness.NpcId,
            harness.OptionId,
            input: "",
            CancellationToken.None);
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        long conquerPoints,
        long money,
        int chiPoints,
        string usersRoot)
    {
        return new WorldSession(
            AccountId,
            Username: "cp-exchange-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "Banker",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
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
                Money: money,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Money"] = money.ToString(CultureInfo.InvariantCulture),
                    ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
                    ["BankCps"] = "0",
                    ["ChiPoints"] = chiPoints.ToString(CultureInfo.InvariantCulture),
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

    private static PlayerItemSnapshotItem CreateInventoryItem(uint uid, uint itemId)
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

    private static MsgItemUsagePacket AssertItemUsage(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyMsgUpdateEntry AssertSingleUpdate(byte[] bytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        return Assert.Single(packet.Updates);
    }

    private static void AssertUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
    {
        var update = AssertSingleUpdate(bytes);
        Assert.Equal(type, update.Type);
        Assert.Equal(value, update.Value);
    }

    private static void AssertMessageBox(byte[] bytes, string expectedText)
    {
        AssertReply(bytes, LegacyNpcReplyInteractType.MessageBox, expectedText);
    }

    private static LegacyNpcReplyPacket AssertReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType,
        string expectedText,
        byte? expectedOptionId = null)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        Assert.Equal(expectedText, packet.Text);
        if (expectedOptionId.HasValue)
        {
            Assert.Equal(expectedOptionId.Value, packet.OptionId);
        }

        return packet;
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseReplies(IEnumerable<byte[]> sent)
    {
        var replies = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply))
            {
                replies.Add(reply);
            }
        }

        return replies;
    }

    private static bool IsMsgUpdate(byte[] bytes)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsMutationFrame(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        return MsgItemUsagePacket.TryParse(frame, out _) ||
            LegacyGameItemPacket.TryParse(frame, out _) ||
            LegacyMsgUpdatePacket.TryParse(frame, out _);
    }

    private static void AssertUnchanged(
        InvocationResult result,
        long conquerPoints,
        long money,
        int chiPoints)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(conquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(money, result.UpdatedSession.Character.Money);
        Assert.Equal(chiPoints, LegacyChiState.FromSnapshot(result.UpdatedSession.Character).ChiPoints);
    }

    private static void AssertPersistedItems(InvocationResult result, PlayerItemSnapshot expected)
    {
        var persisted = new PlayerItemSnapshotLoader(result.PlayerItemsRoot)
            .LoadByCharacterUid(CharacterUid);
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(persisted));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record Harness(
        uint NpcId,
        byte OptionId,
        List<byte[]> Sent,
        StringWriter Log,
        ConquerTcpConnection Connection,
        WorldSessionStore Sessions,
        WorldPacketHandlerContext Context,
        WorldSession OriginalSession,
        string PlayerItemsRoot,
        string CharacterPath,
        byte[] OriginalCharacterBytes);

    private sealed record InvocationResult(
        bool Handled,
        List<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        string PlayerItemsRoot,
        string CharacterPath,
        byte[] OriginalCharacterBytes);
}
