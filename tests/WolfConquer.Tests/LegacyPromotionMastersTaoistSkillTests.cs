using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTaoistSkillTests : IDisposable
{
    private const uint TaoistPromotionNpcId = 10_000;
    private const long CharacterUid = 1_293_700;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTaoistSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> TaoistSkills => new()
    {
        { 20, 1_000, "Thunder", "NpcHandler.cs:42846-42873" },
        { 21, 1_005, "Cure", "NpcHandler.cs:42818-42845" },
        { 22, 1_195, "Meditation", "NpcHandler.cs:42779-42817" },
        { 23, 1_001, "Fire", "NpcHandler.cs:42396-42466" },
        { 24, 1_002, "Tornado", "NpcHandler.cs:42325-42395" },
        { 29, 1_055, "HealingRain", "NpcHandler.cs:42740-42778" },
        { 30, 1_085, "StarofAccuracy", "NpcHandler.cs:42701-42739" },
        { 31, 1_090, "MagicShield", "NpcHandler.cs:42662-42700" },
        { 32, 1_095, "Stigma", "NpcHandler.cs:42623-42661" },
        { 33, 1_075, "Invisibility", "NpcHandler.cs:42584-42622" },
        { 34, 1_100, "Pray", "NpcHandler.cs:42545-42583" },
        { 35, 1_175, "EffectMP", "NpcHandler.cs:42506-42544" },
        { 36, 1_170, "Nectar", "NpcHandler.cs:42467-42505" },
        { 40, 10_309, "ChainBolt", "NpcHandler.cs:42008-42035" },
        { 41, 1_010, "Lightning", "NpcHandler.cs:41969-42007" },
        { 42, 1_125, "Vulcano", "NpcHandler.cs:41930-41968" },
        { 43, 1_050, "Revive", "NpcHandler.cs:41882-41929" },
        { 44, 5_001, "SpeedLightning", "NpcHandler.cs:41843-41881" },
    };

    [Theory]
    [MemberData(nameof(TaoistSkills))]
    public async Task TaoistSkillOptionLearnsSpellAndPersists(
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
        var initialSpells = new PlayerSpellSnapshot(CreatePrerequisiteSpells(optionId));
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, initialSpells);
        var session = CreateSession(level: 100, playerSpells: initialSpells);
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
            TaoistPromotionNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        var expectedSpellIds = initialSpells.SpellIds.Concat([spellId]).ToArray();
        Assert.Equal(expectedSpellIds, updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(expectedSpellIds, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(spellId, spellPacket.Id);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("source=NpcHandler.PromotionTaoist", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task TaoistSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "duplicate-star-of-accuracy");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(1_085)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(level: 100, playerSpells: learned);
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
            TaoistPromotionNpcId,
            optionId: 30,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Equal([1_085U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned StarofAccuracy!");
        Assert.Contains("sourceRange=NpcHandler.cs:42701-42739", log.ToString());
        Assert.Contains("spellId=1085; already learned", log.ToString());
    }

    [Fact]
    public async Task TaoistSkillOptionRejectsBelowLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate-meditation");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(level: 43);
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
            TaoistPromotionNpcId,
            optionId: 22,
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
        Assert.Contains("sourceRange=NpcHandler.cs:42779-42817", log.ToString());
        Assert.Contains("level=43 requiredLevel=44", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PlayerSpellSnapshotEntry[] CreatePrerequisiteSpells(byte optionId)
    {
        return optionId switch
        {
            23 => [Entry(1_000, level: 4)],
            24 => [Entry(1_001, level: 3)],
            _ => [],
        };
    }

    private static WorldSession CreateSession(
        long level = 140,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "taoist-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "TaoistHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 13_002,
                FirstClass: 13_002,
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

    private static PlayerSpellSnapshotEntry Entry(ushort id, ushort level = 0)
    {
        return new PlayerSpellSnapshotEntry(
            id,
            level,
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
