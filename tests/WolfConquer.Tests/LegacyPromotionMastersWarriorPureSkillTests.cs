using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersWarriorPureSkillTests : IDisposable
{
    private const uint WarriorPromotionNpcId = 10_001;
    private const ushort PerseveranceSpellId = 10_311;
    private const long CharacterUid = 1_293_600;
    private const string PureGateText =
        "Only pure Warriors, who have spent all 3 lives as a Warrior, can learn the Pure Skill, Perseverance.";
    private const string PromotionGateText =
        "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersWarriorPureSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PerseveranceOptionLearnsSpellAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Success");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePureWarriorSession();
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
            WarriorPromotionNpcId,
            optionId: 22,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([(uint)PerseveranceSpellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([(uint)PerseveranceSpellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(PerseveranceSpellId, spellPacket.Id);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill Perseverance");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == byte.MaxValue &&
            reply.Text == "Ok , thank you.");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 8);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionWarrior pure skill", logText);
        Assert.Contains("source=NpcHandler.PromotionWarrior", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:45501-45533", logText);
        Assert.Contains("option=22", logText);
        Assert.Contains("spellId=10311", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task PerseveranceOptionRejectsBeforeSecondRebirth()
    {
        var result = await RunPerseveranceGateAsync(CreatePureWarriorSession(reborn: 1));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertContainsDialogOptionAndAvatar(result.Sent, PureGateText, "Ok~I~see.");
        Assert.Contains("sourceRange=NpcHandler.cs:45501-45533", result.Log);
        Assert.Contains("source reborn gate rejected", result.Log);
    }

    [Fact]
    public async Task PerseveranceOptionRejectsNonPureWarriorCombo()
    {
        var result = await RunPerseveranceGateAsync(CreatePureWarriorSession(firstClass: 1_002));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertContainsDialogOptionAndAvatar(result.Sent, PureGateText, "Ok~I~see.");
        Assert.Contains("sourceRange=NpcHandler.cs:45501-45533", result.Log);
        Assert.Contains("source pure Warrior combo gate rejected", result.Log);
    }

    [Theory]
    [InlineData(2_002, 39)]
    [InlineData(2_001, 40)]
    public async Task PerseveranceOptionRejectsBeforePromotedLevelForty(long classCode, long level)
    {
        var result = await RunPerseveranceGateAsync(CreatePureWarriorSession(classCode: classCode, level: level));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertContainsDialogOptionAndAvatar(result.Sent, PromotionGateText, "Okay.~I~see..");
        Assert.Contains("sourceRange=NpcHandler.cs:45501-45533", result.Log);
        Assert.Contains("source promotion gate rejected", result.Log);
    }

    [Fact]
    public async Task PerseveranceOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "AlreadyLearned");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(PerseveranceSpellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreatePureWarriorSession(playerSpells: learned);
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
            WarriorPromotionNpcId,
            optionId: 22,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([(uint)PerseveranceSpellId], updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [(uint)PerseveranceSpellId]);
        AssertContainsDialogOptionAndAvatar(sent, "You`ve learned Perseverance!", "Okay.");

        var logText = log.ToString();
        Assert.Contains("sourceRange=NpcHandler.cs:45501-45533", logText);
        Assert.Contains("spellId=10311; already learned", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<GateResult> RunPerseveranceGateAsync(WorldSession session)
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
            WarriorPromotionNpcId,
            optionId: 22,
            CancellationToken.None);

        return new GateResult(handled, sent, spellsRoot, log.ToString());
    }

    private static WorldSession CreatePureWarriorSession(
        long classCode = 2_002,
        long firstClass = 2_002,
        long secondClass = 2_005,
        long reborn = 2,
        long level = 40,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "warrior-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PureWarrior",
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

    private static void AssertContainsDialogOptionAndAvatar(
        IReadOnlyList<byte[]> sent,
        string expectedText,
        string expectedOption)
    {
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == expectedText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == byte.MaxValue &&
            reply.Text == expectedOption);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 8);
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
