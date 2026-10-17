using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerThunderRampageActiveUsesAttackCenterInclusiveRadiiSourceOrderAndPlayerDoubleDamage()
    {
        const ushort queryX = 840;
        const ushort queryY = 590;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var insideMonsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 839, boundY: 589);
        var boundaryMonsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 829, boundY: 590);
        var outsideMonsterSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 828, boundY: 590);
        var insideSobNpc = CreateMoveLineSobNpc(id: 65_780, x: 839, y: 589);
        var boundarySobNpc = CreateMoveLineSobNpc(id: 65_781, x: 829, y: 590);
        var outsideSobNpc = CreateMoveLineSobNpc(id: 65_782, x: 828, y: 590);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [insideMonsterSpawn, boundaryMonsterSpawn, outsideMonsterSpawn],
            [insideSobNpc, boundarySobNpc, outsideSobNpc],
            [CreateThunderRampageActiveRuntimeRow()]);
        var insideMonster = CreateRuntimeMonsterInstance(worldData, insideMonsterSpawn, monsterFamily);
        var boundaryMonster = CreateRuntimeMonsterInstance(worldData, boundaryMonsterSpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideMonsterSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var insidePlayerConnection = CreateSendingConnection([]);
        var boundaryPlayerConnection = CreateSendingConnection([]);
        var outsidePlayerConnection = CreateSendingConnection([]);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [TestThunderRampageSpellId],
            attributes: ThunderRampageActiveAttributes(),
            classCode: 9_001);
        var insidePlayerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "ThunderRampageInside",
            x: 839,
            y: 589,
            minHitPoints: 100_000);
        var boundaryPlayerSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "ThunderRampageBoundary",
            x: 831,
            y: 590,
            minHitPoints: 100_000);
        var outsidePlayerSession = CreateSession(
            uid: TargetPlayerUid + 2,
            name: "ThunderRampageOutside",
            x: 830,
            y: 590,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(insidePlayerConnection, insidePlayerSession);
        sessions.Set(boundaryPlayerConnection, boundaryPlayerSession);
        sessions.Set(outsidePlayerConnection, outsidePlayerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            TestThunderRampageSpellId,
            out _,
            out var spell));
        var capability = LegacyMagicSortCapabilities.Classify(spell);
        Assert.Equal(LegacyMagicSortCapability.XpSkill, capability.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, capability.Status);
        Assert.True(capability.Supported);
        Assert.Equal("thunderrampage-combat-runtime", capability.NextSeam);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            insideMonster,
            monsterFamily,
            spell);
        var expectedBasePlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            insidePlayerSession,
            spell,
            worldData,
            1U);
        var expectedPlayerDamage = checked(expectedBasePlayerDamage * 2);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateThunderRampageActiveQuery(queryX, queryY)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(queryX, animation.X);
        Assert.Equal(queryY, animation.Y);
        Assert.Equal(TestThunderRampageSpellId, animation.SpellId);
        Assert.Equal(0, animation.NextSpell);
        Assert.Equal(6, animation.Targets.Count);
        Assert.All(animation.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        var targetUids = animation.Targets.Select(target => target.Uid).ToArray();
        Assert.Equal(
            new[] { insideMonster.Uid, boundaryMonster.Uid }.Order().ToArray(),
            targetUids.Take(2).Order().ToArray());
        Assert.Equal(
            new[] { TargetPlayerUid, TargetPlayerUid + 1 }.Order().ToArray(),
            targetUids.Skip(2).Take(2).Order().ToArray());
        Assert.Equal(
            new[] { checked((uint)insideSobNpc.Id), checked((uint)boundarySobNpc.Id) }.Order().ToArray(),
            targetUids.Skip(4).Order().ToArray());
        Assert.DoesNotContain(outsideMonster.Uid, targetUids);
        Assert.DoesNotContain(TargetPlayerUid + 2, targetUids);
        Assert.DoesNotContain(checked((uint)outsideSobNpc.Id), targetUids);

        var damageByUid = animation.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(expectedMonsterDamage, damageByUid[insideMonster.Uid]);
        Assert.Equal(expectedMonsterDamage, damageByUid[boundaryMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid + 1]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)insideSobNpc.Id)]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)boundarySobNpc.Id)]);

        Assert.True(monsterStates.TryGetVisibleInstance(insideMonster, DateTimeOffset.UtcNow, out var damagedInsideMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(boundaryMonster, DateTimeOffset.UtcNow, out var damagedBoundaryMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideMonster, DateTimeOffset.UtcNow, out var untouchedOutsideMonster));
        Assert.Equal(insideMonster.MaxLife - expectedMonsterDamage, damagedInsideMonster.Hitpoints);
        Assert.Equal(boundaryMonster.MaxLife - expectedMonsterDamage, damagedBoundaryMonster.Hitpoints);
        Assert.Equal(outsideMonster.MaxLife, untouchedOutsideMonster.Hitpoints);

        Assert.True(sessions.TryGet(insidePlayerConnection, out var damagedInsidePlayer));
        Assert.True(sessions.TryGet(boundaryPlayerConnection, out var damagedBoundaryPlayer));
        Assert.True(sessions.TryGet(outsidePlayerConnection, out var untouchedOutsidePlayer));
        Assert.NotNull(damagedInsidePlayer);
        Assert.NotNull(damagedBoundaryPlayer);
        Assert.NotNull(untouchedOutsidePlayer);
        Assert.Equal(100_000 - expectedPlayerDamage, damagedInsidePlayer.Character.MinHitPoints);
        Assert.Equal(100_000 - expectedPlayerDamage, damagedBoundaryPlayer.Character.MinHitPoints);
        Assert.Equal(100_000, untouchedOutsidePlayer.Character.MinHitPoints);

        Assert.Equal(
            insideSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(insideSobNpc).Life);
        Assert.Equal(
            boundarySobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(boundarySobNpc).Life);
        Assert.Equal(outsideSobNpc.Life, sobNpcStates.GetVisible(outsideSobNpc).Life);

        var experienceIndex = SingleFrameIndex(
            casterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == TestThunderRampageSpellId);
        Assert.True(experienceIndex < animationIndex);
        Assert.DoesNotContain(
            casterSent.Select(ReadFrame),
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == TestThunderRampageSpellId);
    }

    [Fact]
    public async Task HandlerThunderRampageActiveDoublesWildPhoenixDefenderDamageAfterPhysicalSelection()
    {
        const ushort queryX = 840;
        const ushort queryY = 590;
        var worldData = CreateMagicCatalogCore(
            CreateHighLifeMonsterFamily(),
            [],
            [],
            [
                CreateThunderRampageActiveRuntimeRow(),
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
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [TestThunderRampageSpellId],
            attributes: ThunderRampageActiveAttributes(),
            classCode: 9_001);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "ThunderRampageWildPhoenix",
            x: queryX,
            y: queryY,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore(),
                SobNpcStates: new SobNpcRuntimeStateStore()),
            ReadFrame(
                new MsgAttackPacket(CreateThunderRampageActiveQuery(queryX, queryY)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(2U, target.Damage);
        Assert.True(target.Hit);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(99_998, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", log.ToString());
    }

    [Fact]
    public async Task HandlerThunderRampageActiveDoesNotCapTargetsAtMatchingRowMaxTargets()
    {
        const ushort queryX = 840;
        const ushort queryY = 590;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawns = Enumerable.Range(0, 13)
            .Select(index => CreateSingleInstanceMonsterSpawn(
                generatorId: index + 1,
                boundX: 836 + index % 5,
                boundY: 587 + index / 5))
            .ToArray();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            spawns,
            CreateThunderRampageActiveRuntimeRow());
        var monsters = spawns
            .Select(spawn => CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily))
            .ToArray();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [TestThunderRampageSpellId],
            attributes: ThunderRampageActiveAttributes(),
            classCode: 9_001);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: new SobNpcRuntimeStateStore());
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            TestThunderRampageSpellId,
            out _,
            out var spell));
        Assert.Equal(12, spell.MaxTargets);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateThunderRampageActiveQuery(queryX, queryY)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(13, animation.Targets.Count);
        Assert.Equal(
            monsters.Select(monster => monster.Uid).Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        foreach (var monster in monsters)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
            Assert.True(damagedMonster.Hitpoints < monster.MaxLife);
        }
    }

    [Fact]
    public async Task HandlerThunderRampageActiveWithNoTargetsSendsEmptyAnimationAndUsesFiveHundredMillisecondAttackStamp()
    {
        var worldData = CreateMagicCatalog(
            CreateHighLifeMonsterFamily(),
            [],
            CreateThunderRampageActiveRuntimeRow());
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellIds: [TestThunderRampageSpellId],
            attributes: ThunderRampageActiveAttributes(),
            classCode: 9_001);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var query = ReadFrame(new MsgAttackPacket(CreateThunderRampageActiveQuery(840, 590)).ToFrameBytes());

        await PacketHandlers.HandleAsync(context, query);

        var firstAnimation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(TestThunderRampageSpellId, firstAnimation.SpellId);
        Assert.Empty(firstAnimation.Targets);
        Assert.True(sessions.TryGet(connection, out var firstCastSession));
        Assert.NotNull(firstCastSession);

        await PacketHandlers.HandleAsync(context with { Session = firstCastSession }, query);

        Assert.Single(ParseSpellAnimations(sent));
        Assert.Contains("attackStampRemainingMs=", log.ToString());
        Assert.True(sessions.TryGet(connection, out var blockedSession));
        Assert.NotNull(blockedSession);

        await Task.Delay(600);

        await PacketHandlers.HandleAsync(context with { Session = blockedSession }, query);

        var animations = ParseSpellAnimations(sent);
        Assert.Equal(2, animations.Count);
        Assert.All(animations, animation => Assert.Empty(animation.Targets));
    }

    [Fact]
    public async Task HandlerThunderRampageWithoutXpListOrActiveFlagStillSendsEmptyAnimationWithoutDamage()
    {
        const ushort queryX = 840;
        const ushort queryY = 590;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: queryX, boundY: queryY);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateThunderRampageActiveRuntimeRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [TestThunderRampageSpellId],
            classCode: 9_001);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateThunderRampageActiveQuery(queryX, queryY)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(queryX, animation.X);
        Assert.Equal(queryY, animation.Y);
        Assert.Equal(TestThunderRampageSpellId, animation.SpellId);
        Assert.Empty(animation.Targets);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(monster.MaxLife, untouchedMonster.Hitpoints);
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
    }

    private static MagicType CreateThunderRampageActiveRuntimeRow()
    {
        var row = CreateMagicRow(
            TestThunderRampageSpellId,
            sort: 94,
            useMana: 0,
            damage: 30_500,
            maxTargets: 12,
            range: 20,
            weaponType: 61_681_680,
            useStamina: 0,
            coolDown: 0,
            coldTime: 0,
            duration: 40,
            damageOnHuman: 30_010,
            damage2: 300,
            damage3: 150,
            damageOnMonster: 100,
            gDamage: 100);
        var fields = row.Fields.ToArray();
        fields[3] = "ThunderRampage";
        fields[4] = "1";
        fields[7] = "4";
        fields[16] = TestThunderRampageFlag.ToString(CultureInfo.InvariantCulture);
        fields[21] = "1";
        fields[30] = "1";
        return row with { Name = "ThunderRampage", Fields = fields };
    }

    private static Dictionary<string, string> ThunderRampageActiveAttributes()
    {
        return new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(TestThunderRampageFlag),
        };
    }

    private static MsgAttackQuery CreateThunderRampageActiveQuery(ushort x, ushort y)
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
            SpellId: TestThunderRampageSpellId);
    }
}
