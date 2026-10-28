using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersWindWalkerChaserSkillTests : IDisposable
{
    private const uint WindWalkerPromotionNpcId = 19_634;
    private const long CharacterUid = 1_293_720;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersWindWalkerChaserSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> ChaserSkillOptions => new()
    {
        { 30, 12_890, "SwirlingStorm", "NpcHandler.cs:46910-46948" },
        { 31, 13_090, "ShadowofChaser", "NpcHandler.cs:46871-46909" },
        { 32, 12_850, "TripleBlasts", "NpcHandler.cs:46832-46870" },
        { 33, 12_840, "Thundercloud", "NpcHandler.cs:46793-46831" },
        { 34, 12_970, "Thunderbolt", "NpcHandler.cs:46754-46792" },
    };

    public static TheoryData<byte, ushort, string, long, long, long, string> FrostGazeSkillOptions => new()
    {
        { 40, 12_830, "FrostGazeI", 1, 1_005, 0, "NpcHandler.cs:46584-46628" },
        { 41, 13_070, "FrostGazeII", 1, 16_005, 0, "NpcHandler.cs:46539-46583" },
        { 42, 13_080, "FrostGazeIII", 2, 16_005, 16_005, "NpcHandler.cs:46494-46538" },
    };

    [Theory]
    [MemberData(nameof(ChaserSkillOptions))]
    public async Task ChaserSkillOptionLearnsSpellAndPersists(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "chaser-success-" + optionId);
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(level: 100);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            WindWalkerPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(new uint[] { spellId }, updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(new uint[] { spellId }, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        Assert.Equal(new ushort[] { spellId }, ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("source=NpcHandler.PromotionWindWalker", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [MemberData(nameof(FrostGazeSkillOptions))]
    public async Task FrostGazeOptionLearnsSpellAndPersists(
        byte optionId,
        ushort spellId,
        string spellName,
        long reborn,
        long firstClass,
        long secondClass,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "frost-success-" + optionId);
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(
            level: 100,
            reborn: reborn,
            firstClass: firstClass,
            secondClass: secondClass);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            WindWalkerPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(new uint[] { spellId }, updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(new uint[] { spellId }, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        Assert.Equal(new ushort[] { spellId }, ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        var logText = log.ToString();
        Assert.Contains("source=NpcHandler.PromotionWindWalker", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task ChaserSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "duplicate-thundercloud");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(12_840)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(level: 100, playerSpells: learned);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            WindWalkerPromotionNpcId,
            optionId: 33,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Equal(new uint[] { 12_840 }, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned Thundercloud!");
        Assert.Contains("sourceRange=NpcHandler.cs:46793-46831", log.ToString());
        Assert.Contains("spellId=12840; already learned", log.ToString());
    }

    [Fact]
    public async Task FrostGazeOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "duplicate-frost-gaze-iii");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(13_080)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(
            level: 100,
            reborn: 2,
            firstClass: 16_005,
            secondClass: 16_005,
            playerSpells: learned);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            WindWalkerPromotionNpcId,
            optionId: 42,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Equal(new uint[] { 13_080 }, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Comrade has already acquired this skill. Go practice and feel its power.");
        Assert.Contains("sourceRange=NpcHandler.cs:46494-46538", log.ToString());
        Assert.Contains("spellId=13080; already learned", log.ToString());
    }

    [Fact]
    public async Task ChaserSkillOptionRejectsBelowSourceLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate-swirling-storm");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(level: 99);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            WindWalkerPromotionNpcId,
            optionId: 30,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified.");
        Assert.Contains("sourceRange=NpcHandler.cs:46910-46948", log.ToString());
        Assert.Contains("level=99 requiredLevel=100; source level gate rejected", log.ToString());
    }

    [Fact]
    public async Task FrostGazeIIRejectsWithoutPriorWindWalkerLife()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "frost-prior-class-gate");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(
            level: 100,
            reborn: 1,
            firstClass: 1_005,
            secondClass: 0);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            WindWalkerPromotionNpcId,
            optionId: 41,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.MessageBox &&
                reply.Text == "You are not qualified to learn this skill.");

        var logText = log.ToString();
        Assert.Contains("sourceRange=NpcHandler.cs:46539-46583", logText);
        Assert.Contains("option=41", logText);
        Assert.Contains("reborn=1 class=16005 firstClass=1005 secondClass=0", logText);
        Assert.Contains("source rebirth/class gate rejected", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long level,
        long classCode = 16_005,
        long reborn = 0,
        long firstClass = 16_005,
        long secondClass = 0,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "windwalker-chaser-promotion-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "WindHero",
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
}
