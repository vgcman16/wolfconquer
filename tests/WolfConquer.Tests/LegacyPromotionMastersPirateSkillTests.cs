using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersPirateSkillTests : IDisposable
{
    private const uint PiratePromotionNpcId = 9_391;
    private const long CharacterUid = 1_293_700;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersPirateSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> PirateSkills => new()
    {
        { 6, 11_110, "BladeTempest", "NpcHandler.cs:52000-52038" },
        { 7, 11_070, "GaleBomb", "NpcHandler.cs:52039-52077" },
        { 8, 11_130, "AdrenalineRush", "NpcHandler.cs:52078-52116" },
        { 9, 11_030, "EagleEye", "NpcHandler.cs:52117-52155" },
        { 10, 11_060, "BlackbeardsRage", "NpcHandler.cs:52156-52194" },
        { 11, 11_100, "KrakensRevenge", "NpcHandler.cs:52195-52244" },
        { 12, 11_120, "Blackspot", "NpcHandler.cs:51824-51862" },
        { 13, 11_140, "Windstorm", "NpcHandler.cs:51961-51999" },
        { 14, 1_270, "Golem", "NpcHandler.cs:51922-51960" },
        { 60, 11_050, "CannonBarrage", "NpcHandler.cs:51883-51921" },
    };

    [Theory]
    [MemberData(nameof(PirateSkills))]
    public async Task PirateSkillOptionLearnsSpellAndPersists(
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
            PiratePromotionNpcId,
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
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("source=NpcHandler.PromotionPirate", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task PirateSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "duplicate-blade-tempest");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(11_110)]);
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
            PiratePromotionNpcId,
            optionId: 6,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([11_110U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([11_110U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned BladeTempest!");
        Assert.Contains("sourceRange=NpcHandler.cs:52000-52038", log.ToString());
        Assert.Contains("spellId=11110; already learned", log.ToString());
    }

    [Fact]
    public async Task PirateSkillOptionRejectsBelowSourceLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate-cannon-barrage");
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
            PiratePromotionNpcId,
            optionId: 60,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified.");

        var logText = log.ToString();
        Assert.Contains("sourceRange=NpcHandler.cs:51883-51921", logText);
        Assert.Contains("option=60", logText);
        Assert.Contains("level=99 requiredLevel=100; source level gate rejected", logText);
    }

    [Fact]
    public async Task KrakenRevengeRejectsWithoutSourceRebornPirateGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "kraken-reborn-gate");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(level: 100, reborn: 1, firstClass: 7_000);
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
            PiratePromotionNpcId,
            optionId: 11,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Sorry you are not reborn, or not is pirat-pirat ");
        Assert.Contains("sourceRange=NpcHandler.cs:52195-52244", log.ToString());
        Assert.Contains("source reborn/first-class gate rejected", log.ToString());
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
        long reborn = 2,
        long firstClass = 7_000,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "pirate-promotion-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PirateHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_002,
                FirstClass: firstClass,
                SecondClass: 0,
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
