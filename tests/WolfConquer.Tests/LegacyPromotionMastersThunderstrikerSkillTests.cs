using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersThunderstrikerSkillTests : IDisposable
{
    private const uint ThunderstrikerPromotionNpcId = 23_599;
    private const long CharacterUid = 1_293_710;
    private const string ThunderRampageSourceRange = "NpcHandler.cs:102571-102598";
    private const string ClassSkillSourceRange = "NpcHandler.cs:102622-102677";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersThunderstrikerSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, long> ThunderstrikerClassSkills => new()
    {
        { 20, 15_800, "WindstormBattleaxe", 1 },
        { 21, 15_700, "DevouringStrike", 1 },
        { 22, 15_770, "SkyFall", 1 },
        { 23, 15_720, "CrackingShock", 15 },
        { 24, 15_820, "UndyingImprinting", 40 },
        { 25, 15_750, "LightningShield", 70 },
        { 26, 15_810, "ThunderBlast", 100 },
        { 27, 15_790, "Megabolt", 100 },
    };

    [Theory]
    [MemberData(nameof(ThunderstrikerClassSkills))]
    public async Task ThunderstrikerClassSkillOptionLearnsSpellAndPersists(
        byte optionId,
        ushort spellId,
        string spellName,
        long requiredLevel)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "success-" + optionId);
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(level: Math.Max(100, requiredLevel));
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
            ThunderstrikerPromotionNpcId,
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

        Assert.Equal([spellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "\tYou`ve acquired [" + spellName + "]. Go practice and feel its power.");

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("source=NpcHandler.PromotionThunderstriker", logText);
        Assert.Contains("sourceRange=" + ClassSkillSourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task ThunderstrikerXpSkillOptionLearnsThunderRampageAtLevelThreeWithSourceText()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "xp-skill");
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(level: 3);
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
            ThunderstrikerPromotionNpcId,
            optionId: 13,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal([15_780U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Equal(new ushort[] { 15_780 }, ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "\tYou`ve acquired [ThunderRampage]. Go practice and feel its power.");
        Assert.Contains("sourceRange=" + ThunderRampageSourceRange, log.ToString());
    }

    [Fact]
    public async Task ThunderstrikerXpSkillOptionRejectsBelowLevelThree()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "xp-level-gate");
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(level: 2);
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
            ThunderstrikerPromotionNpcId,
            optionId: 13,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "\tThe study of [ThunderRampage] requires you to reach Level 3.");

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + ThunderRampageSourceRange, logText);
        Assert.Contains("spellId=15780 level=2 requiredLevel=3", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task ThunderstrikerXpSkillOptionAlreadyLearnedUsesSourceTextBeforeLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "xp-duplicate");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(15_780)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(level: 2, playerSpells: learned);
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
            ThunderstrikerPromotionNpcId,
            optionId: 13,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "ThunderGeneral has already acquired this skill. Go practice and feel its power.");

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + ThunderRampageSourceRange, logText);
        Assert.Contains("spellId=15780; already learned", logText);
        Assert.DoesNotContain("requiredLevel=3", logText);
    }

    [Fact]
    public async Task ThunderstrikerClassSkillOptionAlreadyLearnedUsesSourceText()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "duplicate-megabolt");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(15_790)]);
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
            ThunderstrikerPromotionNpcId,
            optionId: 27,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Equal([15_790U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "ThunderGeneral has already acquired this skill. Go practice and feel its power.");
        Assert.Contains("spellId=15790; already learned", log.ToString());
    }

    [Fact]
    public async Task ThunderstrikerClassSkillOptionRejectsBelowLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate-lightning-shield");
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(level: 69);
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
            ThunderstrikerPromotionNpcId,
            optionId: 25,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "\tThe study of [LightningShield] requires you to reach Level 70.");
        Assert.Contains("level=69 requiredLevel=70", log.ToString());
    }

    [Fact]
    public async Task ThunderstrikerUndyingImprintingOptionRejectsBelowLevelFortyWithSourceText()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate-undying-imprinting");
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(level: 39);
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
            ThunderstrikerPromotionNpcId,
            optionId: 24,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "\tThe study of [UndyingImprinting] requires you to reach Level 40.");

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + ClassSkillSourceRange, logText);
        Assert.Contains("spellId=15820 level=39 requiredLevel=40", logText);
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
        long level,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "thunderstriker-promotion-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "ThunderHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 9_005,
                FirstClass: 9_005,
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
