using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTaoistExtraSkillTests : IDisposable
{
    private const uint TaoistPromotionNpcId = 10_000;
    private const long CharacterUid = 1_293_720;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTaoistExtraSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> ExtraTaoistSkills => new()
    {
        { 25, 1_150, "FireRing", "NpcHandler.cs:42286-42324" },
        { 26, 1_180, "FireMeteor", "NpcHandler.cs:42247-42285" },
        { 27, 1_120, "FireCircle", "NpcHandler.cs:42208-42246" },
        { 28, 1_160, "Bomb", "NpcHandler.cs:42169-42207" },
        { 58, 1_165, "FireofHell", "NpcHandler.cs:42036-42074" },
        { 70, 10_309, "ChainBolt", "NpcHandler.cs:42127-42140" },
        { 71, 1_010, "Lightning", "NpcHandler.cs:42141-42168" },
    };

    [Theory]
    [MemberData(nameof(ExtraTaoistSkills))]
    public async Task TaoistExtraSkillOptionLearnsSpellAndPersists(
        byte optionId,
        ushort spellId,
        string spellName,
        string sourceRange)
    {
        var result = await InvokeTaoistPromotionAsync(optionId, level: 100);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([(uint)spellId], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([(uint)spellId], LoadPersistedSpellIds(result.SpellsRoot));

        var spellPacket = Assert.Single(ParseSpellPackets(result.Sent));
        Assert.Equal(spellId, spellPacket.Id);
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        var logText = result.Log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("source=NpcHandler.PromotionTaoist", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [InlineData(70, 10_309, "NpcHandler.cs:42127-42140")]
    [InlineData(71, 1_010, "NpcHandler.cs:42141-42168")]
    public async Task TaoistSourceAliasOptionsLearnAtLevelOneWithAliasSourceRange(
        byte optionId,
        ushort spellId,
        string sourceRange)
    {
        var result = await InvokeTaoistPromotionAsync(optionId, level: 1);

        Assert.True(result.Handled);
        Assert.Equal([(uint)spellId], LoadPersistedSpellIds(result.SpellsRoot));
        Assert.Equal([spellId], ParseSpellPackets(result.Sent).Select(packet => packet.Id).ToArray());

        var logText = result.Log.ToString();
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.DoesNotContain("source level gate rejected", logText);
    }

    [Fact]
    public async Task TaoistExtraSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        var learned = new PlayerSpellSnapshot([Entry(1_160)]);
        var result = await InvokeTaoistPromotionAsync(
            optionId: 28,
            level: 100,
            playerSpells: learned,
            caseName: "duplicate-bomb");

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.Equal([1_160U], LoadPersistedSpellIds(result.SpellsRoot));
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == "You`ve learned Bomb!");

        var logText = result.Log.ToString();
        Assert.Contains("sourceRange=NpcHandler.cs:42169-42207", logText);
        Assert.Contains("option=28", logText);
        Assert.Contains("spellId=1160; already learned", logText);
    }

    [Fact]
    public async Task TaoistExtraSkillOptionRejectsBelowSourceLevelGate()
    {
        var result = await InvokeTaoistPromotionAsync(
            optionId: 58,
            level: 92,
            caseName: "level-gate-fire-of-hell");

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.Empty(LoadPersistedSpellIds(result.SpellsRoot));
        Assert.Contains(
            ParseNpcReplies(result.Sent),
            reply => reply.Text == "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.");

        var logText = result.Log.ToString();
        Assert.Contains("sourceRange=NpcHandler.cs:42036-42074", logText);
        Assert.Contains("option=58", logText);
        Assert.Contains("spellId=1165 level=92 requiredLevel=93; source level gate rejected", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<TaoistPromotionInvokeResult> InvokeTaoistPromotionAsync(
        byte optionId,
        long level,
        PlayerSpellSnapshot? playerSpells = null,
        string? caseName = null)
    {
        var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, caseName ?? "option-" + optionId + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(spellsRoot);

        var initialSpells = playerSpells ?? PlayerSpellSnapshot.Empty;
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, initialSpells);

        var session = CreateFireTaoistSession(level, initialSpells);
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
            TaoistPromotionNpcId,
            optionId,
            CancellationToken.None);

        return new TaoistPromotionInvokeResult(handled, sent, connection, sessions, spellsRoot, log);
    }

    private static WorldSession CreateFireTaoistSession(long level, PlayerSpellSnapshot playerSpells)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "taoist-extra-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "FireTaoistHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 14_002,
                FirstClass: 14_002,
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

    private uint[] LoadPersistedSpellIds(string spellsRoot)
    {
        return new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray();
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

    private sealed record TaoistPromotionInvokeResult(
        bool Handled,
        List<byte[]> Sent,
        ConquerTcpConnection Connection,
        WorldSessionStore Sessions,
        string SpellsRoot,
        StringWriter Log);
}
