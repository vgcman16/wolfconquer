using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersNinjaSkillTests : IDisposable
{
    private const uint NinjaPromotionNpcId = 4_972;
    private const long CharacterUid = 1_293_650;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersNinjaSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> SkillOptions => new()
    {
        { 5, 6_004, "ArcherBane", "NpcHandler.cs:44898-44936" },
        { 14, 6_001, "ToxicFog", "NpcHandler.cs:44859-44897" },
        { 7, 6_000, "TwofoldBlades", "NpcHandler.cs:44765-44803" },
        { 249, 12_080, "SuperTwofoldBlade", "NpcHandler.cs:44804-44842" },
        { 9, 11_180, "MortalDrag", "NpcHandler.cs:44710-44748" },
        { 10, 11_170, "BloodyScythe", "NpcHandler.cs:44671-44709" },
        { 87, 11_230, "GapingWounds", "NpcHandler.cs:44632-44670" },
        { 12, 6_010, "ShurikenVortex", "NpcHandler.cs:44575-44613" },
        { 13, 6_011, "FatalStrike", "NpcHandler.cs:44536-44574" },
    };

    [Theory]
    [MemberData(nameof(SkillOptions))]
    public async Task NinjaSkillOptionLearnsSpellAndPersists(
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
        var session = CreateSession(classCode: 5_005, level: 140);
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
            NinjaPromotionNpcId,
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
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("source=NpcHandler.PromotionNinja", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task NinjaSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "duplicate-gaping-wounds");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(11_230)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(classCode: 5_002, level: 100, playerSpells: learned);
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
            NinjaPromotionNpcId,
            optionId: 87,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([11_230U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([11_230U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned GapingWounds!");
        Assert.Contains("sourceRange=NpcHandler.cs:44632-44670", log.ToString());
        Assert.Contains("spellId=11230; already learned", log.ToString());
    }

    [Fact]
    public async Task NinjaSkillOptionRejectsBelowLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate-toxic-fog");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(classCode: 5_001, level: 14);
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
            NinjaPromotionNpcId,
            optionId: 14,
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
        Assert.Contains("sourceRange=NpcHandler.cs:44859-44897", logText);
        Assert.Contains("option=14", logText);
        Assert.Contains("level=14 requiredLevel=15; source level gate rejected", logText);
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
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "ninja-promotion-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "NinjaHero",
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
