using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyProgressionServicesNpcTests : IDisposable
{
    private const uint InnerPowerNpcId = 78_954;
    private const uint SupClassNpcId = 78_964;
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyProgressionServicesNpcTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(InnerPowerNpcId, "NpcHandler.InnerPower", "fix your InnerPower", 1, "InnerPower~points.", 2)]
    [InlineData(SupClassNpcId, "NpcHandler.SUpClass", "fix your Supclass", 1, null, 0)]
    public async Task EligibleRootDialogsUseSourceOptionIdsAndEnglish(
        uint npcId,
        string source,
        string expectedText,
        byte primaryOption,
        string? secondaryText,
        byte secondaryOption)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 120, reborn: 2);

        var handled = await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.OptionId == primaryOption);
        if (secondaryText is not null)
        {
            Assert.Contains(replies, reply => reply.Text == secondaryText && reply.OptionId == secondaryOption);
        }

        Assert.Contains(replies, reply => reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains($"source={source}", log.ToString());
    }

    [Theory]
    [InlineData(InnerPowerNpcId, "need level 100 and second rebirth")]
    [InlineData(SupClassNpcId, "level 100 and second rebirth")]
    public async Task IneligibleRootDialogsAreLevelAndRebornGated(uint npcId, string expectedText)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 99, reborn: 1);

        var handled = await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.DoesNotContain(replies, reply => reply.OptionId is 1 or 2);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Theory]
    [InlineData(InnerPowerNpcId, 1, 250_000_000, "full InnerPower mutation")]
    public async Task PurchaseAndFixOptionsLogSafeGapWithoutMutatingSession(
        uint npcId,
        byte optionId,
        long conquerPoints,
        string expectedGap)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(level: 140, reborn: 2, conquerPoints: conquerPoints);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.NotNull(stored);
        Assert.Equal(conquerPoints, stored.Character.ConquerPoints);
        Assert.Empty(stored.PlayerItems.InventoryItems);
        Assert.DoesNotContain(ParseConquerPointUpdates(sent), value => value != (ulong)conquerPoints);
        Assert.Contains("Your CPs were not spent.", string.Join(' ', ParseNpcReplies(sent).Select(reply => reply.Text)));
        var output = log.ToString();
        Assert.Contains("PARITY-GAP", output);
        Assert.Contains(expectedGap, output);
    }

    [Theory]
    [InlineData(InnerPowerNpcId, 1, 249_999_999, "250,000,000 CPs")]
    [InlineData(InnerPowerNpcId, 2, 99_999, "100.000 cps")]
    public async Task InsufficientCpOptionsReturnSourceShapedDialogWithoutGap(
        uint npcId,
        byte optionId,
        long conquerPoints,
        string expectedText)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(level: 140, reborn: 2, conquerPoints: conquerPoints);

        var handled = await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task InnerPowerPointsPurchaseDebitsCpAndGrantsOneUnboundItem()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var usersRoot = CreateUsersRoot(100_000);
        var playerItemsRoot = Path.Combine(_root, "PlayerItems");
        var session = CreateSession(conquerPoints: 100_000, usersRoot: usersRoot);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            InnerPowerNpcId,
            optionId: 2,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.NotNull(stored);
        Assert.Equal(0, stored.Character.ConquerPoints);
        var item = Assert.Single(stored.PlayerItems.InventoryItems);
        Assert.Equal(3_007_110U, item.ItemId);
        Assert.Equal((ushort)1, item.StackSize);
        Assert.Equal((byte)0, item.Bound);
        Assert.Contains((ulong)0, ParseConquerPointUpdates(sent));
        Assert.Contains(ParseGameItems(sent), packet => packet.ItemId == 3_007_110U && packet.StackSize == 1);
        Assert.Contains("ConquerPoints=0", File.ReadAllText(Path.Combine(usersRoot, "100.ini")));
        var persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(stored.Character.Uid);
        Assert.Contains(persistedItems.InventoryItems, persisted => persisted.ItemId == 3_007_110U && persisted.Bound == 0);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task SupclassFixDebitsCpLearnsSourceOrderAndSendsEffect()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var usersRoot = CreateUsersRoot(10_000_000, subProfInfo: "0#0#0#", subClassHashPoint: "0");
        var session = CreateSession(
            conquerPoints: 10_000_000,
            usersRoot: usersRoot,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubProfInfo"] = "0#0#0#",
                ["SubClassHashPoint"] = "0",
            });
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            SupClassNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var stored));
        Assert.NotNull(stored);
        Assert.Equal(0, stored.Character.ConquerPoints);
        Assert.Equal("0#0#7#1#1#1#2#1#1#3#1#1#4#1#1#5#1#1#9#1#1#6#1#1#", stored.Character.Attributes["SubProfInfo"]);
        Assert.Equal("100111111", stored.Character.Attributes["SubClassHashPoint"]);
        Assert.Contains((ulong)0, ParseConquerPointUpdates(sent));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 9, 6 }, ParseSubClassUpdates(sent).Select(packet => packet.SubClassId));
        Assert.Contains(ParseStringPackets(sent), packet =>
            packet.Uid == 1_293_600 && packet.Id == LegacyStringId.Effect && packet.Strings.SequenceEqual(["zf2-e300"]));
        var persisted = File.ReadAllText(Path.Combine(usersRoot, "100.ini"));
        Assert.Contains("ConquerPoints=0", persisted);
        Assert.Contains("SubProfInfo=0#0#7#1#1#1#2#1#1#3#1#1#4#1#1#5#1#1#9#1#1#6#1#1#", persisted);
        Assert.Contains("SubClassHashPoint=100111111", persisted);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task SupclassInsufficientCpSendsSourceSystemMessageWithoutGap()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(conquerPoints: 9_999_999);

        var handled = await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            SupClassNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Contains(ParseMessages(sent), packet =>
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            packet.Content.Contains("You don`t have 10,00.000 ConquerPoints ."));
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task UnknownNpcOrOptionIsNotHandled()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        Assert.False(await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId: 1,
            optionId: 0,
            CancellationToken.None));
        Assert.False(await WorldPacketHandlers.TryHandleLegacyProgressionServicesNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            InnerPowerNpcId,
            optionId: 99,
            CancellationToken.None));
    }

    private static WorldSession CreateSession(
        long level = 140,
        long reborn = 2,
        long conquerPoints = 0,
        string? usersRoot = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "progression-services-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "ProgressHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: reborn,
                Level: level,
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
                Attributes: attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private string CreateUsersRoot(long conquerPoints, string? subProfInfo = null, string? subClassHashPoint = null)
    {
        var usersRoot = Path.Combine(_root, "Users", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        var lines = new List<string>
        {
            "[Character]",
            "UID=1293600",
            "Name=ProgressHero",
            $"ConquerPoints={conquerPoints}",
        };
        if (subProfInfo is not null)
        {
            lines.Add($"SubProfInfo={subProfInfo}");
        }

        if (subClassHashPoint is not null)
        {
            lines.Add($"SubClassHashPoint={subClassHashPoint}");
        }

        File.WriteAllLines(Path.Combine(usersRoot, "100.ini"), lines);
        return usersRoot;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
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

    private static IReadOnlyList<ulong> ParseConquerPointUpdates(IEnumerable<byte[]> sent)
    {
        var values = new List<ulong>();
        foreach (var bytes in sent)
        {
            if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                continue;
            }

            values.AddRange(packet.Updates
                .Where(entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints)
                .Select(entry => entry.Value));
        }

        return values;
    }

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacySubClassUpdatePacket> ParseSubClassUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacySubClassUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacySubClassUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyStringPacket> ParseStringPackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyStringPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMessages(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
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

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }
}
