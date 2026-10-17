using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte BurntFrostSort = 86;
    private const ushort BurntFrostSpellId = 12_940;

    [Theory]
    [InlineData(837, 584, true)]
    [InlineData(838, 584, false)]
    [InlineData(830, 587, false)]
    [InlineData(830, 586, true)]
    [InlineData(828, 584, true)]
    [InlineData(827, 584, false)]
    public void BurntFrostGeometryUsesStrictSevenTileFanAndLessThanThreeFallback(
        long targetX,
        long targetY,
        bool expected)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "IsInsideLegacyBurntFrostFanOrCloseRange",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var actual = Assert.IsType<bool>(method.Invoke(
            null,
            [830L, 584L, 837L, 584L, targetX, targetY]));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task HandlerBurntFrostRejectsAnotherSortWithoutSpendingResources()
    {
        var row = CreateBurntFrostRow(
            rowId: BurntFrostSpellId,
            typeId: BurntFrostSpellId,
            sort: BurntFrostSort - 1,
            useMana: 7,
            useStamina: 5,
            coolDown: 800,
            coldTime: 300);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [BurntFrostSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveBurntFrostSpell(session, worldData, BurntFrostSpellId);

        var handled = await InvokeLegacyBurntFrostAsync(
            context,
            session,
            CreateBurntFrostQuery(BurntFrostSpellId),
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

    [Fact]
    public async Task HandlerBurntFrostSpendsResourcesAndStartsCooldownBeforeItsIdGate()
    {
        const ushort wrongSpellId = BurntFrostSpellId + 1;
        var row = CreateBurntFrostRow(
            rowId: wrongSpellId,
            typeId: wrongSpellId,
            useMana: 7,
            useStamina: 5,
            coolDown: 800,
            coldTime: 300);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [wrongSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveBurntFrostSpell(session, worldData, wrongSpellId);

        var handled = await InvokeLegacyBurntFrostAsync(
            context,
            session,
            CreateBurntFrostQuery(wrongSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        var staminaIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)]));
        var manaIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        var coldTimeIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacyMagicColdTimePacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(wrongSpellId, 300)]));
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < coldTimeIndex);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.NotNull(updated.MagicCooldowns);
        Assert.True(updated.MagicCooldowns.ContainsKey(wrongSpellId));
        Assert.DoesNotContain("WorldServer handled legacy BurntFrost magic", log.ToString());
    }

    [Fact]
    public async Task DispatchBurntFrostHitsMixedTargetsInSourceOrderAndProgressesBeforeAnimation()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var frontSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 837, boundY: 584);
        var closeBehindSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 828, boundY: 584);
        var farBehindSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 827, boundY: 584);
        var sideBoundarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 830, boundY: 587);
        var outsideRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 5, boundX: 838, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 62_940, x: 835, y: 583);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [frontSpawn, closeBehindSpawn, farBehindSpawn, sideBoundarySpawn, outsideRangeSpawn],
            [sobNpc],
            [
                CreateBurntFrostRow(
                    rowId: BurntFrostSpellId,
                    typeId: BurntFrostSpellId,
                    level: 0,
                    experience: 1_000_000),
                CreateBurntFrostRow(
                    rowId: BurntFrostSpellId + 1,
                    typeId: BurntFrostSpellId,
                    level: 1,
                    experience: 1_000_000),
            ]);
        var frontMonster = CreateRuntimeMonsterInstance(worldData, frontSpawn, monsterFamily);
        var closeBehindMonster = CreateRuntimeMonsterInstance(worldData, closeBehindSpawn, monsterFamily);
        var farBehindMonster = CreateRuntimeMonsterInstance(worldData, farBehindSpawn, monsterFamily);
        var sideBoundaryMonster = CreateRuntimeMonsterInstance(worldData, sideBoundarySpawn, monsterFamily);
        var outsideRangeMonster = CreateRuntimeMonsterInstance(worldData, outsideRangeSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var sideTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var sideTargetConnection = CreateSendingConnection(sideTargetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [BurntFrostSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FanTarget",
            x: 834,
            y: 586,
            minHitPoints: 100_000,
            spellIds: []);
        var sideTargetSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "SideBoundaryTarget",
            x: 830,
            y: 587,
            minHitPoints: 100_000,
            spellIds: []);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        sessions.Set(sideTargetConnection, sideTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var (_, spell) = ResolveBurntFrostSpell(casterSession, worldData, BurntFrostSpellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            frontMonster,
            monsterFamily,
            spell);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 5 / 7;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedMonsterProgress = InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedMonsterDamage, frontMonster.MaxLife)),
            checked((int)casterSession.Character.Level),
            frontMonster.Level);
        var expectedProgress = checked(expectedMonsterProgress * 2);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateBurntFrostQuery(BurntFrostSpellId)).ToFrameBytes()));

        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, casterAnimation.AttackerUid);
        Assert.Equal(0U, casterAnimation.AttackedUid);
        Assert.Equal(837, casterAnimation.X);
        Assert.Equal(584, casterAnimation.Y);
        Assert.Equal(BurntFrostSpellId, casterAnimation.SpellId);
        Assert.Equal(0, casterAnimation.SpellLevel);
        Assert.Equal(0, casterAnimation.LevelHu);
        Assert.Equal(
            [frontMonster.Uid, closeBehindMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id)],
            casterAnimation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, casterAnimation.Targets[0].Damage);
        Assert.True(casterAnimation.Targets[0].Hit);
        Assert.Equal(expectedMonsterDamage, casterAnimation.Targets[1].Damage);
        Assert.True(casterAnimation.Targets[1].Hit);
        Assert.Equal(expectedPlayerDamage, casterAnimation.Targets[2].Damage);
        Assert.False(casterAnimation.Targets[2].Hit);
        Assert.Equal(expectedSobNpcDamage, casterAnimation.Targets[3].Damage);
        Assert.True(casterAnimation.Targets[3].Hit);
        Assert.All(casterAnimation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));

        Assert.Equal(
            casterAnimation.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            Assert.Single(ParseSpellAnimations(targetSent)).Targets
                .Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());
        Assert.Equal(
            casterAnimation.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            Assert.Single(ParseSpellAnimations(sideTargetSent)).Targets
                .Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(frontMonster, DateTimeOffset.UtcNow, out var visibleFront));
        Assert.True(monsterStates.TryGetVisibleInstance(closeBehindMonster, DateTimeOffset.UtcNow, out var visibleCloseBehind));
        Assert.True(monsterStates.TryGetVisibleInstance(farBehindMonster, DateTimeOffset.UtcNow, out var visibleFarBehind));
        Assert.True(monsterStates.TryGetVisibleInstance(sideBoundaryMonster, DateTimeOffset.UtcNow, out var visibleSideBoundary));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideRangeMonster, DateTimeOffset.UtcNow, out var visibleOutsideRange));
        Assert.Equal(frontMonster.MaxLife - expectedMonsterDamage, visibleFront.Hitpoints);
        Assert.Equal(closeBehindMonster.MaxLife - expectedMonsterDamage, visibleCloseBehind.Hitpoints);
        Assert.Equal(farBehindMonster.MaxLife, visibleFarBehind.Hitpoints);
        Assert.Equal(sideBoundaryMonster.MaxLife, visibleSideBoundary.Hitpoints);
        Assert.Equal(outsideRangeMonster.MaxLife, visibleOutsideRange.Hitpoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTarget.Character.MinHitPoints);
        Assert.True(sessions.TryGet(sideTargetConnection, out var updatedSideTarget));
        Assert.NotNull(updatedSideTarget);
        Assert.Equal(100_000, updatedSideTarget.Character.MinHitPoints);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(
            checked((long)expectedExperience.AppliedExperienceGain),
            updatedCaster.Character.Experience);
        Assert.Equal(expectedProgress, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);

        var experienceUpdateIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(casterSent[index]), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var spellProgressIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacySpellPacket.TryParse(ReadFrame(casterSent[index]), out var packet) &&
                packet.Id == BurntFrostSpellId &&
                packet.Experience == expectedProgress);
        var animationIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out var packet) &&
                packet.SpellId == BurntFrostSpellId);
        Assert.True(experienceUpdateIndex < spellProgressIndex);
        Assert.True(spellProgressIndex < animationIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy BurntFrost magic", output);
        Assert.Contains("spellId=12940", output);
        Assert.Contains("sort=86", output);
        Assert.Contains("fanRange=7", output);
        Assert.Contains("fanWidth=180", output);
        Assert.Contains("closeRange=3", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedProgress}", output);
        Assert.Contains("sobNpcProgress=0", output);
        Assert.Contains($"progressDamage={expectedProgress}", output);
        Assert.Contains("source=BurntFrost.cs spell 12940", output);
    }

    private static MagicType CreateBurntFrostRow(
        int rowId,
        ushort typeId,
        byte sort = BurntFrostSort,
        byte level = 0,
        ushort useMana = 0,
        byte useStamina = 0,
        ushort coolDown = 0,
        int coldTime = 0,
        uint experience = 0)
    {
        var template = CreateMagicRow(
            typeId,
            sort,
            level,
            useMana,
            damage: 100,
            experience,
            maxTargets: 4,
            range: 10,
            useStamina: useStamina,
            coolDown: coolDown,
            coldTime: coldTime);
        var fields = template.Fields.ToArray();
        fields[0] = rowId.ToString(CultureInfo.InvariantCulture);
        fields[1] = typeId.ToString(CultureInfo.InvariantCulture);
        fields[3] = "BurntFrost";
        return new MagicType(rowId, typeId, fields[3], fields);
    }

    private static MsgAttackQuery CreateBurntFrostQuery(ushort spellId)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: 837,
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
        ResolveBurntFrostSpell(
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

    private static async Task<bool> InvokeLegacyBurntFrostAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerBurntFrostMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<Task<bool>>(result);
    }
}
