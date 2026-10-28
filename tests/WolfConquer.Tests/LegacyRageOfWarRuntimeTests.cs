using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort RageOfWarSpellId = 12_930;
    private const int RageOfWarWildPhoenixFlag = 0x170;

    [Fact]
    public async Task HandlerRageOfWarLineHitsMixedTargetsWithSourceDistanceWidthsDamageSingleAnimationAndFloorEffect()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var edgeWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 834, boundY: 585);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 834, boundY: 586);
        var distanceEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 841, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_930, x: 836, y: 585);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, edgeWidthSpawn, outsideWidthSpawn, distanceEdgeSpawn],
            [sobNpc],
            [
                CreateRageOfWarRow(experience: 1),
                CreateRageOfWarRow(level: 1, experience: 100),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var edgeWidthMonster = CreateRuntimeMonsterInstance(worldData, edgeWidthSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var distanceEdgeMonster = CreateRuntimeMonsterInstance(worldData, distanceEdgeSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var offLineSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var offLineConnection = CreateSendingConnection(offLineSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [RageOfWarSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 838,
            y: 584,
            minHitPoints: 100_000);
        var offLineSession = CreateSession(
            uid: TargetPlayerUid + 10,
            name: "OffLine",
            x: 836,
            y: 585,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        sessions.Set(offLineConnection, offLineSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            FloorItems: floorItems);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;
        var query = CreateMagicQuery(
            onLineMonster,
            legacyEncodedQuery: false,
            spellId: RageOfWarSpellId,
            x: 850,
            y: 584);
        var (_, spell) = ResolveRageOfWarSpell(casterSession, worldData);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 10 / 13;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        var castStartedAt = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        var offLineSpell = Assert.Single(ParseSpellAnimations(offLineSent));
        foreach (var animation in new[] { casterSpell, targetSpell, offLineSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(850, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(RageOfWarSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.All(animation.Targets, target =>
            {
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        }

        var casterFloorItems = ParseLegacyFloorItems(casterSent);
        Assert.True(casterFloorItems.Count == 1, DescribeFrameOpcodes(casterSent));
        var floorItem = casterFloorItems[0];
        Assert.Equal(floorItem, Assert.Single(ParseLegacyFloorItems(targetSent)));
        Assert.Equal(floorItem, Assert.Single(ParseLegacyFloorItems(offLineSent)));
        Assert.Equal(1_500U, floorItem.ItemId);
        Assert.Equal(833, floorItem.X);
        Assert.Equal(584, floorItem.Y);
        Assert.Equal(0, floorItem.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal(0U, floorItem.Life);
        Assert.Equal(14, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal(0U, floorItem.GuildId);
        Assert.Equal(0, floorItem.FlowerType);
        Assert.Equal(0UL, floorItem.Timer);
        Assert.Equal(830, floorItem.OwnerX);
        Assert.Equal(584, floorItem.OwnerY);
        Assert.Equal("trap", floorItem.Name);
        var floorAnimationTarget = Assert.Single(casterSpell.Targets, target => target.Uid == floorItem.Uid);
        Assert.Equal(1U, floorAnimationTarget.Damage);
        Assert.True(floorAnimationTarget.Hit);
        Assert.Equal(830, floorAnimationTarget.NewX);
        Assert.Equal(584, floorAnimationTarget.NewY);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var floorEntry));
        Assert.Equal(casterSession.Character.MapId, floorEntry.MapId);
        Assert.Equal(floorItem, floorEntry.Packet);
        Assert.NotNull(floorEntry.Effect);
        var runtimeEffect = floorEntry.Effect!;
        Assert.Equal(RageOfWarSpellId, runtimeEffect.SpellId);
        Assert.Equal(830, runtimeEffect.OriginX);
        Assert.Equal(584, runtimeEffect.OriginY);
        Assert.True(runtimeEffect.DueAt >= castStartedAt.AddMilliseconds(2_000));
        Assert.True(runtimeEffect.DueAt <= DateTimeOffset.UtcNow.AddMilliseconds(2_000));

        var damageByUid = casterSpell.Targets
            .Where(target => target.Uid != floorItem.Uid)
            .ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, edgeWidthMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(expectedMonsterDamage, damageByUid[edgeWidthMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            offLineSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(edgeWidthMonster, DateTimeOffset.UtcNow, out var visibleEdgeWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(distanceEdgeMonster, DateTimeOffset.UtcNow, out var visibleDistanceEdge));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, visibleOnLine.Hitpoints);
        Assert.Equal(edgeWidthMonster.MaxLife - expectedMonsterDamage, visibleEdgeWidth.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(distanceEdgeMonster.MaxLife, visibleDistanceEdge.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(sessions.TryGet(offLineConnection, out var updatedOffLineSession));
        Assert.NotNull(updatedOffLineSession);
        Assert.Equal(100_000, updatedOffLineSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.Contains(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 85)]));
        Assert.Empty(ParseMagicColdTimePackets(casterSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("85", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey("HitShoot"));
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(RageOfWarSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy RageOfWar Line magic", output);
        Assert.Contains("range=20", output);
        Assert.Contains("targetDistanceGate=11", output);
        Assert.Contains("monsterSobWidth=1", output);
        Assert.Contains("playerWidth=0", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage * 2UL}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcDamage}", output);
        Assert.Contains("WorldServer sent legacy RageOfWar floor effect", output);
        Assert.Contains($"floorUid={floorItem.Uid}", output);
        Assert.Contains("nearestX=833", output);
        Assert.Contains("nearestY=584", output);
        Assert.Contains("floorItemId=1500", output);
        Assert.Contains("ownerX=830", output);
        Assert.Contains("ownerY=584", output);
        Assert.Contains("durationMs=2000", output);
        Assert.Contains("counterHitShoots=0", output);
        Assert.DoesNotContain("PARITY-GAP RageOfWar floor-effect runtime not modeled", output);
        Assert.DoesNotContain("PARITY-GAP RageOfWar counter-hit map HitShoot state not modeled", output);
        Assert.Contains("source=Line.cs case 12930", output);
    }

    [Fact]
    public async Task HandlerRageOfWarLineAgainstWildPhoenixPlayerConsumesDefenderChargeScalesDamageToZeroAndStillCreatesFloorEffect()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateRageOfWarRow(),
                CreateMagicRow(
                    WildPhoenixSpellId,
                    sort: 6,
                    useMana: 0,
                    damage: 4,
                    coldTime: 0,
                    duration: 32,
                    damageOnHuman: 18,
                    damage2: 7),
            ]);
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [RageOfWarSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 838,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(RageOfWarWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);
        var expectedPlayerDamage = 1U * 10 / 13;
        Assert.Equal(0U, expectedPlayerDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 850,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: RageOfWarSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(850, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(RageOfWarSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);

            var playerTarget = Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid);
            Assert.Equal(expectedPlayerDamage, playerTarget.Damage);
            Assert.True(playerTarget.Hit);
            Assert.Equal(MsgAttackEffect.None, playerTarget.Effect);
        }

        var floorItem = Assert.Single(
            ParseLegacyFloorItems(casterSent),
            packet => packet.ItemId == 1_500U &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.Equal(
            floorItem,
            Assert.Single(
                ParseLegacyFloorItems(targetSent),
                packet => packet.ItemId == 1_500U &&
                    packet.DropType == LegacyFloorItemDropType.Effect));
        Assert.Equal(838, floorItem.X);
        Assert.Equal(584, floorItem.Y);
        Assert.Equal(830, floorItem.OwnerX);
        Assert.Equal(584, floorItem.OwnerY);
        Assert.Equal("trap", floorItem.Name);

        var floorAnimationTarget = Assert.Single(casterSpell.Targets, target => target.Uid == floorItem.Uid);
        Assert.Equal(1U, floorAnimationTarget.Damage);
        Assert.True(floorAnimationTarget.Hit);
        Assert.Equal(830, floorAnimationTarget.NewX);
        Assert.Equal(584, floorAnimationTarget.NewY);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var floorEntry));
        Assert.Equal(floorItem, floorEntry.Packet);
        Assert.NotNull(floorEntry.Effect);
        Assert.Equal(RageOfWarSpellId, floorEntry.Effect!.SpellId);
        Assert.Equal(830, floorEntry.Effect.OriginX);
        Assert.Equal(584, floorEntry.Effect.OriginY);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, RageOfWarWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == RageOfWarWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy RageOfWar Line magic", output);
        Assert.Contains("targets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("totalAppliedDamage=0", output);
        Assert.Contains("WorldServer sent legacy RageOfWar floor effect", output);
        Assert.Contains("source=Line.cs case 12930", output);
    }

    [Theory]
    [InlineData(1_005, "9", 2)]
    [InlineData(1_015, "7", 0)]
    public async Task HandlerRageOfWarLineCounterHitMapUpdatesHitShootForInitialPlayerHits(
        long mapId,
        string expectedHitShoot,
        int expectedCounterHitShoots)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 21, boundX: 833, boundY: 584) with
        {
            MapId = checked((int)mapId),
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [CreateRageOfWarRow()]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var firstTargetSent = new List<byte[]>();
        var secondTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var firstTargetConnection = CreateSendingConnection(firstTargetSent);
        var secondTargetConnection = CreateSendingConnection(secondTargetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [RageOfWarSpellId],
            mapId: mapId,
            attributes: new Dictionary<string, string>
            {
                ["HitShoot"] = "7",
            });
        var firstTargetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "CounterTargetOne",
            x: 832,
            y: 584,
            minHitPoints: 100_000,
            mapId: mapId);
        var secondTargetSession = CreateSession(
            uid: TargetPlayerUid + 20,
            name: "CounterTargetTwo",
            x: 836,
            y: 584,
            minHitPoints: 100_000,
            mapId: mapId);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(firstTargetConnection, firstTargetSession);
        sessions.Set(secondTargetConnection, secondTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: RageOfWarSpellId,
                x: 850,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(expectedHitShoot, updatedSession.Character.Attributes["HitShoot"]);

        var output = log.ToString();
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=2", output);
        Assert.Contains($"counterHitShoots={expectedCounterHitShoots}", output);
        Assert.DoesNotContain("PARITY-GAP RageOfWar counter-hit map HitShoot state not modeled", output);
    }

    [Fact]
    public async Task HandlerRageOfWarFloorRuntimeDoesNotUpdateCounterHitShootForDelayedPlayerHits()
    {
        const long mapId = 1_005;
        var monsterFamily = CreateMonsterFamily();
        var anchorSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 22, boundX: 833, boundY: 584) with
        {
            MapId = checked((int)mapId),
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [anchorSpawn],
            [],
            [CreateRageOfWarRow()]);
        var anchorMonster = CreateRuntimeMonsterInstance(worldData, anchorSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [RageOfWarSpellId],
            mapId: mapId,
            attributes: new Dictionary<string, string>
            {
                ["HitShoot"] = "7",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FloorTarget",
            x: 834,
            y: 586,
            minHitPoints: 100_000,
            mapId: mapId);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                anchorMonster,
                legacyEncodedQuery: false,
                spellId: RageOfWarSpellId,
                x: 850,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var afterInitialCast));
        Assert.NotNull(afterInitialCast);
        Assert.Equal("7", afterInitialCast.Character.Attributes["HitShoot"]);
        var floorItem = Assert.Single(
            ParseLegacyFloorItems(casterSent),
            packet => packet.ItemId == 1_500U &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var floorEntry));
        Assert.NotNull(floorEntry.Effect);

        var processedCount = await WorldPacketHandlers.SendDueLegacyRageOfWarFloorSpellsAsync(
            context,
            floorEntry.Effect!.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        var runtimeAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            spellAnimation => spellAnimation.SpellId == RageOfWarSpellId &&
                spellAnimation.BombFlag == 1);
        Assert.Contains(runtimeAnimation.Targets, target => target.Uid == TargetPlayerUid);
        Assert.True(sessions.TryGet(casterConnection, out var afterFloorRuntime));
        Assert.NotNull(afterFloorRuntime);
        Assert.Equal("7", afterFloorRuntime.Character.Attributes["HitShoot"]);

        var output = log.ToString();
        Assert.Contains("counterHitShoots=0", output);
        Assert.Contains("WorldServer handled legacy RageOfWar floor spell runtime", output);
        Assert.Contains("playerTargets=1", output);
    }

    [Fact]
    public async Task HandlerRageOfWarFloorRuntimeAgainstWildPhoenixPlayerConsumesDefenderChargeScalesDamageToZeroAndPreservesBombTarget()
    {
        var monsterFamily = CreateMonsterFamily();
        var anchorSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 23, boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [anchorSpawn],
            [],
            [
                CreateRageOfWarRow(),
                CreateMagicRow(
                    WildPhoenixSpellId,
                    sort: 6,
                    useMana: 0,
                    damage: 4,
                    coldTime: 0,
                    duration: 32,
                    damageOnHuman: 18,
                    damage2: 7),
            ]);
        var anchorMonster = CreateRuntimeMonsterInstance(worldData, anchorSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [RageOfWarSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixFloorTarget",
            x: 834,
            y: 586,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(RageOfWarWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                anchorMonster,
                legacyEncodedQuery: false,
                spellId: RageOfWarSpellId,
                x: 850,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var afterInitialCast));
        Assert.NotNull(afterInitialCast);
        Assert.Equal(100_000, afterInitialCast.Character.MinHitPoints);
        Assert.Equal(
            "2",
            afterInitialCast.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        var floorItem = Assert.Single(
            ParseLegacyFloorItems(casterSent),
            packet => packet.ItemId == 1_500U &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var floorEntry));
        Assert.NotNull(floorEntry.Effect);
        var targetFrameCountBeforeRuntime = targetSent.Count;
        var expectedPlayerRuntimeDamage = 1U * 60 / 100;
        Assert.Equal(0U, expectedPlayerRuntimeDamage);

        var processedCount = await WorldPacketHandlers.SendDueLegacyRageOfWarFloorSpellsAsync(
            context,
            floorEntry.Effect!.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        var casterRuntimeAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            spellAnimation => spellAnimation.SpellId == RageOfWarSpellId &&
                spellAnimation.BombFlag == 1);
        var targetRuntimeAnimation = Assert.Single(
            ParseSpellAnimations(targetSent.Skip(targetFrameCountBeforeRuntime)),
            spellAnimation => spellAnimation.SpellId == RageOfWarSpellId &&
                spellAnimation.BombFlag == 1);
        foreach (var animation in new[] { casterRuntimeAnimation, targetRuntimeAnimation })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(floorItem.X, animation.X);
            Assert.Equal(floorItem.Y, animation.Y);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.Equal(1, animation.SpellEffect);

            var playerTarget = Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid);
            Assert.Equal(expectedPlayerRuntimeDamage, playerTarget.Damage);
            Assert.True(playerTarget.Hit);
            Assert.Equal(MsgAttackEffect.None, playerTarget.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, RageOfWarWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent.Skip(targetFrameCountBeforeRuntime)).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == RageOfWarWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy RageOfWar floor spell runtime", output);
        Assert.Contains("playerTargets=1", output);
    }

    [Fact]
    public async Task HandlerRageOfWarFloorRuntimeInvokesDueRadiusDamageBombAnimationAndRemoveEffect()
    {
        var monsterFamily = CreateMonsterFamily();
        var anchorSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 11, boundX: 833, boundY: 584);
        var radiusOnlySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 12, boundX: 833, boundY: 587);
        var outsideRadiusSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 13, boundX: 837, boundY: 588);
        var sobNpc = CreateMoveLineSobNpc(id: 65_932, x: 835, y: 586);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [anchorSpawn, radiusOnlySpawn, outsideRadiusSpawn],
            [sobNpc],
            [CreateRageOfWarRow()]);
        var anchorMonster = CreateRuntimeMonsterInstance(worldData, anchorSpawn, monsterFamily);
        var radiusOnlyMonster = CreateRuntimeMonsterInstance(worldData, radiusOnlySpawn, monsterFamily);
        var outsideRadiusMonster = CreateRuntimeMonsterInstance(worldData, outsideRadiusSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [RageOfWarSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            FloorItems: floorItems);
        var query = CreateMagicQuery(
            anchorMonster,
            legacyEncodedQuery: false,
            spellId: RageOfWarSpellId,
            x: 850,
            y: 584);
        var (_, spell) = ResolveRageOfWarSpell(casterSession, worldData);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            anchorMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 60 / 100;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var floorItem = Assert.Single(
            ParseLegacyFloorItems(casterSent),
            packet => packet.ItemId == 1_500U &&
                packet.DropType == LegacyFloorItemDropType.Effect);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var floorEntry));
        Assert.NotNull(floorEntry.Effect);
        Assert.Equal(3.0, floorEntry.Effect!.HitRadius);
        Assert.Equal(60, floorEntry.Effect.PlayerDamagePercent);
        Assert.Equal(18, floorEntry.Effect.RemovalVisibilityRadius);

        var processedCount = await WorldPacketHandlers.SendDueLegacyRageOfWarFloorSpellsAsync(
            context,
            floorEntry.Effect.DueAt.AddMilliseconds(1),
            CancellationToken.None);

        Assert.Equal(1, processedCount);

        var casterAnimations = ParseSpellAnimations(casterSent);
        Assert.Equal(2, casterAnimations.Count(spellAnimation => spellAnimation.SpellId == RageOfWarSpellId));
        var runtimeAnimation = Assert.Single(
            casterAnimations,
            spellAnimation => spellAnimation.SpellId == RageOfWarSpellId &&
                spellAnimation.BombFlag == 1);
        Assert.Equal(PlayerUid, runtimeAnimation.AttackerUid);
        Assert.Equal(0U, runtimeAnimation.AttackedUid);
        Assert.Equal(floorItem.X, runtimeAnimation.X);
        Assert.Equal(floorItem.Y, runtimeAnimation.Y);
        Assert.Equal(0, runtimeAnimation.SpellLevel);
        Assert.Equal(0, runtimeAnimation.LevelHu);
        Assert.Equal(1, runtimeAnimation.SpellEffect);
        var targetDamageByUid = runtimeAnimation.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { anchorMonster.Uid, radiusOnlyMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            targetDamageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage, targetDamageByUid[anchorMonster.Uid]);
        Assert.Equal(expectedMonsterDamage, targetDamageByUid[radiusOnlyMonster.Uid]);
        Assert.Equal(expectedPlayerRuntimeDamage, targetDamageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, targetDamageByUid[checked((uint)sobNpc.Id)]);

        Assert.Contains(
            ParseSpellAnimations(targetSent),
            spellAnimation => spellAnimation.SpellId == RageOfWarSpellId &&
                spellAnimation.BombFlag == 1 &&
                spellAnimation.X == floorItem.X &&
                spellAnimation.Y == floorItem.Y);

        var casterRemove = Assert.Single(
            ParseLegacyFloorItems(casterSent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.Equal(1_500U, casterRemove.ItemId);
        Assert.Equal("trap", casterRemove.Name);
        Assert.Contains(
            ParseLegacyFloorItems(targetSent),
            packet => packet.Uid == floorItem.Uid &&
                packet.DropType == LegacyFloorItemDropType.RemoveEffect);
        Assert.False(floorItems.TryGetVisibleEntry(floorItem.Uid, out _));

        Assert.True(monsterStates.TryGetVisibleInstance(anchorMonster, DateTimeOffset.UtcNow, out var visibleAnchor));
        Assert.True(monsterStates.TryGetVisibleInstance(radiusOnlyMonster, DateTimeOffset.UtcNow, out var visibleRadiusOnly));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideRadiusMonster, DateTimeOffset.UtcNow, out var visibleOutsideRadius));
        Assert.Equal(anchorMonster.MaxLife - (expectedMonsterDamage * 2U), visibleAnchor.Hitpoints);
        Assert.Equal(radiusOnlyMonster.MaxLife - expectedMonsterDamage, visibleRadiusOnly.Hitpoints);
        Assert.Equal(outsideRadiusMonster.MaxLife, visibleOutsideRadius.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy RageOfWar floor spell runtime", output);
        Assert.Contains("trapCount=1", output);
        Assert.Contains("targetCount=4", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage * 2UL}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcDamage}", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("removeViewerCount=2", output);
        Assert.Contains("source=ThreadPool.cs case 12930", output);
    }

    [Fact]
    public async Task HandlerRageOfWarLineSkipsGuardMonstersEvenWhenSharedMonsterGateWouldAllowThem()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            boundX: 833,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var sobNpc = CreateMoveLineSobNpc(id: 65_931, x: 836, y: 585);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            [sobNpc],
            [CreateRageOfWarRow()]);
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [RageOfWarSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            FloorItems: floorItems);
        var query = CreateMagicQuery(
            guard,
            legacyEncodedQuery: false,
            spellId: RageOfWarSpellId,
            x: 850,
            y: 584);
        var (_, spell) = ResolveRageOfWarSpell(casterSession, worldData);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 10 / 13;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        var casterFloorItems = ParseLegacyFloorItems(casterSent);
        Assert.True(casterFloorItems.Count == 1, DescribeFrameOpcodes(casterSent));
        var floorItem = casterFloorItems[0];
        Assert.Equal(floorItem, Assert.Single(ParseLegacyFloorItems(targetSent)));
        Assert.Equal(1_500U, floorItem.ItemId);
        Assert.Equal(834, floorItem.X);
        Assert.Equal(584, floorItem.Y);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal(14, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal(830, floorItem.OwnerX);
        Assert.Equal(584, floorItem.OwnerY);
        Assert.Equal("trap", floorItem.Name);
        var floorAnimationTarget = Assert.Single(casterSpell.Targets, target => target.Uid == floorItem.Uid);
        Assert.Equal(1U, floorAnimationTarget.Damage);
        Assert.True(floorAnimationTarget.Hit);
        Assert.Equal(830, floorAnimationTarget.NewX);
        Assert.Equal(584, floorAnimationTarget.NewY);
        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, out var floorEntry));
        Assert.Equal(floorItem, floorEntry.Packet);
        Assert.NotNull(floorEntry.Effect);
        var runtimeEffect = floorEntry.Effect!;
        Assert.Equal(RageOfWarSpellId, runtimeEffect.SpellId);
        Assert.Equal(830, runtimeEffect.OriginX);
        Assert.Equal(584, runtimeEffect.OriginY);

        var damageByUid = casterSpell.Targets
            .Where(target => target.Uid != floorItem.Uid)
            .ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);

        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleGuard));
        Assert.Equal(guard.MaxLife, visibleGuard.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        var output = log.ToString();
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("WorldServer sent legacy RageOfWar floor effect", output);
        Assert.DoesNotContain("PARITY-GAP RageOfWar floor-effect runtime not modeled", output);
        Assert.Contains("source=Line.cs case 12930", output);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveRageOfWarSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            RageOfWarSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static string DescribeFrameOpcodes(IEnumerable<byte[]> frames)
    {
        return string.Join(
            ",",
            frames.Select(frame =>
            {
                var decoded = ReadFrame(frame);
                return decoded.Header.Opcode + ":" + decoded.Payload.Length;
            }));
    }

    private static MagicType CreateRageOfWarRow(
        byte level = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            RageOfWarSpellId,
            sort: 14,
            level: level,
            useMana: 0,
            damage: level switch
            {
                0 => 30_110U,
                1 => 30_113U,
                2 => 30_116U,
                3 => 30_120U,
                4 => 30_123U,
                5 => 30_126U,
                _ => 30_135U,
            },
            experience: experience,
            maxTargets: 11,
            range: 20,
            needLevel: level switch
            {
                0 => 40,
                1 => 50,
                2 => 60,
                3 => 70,
                4 => 80,
                5 => 90,
                _ => 0,
            },
            weaponType: 61_626_626,
            useStamina: 15,
            coolDown: 600,
            coldTime: 0);
    }
}
