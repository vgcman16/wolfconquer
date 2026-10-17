using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort RoamerWildwindSpellId = 7_040;

    [Fact]
    public async Task HandlerRoamerWildwindSourceSilentNoOpDoesNotEmitUnsupportedGap()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateRoamerWildwindRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [RoamerWildwindSpellId],
            legacyRandomSpell: RoamerWildwindSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var handled = await InvokeLegacyPlayerMonsterMagicAttackAsync(
            context,
            new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: RoamerWildwindSpellId)));

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.True(updatedSession.MagicCooldowns is null || !updatedSession.MagicCooldowns.ContainsKey(RoamerWildwindSpellId));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped source-silent legacy player magic", output);
        Assert.Contains("spellId=7040", output);
        Assert.Contains("sort=90", output);
        Assert.Contains("status=sourceSilentNoOp", output);
        Assert.Contains("has no case for this spell ID", output);
        Assert.DoesNotContain("PARITY-GAP", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
    }

    private static MagicType CreateRoamerWildwindRow()
    {
        return CreateMagicRow(
            RoamerWildwindSpellId,
            sort: 90,
            useMana: 0,
            damage: 30_500,
            maxTargets: 2,
            range: 8,
            needLevel: 15,
            weaponType: 440_614,
            useStamina: 0,
            coolDown: 0,
            coldTime: 0,
            duration: 5,
            damageOnHuman: 30_110,
            damage2: 20_000,
            damage3: 5_000);
    }

    private static async ValueTask<bool> InvokeLegacyPlayerMonsterMagicAttackAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerMonsterMagicAttackAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(null, [context, packet, CancellationToken.None, false]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }
}
