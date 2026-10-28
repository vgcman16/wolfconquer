using System.Buffers;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTrojanSimpleSkillTests : IDisposable
{
    private const uint TrojanPromotionNpcId = 10_022;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTrojanSimpleSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> SimpleSkills => new()
    {
        { 8, 11_970, "SuperCyclone", "NpcHandler.cs:48279-48317" },
        { 10, 1_270, "Golem", "NpcHandler.cs:48318-48356" },
        { 11, 1_115, "Hercules", "NpcHandler.cs:48357-48395" },
        { 12, 1_190, "SpiritHealing", "NpcHandler.cs:48396-48434" },
        { 13, 11_960, "BreathFocus", "NpcHandler.cs:48435-48473" },
    };

    [Theory]
    [MemberData(nameof(SimpleSkills))]
    public async Task TrojanSimpleSkillOptionLearnsSpellAndPersists(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "success-" + optionId);
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
            TrojanPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([spellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(spellId, spellPacket.Id);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionTrojan skill", logText);
        Assert.Contains("source=NpcHandler.PromotionTrojan", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task TrojanSimpleSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "duplicate-super-cyclone");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot(
        [
            PlayerSpellSnapshotLoaderTests.Entry(11_970, level: 0),
        ]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(playerSpells: learned);
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
            TrojanPromotionNpcId,
            optionId: 8,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([11_970U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([11_970U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned SuperCyclone!");
        Assert.Contains("sourceRange=NpcHandler.cs:48279-48317", log.ToString());
        Assert.Contains("spellId=11970; already learned", log.ToString());
    }

    [Fact]
    public async Task TrojanSimpleSkillOptionRejectsBelowLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate-super-cyclone");
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
            TrojanPromotionNpcId,
            optionId: 8,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Equal([], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text.Contains("not strong enough", StringComparison.Ordinal));
        Assert.Contains("sourceRange=NpcHandler.cs:48279-48317", log.ToString());
        Assert.Contains("level=99 requiredLevel=100", log.ToString());
    }

    [Fact]
    public async Task TrojanSimpleSkillDirectBranchRejectsNonTrojanClassBeforeLearning()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "class-gate-super-cyclone");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(level: 100, classCode: 2_002);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeTrojanPromotionSkillAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            TrojanPromotionNpcId,
            optionId: 8);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.Same(session, updated);
        Assert.Equal([], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Sorry, but you are not a member of the Trojan class.");

        var logText = log.ToString();
        Assert.Contains("source=NpcHandler.PromotionTrojan", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:48279-48317", logText);
        Assert.Contains("option=8", logText);
        Assert.Contains("class=2002; source class-family gate rejected", logText);
        Assert.DoesNotContain("WorldServer handled LegacyNpc PromotionTrojan skill", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long level = 140,
        long classCode = 1_002,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "trojan-simple-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "TrojanHero",
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

    private static async ValueTask<bool> InvokeTrojanPromotionSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryHandleLegacyTrojanPromotionSkillAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = Assert.IsType<ValueTask<bool>>(method.Invoke(
            null,
            [context, session, npcId, optionId, CancellationToken.None]));
        return await result;
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
