using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersPiratePureSkillTests : IDisposable
{
    private const uint PiratePromotionNpcId = 9_391;
    private const byte ScurvyBombOption = 21;
    private const ushort ScurvyBombSpellId = 11_040;
    private const long CharacterUid = 1_293_710;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersPiratePureSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScurvyBombOptionLearnsSpellAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Success");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePurePirateSession();
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
            PiratePromotionNpcId,
            ScurvyBombOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(new[] { (uint)ScurvyBombSpellId }, updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [ScurvyBombSpellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(ScurvyBombSpellId, spellPacket.Id);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill ScurvyBomb");
        AssertOption(replies, "Ok , thank you.");
        AssertAvatar(replies, 5);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionPirate pure skill", logText);
        Assert.Contains("source=NpcHandler.PromotionPirate", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:51546-51564", logText);
        Assert.Contains("option=21", logText);
        Assert.Contains("spellId=11040", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task ScurvyBombOptionRejectsBeforeSecondRebirth()
    {
        var result = await RunScurvyBombGateAsync(CreatePurePirateSession(reborn: 1));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply =>
            reply.Text == "If you want to learn the Scurvy Bomb, you`ll need to be a pure Pirate, who`ve been a Pirate for 3 straight lives.");
        AssertOption(replies, "OK");
        AssertAvatar(replies, 234);
        Assert.Contains("sourceRange=NpcHandler.cs:51546-51564", result.Log);
        Assert.Contains("source pure Pirate combo gate rejected", result.Log);
    }

    [Fact]
    public async Task ScurvyBombOptionRejectsNonPurePirateCombo()
    {
        var result = await RunScurvyBombGateAsync(CreatePurePirateSession(secondClass: 1_002));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply =>
            reply.Text == "If you want to learn the Scurvy Bomb, you`ll need to be a pure Pirate, who`ve been a Pirate for 3 straight lives.");
        AssertOption(replies, "OK");
        AssertAvatar(replies, 234);
        Assert.Contains("firstClass=7002 secondClass=1002", result.Log);
        Assert.Contains("source pure Pirate combo gate rejected", result.Log);
    }

    [Fact]
    public async Task ScurvyBombOptionHasNoLevelOrCurrentClassGateWhenPurePirate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "NoLevelOrCurrentClassGate");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePurePirateSession(
            classCode: 7_000,
            firstClass: 7_000,
            secondClass: 7_000,
            level: 1);
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
            PiratePromotionNpcId,
            ScurvyBombOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(new[] { (uint)ScurvyBombSpellId }, updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [ScurvyBombSpellId]);
        Assert.Single(ParseSpellPackets(sent));
        Assert.Contains(ParseNpcReplies(sent), reply =>
            reply.Text == "Congratulation! You just have learned the skill ScurvyBomb");

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionPirate pure skill", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:51546-51564", logText);
        Assert.DoesNotContain("source level gate rejected", logText);
        Assert.DoesNotContain("source promotion gate rejected", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task ScurvyBombOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "AlreadyLearned");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(ScurvyBombSpellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreatePurePirateSession(playerSpells: learned);
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
            PiratePromotionNpcId,
            ScurvyBombOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(new[] { (uint)ScurvyBombSpellId }, updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [ScurvyBombSpellId]);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "You`ve learned ScurvyBomb!");
        AssertOption(replies, "Okay.");
        AssertAvatar(replies, 5);
        Assert.Contains("sourceRange=NpcHandler.cs:51546-51564", log.ToString());
        Assert.Contains("spellId=11040; already learned", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<GateResult> RunScurvyBombGateAsync(WorldSession session)
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
            PiratePromotionNpcId,
            ScurvyBombOption,
            CancellationToken.None);

        return new GateResult(handled, sent, spellsRoot, log.ToString());
    }

    private static WorldSession CreatePurePirateSession(
        long classCode = 7_002,
        long firstClass = 7_002,
        long secondClass = 7_003,
        long reborn = 2,
        long level = 140,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "pirate-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PurePirate",
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

    private static void AssertPersistedSpells(string spellsRoot, IReadOnlyList<ushort> expectedSpellIds)
    {
        Assert.Equal(
            expectedSpellIds.Select(static spellId => (uint)spellId).ToArray(),
            new PlayerSpellSnapshotLoader(spellsRoot)
                .LoadByCharacterUid(CharacterUid)
                .SpellIds
                .ToArray());
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text)
    {
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.Text == text &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
    }

    private static void AssertAvatar(IReadOnlyList<LegacyNpcReplyPacket> replies, ushort avatarId)
    {
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
            reply.InputMaxLength == avatarId);
    }

    private sealed record GateResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string SpellsRoot,
        string Log);
}
