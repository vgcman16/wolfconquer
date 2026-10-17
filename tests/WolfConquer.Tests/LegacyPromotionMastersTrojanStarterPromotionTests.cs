using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTrojanStarterPromotionTests : IDisposable
{
    private const uint TrojanPromotionNpcId = 10_022;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTrojanStarterPromotionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option1SendsSourcePromptWhenStarterIsEligible()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: 1_000, level: 15);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TrojanPromotionNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.Text == "The Trojans are all-out offensive melee combat fighters. Do you want to be a qualified Trojan, now?.");
        Assert.Contains(replies, reply => reply.Text == "Yeah,~please!" && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 5);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_000, updated.Character.Class);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Theory]
    [InlineData(14, 1_000)]
    [InlineData(15, 1_001)]
    public async Task Option1SendsSourceFledglingRejectionWhenStarterIsNotEligible(long level, long classCode)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: classCode, level: level);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            TrojanPromotionNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(
            replies,
            reply => reply.Text == "Only the powerful deserve to be trained. You are still a mere fledgeling.");
        Assert.Contains(replies, reply => reply.Text == "Oh,~I~see.." && reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 5);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task Option2MutatesClassAndLearnsAndPersistsStarterSpellsWhenEligible()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users");
        var spellsRoot = Path.Combine(_root, "PlayersSpells");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 1_000);
        var session = CreateSession(classCode: 1_000, level: 15, usersRoot: usersRoot);
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
            TrojanPromotionNpcId,
            optionId: 2,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseNpcReplies(sent));
        Assert.Equal([1_045, 1_046], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_001, updated.Character.Class);
        Assert.Equal([1_045U, 1_046U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([1_045U, 1_046U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=1001", persistedCharacter, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Theory]
    [InlineData(14, 1_000)]
    [InlineData(15, 1_001)]
    public async Task Option2DoesNothingWhenStarterIsNotEligible(long level, long classCode)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-" + level + "-" + classCode);
        var spellsRoot = Path.Combine(_root, "PlayersSpells-" + level + "-" + classCode);
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
            TrojanPromotionNpcId,
            optionId: 2,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseNpcReplies(sent));
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.False(File.Exists(Path.Combine(spellsRoot, CharacterUid + ".bin")));

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=" + classCode, persistedCharacter, StringComparison.Ordinal);
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
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
            Username: "trojan-starter-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PromotionHero",
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
