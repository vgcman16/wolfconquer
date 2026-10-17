using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersNinjaPoisonStarTests : IDisposable
{
    private const uint NinjaPromotionNpcId = 4_972;
    private const byte PoisonStarOption = 4;
    private const ushort PoisonStarSpellId = 6_002;
    private const long CharacterUid = 1_293_670;
    private const ushort NinjaAvatarId = 203;
    private const string PoisonStarSourceRange = "NpcHandler.cs:44937-44999";
    private const string LineageGateText =
        "Only reborn Ninja-ninja, who have spent all 2 lives as Ninja, can learn the that Skill, PoisonStar.";
    private const string PromotionGateText =
        "Sorry. I can`t teach you this skill until you reach level 70 and get promoted.";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersNinjaPoisonStarTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PoisonStarOptionRejectsNonRebornNinja()
    {
        var result = await RunPoisonStarAsync(CreatePoisonStarSession(reborn: 0));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), LineageGateText, "Ok~I~see.");
        Assert.Contains("sourceRange=" + PoisonStarSourceRange, result.Log);
        Assert.Contains("source reborn gate rejected", result.Log);
    }

    [Theory]
    [InlineData(1, 4_002, 0)]
    [InlineData(2, 5_003, 4_002)]
    public async Task PoisonStarOptionRejectsNonNinjaSourceLife(long reborn, long firstClass, long secondClass)
    {
        var result = await RunPoisonStarAsync(CreatePoisonStarSession(
            reborn: reborn,
            firstClass: firstClass,
            secondClass: secondClass));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), LineageGateText, "Ok~I~see.");
        Assert.Contains("sourceRange=" + PoisonStarSourceRange, result.Log);
        Assert.Contains("source PoisonStar lineage gate rejected", result.Log);
    }

    [Theory]
    [InlineData(5_003, 69)]
    [InlineData(5_002, 70)]
    public async Task PoisonStarOptionRejectsBeforeLevelSeventyAndPromotion(long classCode, long level)
    {
        var result = await RunPoisonStarAsync(CreatePoisonStarSession(classCode: classCode, level: level));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), PromotionGateText, "Okay.~I~see..");
        Assert.Contains("sourceRange=" + PoisonStarSourceRange, result.Log);
        Assert.Contains("source promotion gate rejected", result.Log);
    }

    [Fact]
    public async Task PoisonStarOptionAlreadyLearnedDoesNotDuplicate()
    {
        var learned = new PlayerSpellSnapshot([Entry(PoisonStarSpellId)]);
        var result = await RunPoisonStarAsync(
            CreatePoisonStarSession(playerSpells: learned),
            persistedSpells: learned);

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, [PoisonStarSpellId]);
        AssertDialog(ParseNpcReplies(result.Sent), "You`ve learned PoisonStar!", "Okay.");
        Assert.Contains("sourceRange=" + PoisonStarSourceRange, result.Log);
        Assert.Contains("spellId=6002; already learned", result.Log);
    }

    [Fact]
    public async Task PoisonStarOptionLearnsSpellSendsPacketAndPersists()
    {
        var result = await RunPoisonStarAsync(CreatePoisonStarSession());

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([PoisonStarSpellId], updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(result.SpellsRoot, [PoisonStarSpellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(result.Sent));
        Assert.Equal(PoisonStarSpellId, spellPacket.Id);
        AssertDialog(
            ParseNpcReplies(result.Sent),
            "Congratulation! You just have learned the skill PoisonStar",
            "Ok , thank you.");

        Assert.Contains("WorldServer handled LegacyNpc PromotionNinja PoisonStar skill", result.Log);
        Assert.Contains("source=NpcHandler.PromotionNinja", result.Log);
        Assert.Contains("sourceRange=" + PoisonStarSourceRange, result.Log);
        Assert.Contains("option=4", result.Log);
        Assert.Contains("spellId=6002", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<RunResult> RunPoisonStarAsync(
        WorldSession session,
        PlayerSpellSnapshot? persistedSpells = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "PoisonStar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(spellsRoot);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(
            CharacterUid,
            persistedSpells ?? PlayerSpellSnapshot.Empty);
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
            PoisonStarOption,
            CancellationToken.None);

        return new RunResult(handled, sent, spellsRoot, log.ToString(), sessions, connection);
    }

    private static WorldSession CreatePoisonStarSession(
        long classCode = 5_003,
        long firstClass = 5_003,
        long secondClass = 0,
        long reborn = 1,
        long level = 70,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "ninja-poison-star-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PoisonNinja",
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

    private sealed record RunResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string SpellsRoot,
        string Log,
        WorldSessionStore Sessions,
        ConquerTcpConnection Connection);
}
