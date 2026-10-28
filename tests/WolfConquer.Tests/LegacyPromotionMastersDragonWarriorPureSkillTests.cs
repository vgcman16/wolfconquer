using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersDragonWarriorPureSkillTests : IDisposable
{
    private const uint DragonWarriorPromotionNpcId = 17_126;
    private const ushort DragonFurySpellId = 12_300;
    private const long CharacterUid = 1_293_740;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersDragonWarriorPureSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DragonFuryOptionLearnsSpellAndPersists()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Success");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePureDragonWarriorSession();
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
            DragonWarriorPromotionNpcId,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([DragonFurySpellId], updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(spellsRoot, [DragonFurySpellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(sent));
        Assert.Equal(DragonFurySpellId, spellPacket.Id);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill DragonFury");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == "Ok , thank you.");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 259);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionLeeLong pure skill", logText);
        Assert.Contains("source=NpcHandler.PromotionLeeLong", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:49920-49939", logText);
        Assert.Contains("option=31", logText);
        Assert.Contains("spellId=12300", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task DragonFuryOptionRejectsBeforeSecondRebirth()
    {
        var result = await RunDragonFuryGateAsync(CreatePureDragonWarriorSession(reborn: 1));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.Text == "To learn [Dragon Fury], you should be Dragon-Warrior for three lives in a row and reach at least Level 70.");
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == "OK");
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 259);
        Assert.Contains("sourceRange=NpcHandler.cs:49920-49939", result.Log);
        Assert.Contains("source reborn gate rejected", result.Log);
    }

    [Fact]
    public async Task DragonFuryOptionRejectsNonPureDragonWarriorCombo()
    {
        var result = await RunDragonFuryGateAsync(CreatePureDragonWarriorSession(firstClass: 7_005));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.Text == "To learn [Dragon Fury], you should be Dragon-Warrior for three lives in a row and reach at least Level 70.");
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == "OK");
        Assert.Contains(ParseNpcReplies(result.Sent), reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 259);
        Assert.Contains("sourceRange=NpcHandler.cs:49920-49939", result.Log);
        Assert.Contains("source pure Dragon-Warrior combo gate rejected", result.Log);
    }

    [Fact]
    public async Task DragonFuryOptionHasNoLevelOrAdvancedClassGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "BaseClassLevelOne");
        Directory.CreateDirectory(spellsRoot);
        var session = CreatePureDragonWarriorSession(
            classCode: 8_000,
            firstClass: 8_000,
            secondClass: 8_001,
            level: 1);
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
            DragonWarriorPromotionNpcId,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal([DragonFurySpellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        AssertPersistedSpells(spellsRoot, [DragonFurySpellId]);
        Assert.DoesNotContain("source level gate rejected", log.ToString());
        Assert.DoesNotContain("source promotion gate rejected", log.ToString());
    }

    [Fact]
    public async Task DragonFuryOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "AlreadyLearned");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(DragonFurySpellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreatePureDragonWarriorSession(playerSpells: learned);
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
            DragonWarriorPromotionNpcId,
            optionId: 31,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        AssertPersistedSpells(spellsRoot, [DragonFurySpellId]);

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "You`ve learned DragonFury!");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == "Okay.");
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 5);
        Assert.Contains("sourceRange=NpcHandler.cs:49920-49939", log.ToString());
        Assert.Contains("spellId=12300; already learned", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<GateResult> RunDragonFuryGateAsync(WorldSession session)
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
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            DragonWarriorPromotionNpcId,
            optionId: 31,
            CancellationToken.None);

        return new GateResult(handled, sent, spellsRoot, log.ToString());
    }

    private static WorldSession CreatePureDragonWarriorSession(
        long classCode = 8_005,
        long firstClass = 8_001,
        long secondClass = 8_005,
        long reborn = 2,
        long level = 70,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "dragon-warrior-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PureDragon",
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

    private sealed record GateResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string SpellsRoot,
        string Log);
}
