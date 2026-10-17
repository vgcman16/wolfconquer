using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySkillEraserNpcTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacySkillEraserNpcTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RootDialogIsSourceBackedEnglishTranslation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        var handled = await WorldPacketHandlers.TryHandleLegacySkillEraserNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId: 895,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(5, sent.Count);
        var text = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("forget skills you don't need anymore", text.Text);
        Assert.DoesNotContain("roder", text.Text);
        var forget = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal("Forget skills in this life.", forget.Text);
        Assert.Equal(1, forget.OptionId);
        var close = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Contains("source=NpcHandler.SkillEraser", log.ToString());
    }

    [Fact]
    public async Task LearnedSkillRemovalUpdatesSessionAndPersistsSpells()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "PlayersSpells");
        Directory.CreateDirectory(spellsRoot);
        var learned = new[]
        {
            Entry(1_000, level: 2, experience: 55),
            Entry(1_046, level: 3),
        };
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(1_293_600, new PlayerSpellSnapshot(learned));
        var session = CreateSession(new PlayerSpellSnapshot(learned));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySkillEraserNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            npcId: 895,
            optionId: 12,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([1_046U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([1_046U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(1_293_600)
            .SpellIds
            .ToArray());

        AssertSpellRemovalAction(sent[0], 1_293_600, 1_000);
        var text = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("All done, the skill is now removed.", text.Text);
        Assert.Contains("spellId=1000", log.ToString());
        Assert.Contains("source=NpcHandler.SkillEraser", log.ToString());
        Assert.Contains("persisted=true", log.ToString());
    }

    [Fact]
    public async Task MissingSkillSendsLegacyNotLearnedDialogWithoutChangingPersistence()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "PlayersSpellsMissing");
        Directory.CreateDirectory(spellsRoot);
        var learned = new[] { Entry(1_046, level: 3) };
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(1_293_600, new PlayerSpellSnapshot(learned));
        var session = CreateSession(new PlayerSpellSnapshot(learned));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySkillEraserNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            npcId: 895,
            optionId: 12,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([1_046U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([1_046U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(1_293_600)
            .SpellIds
            .ToArray());

        var text = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Sorry, you haven't learned the skill Thunder. How can you forget it?", text.Text);
    }

    [Fact]
    public async Task LearnedSkillRemovalWithoutPersistenceRootUpdatesRuntimeAndSendsSourcePacket()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(new PlayerSpellSnapshot(
            [Entry(1_000, level: 2)]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySkillEraserNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 895,
            optionId: 12,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Empty(updated.PlayerSpells.SpellIds);

        AssertSpellRemovalAction(sent[0], 1_293_600, 1_000);
        var text = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("All done, the skill is now removed.", text.Text);
        Assert.Contains("persisted=false", log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "skill-eraser-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "SkillEraserTest",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 1_005,
                FirstClass: 1_005,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 380,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 380,
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
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: null);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PlayerSpellSnapshotEntry Entry(
        ushort id,
        ushort level,
        int experience = 0,
        byte previousLevel = 0,
        byte soulLevel = 0,
        byte useSpellSoul = 0)
    {
        return new PlayerSpellSnapshotEntry(id, level, experience, previousLevel, soulLevel, useSpellSoul);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static void AssertSpellRemovalAction(byte[] bytes, uint characterUid, ushort spellId)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal((MsgActionType)109, packet.Type);
        Assert.Equal(characterUid, packet.ObjId);
        Assert.Equal((uint)spellId, packet.DwParam);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
