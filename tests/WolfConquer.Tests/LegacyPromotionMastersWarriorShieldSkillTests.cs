using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersWarriorShieldSkillTests : IDisposable
{
    private const uint WarriorPromotionNpcId = 10_001;
    private const long CharacterUid = 1_293_600;
    private const byte ShieldSkillDialogOption = 4;
    private const ushort DashSpellId = 1_051;
    private const ushort ShieldBlockSpellId = 10_470;
    private const ushort MagicDefenderSpellId = 11_200;
    private const ushort DefensiveStanceSpellId = 11_160;
    private const string ShieldSkillDialogSourceRange = "NpcHandler.cs:45853-45872";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersWarriorShieldSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> ShieldSkillOptions => new()
    {
        { 5, DashSpellId, "Dash", "NpcHandler.cs:45873-45911" },
        { 6, ShieldBlockSpellId, "ShieldBlock", "NpcHandler.cs:45912-45950" },
        { 7, MagicDefenderSpellId, "MagicDefender", "NpcHandler.cs:45951-45989" },
        { 8, DefensiveStanceSpellId, "DefensiveStance", "NpcHandler.cs:45990-46028" },
    };

    [Fact]
    public async Task WarriorShieldSkillDialogShowsSourceOptionsWithLearnedLabels()
    {
        var result = await HandleShieldSkillAsync(
            optionId: ShieldSkillDialogOption,
            level: 100,
            playerSpells: CreateSpells(DashSpellId, MagicDefenderSpellId));

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Which skill do you want to learn?");
        AssertOption(replies, "Dash(Learnt)", 5);
        AssertOption(replies, "Shield~Block(Lv.40)", 6);
        AssertOption(replies, "Magic~Defender(Learnt)", 7);
        AssertOption(replies, "Defensive~Stance(Lv.70)", 8);
        AssertOption(replies, "Let~me~think~it~over.", byte.MaxValue);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 8);

        Assert.Contains("WorldServer handled LegacyNpc PromotionWarrior shield skill dialog", result.LogText);
        Assert.Contains("source=NpcHandler.PromotionWarrior", result.LogText);
        Assert.Contains("sourceRange=" + ShieldSkillDialogSourceRange, result.LogText);
        Assert.DoesNotContain("PARITY-GAP", result.LogText);
    }

    [Fact]
    public async Task WarriorShieldSkillOptionRejectsBelowSourceLevelGate()
    {
        var result = await HandleShieldSkillAsync(optionId: 7, level: 99);

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.Empty(result.PersistedSpells.SpellIds);

        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified.");
        Assert.Contains("sourceRange=NpcHandler.cs:45951-45989", result.LogText);
        Assert.Contains("option=7", result.LogText);
        Assert.Contains("spellId=11200 level=99 requiredLevel=100; source level gate rejected", result.LogText);
    }

    [Theory]
    [MemberData(nameof(ShieldSkillOptions))]
    public async Task WarriorShieldSkillOptionLearnsSpellAndPersists(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        var result = await HandleShieldSkillAsync(optionId, level: 100);

        Assert.True(result.Handled);
        Assert.NotNull(result.UpdatedSession);
        Assert.Equal([spellId], result.UpdatedSession.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([spellId], result.PersistedSpells.SpellIds.ToArray());
        Assert.Equal([spellId], ParseSpellPackets(result.Sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", result.LogText);
        Assert.Contains("source=NpcHandler.PromotionWarrior", result.LogText);
        Assert.Contains("sourceRange=" + sourceRange, result.LogText);
        Assert.Contains("option=" + optionId, result.LogText);
        Assert.Contains("spellId=" + spellId, result.LogText);
        Assert.DoesNotContain("PARITY-GAP", result.LogText);
    }

    [Fact]
    public async Task WarriorShieldSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        var learned = CreateSpells(DefensiveStanceSpellId);
        var result = await HandleShieldSkillAsync(
            optionId: 8,
            level: 100,
            playerSpells: learned);

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.Equal([DefensiveStanceSpellId], result.PersistedSpells.SpellIds.ToArray());
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == "You`ve learned DefensiveStance!");
        Assert.Contains("sourceRange=NpcHandler.cs:45990-46028", result.LogText);
        Assert.Contains("spellId=11160; already learned", result.LogText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<HandleResult> HandleShieldSkillAsync(
        byte optionId,
        long level,
        PlayerSpellSnapshot? playerSpells = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(spellsRoot);
        var initialSpells = playerSpells ?? PlayerSpellSnapshot.Empty;
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, initialSpells);

        var session = CreateSession(level, initialSpells);
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
            WarriorPromotionNpcId,
            optionId,
            CancellationToken.None);

        sessions.TryGet(connection, out var updatedSession);
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(CharacterUid);
        return new HandleResult(handled, sent, log.ToString(), updatedSession, persistedSpells);
    }

    private static PlayerSpellSnapshot CreateSpells(params ushort[] spellIds)
    {
        return new PlayerSpellSnapshot(spellIds.Select(static spellId => new PlayerSpellSnapshotEntry(
                spellId,
                Level: 0,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0))
            .ToArray());
    }

    private static WorldSession CreateSession(long level, PlayerSpellSnapshot playerSpells)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "warrior-shield-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "WarriorShieldHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 2_002,
                FirstClass: 2_002,
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
            PlayerSpells: playerSpells,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
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

    private sealed record HandleResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string LogText,
        WorldSession? UpdatedSession,
        PlayerSpellSnapshot PersistedSpells);
}
