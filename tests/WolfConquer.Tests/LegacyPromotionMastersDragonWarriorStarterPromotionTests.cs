using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersDragonWarriorStarterPromotionTests : IDisposable
{
    private const uint DragonWarriorPromotionNpcId = 17_126;
    private const long CharacterUid = 1_293_730;

    private static readonly uint[] StarterSpellIds =
    [
        12_240,
        12_290,
        12_270,
        12_340,
        12_330,
        12_320,
    ];

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersDragonWarriorStarterPromotionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option3PromotesInternAndLearnsStarterSpellsWhenEligible()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-success");
        var spellsRoot = Path.Combine(_root, "PlayersSpells-success");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 8_000);
        var session = CreateSession(classCode: 8_000, level: 15, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 3,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(8_001, updated.Character.Class);
        Assert.Equal(StarterSpellIds, updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(StarterSpellIds, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Equal(StarterSpellIds.Select(id => (ushort)id).ToArray(), ParseSpellPackets(sent)
            .Select(packet => packet.Id)
            .ToArray());

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == "You`ve been promoted!");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == "Great,~thanks!");

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=8001", persistedCharacter, StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionLeeLong starter promotion", logText);
        Assert.Contains("source=NpcHandler.PromotionLeeLong", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:50704-50751", logText);
        Assert.Contains("option=3", logText);
        Assert.Contains("spellIds=12240,12290,12270,12340,12330,12320", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [InlineData(14, 8_000)]
    [InlineData(15, 8_001)]
    public async Task Option3RejectsWithoutMutationWhenStarterGateFails(long level, long classCode)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-reject-" + level + "-" + classCode);
        var spellsRoot = Path.Combine(_root, "PlayersSpells-reject-" + level + "-" + classCode);
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var session = CreateSession(classCode: classCode, level: level, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 3,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.False(File.Exists(Path.Combine(spellsRoot, CharacterUid + ".bin")));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == "Sorry, you need level 15.");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == "Ah ok.");

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=" + classCode, persistedCharacter, StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer skipped LegacyNpc PromotionLeeLong starter promotion", logText);
        Assert.Contains("source=NpcHandler.PromotionLeeLong", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:50704-50751", logText);
        Assert.Contains("option=3", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long classCode,
        long level,
        string? usersRoot = null,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "dragon-warrior-starter-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "DragonStarter",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static void CreateMinimalCharacterSnapshot(string usersRoot, long classCode)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid + ".ini"),
            "[Character]" + Environment.NewLine +
            "UID=" + CharacterUid + Environment.NewLine +
            "Class=" + classCode + Environment.NewLine);
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

    private static IReadOnlyList<LegacySpellPacket> ParseSpellPackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacySpellPacket>();
        foreach (var bytes in sent)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet))
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
}
