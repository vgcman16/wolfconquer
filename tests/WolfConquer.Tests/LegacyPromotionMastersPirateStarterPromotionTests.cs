using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersPirateStarterPromotionTests : IDisposable
{
    private const uint PiratePromotionNpcId = 9_391;
    private const long CharacterUid = 1_293_701;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersPirateStarterPromotionTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option3MutatesClassAndLearnsAndPersistsStarterSpellsWhenEligible()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users");
        var spellsRoot = Path.Combine(_root, "PlayersSpells");
        var session = CreateSession(classCode: 7_000, level: 15, usersRoot: usersRoot);
        WriteCompleteCharacterSnapshot(session);
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
            optionId: 3,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal([11_050, 11_110], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "You`ve been promoted!");
        Assert.Contains(replies, reply => reply.Text == "Great,~thanks!" && reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 234);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(7_001, updated.Character.Class);
        Assert.Equal([11_050U, 11_110U], updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal([11_050U, 11_110U], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=7001", persistedCharacter, StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionPirate starter promotion", logText);
        Assert.Contains("source=NpcHandler.PromotionPirate", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:51587-51643", logText);
        Assert.Contains("option=3", logText);
        Assert.Contains("spellIds=11050,11110", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [InlineData(14, 7_000)]
    [InlineData(15, 7_001)]
    public async Task Option3SendsSourceRejectionAndDoesNotMutateWhenStarterIsNotEligible(long level, long classCode)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users-" + level + "-" + classCode);
        var spellsRoot = Path.Combine(_root, "PlayersSpells-" + level + "-" + classCode);
        var session = CreateSession(classCode: classCode, level: level, usersRoot: usersRoot);
        WriteCompleteCharacterSnapshot(session);
        var characterBefore = File.ReadAllBytes(Path.Combine(usersRoot, CharacterUid + ".ini"));
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
            optionId: 3,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Sorry, you need level 15.");
        Assert.Contains(replies, reply => reply.Text == "Ah ok." && reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 234);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.Equal(PlayerSpellSnapshotLoader.ToLegacyBytes(PlayerSpellSnapshot.Empty),
            File.ReadAllBytes(Path.Combine(spellsRoot, CharacterUid + ".bin"))); // The genuine owner seeded this exact empty baseline.

        var persistedCharacter = File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini"));
        Assert.Contains("Class=" + classCode, persistedCharacter, StringComparison.Ordinal);
        Assert.Equal(characterBefore, File.ReadAllBytes(Path.Combine(usersRoot, CharacterUid + ".ini")));

        var logText = log.ToString();
        Assert.Contains("WorldServer skipped LegacyNpc PromotionPirate starter promotion", logText);
        Assert.Contains("source=NpcHandler.PromotionPirate", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:51587-51643", logText);
        Assert.Contains("option=3", logText);
        Assert.Contains("level=" + level + " class=" + classCode, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long classCode,
        long level,
        string? usersRoot = null,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: CharacterUid,
            Username: "pirate-starter-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PiratePromotionHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
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
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static void WriteCompleteCharacterSnapshot(WorldSession session)
    {
        var usersRoot = session.UsersRoot ?? throw new InvalidOperationException("Explicit saved fixture root is required.");
        Directory.CreateDirectory(usersRoot);
        var fields = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(session.Character)) fields[pair.Key] = pair.Value;
        fields["Name"] = session.Character.Name;
        File.WriteAllText(Path.Combine(usersRoot, session.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini"),
            "[Character]" + Environment.NewLine + string.Join(Environment.NewLine, fields.Select(pair => pair.Key + "=" + pair.Value)) + Environment.NewLine);
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
}
