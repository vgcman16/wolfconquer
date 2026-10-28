using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTrojanWeaponSkillTests : IDisposable
{
    private const long CharacterUid = 1_293_600;
    private const ushort FastBladeSpellId = 1_045;
    private const ushort ScentSwordSpellId = 1_046;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTrojanWeaponSkillTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TrojanWeaponSkillOptionLearnsFastBladerThenScrenSwordWhenMissing()
    {
        var result = await HandleWeaponSkillAsync(level: 100);

        Assert.True(result.Handled);
        Assert.NotNull(result.UpdatedSession);
        Assert.Equal([FastBladeSpellId, ScentSwordSpellId], result.UpdatedSession.PlayerSpells.SpellIds);
        Assert.Equal([FastBladeSpellId, ScentSwordSpellId], result.PersistedSpells.SpellIds);
        Assert.Equal([FastBladeSpellId, ScentSwordSpellId], ParseSpellPackets(result.Sent).Select(packet => packet.Id));

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill FastBlader");
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill ScrenSword");
        Assert.Contains("sourceRange=NpcHandler.cs:48557-48632", result.LogText);
        Assert.DoesNotContain("PARITY-GAP", result.LogText);
    }

    [Fact]
    public async Task TrojanWeaponSkillOptionStopsWhenFastBladerAlreadyLearned()
    {
        var learned = CreateSpells(FastBladeSpellId);
        var result = await HandleWeaponSkillAsync(level: 100, playerSpells: learned);

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.Equal([FastBladeSpellId], result.PersistedSpells.SpellIds);

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "You`ve learned FastBlader!");
        Assert.DoesNotContain(replies, reply => reply.Text?.Contains("ScrenSword", StringComparison.Ordinal) == true);
        Assert.Contains("spellId=1045; already learned", result.LogText);
    }

    [Fact]
    public async Task TrojanWeaponSkillOptionStopsWhenScrenSwordAlreadyLearnedAfterAddingFastBlader()
    {
        var learned = CreateSpells(ScentSwordSpellId);
        var result = await HandleWeaponSkillAsync(level: 100, playerSpells: learned);

        Assert.True(result.Handled);
        Assert.NotNull(result.UpdatedSession);
        Assert.Equal([ScentSwordSpellId, FastBladeSpellId], result.UpdatedSession.PlayerSpells.SpellIds);
        Assert.Equal([ScentSwordSpellId, FastBladeSpellId], result.PersistedSpells.SpellIds);
        Assert.Equal([FastBladeSpellId], ParseSpellPackets(result.Sent).Select(packet => packet.Id));

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill FastBlader");
        Assert.Contains(replies, reply => reply.Text == "You`ve learned ScrenSword!");
        Assert.Contains("spellId=1046; already learned", result.LogText);
    }

    [Fact]
    public async Task TrojanWeaponSkillOptionLearnsForPromotedLowLevelTrojan()
    {
        var result = await HandleWeaponSkillAsync(level: 15, classCode: 1_001);

        Assert.True(result.Handled);
        Assert.NotNull(result.UpdatedSession);
        Assert.Equal([FastBladeSpellId, ScentSwordSpellId], result.UpdatedSession.PlayerSpells.SpellIds);
        Assert.Equal([FastBladeSpellId, ScentSwordSpellId], result.PersistedSpells.SpellIds);
        Assert.Equal([FastBladeSpellId, ScentSwordSpellId], ParseSpellPackets(result.Sent).Select(packet => packet.Id));

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill FastBlader");
        Assert.Contains(replies, reply => reply.Text == "Congratulation! You just have learned the skill ScrenSword");
        Assert.Contains("sourceRange=NpcHandler.cs:48557-48632", result.LogText);
        Assert.DoesNotContain("requiredLevel", result.LogText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<HandleResult> HandleWeaponSkillAsync(
        long level,
        PlayerSpellSnapshot? playerSpells = null,
        long classCode = 1_002)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var initialSpells = playerSpells ?? PlayerSpellSnapshot.Empty;
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, initialSpells);

        var session = CreateSession(level, initialSpells, classCode);
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
            npcId: 10_022,
            optionId: 15,
            CancellationToken.None);

        sessions.TryGet(connection, out var updatedSession);
        var persistedSpells = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(CharacterUid);
        return new HandleResult(handled, sent, log.ToString(), updatedSession, persistedSpells);
    }

    private static PlayerSpellSnapshot CreateSpells(params ushort[] spellIds)
    {
        return new PlayerSpellSnapshot(spellIds
            .Select(static spellId => PlayerSpellSnapshotLoaderTests.Entry(spellId, level: 0))
            .ToArray());
    }

    private static WorldSession CreateSession(long level, PlayerSpellSnapshot playerSpells, long classCode)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "promotion-master-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PromotionHero",
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
            PlayerSpells: playerSpells,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
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
