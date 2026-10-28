using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersThunderstrikerPureSkillTests : IDisposable
{
    private const uint ThunderstrikerPromotionNpcId = 23_599;
    private const byte HeavensWrathOption = 3;
    private const ushort HeavensWrathSpellId = 15_710;
    private const long CharacterUid = 1_293_711;
    private const ushort ThunderstrikerAvatarId = 234;
    private const ushort AlreadyLearnedAvatarId = 5;
    private const string HeavensWrathSourceRange = "NpcHandler.cs:102599-102620";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersThunderstrikerPureSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task HeavensWrathOptionLearnsSpellPersistsAndSendsLegacyPacketForPureLineage()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "success");
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
            ThunderstrikerPromotionNpcId,
            HeavensWrathOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([HeavensWrathSpellId], updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [HeavensWrathSpellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(HeavensWrathSpellId, spellPacket.Id);

        var replies = ParseNpcReplies(sent);
        AssertReply(replies, "\tYou`ve acquired [HeavensWrath]. Go practice and feel its power.");
        AssertOption(replies, "Thanks!");
        AssertAvatar(replies, ThunderstrikerAvatarId);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionThunderstriker pure skill", logText);
        Assert.Contains("source=NpcHandler.PromotionThunderstriker", logText);
        Assert.Contains("sourceRange=" + HeavensWrathSourceRange, logText);
        Assert.Contains("option=3", logText);
        Assert.Contains("spellId=15710", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task HeavensWrathOptionRejectsBelowLevelOneHundredWithoutLearning()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "level-gate");
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
            ThunderstrikerPromotionNpcId,
            HeavensWrathOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        AssertPersistedSpells(spellsRoot, []);

        var replies = ParseNpcReplies(sent);
        AssertReply(replies, "\tYou must be a Thunderstriker level 100 in three straight lives till now before you can learn [Heaven`sWrath].");
        AssertOption(replies, "Alright.");
        AssertAvatar(replies, ThunderstrikerAvatarId);

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + HeavensWrathSourceRange, logText);
        Assert.Contains("spellId=15710 level=99 requiredLevel=100", logText);
        Assert.Contains("source pure Thunderstriker gate rejected", logText);
    }

    [Fact]
    public async Task HeavensWrathOptionRejectsNonPureThunderstrikerLineageWithoutLearning()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "lineage-gate");
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, PlayerSpellSnapshot.Empty);
        var session = CreateSession(
            level: 100,
            firstClass: 1_002,
            secondClass: 7_005,
            reborn: 0);
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
            HeavensWrathOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        AssertPersistedSpells(spellsRoot, []);

        var replies = ParseNpcReplies(sent);
        AssertReply(replies, "\tYou must be a Thunderstriker level 100 in three straight lives till now before you can learn [Heaven`sWrath].");
        AssertOption(replies, "Alright.");
        AssertAvatar(replies, ThunderstrikerAvatarId);

        var logText = log.ToString();
        Assert.Contains("sourceRange=" + HeavensWrathSourceRange, logText);
        Assert.Contains("reborn=0 firstClass=1002 secondClass=7005", logText);
        Assert.Contains("source pure Thunderstriker gate rejected", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task HeavensWrathOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "already-learned");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(HeavensWrathSpellId)]);
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
            HeavensWrathOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([HeavensWrathSpellId], updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [HeavensWrathSpellId]);

        var replies = ParseNpcReplies(sent);
        AssertReply(replies, "You`ve learned HeavensWrath!");
        AssertOption(replies, "Okay.");
        AssertAvatar(replies, AlreadyLearnedAvatarId);
        Assert.Contains("sourceRange=" + HeavensWrathSourceRange, log.ToString());
        Assert.Contains("spellId=15710; already learned", log.ToString());
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
        long classCode = 9_005,
        long firstClass = 9_005,
        long secondClass = 9_005,
        long reborn = 2,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "thunderstriker-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "ThunderPure",
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

    private static void AssertPersistedSpells(string spellsRoot, IReadOnlyList<uint> expectedSpellIds)
    {
        Assert.Equal(expectedSpellIds, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
    }

    private static void AssertReply(IReadOnlyList<LegacyNpcReplyPacket> replies, string text)
    {
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog && reply.Text == text);
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
                reply.Text == text);
    }

    private static void AssertAvatar(IReadOnlyList<LegacyNpcReplyPacket> replies, ushort avatarId)
    {
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == avatarId);
    }
}
