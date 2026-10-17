using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersArcherPureSkillTests : IDisposable
{
    private const uint ArcherPromotionNpcId = 400;
    private const ushort StarArrowSpellId = 10_313;
    private const long CharacterUid = 1_293_641;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersArcherPureSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StarArrowOptionLearnsSpellAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Success");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePureArcherSession();
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
            ArcherPromotionNpcId,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([StarArrowSpellId], updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [StarArrowSpellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(StarArrowSpellId, spellPacket.Id);

        AssertContainsLegacyArcherDialog(
            ParseNpcReplies(sent),
            "Congratulation! You just have learned the skill StarArrow",
            "Ok , thank you.");

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionArcher pure skill", logText);
        Assert.Contains("source=NpcHandler.PromotionArcher", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:49029-49048", logText);
        Assert.Contains("option=31", logText);
        Assert.Contains("spellId=10313", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task StarArrowOptionRejectsBeforeSecondRebirth()
    {
        var result = await RunStarArrowGateAsync(CreatePureArcherSession(reborn: 1));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertContainsLegacyArcherDialog(
            ParseNpcReplies(result.Sent),
            "Only pure Archers, whose classes for their past two lives and their current life are all Archers, can learn the pure skill Star Arrow.",
            "Okay.~I~see.");
        Assert.Contains("sourceRange=NpcHandler.cs:49029-49048", result.Log);
        Assert.Contains("source reborn gate rejected", result.Log);
    }

    [Fact]
    public async Task StarArrowOptionRejectsNonPureArcherCombo()
    {
        var result = await RunStarArrowGateAsync(CreatePureArcherSession(firstClass: 2_002));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertContainsLegacyArcherDialog(
            ParseNpcReplies(result.Sent),
            "Only pure Archers, whose classes for their past two lives and their current life are all Archers, can learn the pure skill Star Arrow.",
            "Okay.~I~see.");
        Assert.Contains("sourceRange=NpcHandler.cs:49029-49048", result.Log);
        Assert.Contains("source pure Archer combo gate rejected", result.Log);
    }

    [Fact]
    public async Task StarArrowOptionDoesNotRequireLevelOrPromotedArcherClass()
    {
        var result = await RunStarArrowGateAsync(CreatePureArcherSession(classCode: 4_000, level: 1));

        Assert.True(result.Handled);
        AssertPersistedSpells(result.SpellsRoot, [StarArrowSpellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(result.Sent));
        Assert.Equal(StarArrowSpellId, spellPacket.Id);
        AssertContainsLegacyArcherDialog(
            ParseNpcReplies(result.Sent),
            "Congratulation! You just have learned the skill StarArrow",
            "Ok , thank you.");
        Assert.Contains("WorldServer handled LegacyNpc PromotionArcher pure skill", result.Log);
        Assert.DoesNotContain("level=1", result.Log);
        Assert.DoesNotContain("requiredLevel", result.Log);
        Assert.DoesNotContain("requiredClass", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task StarArrowOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "AlreadyLearned");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(StarArrowSpellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreatePureArcherSession(playerSpells: learned);
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
            ArcherPromotionNpcId,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        AssertPersistedSpells(spellsRoot, [StarArrowSpellId]);
        AssertContainsLegacyArcherDialog(ParseNpcReplies(sent), "You`ve learned StarArrow!", "Okay.");
        Assert.Contains("sourceRange=NpcHandler.cs:49029-49048", log.ToString());
        Assert.Contains("spellId=10313; already learned", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<GateResult> RunStarArrowGateAsync(WorldSession session)
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
            ArcherPromotionNpcId,
            optionId: 31,
            CancellationToken.None);

        return new GateResult(handled, sent, spellsRoot, log.ToString());
    }

    private static WorldSession CreatePureArcherSession(
        long classCode = 4_002,
        long firstClass = 4_001,
        long secondClass = 4_005,
        long reborn = 2,
        long level = 100,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "archer-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PureArcher",
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

    private static void AssertContainsLegacyArcherDialog(
        IReadOnlyList<LegacyNpcReplyPacket> replies,
        string dialogText,
        string optionText)
    {
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == dialogText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.Text == optionText &&
            reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
            reply.InputMaxLength == 10);
    }

    private sealed record GateResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string SpellsRoot,
        string Log);
}
