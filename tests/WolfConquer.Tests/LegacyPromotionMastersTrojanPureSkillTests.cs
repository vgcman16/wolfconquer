using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTrojanPureSkillTests : IDisposable
{
    private const ushort DragonWhirlSpellId = 10_315;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTrojanPureSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DragonWhirlOptionLearnsSpellAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Success");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePureTrojanSession();
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
            npcId: 10_022,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([DragonWhirlSpellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([DragonWhirlSpellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(1_293_600)
            .SpellIds
            .ToArray());

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(DragonWhirlSpellId, spellPacket.Id);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill DragonWhirl");
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 5);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionTrojan pure skill", logText);
        Assert.Contains("source=NpcHandler.PromotionTrojan", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:47713-47744", logText);
        Assert.Contains("spellId=10315", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task DragonWhirlOptionRejectsBeforeSecondRebirth()
    {
        var result = await RunDragonWhirlGateAsync(CreatePureTrojanSession(reborn: 1));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.Text == "After your 2nd Rebirth, you still have to be Reincarnated and become a Pure Trojan, to be able to learn the Dragon Whirl skill.");
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 5);
        Assert.Contains("source reborn gate rejected", result.Log);
    }

    [Fact]
    public async Task DragonWhirlOptionRejectsNonPureTrojanCombo()
    {
        var result = await RunDragonWhirlGateAsync(CreatePureTrojanSession(firstClass: 2_002));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.Text == "You don'thave the combo ( Tro - Tro - Tro ) in your status.");
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 5);
        Assert.Contains("source pure Trojan combo gate rejected", result.Log);
    }

    [Theory]
    [InlineData(1_002, 39)]
    [InlineData(1_001, 40)]
    public async Task DragonWhirlOptionRejectsBeforePromotedLevelForty(long classCode, long level)
    {
        var result = await RunDragonWhirlGateAsync(CreatePureTrojanSession(classCode: classCode, level: level));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.Text == "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.");
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 5);
        Assert.Contains("source promotion gate rejected", result.Log);
    }

    [Fact]
    public async Task DragonWhirlOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "AlreadyLearned");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot(
        [
            PlayerSpellSnapshotLoaderTests.Entry(DragonWhirlSpellId, level: 0),
        ]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(1_293_600, learned);
        var session = CreatePureTrojanSession(playerSpells: learned);
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
            npcId: 10_022,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        AssertPersistedSpells(spellsRoot, [DragonWhirlSpellId]);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned DragonWhirl!");
        Assert.Contains(ParseNpcReplies(sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 5);
        Assert.Contains("spellId=10315; already learned", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<GateResult> RunDragonWhirlGateAsync(WorldSession session)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(1_293_600, PlayerSpellSnapshot.Empty);
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
            npcId: 10_022,
            optionId: 31,
            CancellationToken.None);

        return new GateResult(handled, sent, spellsRoot, log.ToString());
    }

    private static WorldSession CreatePureTrojanSession(
        long classCode = 1_002,
        long firstClass = 1_002,
        long secondClass = 1_005,
        long reborn = 2,
        long level = 40,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "trojan-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "PureTrojan",
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
            .LoadByCharacterUid(1_293_600)
            .SpellIds
            .ToArray());
    }

    private sealed record GateResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string SpellsRoot,
        string Log);
}
