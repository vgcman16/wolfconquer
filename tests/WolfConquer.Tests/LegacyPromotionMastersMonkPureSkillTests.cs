using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersMonkPureSkillTests : IDisposable
{
    private const uint MonkPromotionNpcId = 8_314;
    private const long CharacterUid = 1_293_600;
    private const ushort SoulShackleSpellId = 10_405;
    private const byte SoulShackleOption = 21;
    private const string SoulShackleSourceRange = "NpcHandler.cs:43457-43489";
    private const string PureGateText =
        "You can only learn the Pure Skill `Soul Shackle` when you`ve been reborn twice, and have become a Pure Monk or Saint.";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersMonkPureSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SoulShackleOptionLearnsSpellAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Success");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePureMonkSession();
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
            MonkPromotionNpcId,
            SoulShackleOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([SoulShackleSpellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([SoulShackleSpellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(SoulShackleSpellId, spellPacket.Id);

        var replies = ParseNpcReplies(sent);
        AssertReply(replies, "Congratulation! You just have learned the skill SoulShackle");
        AssertOption(replies, "Ok , thank you.");
        AssertAvatar(replies);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMonk pure skill", logText);
        Assert.Contains("source=NpcHandler.PromotionMonk", logText);
        Assert.Contains("sourceRange=" + SoulShackleSourceRange, logText);
        Assert.Contains("option=21", logText);
        Assert.Contains("spellId=10405", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task SoulShackleOptionRejectsBeforeSecondRebirth()
    {
        var result = await RunSoulShackleGateAsync(CreatePureMonkSession(reborn: 1));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        var replies = ParseNpcReplies(result.Sent);
        AssertReply(replies, PureGateText);
        AssertOption(replies, "Ok~I~see.");
        AssertAvatar(replies);
        Assert.Contains("source reborn gate rejected", result.Log);
        Assert.Contains("sourceRange=" + SoulShackleSourceRange, result.Log);
    }

    [Theory]
    [InlineData(1_002, 6_002)]
    [InlineData(6_002, 1_002)]
    public async Task SoulShackleOptionRejectsNonPureMonkCombo(long firstClass, long secondClass)
    {
        var result = await RunSoulShackleGateAsync(CreatePureMonkSession(
            firstClass: firstClass,
            secondClass: secondClass));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        var replies = ParseNpcReplies(result.Sent);
        AssertReply(replies, PureGateText);
        AssertOption(replies, "Ok~I~see.");
        AssertAvatar(replies);
        Assert.Contains("source pure Monk combo gate rejected", result.Log);
        Assert.Contains("sourceRange=" + SoulShackleSourceRange, result.Log);
    }

    [Theory]
    [InlineData(6_002, 39)]
    [InlineData(6_001, 40)]
    public async Task SoulShackleOptionRejectsBeforePromotedLevelForty(long classCode, long level)
    {
        var result = await RunSoulShackleGateAsync(CreatePureMonkSession(classCode: classCode, level: level));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        var replies = ParseNpcReplies(result.Sent);
        AssertReply(replies, "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.");
        AssertOption(replies, "Okay.~I~see..");
        AssertAvatar(replies);
        Assert.Contains("source promotion gate rejected", result.Log);
        Assert.Contains("sourceRange=" + SoulShackleSourceRange, result.Log);
    }

    [Fact]
    public async Task SoulShackleOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "AlreadyLearned");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(SoulShackleSpellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreatePureMonkSession(playerSpells: learned);
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
            MonkPromotionNpcId,
            SoulShackleOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        AssertPersistedSpells(spellsRoot, [SoulShackleSpellId]);
        var replies = ParseNpcReplies(sent);
        AssertReply(replies, "You`ve learned SoulShackle!");
        AssertOption(replies, "Okay.");
        AssertAvatar(replies);
        Assert.Contains("spellId=10405; already learned", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<GateResult> RunSoulShackleGateAsync(WorldSession session)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
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
            MonkPromotionNpcId,
            SoulShackleOption,
            CancellationToken.None);

        return new GateResult(handled, sent, spellsRoot, log.ToString());
    }

    private static WorldSession CreatePureMonkSession(
        long classCode = 6_002,
        long firstClass = 6_002,
        long secondClass = 6_005,
        long reborn = 2,
        long level = 40,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "monk-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PureMonk",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: firstClass,
                SecondClass: secondClass,
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
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerSpellSnapshotEntry Entry(ushort id)
    {
        return new PlayerSpellSnapshotEntry(
            id,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
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

    private static void AssertPersistedSpells(string spellsRoot, IReadOnlyList<uint> expectedSpellIds)
    {
        Assert.Equal(expectedSpellIds, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
    }

    private static void AssertReply(IReadOnlyList<LegacyNpcReplyPacket> replies, string text)
    {
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog && reply.Text == text);
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
                reply.Text == text);
    }

    private static void AssertAvatar(IReadOnlyList<LegacyNpcReplyPacket> replies)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 226);
    }

    private sealed record GateResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string SpellsRoot,
        string Log);
}
