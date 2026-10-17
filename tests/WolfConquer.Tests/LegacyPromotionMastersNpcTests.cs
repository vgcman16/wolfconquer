using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersNpcTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersNpcTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<uint, long, string, string, byte, string> PromotionMasterRootDialogs => new()
    {
        { 10_000, 14_057, "NpcHandler.PromotionTaoist", "Every Taoist wants holy spirit", 10, "Learn~class~skills." },
        { 8_314, 6_057, "NpcHandler.PromotionMonk", "Monks and Saints understand", 3, "Learn~class~skills." },
        { 4_972, 5_057, "NpcHandler.PromotionNinja", "Cloaked in shadow", 6, "Learn~Katana~skills." },
        { 10_001, 2_057, "NpcHandler.PromotionWarrior", "To be a good Warrior", 10, "Learn~XP~skills." },
        { 19_634, 16_057, "NpcHandler.PromotionWindWalker", "WindWalker school follows", 8, "Learn~skills." },
        { 10_022, 1_057, "NpcHandler.PromotionTrojan", "Wielding dual weapons", 4, "Learn~class~and~weapon~skills." },
        { 400, 4_057, "NpcHandler.PromotionArcher", "Born with sharp insight", 5, "Learn~the~Class~Skill." },
        { 17_126, 8_057, "NpcHandler.PromotionLeeLong", "Dragon-Warriors fight", 5, "Learn~the~Class~Skill." },
        { 23_599, 9_057, "NpcHandler.PromotionThunderstriker", "Thunderstrikers inherit", 2, "Learn~Class~skills." },
        { 9_391, 7_057, "NpcHandler.PromotionPirate", "Pirates pursue freedom", 5, "Learn~the~Class~Skill." },
    };

    [Theory]
    [MemberData(nameof(PromotionMasterRootDialogs))]
    public async Task HandlerRecognizesPromotionMasterAndSendsSourceBackedRootDialog(
        uint npcId,
        long classCode,
        string sourceMethod,
        string expectedTextFragment,
        byte expectedOptionId,
        string expectedOptionText)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: classCode);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedTextFragment, StringComparison.Ordinal));
        AssertOption(replies, expectedOptionText, expectedOptionId);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains($"source={sourceMethod}", log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsDeterministicSubmenuWithoutMutationForTaoistSkills()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: 13_002);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 10_000,
            optionId: 10,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "I know many skills and spells for a true Taoist. Which kind do you want to learn first?");
        AssertOption(replies, "Learn~magic.", 11);
        AssertOption(replies, "Learn~XP~Skill.", 12);
        AssertOption(replies, "Learn~Epic~Skills.", 95);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.PromotionTaoist", log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTaoistAdvancedFireSkillSubmenuForModeledSourceBranches()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(classCode: 14_002);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 10_000,
            optionId: 11,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        AssertOption(replies, "Advanced~fire~magic.", 13);

        sent.Clear();
        handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 10_000,
            optionId: 13,
            CancellationToken.None);

        Assert.True(handled);
        replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Advanced Fire Taoist magic includes source branches hidden behind the legacy fire path.");
        AssertOption(replies, "ChainBolt(Lv.1).", 70);
        AssertOption(replies, "Lightning(Lv.1).", 71);
        AssertOption(replies, "FireRing(Lv.55).", 25);
        AssertOption(replies, "FireMeteor(Lv.52).", 26);
        AssertOption(replies, "FireCircle(Lv.65).", 27);
        AssertOption(replies, "Bomb(Lv.82).", 28);
        AssertOption(replies, "FireofHell(Lv.93).", 58);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Fact]
    public async Task TrojanAccuracyCycloneOptionLearnsSpellsAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "TrojanAccuracyCyclone");
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(classCode: 1_002, level: 40);
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
            npcId: 10_022,
            optionId: 9,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([1_015U, 1_110U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([1_015U, 1_110U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(1_293_600)
            .SpellIds
            .ToArray());

        var spellPackets = ParseSpellPackets(sent);
        Assert.Equal([1_015, 1_110], spellPackets.Select(packet => packet.Id).ToArray());
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "You just have learned the skill Accuracy/Cyclone");

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionTrojan skill", logText);
        Assert.Contains("source=NpcHandler.PromotionTrojan", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:100743-100779", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task TrojanAccuracyCycloneOptionLearnsMissingCycloneWhenAccuracyAlreadyExists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "TrojanAccuracyCycloneMissingCyclone");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot(
        [
            PlayerSpellSnapshotLoaderTests.Entry(1_015, level: 0),
        ]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(1_293_600, learned);
        var session = CreateSession(classCode: 1_002, level: 40, playerSpells: learned);
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
            npcId: 10_022,
            optionId: 9,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([1_015U, 1_110U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([1_015U, 1_110U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(1_293_600)
            .SpellIds
            .ToArray());

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(1_110, spellPacket.Id);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You just have learned the skill /Cyclone");

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionTrojan skill", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:100743-100779", logText);
        Assert.Contains("spellIds=1015,1110", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task TrojanAccuracyCycloneOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "TrojanAccuracyCycloneDuplicate");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot(
        [
            PlayerSpellSnapshotLoaderTests.Entry(1_015, level: 0),
            PlayerSpellSnapshotLoaderTests.Entry(1_110, level: 0),
        ]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(1_293_600, learned);
        var session = CreateSession(classCode: 1_002, playerSpells: learned);
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
            npcId: 10_022,
            optionId: 9,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Equal([1_015U, 1_110U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(1_293_600)
            .SpellIds
            .ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned Accuracy and Cyclone!");
        Assert.Contains("spellIds=1015,1110; already learned", log.ToString());
    }

    [Theory]
    [InlineData(1_002, 39)]
    [InlineData(1_001, 40)]
    public async Task TrojanAccuracyCycloneOptionRejectsBeforeLearningWhenSourceGateFails(
        long classCode,
        long level)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "TrojanAccuracyCycloneGate" + classCode + "-" + level);
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(1_293_600, PlayerSpellSnapshot.Empty);
        var session = CreateSession(classCode: classCode, level: level);
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
            npcId: 10_022,
            optionId: 9,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Equal([], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(1_293_600)
            .SpellIds
            .ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.");

        var logText = log.ToString();
        Assert.Contains("source=NpcHandler.PromotionTrojan", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:100743-100779", logText);
        Assert.Contains("requiredLevel=40", logText);
        Assert.Contains("requiredClass=1002", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
    }

    private static WorldSession CreateSession(
        long classCode,
        long conquerPoints = 0,
        long level = 140,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "promotion-master-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
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
                ConquerPoints: conquerPoints,
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

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }
}
