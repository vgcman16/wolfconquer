using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersPirateEpicSkillTests : IDisposable
{
    private const uint PiratePromotionNpcId = 9_391;
    private const long CharacterUid = 1_293_720;
    private const long StartingConquerPoints = 100;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersPirateEpicSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> PirateEpicSkills => new()
    {
        { 249, 14_680, "SeaBurial", "NpcHandler.cs:51693-51731" },
        { 248, 14_720, "BloomofDeath", "NpcHandler.cs:51732-51770" },
        { 247, 14_710, "ImmortalForce", "NpcHandler.cs:51771-51809" },
    };

    [Theory]
    [MemberData(nameof(PirateEpicSkills))]
    public async Task PirateEpicSkillOptionLearnsSpellPersistsAndDoesNotChangeConquerPoints(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Success-" + optionId);
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(level: 100, conquerPoints: StartingConquerPoints);
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
        Assert.Equal(StartingConquerPoints, updated.Character.ConquerPoints);
        Assert.Equal(new[] { (uint)spellId }, updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [spellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(spellId, spellPacket.Id);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);
        AssertOption(replies, "Ok , thank you.");
        AssertAvatar(replies, 8);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("source=NpcHandler.PromotionPirate", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [MemberData(nameof(PirateEpicSkills))]
    public async Task PirateEpicSkillOptionRejectsBelowSourceLevelGate(
        byte optionId,
        ushort spellId,
        string _,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "LevelGate-" + optionId);
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(level: 99, conquerPoints: StartingConquerPoints);
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
        Assert.Empty(ParseSpellPackets(sent));
        AssertPersistedSpells(spellsRoot, []);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(StartingConquerPoints, updated.Character.ConquerPoints);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply =>
            reply.Text == "You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified.");
        AssertOption(replies, "Alright.");
        AssertAvatar(replies, 8);

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.Contains("level=99 requiredLevel=100; source level gate rejected", logText);
    }

    [Theory]
    [MemberData(nameof(PirateEpicSkills))]
    public async Task PirateEpicSkillOptionAlreadyLearnedDoesNotDuplicate(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "AlreadyLearned-" + optionId);
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(spellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(
            level: 100,
            conquerPoints: StartingConquerPoints,
            playerSpells: learned);
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
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(StartingConquerPoints, updated.Character.ConquerPoints);
        Assert.Equal(new[] { (uint)spellId }, updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [spellId]);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "You`ve learned " + spellName + "!");
        AssertOption(replies, "Okay.");
        AssertAvatar(replies, 8);

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("spellId=" + spellId + "; already learned", logText);
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
        long conquerPoints,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "pirate-epic-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "EpicPirate",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_002,
                FirstClass: 7_000,
                SecondClass: 0,
                Reborn: 2,
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
}
