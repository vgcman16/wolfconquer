using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersWindWalkerBranchSwitchTests : IDisposable
{
    private const uint WindWalkerPromotionNpcId = 19_634;
    private const long CharacterUid = 1_293_745;
    private const uint WindCoreItemId = 3_301_705;
    private const ulong OnMeleeAttackFlag = 8;
    private const uint LegacyMainFlagUpdateType = 0x47;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersWindWalkerBranchSwitchTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CpSwitchOptionTogglesMainFlagDebitsConquerPointsSendsUpdatesAndPersistsCharacter()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "UsersCpSwitch");
        WriteCharacterSnapshot(usersRoot, conquerPoints: 50, mainFlag: 0);
        var session = CreateSession(conquerPoints: 50, mainFlag: 0, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WindWalkerPromotionNpcId,
            optionId: 5,
            CancellationToken.None);

        Assert.True(handled);
        var updated = AssertUpdatedSession(sessions, connection);
        Assert.Equal(23, updated.Character.ConquerPoints);
        Assert.Equal(OnMeleeAttackFlag.ToString(CultureInfo.InvariantCulture), updated.Character.Attributes["MainFlag"]);
        Assert.Equal(23, LoadPersistedCharacter(usersRoot).ConquerPoints);
        Assert.Equal(OnMeleeAttackFlag.ToString(CultureInfo.InvariantCulture), LoadPersistedCharacter(usersRoot).Attributes["MainFlag"]);

        Assert.Contains(ParseMsgUpdates(sent), packet => packet.Updates.Any(entry =>
            (uint)entry.Type == LegacyMainFlagUpdateType && entry.Value == OnMeleeAttackFlag));
        Assert.Contains(ParseMsgUpdates(sent), packet => packet.Updates.Any(entry =>
            entry.Type == LegacyMsgUpdateDataType.ConquerPoints && entry.Value == 23));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Okay, I have switched your branch to [MeleeStomper].");
    }

    [Fact]
    public async Task ItemSwitchOptionConsumesUnboundWindCoreTogglesMainFlagSendsMainFlagUpdateAndPersistsItems()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var itemsRoot = Path.Combine(_root, "ItemsCoreSwitch");
        var session = CreateSession(
            conquerPoints: 0,
            mainFlag: OnMeleeAttackFlag,
            playerItems: Inventory(Item(uid: 7_000, itemId: WindCoreItemId)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            WindWalkerPromotionNpcId,
            optionId: 6,
            CancellationToken.None);

        Assert.True(handled);
        var updated = AssertUpdatedSession(sessions, connection);
        Assert.Equal("0", updated.Character.Attributes["MainFlag"]);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == WindCoreItemId);
        Assert.DoesNotContain(
            new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid).InventoryItems,
            item => item.ItemId == WindCoreItemId);

        Assert.Contains(ParseMsgUpdates(sent), packet => packet.Updates.Any(entry =>
            (uint)entry.Type == LegacyMainFlagUpdateType && entry.Value == 0));
        var remove = Assert.Single(ParseItemUsagePackets(sent));
        Assert.Equal(7_000U, remove.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, remove.Action);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Okay, I have switched your branch to [RangedChaser].");
    }

    [Fact]
    public async Task MissingWindCoreDoesNotMutateMainFlagOrInventoryAndSendsSourceDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var originalItems = Inventory(Item(uid: 7_010, itemId: WindCoreItemId, bound: 1));
        var session = CreateSession(conquerPoints: 0, mainFlag: 0, playerItems: originalItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WindWalkerPromotionNpcId,
            optionId: 4,
            CancellationToken.None);

        Assert.True(handled);
        var updated = AssertUpdatedSession(sessions, connection);
        Assert.Equal("0", updated.Character.Attributes["MainFlag"]);
        Assert.Equal(originalItems.InventoryItems.Select(item => (item.Uid, item.ItemId, item.Bound)), updated.PlayerItems.InventoryItems.Select(item => (item.Uid, item.ItemId, item.Bound)));
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "I can't help you switch your WindWalker branch without a Wind Core. You can exchange one in the Horse Racing Shop.");
    }

    [Fact]
    public async Task InsufficientConquerPointsDoesNotMutateMainFlagOrConquerPointsAndSendsSourceDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(conquerPoints: 26, mainFlag: OnMeleeAttackFlag);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WindWalkerPromotionNpcId,
            optionId: 7,
            CancellationToken.None);

        Assert.True(handled);
        var updated = AssertUpdatedSession(sessions, connection);
        Assert.Equal(26, updated.Character.ConquerPoints);
        Assert.Equal(OnMeleeAttackFlag.ToString(CultureInfo.InvariantCulture), updated.Character.Attributes["MainFlag"]);
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "I'm afraid I can't help you switch branches without enough CPs.");
    }

    [Fact]
    public async Task FreeCooldownOptionSetsThirtyDayCooldownTogglesMainFlagAndFutureCooldownIsSilent()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "UsersFreeSwitch");
        WriteCharacterSnapshot(usersRoot, conquerPoints: 0, mainFlag: 0, canChangeWindWalkerFree: DateTime.UtcNow.AddDays(-1));
        var session = CreateSession(
            conquerPoints: 0,
            mainFlag: 0,
            usersRoot: usersRoot,
            canChangeWindWalkerFree: DateTime.UtcNow.AddDays(-1));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var before = DateTime.UtcNow;

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WindWalkerPromotionNpcId,
            optionId: 2,
            CancellationToken.None);

        Assert.True(handled);
        var updated = AssertUpdatedSession(sessions, connection);
        Assert.Equal(OnMeleeAttackFlag.ToString(CultureInfo.InvariantCulture), updated.Character.Attributes["MainFlag"]);
        Assert.True(TryReadAttributeDate(updated.Character, "CanChangeWindWalkerFree", out var cooldown));
        Assert.InRange(cooldown, before.AddDays(30).AddMinutes(-1), DateTime.UtcNow.AddDays(30).AddMinutes(1));
        Assert.Contains(ParseMsgUpdates(sent), packet => packet.Updates.Any(entry =>
            (uint)entry.Type == LegacyMainFlagUpdateType && entry.Value == OnMeleeAttackFlag));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Okay, I have switched your branch to [MeleeStomper].");

        sent.Clear();
        handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: updated, Sessions: sessions),
            updated,
            WindWalkerPromotionNpcId,
            optionId: 3,
            CancellationToken.None);

        Assert.True(handled);
        var unchanged = AssertUpdatedSession(sessions, connection);
        Assert.Equal(updated.Character.Attributes["MainFlag"], unchanged.Character.Attributes["MainFlag"]);
        Assert.Equal(updated.Character.Attributes["CanChangeWindWalkerFree"], unchanged.Character.Attributes["CanChangeWindWalkerFree"]);
        Assert.Empty(sent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long conquerPoints,
        ulong mainFlag,
        string? usersRoot = null,
        DateTime? canChangeWindWalkerFree = null,
        PlayerItemSnapshot? playerItems = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MainFlag"] = mainFlag.ToString(CultureInfo.InvariantCulture),
        };
        if (canChangeWindWalkerFree.HasValue)
        {
            attributes["CanChangeWindWalkerFree"] = FormatDate(canChangeWindWalkerFree.Value);
        }

        return new WorldSession(
            AccountId: 100,
            Username: "windwalker-branch-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "WindBranchHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 16_005,
                FirstClass: 16_005,
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
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot Inventory(params PlayerItemSnapshotItem[] items)
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = items,
        };
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, byte bound = 0, ushort stackSize = 1)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            Bound = bound,
            StackSize = stackSize,
        };
    }

    private static void WriteCharacterSnapshot(
        string usersRoot,
        long conquerPoints,
        ulong mainFlag,
        DateTime? canChangeWindWalkerFree = null)
    {
        Directory.CreateDirectory(usersRoot);
        var extra = canChangeWindWalkerFree.HasValue
            ? "CanChangeWindWalkerFree=" + FormatDate(canChangeWindWalkerFree.Value) + Environment.NewLine
            : string.Empty;
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini"),
            "[Character]" + Environment.NewLine +
            "UID=" + CharacterUid + Environment.NewLine +
            "Body=1003" + Environment.NewLine +
            "Face=0" + Environment.NewLine +
            "Name=WindBranchHero" + Environment.NewLine +
            "Class=16005" + Environment.NewLine +
            "Map=1002" + Environment.NewLine +
            "X=430" + Environment.NewLine +
            "Y=378" + Environment.NewLine +
            "FirstClass=16005" + Environment.NewLine +
            "SecoundeClass=0" + Environment.NewLine +
            "Reborn=0" + Environment.NewLine +
            "Level=140" + Environment.NewLine +
            "Haire=0" + Environment.NewLine +
            "Experience=0" + Environment.NewLine +
            "PMap=1002" + Environment.NewLine +
            "PMapX=430" + Environment.NewLine +
            "PMapY=378" + Environment.NewLine +
            "Agility=0" + Environment.NewLine +
            "Strength=0" + Environment.NewLine +
            "Vitaliti=0" + Environment.NewLine +
            "Spirit=0" + Environment.NewLine +
            "Atributes=0" + Environment.NewLine +
            "MinHitPoints=1" + Environment.NewLine +
            "MinMana=0" + Environment.NewLine +
            "MaxMana=0" + Environment.NewLine +
            "ConquerPoints=" + conquerPoints.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
            "BankCps=0" + Environment.NewLine +
            "BoundConquerPoints=0" + Environment.NewLine +
            "Money=0" + Environment.NewLine +
            "PkPoints=0" + Environment.NewLine +
            "Online=0" + Environment.NewLine +
            "MainFlag=" + mainFlag.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
            extra);
    }

    private static CharacterSnapshot LoadPersistedCharacter(string usersRoot)
    {
        return CharacterSnapshotParser.ParseText(
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini")));
    }

    private static WorldSession AssertUpdatedSession(WorldSessionStore sessions, ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        return updated;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgItemUsagePacket> ParseItemUsagePackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgItemUsagePacket>();
        foreach (var bytes in sent)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static bool TryReadAttributeDate(CharacterSnapshot snapshot, string key, out DateTime value)
    {
        value = default;
        return snapshot.Attributes.TryGetValue(key, out var raw) &&
            DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }
}
