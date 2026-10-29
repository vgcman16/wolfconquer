using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public Task HandlerAttachStatusZeroOpponentPreservesResourcesAndEmitsNoSuccess() =>
        AssertClassicInvalidTargetRefusesAsync(0, useCasterCoordinates: true);

    [Fact]
    public Task HandlerAttachStatusMissingVisibleTargetPreservesResourcesAndProgress() =>
        AssertClassicInvalidTargetRefusesAsync(TargetPlayerUid, useCasterCoordinates: false);

    private static async Task AssertClassicInvalidTargetRefusesAsync(uint targetUid, bool useCasterCoordinates)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                MagicShieldSpellId,
                sort: 6,
                useMana: 10,
                useStamina: 5,
                duration: 60,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [MagicShieldSpellId]);
        sessions.Set(connection, session);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
            sessions, connection, PlayerUid, out var identity));
        var attributes = session.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        var spells = session.PlayerSpells.Spells.ToArray();
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, LegacyMagicShieldFlag));
        Assert.Null(session.MagicCooldowns);
        Assert.Equal((ushort)0, session.LegacyRandomSpell);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: MagicShieldSpellId,
                x: useCasterCoordinates ? checked((ushort)session.Character.X) : (ushort?)null,
                y: useCasterCoordinates ? checked((ushort)session.Character.Y) : (ushort?)null,
                opponentUid: targetUid)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.Same(session, updated);
        Assert.True(LegacyNativeClientPublication.TryCurrent(
            sessions, connection, PlayerUid, identity, out var current));
        Assert.Same(session, current);
        Assert.Equal(500L, updated!.Character.MinMana);
        Assert.Equal("100", updated.Character.Attributes["Stamina"]);
        Assert.Equal(attributes, updated.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
        Assert.Null(updated.MagicCooldowns);
        Assert.Equal((ushort)0, updated.LegacyRandomSpell);
        Assert.Equal(spells, updated.PlayerSpells.Spells.ToArray());
        Assert.Equal(0, Assert.Single(updated.PlayerSpells.Spells, row => row.Id == MagicShieldSpellId).Experience);
        Assert.False(HasLegacyStatusFlag(updated.Character.Attributes, LegacyMagicShieldFlag));
        Assert.False(monsterStates.TryGetTimedStatusFlag(
            monster, LegacyMagicShieldFlag, DateTimeOffset.UtcNow, out _));
        Assert.Empty(sent);
        Assert.Contains("spellId=1090: classicTargetUnavailable", log.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("WorldServer handled legacy classic AttachStatus buff", log.ToString(), StringComparison.Ordinal);
    }
}
