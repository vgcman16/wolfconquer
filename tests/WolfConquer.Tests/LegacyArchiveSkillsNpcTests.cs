using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyArchiveSkillsNpcTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyArchiveSkillsNpcTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AllNinjaSkillDialogIsSourceBackedAndClassGated()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: 5_051, reborn: 2);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyArchiveSkillsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 1_021,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(6, sent.Count);
        var text = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("secret Ninja archive skills", text.Text);
        var learn = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, learn.OptionId);
        var randomBook = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(2, randomBook.OptionId);
        Assert.Equal("Buy~a~random~skill~book.", randomBook.Text);
        Assert.DoesNotContain("\\u", text.Text);
        Assert.Contains("source=NpcHandler.AllNinjaSKill", log.ToString());
    }

    [Fact]
    public async Task NewJobCenterTeleportsSourceClassRangeToMappedTrainingMap()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: 9_049, mapId: 1_002, x: 430, y: 380);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyArchiveSkillsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 8_591,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(10_444, updated.Character.MapId);
        Assert.Equal(82, updated.Character.X);
        Assert.Equal(64, updated.Character.Y);

        var teleport = Assert.Single(ParseTeleports(sent));
        Assert.Equal(10_444U, teleport.DwParam);
        Assert.Equal(82U, teleport.PositionX);
        Assert.Equal(64U, teleport.PositionY);
        Assert.Contains("source=NpcHandler.NewJopCenter", log.ToString());
    }

    [Fact]
    public async Task AllNinjaRandomSkillBookDebitsConquerPointsAndPersistsItem()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users");
        var playerItemsRoot = Path.Combine(_root, "PlayerItems");
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, "100.ini"),
            """
            [Character]
            UID=1293600
            Name=ArchiveTest
            ConquerPoints=1500000
            """);
        var session = CreateSession(
            accountId: 100,
            classCode: 5_051,
            reborn: 2,
            conquerPoints: 1_500_000,
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyArchiveSkillsNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            npcId: 1_021,
            optionId: 2,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(500_000, updated.Character.ConquerPoints);
        Assert.Contains(updated.PlayerItems.InventoryItems, item => item.ItemId == 729_464U);
        var walletUpdate = Assert.Single(ParseWalletUpdates(sent));
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, walletUpdate.Type);
        Assert.Equal(500_000UL, walletUpdate.Value);
        Assert.Contains(ParseGameItems(sent), item => item.ItemId == 729_464U);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(updated.Character.Uid);
        Assert.Contains(persisted.InventoryItems, item => item.ItemId == 729_464U);
        Assert.Contains("ConquerPoints=500000", File.ReadAllText(Path.Combine(usersRoot, "100.ini")));
        var output = log.ToString();
        Assert.Contains("source=NpcHandler.AllNinjaSKill", output);
        Assert.Contains("economy=ConquerPoints", output);
        Assert.Contains("cost=1000000", output);
        Assert.Contains("conquerPointsBefore=1500000", output);
        Assert.Contains("conquerPointsAfter=500000", output);
        Assert.Contains("conquerPointsDelta=-1000000", output);
    }

    [Fact]
    public async Task SupClassFixLearnsSourceSubclassesAndDebitsConquerPoints()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users");
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, "100.ini"),
            """
            [Character]
            UID=1293600
            Name=ArchiveTest
            ConquerPoints=10500000
            SubProfInfo=0#0#0#
            SubClassHashPoint=0
            """);
        var session = CreateSession(
            accountId: 100,
            classCode: 2_051,
            reborn: 2,
            level: 140,
            conquerPoints: 10_500_000,
            usersRoot: usersRoot,
            attributes: new Dictionary<string, string>
            {
                ["SubProfInfo"] = "0#0#0#",
                ["SubClassHashPoint"] = "0",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyArchiveSkillsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 78_964,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(500_000, updated.Character.ConquerPoints);
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updated.Character.Attributes["SubProfInfo"],
            out var state));
        Assert.Equal([1, 2, 3, 4, 5, 6, 9], state.Entries.Select(entry => (int)entry.Id).Order().ToArray());
        Assert.All(state.Entries, entry =>
        {
            Assert.Equal(1, entry.Level);
            Assert.Equal(1, entry.Phrase);
        });
        var walletUpdate = Assert.Single(ParseWalletUpdates(sent));
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, walletUpdate.Type);
        Assert.Equal(500_000UL, walletUpdate.Value);

        var learned = ParseSubClassUpdates(sent)
            .Where(packet => packet.Action == LegacySubClassAction.LearnSubClass)
            .Select(packet => (int)packet.SubClassId)
            .Order()
            .ToArray();
        Assert.Equal([1, 2, 3, 4, 5, 6, 9], learned);
        var userFile = File.ReadAllText(Path.Combine(usersRoot, "100.ini"));
        Assert.Contains("ConquerPoints=500000", userFile);
        Assert.Contains("SubProfInfo=", userFile);
        var output = log.ToString();
        Assert.Contains("source=NpcHandler.SUpClass", output);
        Assert.Contains("economy=ConquerPoints", output);
        Assert.Contains("cost=10000000", output);
        Assert.Contains("conquerPointsBefore=10500000", output);
        Assert.Contains("conquerPointsAfter=500000", output);
        Assert.Contains("conquerPointsDelta=-10000000", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long accountId = 100,
        long classCode = 5_051,
        int reborn = 0,
        int level = 140,
        long conquerPoints = 0,
        int mapId = 1_002,
        int x = 430,
        int y = 380,
        string? usersRoot = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: accountId,
            Username: "archive-skills-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "ArchiveTest",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: reborn,
                Level: level,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
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
                Attributes: attributes ?? new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<MsgActionPacket> ParseTeleports(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Type == MsgActionType.Teleport)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgUpdateEntry> ParseWalletUpdates(IEnumerable<byte[]> sent)
    {
        var entries = new List<LegacyMsgUpdateEntry>();
        foreach (var bytes in sent)
        {
            if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                continue;
            }

            entries.AddRange(packet.Updates.Where(entry =>
                entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.ConquerPoints));
        }

        return entries;
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

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
