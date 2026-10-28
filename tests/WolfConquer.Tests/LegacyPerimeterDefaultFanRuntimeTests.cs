using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte PerimeterDefaultFanSort = 78;

    private static readonly HashSet<ushort> ExpectedPerimeterDefaultFanSpellIds =
    [
        12_210,
        12_220,
        12_580,
        12_590,
        12_740,
        12_750,
    ];

    [Fact]
    public void PerimeterDefaultFanIdGateAllowsExactlyTheSixDecompiledDefaultBranchIds()
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "IsLegacyPerimeterDefaultFanSpell",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        for (var candidate = 0; candidate <= ushort.MaxValue; candidate++)
        {
            var spellId = (ushort)candidate;
            var supported = Assert.IsType<bool>(method.Invoke(null, [spellId]));
            Assert.True(
                supported == ExpectedPerimeterDefaultFanSpellIds.Contains(spellId),
                $"Unexpected Perimeter default Fan gate result for spell ID {spellId}.");
        }
    }

    [Theory]
    [InlineData(12_210)]
    [InlineData(12_220)]
    [InlineData(12_580)]
    [InlineData(12_590)]
    [InlineData(12_740)]
    [InlineData(12_750)]
    public async Task HandlerPerimeterDefaultFanRejectsEveryExactIdUnderAnotherSortWithoutSpendingResources(
        ushort spellId)
    {
        var row = CreatePerimeterDefaultFanRow(
            spellId,
            sort: PerimeterDefaultFanSort - 1,
            useMana: 7,
            useStamina: 5,
            coolDown: 800,
            coldTime: 300);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var caster = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        sessions.Set(connection, caster);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolvePerimeterDefaultFanSpell(caster, worldData, spellId);

        var handled = await InvokeLegacyPerimeterDefaultFanAsync(
            context,
            caster,
            CreatePerimeterDefaultFanQuery(spellId),
            playerSpell,
            spell);

        Assert.False(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.NotNull(unchanged);
        Assert.Equal(500, unchanged.Character.MinMana);
        Assert.Equal("100", unchanged.Character.Attributes["Stamina"]);
        Assert.Null(unchanged.MagicCooldowns);
    }

    [Theory]
    [InlineData(12_210)]
    [InlineData(12_220)]
    [InlineData(12_580)]
    [InlineData(12_590)]
    [InlineData(12_740)]
    [InlineData(12_750)]
    public async Task HandlerPerimeterDefaultFanSpendsResourcesAndSendsEmpty2239ForEveryExactId(
        ushort spellId)
    {
        var row = CreatePerimeterDefaultFanRow(
            spellId,
            useMana: 7,
            useStamina: 5,
            coolDown: 800,
            coldTime: 300);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var caster = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        sessions.Set(connection, caster);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolvePerimeterDefaultFanSpell(caster, worldData, spellId);

        var handled = await InvokeLegacyPerimeterDefaultFanAsync(
            context,
            caster,
            CreatePerimeterDefaultFanQuery(spellId),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(835, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Empty(animation.Targets);
        Assert.Single(sent, frame => ReadFrame(frame).Header.Opcode == 2_239);
        Assert.Single(ParseMagicColdTimePackets(sent));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.NotNull(updated.MagicCooldowns);
        Assert.True(updated.MagicCooldowns.ContainsKey(spellId));
        Assert.Equal(0, Assert.Single(updated.PlayerSpells.Spells).Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Perimeter default Fan magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("sort=78", output);
        Assert.Contains("fanRange=5", output);
        Assert.Contains("fanWidth=160", output);
        Assert.Contains("targets=0", output);
        Assert.Contains("progressDamage=0", output);
        Assert.Contains("source=Perimeter.cs default branch", output);
    }

    [Fact]
    public async Task HandlerPerimeterDefaultFanHitsMixedTargetsInSourceOrderWithExactScalingAndProgress()
    {
        const ushort spellId = 12_210;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var insideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 832, boundY: 584);
        var behindSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 828, boundY: 584);
        var outsideRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 836, boundY: 584);
        var insideSobNpc = CreateMoveLineSobNpc(id: 62_210, x: 834, y: 583);
        var behindSobNpc = CreateMoveLineSobNpc(id: 62_211, x: 828, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [insideSpawn, behindSpawn, outsideRangeSpawn],
            [insideSobNpc, behindSobNpc],
            [
                CreatePerimeterDefaultFanRow(spellId, level: 0, experience: 1_000_000),
                CreatePerimeterDefaultFanRow(spellId, level: 1, experience: 1_000_000),
            ]);
        var insideMonster = CreateRuntimeMonsterInstance(worldData, insideSpawn, monsterFamily);
        var behindMonster = CreateRuntimeMonsterInstance(worldData, behindSpawn, monsterFamily);
        var outsideRangeMonster = CreateRuntimeMonsterInstance(worldData, outsideRangeSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var playerSent = new List<byte[]>();
        var behindPlayerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var playerConnection = CreateSendingConnection(playerSent);
        var behindPlayerConnection = CreateSendingConnection(behindPlayerSent);
        var caster = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        var insidePlayer = CreateSession(
            uid: TargetPlayerUid,
            name: "InsideFanPlayer",
            x: 833,
            y: 585,
            minHitPoints: 100_000);
        var behindPlayer = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "BehindFanPlayer",
            x: 828,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, caster);
        sessions.Set(playerConnection, insidePlayer);
        sessions.Set(behindPlayerConnection, behindPlayer);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref caster);
        context = combatSpellOwner.Context;
        var query = CreatePerimeterDefaultFanQuery(spellId);
        var (playerSpell, spell) = ResolvePerimeterDefaultFanSpell(caster, worldData, spellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            caster,
            worldData,
            insideMonster,
            monsterFamily,
            spell) * 2;
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            caster,
            insidePlayer,
            spell,
            worldData,
            1U) * 2 / 3;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            caster,
            spell,
            worldData,
            1U) * 4;
        Assert.InRange(expectedMonsterDamage, 1U, insideMonster.MaxLife - 1);
        Assert.InRange(expectedSobNpcDamage, 1U, checked((uint)insideSobNpc.Life - 1));

        var handled = await InvokeLegacyPerimeterDefaultFanAsync(
            context,
            caster,
            query,
            playerSpell,
            spell);

        Assert.True(handled);
        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(
            new[]
            {
                insideMonster.Uid,
                TargetPlayerUid,
                checked((uint)insideSobNpc.Id),
            },
            casterAnimation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, casterAnimation.Targets[0].Damage);
        Assert.True(casterAnimation.Targets[0].Hit);
        Assert.Equal(expectedPlayerDamage, casterAnimation.Targets[1].Damage);
        Assert.False(casterAnimation.Targets[1].Hit);
        Assert.Equal(expectedSobNpcDamage, casterAnimation.Targets[2].Damage);
        Assert.True(casterAnimation.Targets[2].Hit);
        Assert.All(casterAnimation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));
        Assert.Equal(
            casterAnimation.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            Assert.Single(ParseSpellAnimations(playerSent)).Targets
                .Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(insideMonster, DateTimeOffset.UtcNow, out var visibleInside));
        Assert.True(monsterStates.TryGetVisibleInstance(behindMonster, DateTimeOffset.UtcNow, out var visibleBehind));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideRangeMonster, DateTimeOffset.UtcNow, out var visibleOutsideRange));
        Assert.Equal(insideMonster.MaxLife - expectedMonsterDamage, visibleInside.Hitpoints);
        Assert.Equal(behindMonster.MaxLife, visibleBehind.Hitpoints);
        Assert.Equal(outsideRangeMonster.MaxLife, visibleOutsideRange.Hitpoints);
        Assert.Equal(
            insideSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(insideSobNpc).Life);
        Assert.Equal(behindSobNpc.Life, sobNpcStates.GetVisible(behindSobNpc).Life);

        Assert.True(sessions.TryGet(playerConnection, out var updatedInsidePlayer));
        Assert.NotNull(updatedInsidePlayer);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedInsidePlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(behindPlayerConnection, out var updatedBehindPlayer));
        Assert.NotNull(updatedBehindPlayer);
        Assert.Equal(100_000, updatedBehindPlayer.Character.MinHitPoints);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.True(updatedCaster.Character.Experience > 0);
        var updatedSpell = Assert.Single(updatedCaster.PlayerSpells.Spells, entry => entry.Id == spellId);
        var expectedProgress = checked((int)(expectedMonsterDamage + expectedSobNpcDamage));
        Assert.Equal(expectedProgress, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcDamage}", output);
        Assert.Contains($"progressDamage={expectedProgress}", output);
    }

    private static MagicType CreatePerimeterDefaultFanRow(
        ushort spellId,
        byte sort = PerimeterDefaultFanSort,
        byte level = 0,
        ushort useMana = 0,
        byte useStamina = 0,
        ushort coolDown = 0,
        int coldTime = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            spellId,
            sort,
            level,
            useMana,
            damage: 100,
            experience,
            maxTargets: 0,
            range: 0,
            useStamina: useStamina,
            coolDown: coolDown,
            coldTime: coldTime);
    }

    private static MsgAttackQuery CreatePerimeterDefaultFanQuery(ushort spellId)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: 835,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)
        ResolvePerimeterDefaultFanSpell(
            WorldSession session,
            WorldDataCatalog worldData,
            ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static async ValueTask<bool> InvokeLegacyPerimeterDefaultFanAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerPerimeterDefaultFanMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }
}
