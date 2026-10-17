using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const byte AirKickRuntimeSort = 79;
    private const ushort AirKickRuntimeSpellId = 12_320;
    private const ushort AirKickRuntimeNextSpellId = 12_330;

    [Theory]
    [InlineData(834, 584, true)]
    [InlineData(834, 585, true)]
    [InlineData(834, 586, false)]
    [InlineData(829, 583, true)]
    [InlineData(828, 584, false)]
    public void AirKickPathUsesSourceChebyshevWidthOne(long targetX, long targetY, bool expected)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "IsWithinLegacyAirKickPath",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        IReadOnlyList<LegacyMoveLineCoordinate> path =
        [
            new(830, 584),
            new(831, 584),
            new(832, 584),
            new(833, 584),
            new(834, 584),
        ];

        var actual = Assert.IsType<bool>(method.Invoke(null, [path, targetX, targetY]));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AirKickSobNpcProgressPreservesSourceNestedCompoundAssignment()
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "AccumulateLegacyAirKickSobNpcProgress",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        Assert.Equal(3U, Assert.IsType<uint>(method.Invoke(null, [0U, 3U])));
        Assert.Equal(23U, Assert.IsType<uint>(method.Invoke(null, [10U, 3U])));
    }

    [Fact]
    public async Task HandlerAirKickRejectsAnotherSortWithoutSpendingResources()
    {
        var row = CreateAirKickRuntimeRow(sort: AirKickRuntimeSort - 1, useMana: 7, useStamina: 5, coldTime: 300);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [AirKickRuntimeSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveAirKickRuntimeSpell(session, worldData, AirKickRuntimeSpellId);

        var handled = await InvokeLegacyAirKickAsync(
            context,
            session,
            CreateAirKickRuntimeQuery(),
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
    public async Task HandlerAirKickSpendsResourcesAndStartsCooldownBeforeItsIdGate()
    {
        const ushort wrongSpellId = AirKickRuntimeSpellId + 1;
        var row = CreateAirKickRuntimeRow(
            rowId: wrongSpellId,
            typeId: wrongSpellId,
            useMana: 7,
            useStamina: 5,
            coolDown: 800,
            coldTime: 300);
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], row);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
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
        var (playerSpell, spell) = ResolveAirKickRuntimeSpell(session, worldData, wrongSpellId);

        var handled = await InvokeLegacyAirKickAsync(
            context,
            session,
            CreateAirKickRuntimeQuery(spellId: wrongSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Equal("95", updated.Character.Attributes["Stamina"]);
        Assert.True(updated.MagicCooldowns?.ContainsKey(wrongSpellId));
        Assert.Equal(830, updated.Character.X);
        Assert.Equal(584, updated.Character.Y);

        var staminaIndex = SingleFrameIndex(
            sent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)]));
        var manaIndex = SingleFrameIndex(
            sent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        var coldTimeIndex = SingleFrameIndex(
            sent,
            frame => LegacyMagicColdTimePacket.TryParse(frame, out var packet) &&
                packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(wrongSpellId, 300)]));
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < coldTimeIndex);
        Assert.DoesNotContain("WorldServer handled legacy AirKick magic", log.ToString());
    }

    [Fact]
    public async Task HandlerAirKickMovesHitsMixedPathTargetsScalesPlayersAndProgressesBeforeNextSpellAnimation()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var onPathSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584);
        var diagonalSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 838, boundY: 585);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838, boundY: 586);
        var outsideCastRadiusSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 831, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 62_320, x: 837, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onPathSpawn, diagonalSpawn, outsideWidthSpawn, outsideCastRadiusSpawn],
            [sobNpc],
            [
                CreateAirKickRuntimeRow(experience: 1_000_000),
                CreateAirKickRuntimeRow(
                    rowId: AirKickRuntimeSpellId + 1,
                    level: 1,
                    experience: 1_000_000),
            ]);
        var onPathMonster = CreateRuntimeMonsterInstance(worldData, onPathSpawn, monsterFamily);
        var diagonalMonster = CreateRuntimeMonsterInstance(worldData, diagonalSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var outsideCastRadiusMonster = CreateRuntimeMonsterInstance(worldData, outsideCastRadiusSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var outsideTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var outsideTargetConnection = CreateSendingConnection(outsideTargetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [AirKickRuntimeSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "AirKickTarget",
            x: 836,
            y: 585,
            minHitPoints: 100_000);
        var outsideTargetSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "AirKickOutside",
            x: 836,
            y: 586,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        sessions.Set(outsideTargetConnection, outsideTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var (playerSpell, spell) = ResolveAirKickRuntimeSpell(casterSession, worldData, AirKickRuntimeSpellId);
        var movedCasterSession = casterSession with
        {
            Character = casterSession.Character with { X = 838, Y = 584 },
        };
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            movedCasterSession,
            worldData,
            onPathMonster,
            monsterFamily,
            spell);
        var expectedBasePlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            movedCasterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedPlayerDamage = checked(expectedBasePlayerDamage * 13 / 5);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            movedCasterSession,
            spell,
            worldData,
            1U);
        var expectedMonsterProgress = InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedMonsterDamage, onPathMonster.MaxLife)),
            checked((int)movedCasterSession.Character.Level),
            onPathMonster.Level);
        var expectedMonsterProgressTotal = checked((uint)(expectedMonsterProgress * 2));
        var expectedProgress = checked(expectedMonsterProgressTotal * 2);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(movedCasterSession, expectedProgress));

        var handled = await InvokeLegacyAirKickAsync(
            context,
            casterSession,
            CreateAirKickRuntimeQuery(x: 842, y: 584),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(838, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(AirKickRuntimeSpellId, animation.SpellId);
        Assert.Equal(AirKickRuntimeNextSpellId, animation.NextSpell);
        Assert.Equal(
            [onPathMonster.Uid, diagonalMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id)],
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedMonsterDamage, animation.Targets[1].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[2].Damage);
        Assert.Equal(expectedSobNpcDamage, animation.Targets[3].Damage);
        Assert.All(animation.Targets, target => Assert.True(target.Hit));
        Assert.All(animation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));

        Assert.Equal(
            animation.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            Assert.Single(ParseSpellAnimations(targetSent)).Targets
                .Select(target => (target.Uid, target.Damage)).ToArray());
        Assert.Single(ParseSpellAnimations(outsideTargetSent));

        Assert.True(monsterStates.TryGetVisibleInstance(onPathMonster, DateTimeOffset.UtcNow, out var visibleOnPath));
        Assert.True(monsterStates.TryGetVisibleInstance(diagonalMonster, DateTimeOffset.UtcNow, out var visibleDiagonal));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideCastRadiusMonster, DateTimeOffset.UtcNow, out var visibleOutsideRadius));
        Assert.Equal(onPathMonster.MaxLife - expectedMonsterDamage, visibleOnPath.Hitpoints);
        Assert.Equal(diagonalMonster.MaxLife - expectedMonsterDamage, visibleDiagonal.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(outsideCastRadiusMonster.MaxLife, visibleOutsideRadius.Hitpoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTarget.Character.MinHitPoints);
        Assert.True(sessions.TryGet(outsideTargetConnection, out var unchangedOutsideTarget));
        Assert.NotNull(unchangedOutsideTarget);
        Assert.Equal(100_000, unchangedOutsideTarget.Character.MinHitPoints);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(838, updatedCaster.Character.X);
        Assert.Equal(584, updatedCaster.Character.Y);
        Assert.Equal(493, updatedCaster.Character.MinMana);
        Assert.Equal("95", updatedCaster.Character.Attributes["Stamina"]);
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCaster.Character.Experience);
        Assert.Equal(checked((int)expectedProgress), Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);

        var experienceIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var spellProgressIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == AirKickRuntimeSpellId &&
                packet.Experience == expectedProgress);
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == AirKickRuntimeSpellId);
        Assert.True(experienceIndex < spellProgressIndex);
        Assert.True(spellProgressIndex < animationIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AirKick magic", output);
        Assert.Contains("spellId=12320 sort=79", output);
        Assert.Contains("pathCells=9 pathDistance=8 landingX=838 landingY=584", output);
        Assert.Contains("pathHitRange=1 nextSpell=12330", output);
        Assert.Contains("monsterTargets=2 playerTargets=1 sobNpcTargets=1", output);
        Assert.Contains($"sourceProgress={expectedProgress}", output);
        Assert.Contains("source=AirKick.cs spell 12320", output);
    }

    [Fact]
    public async Task HandlerAirKickStopsPathAtFirstInvalidFloorAndAnimatesAtLastValidCell()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateAirKickRuntimeRow());
        var terrain = CreateAirKickTerrainWithBlockedTile(blockedX: 835, blockedY: 584);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [AirKickRuntimeSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            Terrain: terrain,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var (playerSpell, spell) = ResolveAirKickRuntimeSpell(session, worldData, AirKickRuntimeSpellId);

        var handled = await InvokeLegacyAirKickAsync(
            context,
            session,
            CreateAirKickRuntimeQuery(x: 842, y: 584),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(834, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(AirKickRuntimeNextSpellId, animation.NextSpell);
        Assert.Empty(animation.Targets);
        Assert.True(sessions.TryGet(connection, out var movedSession));
        Assert.NotNull(movedSession);
        Assert.Equal(834, movedSession.Character.X);
        Assert.Equal(584, movedSession.Character.Y);
        Assert.Contains("pathCells=5", log.ToString());
        Assert.Contains("landingX=834 landingY=584", log.ToString());
    }

    [Fact]
    public async Task HandlerAirKickGuildWarRejectedEndpointSpendsResourcesPullsBackAndDoesNotMoveOrAnimate()
    {
        const ushort originX = 2;
        const ushort originY = 2;
        const ushort endpointX = 14;
        const ushort endpointY = originY;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateAirKickRuntimeRow(range: 15));
        var terrain = CreateGuildWarMoveLineTerrain(originX, originY, endpointX, endpointY, endpointSurface: 0);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: originX,
            y: originY,
            minMana: 500,
            stamina: 100,
            spellIds: [AirKickRuntimeSpellId],
            attributes: new Dictionary<string, string>
            {
                ["CurrentTerrainMask"] = "3",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            Terrain: terrain,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(),
            GuildWarStates: new LegacyGuildWarRuntimeStateStore());
        var (playerSpell, spell) = ResolveAirKickRuntimeSpell(session, worldData, AirKickRuntimeSpellId);

        var handled = await InvokeLegacyAirKickAsync(
            context,
            session,
            CreateAirKickRuntimeQuery(x: endpointX, y: endpointY),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(originX, updatedSession.Character.X);
        Assert.Equal(originY, updatedSession.Character.Y);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        var pullback = Assert.Single(
            ParseMsgActions(sent),
            action => action.Type == MsgActionType.SetLocation && action.ObjId == PlayerUid);
        Assert.Equal((uint)LegacySobNpcEventRules.GuildWarMapId, pullback.DwParam);
        Assert.Equal(originX, pullback.PositionX);
        Assert.Equal(originY, pullback.PositionY);
        Assert.Contains("source=AirKick.cs CheckGuildWar rejection", log.ToString());
    }

    private static MagicType CreateAirKickRuntimeRow(
        int rowId = AirKickRuntimeSpellId,
        ushort typeId = AirKickRuntimeSpellId,
        byte sort = AirKickRuntimeSort,
        byte level = 0,
        ushort useMana = 7,
        byte useStamina = 5,
        ushort coolDown = 800,
        int coldTime = 300,
        byte range = 8,
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
            range,
            useStamina: useStamina,
            coolDown: coolDown,
            coldTime: coldTime,
            damageOnHuman: 30_020,
            damageOnMonster: 30_060);
        var fields = template.Fields.ToArray();
        fields[0] = rowId.ToString(CultureInfo.InvariantCulture);
        fields[1] = typeId.ToString(CultureInfo.InvariantCulture);
        fields[3] = "AirKick";
        return new MagicType(rowId, typeId, fields[3], fields);
    }

    private static MsgAttackQuery CreateAirKickRuntimeQuery(
        ushort spellId = AirKickRuntimeSpellId,
        ushort x = 842,
        ushort y = 584)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: 0,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)
        ResolveAirKickRuntimeSpell(WorldSession session, WorldDataCatalog worldData, ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static async ValueTask<bool> InvokeLegacyAirKickAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerAirKickMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }

    private static LegacyGameMapTerrainQuery CreateAirKickTerrainWithBlockedTile(int blockedX, int blockedY)
    {
        const int mapId = 1_015;
        var width = Math.Max(blockedX + 10, 850);
        var height = Math.Max(blockedY + 10, 600);
        var cells = new List<LegacyDMapCell>(width * height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var terrainFlag = (short)(x == blockedX && y == blockedY ? 1 : 0);
                cells.Add(new LegacyDMapCell(x, y, terrainFlag, Surface: 1, Altitude: 0));
            }
        }

        return new LegacyGameMapTerrainQuery(
            [new LegacyDMapTerrain(mapId, "fixture://airkick.dmap", width, height, cells)]);
    }
}
