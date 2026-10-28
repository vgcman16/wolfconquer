using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTrojanNewSkillTests : IDisposable
{
    private const long CharacterUid = 1_293_600;
    private const uint TrojanNpcId = 10_022;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTrojanNewSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, ushort, string, string> NewSkillOptions => new()
    {
        { 251, 15_950, "MonsterHunter", "NpcHandler.cs:48101-48139" },
        { 252, 15_960, "UndyingWill", "NpcHandler.cs:48140-48178" },
    };

    [Theory]
    [MemberData(nameof(NewSkillOptions))]
    public async Task TrojanNewSkillOptionLearnsSpellAndPersists(
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
            TrojanNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(new[] { (uint)spellId }, updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(new[] { (uint)spellId }, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());

        Assert.Equal([spellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "Congratulation! You just have learned the skill " + spellName);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionTrojan skill", logText);
        Assert.Contains("source=NpcHandler.PromotionTrojan", logText);
        Assert.Contains("sourceRange=" + sourceRange, logText);
        Assert.Contains("option=" + optionId, logText);
        Assert.Contains("spellId=" + spellId, logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task TrojanNewSkillOptionAlreadyLearnedDoesNotDuplicate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "Duplicate");
        Directory.CreateDirectory(spellsRoot);
        var learned = new PlayerSpellSnapshot([Entry(15_950)]);
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
            TrojanNpcId,
            optionId: 251,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Equal(new uint[] { 15_950 }, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned MonsterHunter!");
        Assert.Contains("sourceRange=NpcHandler.cs:48101-48139", log.ToString());
        Assert.Contains("spellId=15950; already learned", log.ToString());
    }

    [Fact]
    public async Task TrojanNewSkillOptionRejectsBeforeLevelGate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "LevelGate");
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
            TrojanNpcId,
            optionId: 252,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "You`re not strong enough to learn this skill. Keep practicing, and come back when you`re qualified.");

        var logText = log.ToString();
        Assert.Contains("sourceRange=NpcHandler.cs:48140-48178", logText);
        Assert.Contains("option=252", logText);
        Assert.Contains("level=99 requiredLevel=100; source level gate rejected", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(long level, PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "promotion-master-new-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "TrojanNewSkillHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 1_002,
                FirstClass: 1_002,
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
}
