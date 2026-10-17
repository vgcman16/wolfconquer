using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersNinjaPureSkillTests : IDisposable
{
    private const uint NinjaPromotionNpcId = 4_972;
    private const byte CounterKillOption = 16;
    private const ushort CounterKillSpellId = 6_003;
    private const long CharacterUid = 1_293_660;
    private const ushort NinjaAvatarId = 203;
    private const string CounterKillSourceRange = "NpcHandler.cs:44459-44491";
    private const string PureNinjaGateText =
        "Only pure Ninja, who have spent all 3 lives as a Ninja, can learn the Pure Skill, Counter Kill.";
    private const string PromotionGateText =
        "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersNinjaPureSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CounterKillOptionLearnsSpellAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Success");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePureNinjaSession();
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
            NinjaPromotionNpcId,
            CounterKillOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(new uint[] { CounterKillSpellId }, updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [CounterKillSpellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(CounterKillSpellId, spellPacket.Id);

        AssertDialog(
            ParseNpcReplies(sent),
            "Congratulation! You just have learned the skill CounterKill",
            "Ok , thank you.");

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionNinja pure skill", logText);
        Assert.Contains("source=NpcHandler.PromotionNinja", logText);
        Assert.Contains("sourceRange=" + CounterKillSourceRange, logText);
        Assert.Contains("option=16", logText);
        Assert.Contains("spellId=6003", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task CounterKillOptionRejectsBeforeSecondRebirth()
    {
        var result = await RunCounterKillGateAsync(CreatePureNinjaSession(reborn: 1));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), PureNinjaGateText, "Ok~I~see.");
        Assert.Contains("sourceRange=" + CounterKillSourceRange, result.Log);
        Assert.Contains("source reborn gate rejected", result.Log);
    }

    [Fact]
    public async Task CounterKillOptionRejectsNonPureNinjaClassCombo()
    {
        var result = await RunCounterKillGateAsync(CreatePureNinjaSession(firstClass: 4_002));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), PureNinjaGateText, "Ok~I~see.");
        Assert.Contains("sourceRange=" + CounterKillSourceRange, result.Log);
        Assert.Contains("source pure Ninja combo gate rejected", result.Log);
    }

    [Theory]
    [InlineData(5_002, 39)]
    [InlineData(5_001, 40)]
    public async Task CounterKillOptionRejectsBeforePromotedLevelForty(long classCode, long level)
    {
        var result = await RunCounterKillGateAsync(CreatePureNinjaSession(classCode: classCode, level: level));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), PromotionGateText, "Okay.~I~see..");
        Assert.Contains("sourceRange=" + CounterKillSourceRange, result.Log);
        Assert.Contains("source promotion gate rejected", result.Log);
    }

    [Fact]
    public async Task CounterKillOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "AlreadyLearned");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(CounterKillSpellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreatePureNinjaSession(playerSpells: learned);
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
            NinjaPromotionNpcId,
            CounterKillOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        AssertPersistedSpells(spellsRoot, [CounterKillSpellId]);
        AssertDialog(ParseNpcReplies(sent), "You`ve learned CounterKill!", "Okay.");
        Assert.Contains("sourceRange=" + CounterKillSourceRange, log.ToString());
        Assert.Contains("spellId=6003; already learned", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<GateResult> RunCounterKillGateAsync(WorldSession session)
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
            NinjaPromotionNpcId,
            CounterKillOption,
            CancellationToken.None);

        return new GateResult(handled, sent, spellsRoot, log.ToString());
    }

    private static WorldSession CreatePureNinjaSession(
        long classCode = 5_002,
        long firstClass = 5_002,
        long secondClass = 5_005,
        long reborn = 2,
        long level = 40,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "ninja-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PureNinja",
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

    private static void AssertDialog(
        IReadOnlyList<LegacyNpcReplyPacket> replies,
        string expectedText,
        string expectedOptionText)
    {
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == expectedText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.Text == expectedOptionText &&
            reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
            reply.InputMaxLength == NinjaAvatarId);
    }

    private static void AssertPersistedSpells(string spellsRoot, IReadOnlyList<uint> expectedSpellIds)
    {
        Assert.Equal(expectedSpellIds, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
    }

    private sealed record GateResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string SpellsRoot,
        string Log);
}
