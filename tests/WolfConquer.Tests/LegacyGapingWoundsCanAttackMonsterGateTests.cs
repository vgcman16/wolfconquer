using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerGapingWoundsCanAttackMonsterGuardInPeaceModeSpendsResourcesThenRejectsBeforeDamage()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name,
            boundX: 834,
            boundY: 584);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            sobNpcs: [],
            magicRows: [CreateGapingWoundsRow(coldTime: 0)]);
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "1",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                guard.Uid,
                guard.X,
                guard.Y)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        AssertGapingWoundsAnimationHeader(spell, guard.Uid, guard.X, guard.Y);
        Assert.Empty(spell.Targets);
        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleGuard));
        Assert.Equal(guard.MaxLife, visibleGuard.Hitpoints);
        Assert.Empty(ParseCruelShadeSpellPackets(sent));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("source CanAttackMonster.Verified rejected Guard monster family because pkMode=1", output);
        Assert.Contains("WorldServer handled legacy GapingWounds magic", output);
        Assert.Contains("targets=0", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    [Fact]
    public async Task HandlerGapingWoundsCanAttackMonsterGuardInPkModeAppliesLegacyFlashingNameThenDamagesGuard()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name,
            boundX: 834,
            boundY: 584);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            sobNpcs: [],
            magicRows: [CreateGapingWoundsRow(coldTime: 0)]);
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                guard.Uid,
                guard.X,
                guard.Y)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        AssertGapingWoundsAnimationHeader(spell, guard.Uid, guard.X, guard.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(guard.Uid, target.Uid);
        Assert.Equal(ExpectedGapingWoundsMonsterDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleGuard));
        Assert.Equal(guard.MaxLife - ExpectedGapingWoundsMonsterDamage, visibleGuard.Hitpoints);
        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy GapingWounds magic", output);
        Assert.Contains("targetKind=monster", output);
        Assert.Contains("source=DecLife.cs", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }
}
