using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLettersExchangeTests : IDisposable
{
    private const uint LettersNpcId = 101_012;
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;

    private static readonly uint[] LetterItemIds =
    [
        3_303_130,
        3_303_131,
        3_303_124,
        3_303_125,
        3_303_126,
        3_303_127,
        3_303_128,
        3_303_129,
    ];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyLettersExchangeTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option1ConsumesExactLettersAwardsCpPersistsAndSendsSourcePacketOrder()
    {
        var originalItems = CreateLetterItems(firstStackSize: 2, includeSentinel: true);
        var result = await InvokeAsync(originalItems, conquerPoints: 123);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(2_000_123, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(0, result.UpdatedSession.Character.BankConquerPoints);
        Assert.Equal("1", result.UpdatedSession.Character.Attributes["Coonquer14"]);

        var remainingLetter = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == LetterItemIds[0]);
        Assert.Equal(1, remainingLetter.StackSize);
        Assert.Contains(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == 3_303_123);
        Assert.DoesNotContain(
            LetterItemIds.Skip(1),
            itemId => result.UpdatedSession.PlayerItems.InventoryItems.Any(item => item.ItemId == itemId));

        Assert.Equal(9, result.Sent.Count);
        var firstRemoval = AssertGameItem(result.Sent[0]);
        Assert.Equal(LetterItemIds[0], firstRemoval.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, firstRemoval.Mode);
        Assert.Equal(1, firstRemoval.StackSize);

        for (var index = 1; index < LetterItemIds.Length; index++)
        {
            var removal = AssertItemUsage(result.Sent[index]);
            Assert.Equal(checked((uint)(10 + index)), removal.Id);
            Assert.Equal(MsgItemUsageAction.RemoveInventory, removal.Action);
        }

        AssertConquerPointsUpdate(result.Sent[8], 2_000_123);
        Assert.DoesNotContain(result.Sent, IsNpcReply);

        var persistedItems = new PlayerItemSnapshotLoader(result.PlayerItemsRoot)
            .LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persistedItems);
        var persistedCharacter = File.ReadAllText(result.CharacterPath);
        Assert.Contains("ConquerPoints=2000123", persistedCharacter);
        Assert.Contains("BankCps=0", persistedCharacter);
        Assert.Contains("Coonquer14=1", persistedCharacter);
        Assert.Contains("itemIds=3303130,3303131,3303124,3303125,3303126,3303127,3303128,3303129", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc MiscWarEvent", result.Log);
    }

    [Fact]
    public async Task Option1UsesSourceConquerPointsOverflowSetterBeforeUpdatePacket()
    {
        var result = await InvokeAsync(
            CreateLetterItems(),
            conquerPoints: 1_999_000_000,
            bankConquerPoints: 7);

        Assert.True(result.Handled);
        Assert.Equal(2_000_000_000, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(1_000_007, result.UpdatedSession.Character.BankConquerPoints);
        Assert.Equal(10, result.Sent.Count);

        var messageBox = AssertNpcReply(result.Sent[8]);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal(
            "1000000 Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.",
            messageBox.Text);
        AssertConquerPointsUpdate(result.Sent[9], 2_000_000_000);

        var persistedCharacter = File.ReadAllText(result.CharacterPath);
        Assert.Contains("ConquerPoints=2000000000", persistedCharacter);
        Assert.Contains("BankCps=1000007", persistedCharacter);
        Assert.Contains("Coonquer14=1", persistedCharacter);
    }

    [Theory]
    [InlineData("flag")]
    [InlineData("missing")]
    [InlineData("bound")]
    [InlineData("zero-stack")]
    public async Task Option1SourceGuardsDoNotMutate(string guard)
    {
        var flag = guard == "flag" ? 1U : 0U;
        var inventory = CreateLetterItems().InventoryItems.ToArray();
        if (guard == "missing")
        {
            inventory = inventory.Where(item => item.ItemId != LetterItemIds[^1]).ToArray();
        }
        else if (guard == "bound")
        {
            inventory = inventory
                .Select(item => item.ItemId == LetterItemIds[^1] ? item.WithBound(1) : item)
                .ToArray();
        }
        else if (guard == "zero-stack")
        {
            inventory = inventory
                .Select(item => item.ItemId == LetterItemIds[^1] ? item.WithStackSize(0) : item)
                .ToArray();
        }

        var originalItems = CreatePlayerItems(inventory);
        var result = await InvokeAsync(
            originalItems,
            conquerPoints: 500,
            coonquer14: flag);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(500, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(flag.ToString(), result.UpdatedSession.Character.Attributes["Coonquer14"]);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.DoesNotContain(result.Sent, IsMutationFrame);

        var persistedItems = new PlayerItemSnapshotLoader(result.PlayerItemsRoot)
            .LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, persistedItems);
        Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
    }

    [Fact]
    public async Task Option1MissingCommitDependenciesDoesNotPersistCommitOrSendPackets()
    {
        foreach (var guard in new[] { "sessions", "items-root", "users-root" })
        {
            var originalItems = CreateLetterItems();
            var result = await InvokeAsync(
                originalItems,
                conquerPoints: 700,
                includeSessionStore: guard != "sessions",
                includePlayerItemsRoot: guard != "items-root",
                includeUsersRoot: guard != "users-root");

            Assert.True(result.Handled);
            Assert.Same(result.OriginalSession, result.UpdatedSession);
            Assert.Equal(700, result.UpdatedSession.Character.ConquerPoints);
            AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
            Assert.Empty(result.Sent);
            Assert.Contains("no character or item mutation was persisted", result.Log);

            var persistedItems = new PlayerItemSnapshotLoader(result.PlayerItemsRoot)
                .LoadByCharacterUid(CharacterUid);
            AssertPlayerItemsEqual(originalItems, persistedItems);
            Assert.Equal(result.OriginalCharacterBytes, File.ReadAllBytes(result.CharacterPath));
        }
    }

    [Fact]
    public async Task Option1PersistenceFailuresDoNotCommitOrSendMutationPackets()
    {
        var blockedItems = await InvokeAsync(
            CreateLetterItems(),
            conquerPoints: 900,
            blockPlayerItemsRoot: true);

        Assert.True(blockedItems.Handled);
        Assert.Same(blockedItems.OriginalSession, blockedItems.UpdatedSession);
        Assert.Empty(blockedItems.Sent);
        Assert.Contains("item persistence failed", blockedItems.Log);
        Assert.Equal("blocked", File.ReadAllText(blockedItems.PlayerItemsRoot));
        Assert.Equal(blockedItems.OriginalCharacterBytes, File.ReadAllBytes(blockedItems.CharacterPath));

        var originalItems = CreateLetterItems();
        var missingCharacter = await InvokeAsync(
            originalItems,
            conquerPoints: 901,
            omitCharacterFile: true);

        Assert.True(missingCharacter.Handled);
        Assert.Same(missingCharacter.OriginalSession, missingCharacter.UpdatedSession);
        Assert.Empty(missingCharacter.Sent);
        Assert.Contains("character persistence failed", missingCharacter.Log);
        Assert.Contains("reason=snapshotMissing", missingCharacter.Log);
        Assert.Contains("itemRollback=none", missingCharacter.Log);
        var rolledBackItems = new PlayerItemSnapshotLoader(missingCharacter.PlayerItemsRoot)
            .LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, rolledBackItems);
        Assert.False(File.Exists(missingCharacter.CharacterPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        PlayerItemSnapshot playerItems,
        long conquerPoints = 0,
        long bankConquerPoints = 0,
        uint coonquer14 = 0,
        bool includeSessionStore = true,
        bool includePlayerItemsRoot = true,
        bool includeUsersRoot = true,
        bool blockPlayerItemsRoot = false,
        bool omitCharacterFile = false)
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

        var characterPath = Path.Combine(usersRoot, AccountId + ".ini");
        if (!omitCharacterFile)
        {
            File.WriteAllText(
                characterPath,
                $"[Character]{Environment.NewLine}" +
                $"UID={CharacterUid}{Environment.NewLine}" +
                $"ConquerPoints={conquerPoints}{Environment.NewLine}" +
                $"BankCps={bankConquerPoints}{Environment.NewLine}" +
                $"Coonquer14={coonquer14}{Environment.NewLine}");
        }

        var originalCharacterBytes = File.Exists(characterPath) ? File.ReadAllBytes(characterPath) : [];
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            playerItems,
            conquerPoints,
            bankConquerPoints,
            coonquer14,
            includeUsersRoot ? usersRoot : null);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyMiscWarEventNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: includePlayerItemsRoot ? playerItemsRoot : null),
            session,
            LettersNpcId,
            optionId: 1,
            new DateTime(2026, 7, 10, 12, 0, 0),
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            handled,
            sent,
            log.ToString(),
            session,
            updatedSession,
            playerItemsRoot,
            characterPath,
            originalCharacterBytes);
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        long conquerPoints,
        long bankConquerPoints,
        uint coonquer14,
        string? usersRoot)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
            ["ConquerPoints"] = conquerPoints.ToString(),
            ["BankCps"] = bankConquerPoints.ToString(),
            ["Coonquer14"] = coonquer14.ToString(),
        };

        return new WorldSession(
            AccountId,
            Username: "letters-exchange-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "LettersHero",
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
                BankConquerPoints: bankConquerPoints,
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

    private static PlayerItemSnapshot CreateLetterItems(
        ushort firstStackSize = 1,
        bool includeSentinel = false)
    {
        var items = LetterItemIds
            .Select((itemId, index) => CreateInventoryItem(
                checked((uint)(10 + index)),
                itemId,
                index == 0 ? firstStackSize : (ushort)1))
            .ToList();
        if (includeSentinel)
        {
            items.Add(CreateInventoryItem(100, 3_303_123));
        }

        return CreatePlayerItems(items);
    }

    private static PlayerItemSnapshot CreatePlayerItems(IEnumerable<PlayerItemSnapshotItem> inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems.ToArray(), [], [], [], []);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = stackSize,
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

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertConquerPointsUpdate(byte[] bytes, ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static bool IsNpcReply(byte[] bytes)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsMutationFrame(byte[] bytes)
    {
        var frame = ReadFrame(bytes);
        return MsgItemUsagePacket.TryParse(frame, out _) ||
            LegacyGameItemPacket.TryParse(frame, out _) ||
            LegacyMsgUpdatePacket.TryParse(frame, out _);
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

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        string PlayerItemsRoot,
        string CharacterPath,
        byte[] OriginalCharacterBytes);
}
