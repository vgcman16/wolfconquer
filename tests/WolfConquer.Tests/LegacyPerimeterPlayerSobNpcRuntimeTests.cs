using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort LeftChopPerimeterSpellId = 14_570;
    private const ushort RightChopPerimeterSpellId = 14_580;
    private const uint ChopPerimeterWeaponTypes = 60_671_670;
    private const ushort GaleBombardmentPerimeterSpellId = 13_040;
    private const ushort ThunderstrikerLeftPerimeterSpellId = 15_670;
    private const ushort ThunderstrikerRightPerimeterSpellId = 15_680;
    private const uint GaleBombardmentPerimeterWeaponTypes = 61_626_626;
    private const uint ThunderstrikerPerimeterWeaponTypes = 61_681_680;

    [Theory]
    [InlineData(LeftChopPerimeterSpellId)]
    [InlineData(RightChopPerimeterSpellId)]
    public async Task HandlerChopPerimeterPlayerPrimaryReanchorsAndHitsOnlyPlayerAndSobTargetsInsideSourceSector(
        ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var insideSobNpc = CreateMoveLineSobNpc(id: 64_570, x: 833, y: 585);
        var outsideAngleSobNpc = CreateMoveLineSobNpc(id: 64_571, x: 830, y: 587);
        var outsideDistanceSobNpc = CreateMoveLineSobNpc(id: 64_572, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            [insideSobNpc, outsideAngleSobNpc, outsideDistanceSobNpc],
            [CreateChopPerimeterRow(spellId)]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [spellId],
            legacyRandomSpell: spellId);
        sessions.Set(casterConnection, casterSession);

        var primaryTarget = CreateSession(
            uid: TargetPlayerUid,
            name: "PerimeterPrimary",
            x: 832,
            y: 584,
            minHitPoints: 1);
        var insideTarget = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "PerimeterInside",
            x: 833,
            y: 585,
            minHitPoints: 100_000);
        var outsideAngleTarget = CreateSession(
            uid: TargetPlayerUid + 2,
            name: "PerimeterOutsideAngle",
            x: 830,
            y: 587,
            minHitPoints: 100_000);
        var outsideDistanceTarget = CreateSession(
            uid: TargetPlayerUid + 3,
            name: "PerimeterOutsideDistance",
            x: 834,
            y: 584,
            minHitPoints: 100_000);
        var playerConnections = new Dictionary<long, ConquerTcpConnection>();
        foreach (var target in new[] { primaryTarget, insideTarget, outsideAngleTarget, outsideDistanceTarget })
        {
            var targetConnection = CreateSendingConnection([]);
            playerConnections[target.Character.Uid] = targetConnection;
            sessions.Set(targetConnection, target);
        }

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);
        var (_, spell) = ResolveChopPerimeterSpell(casterSession, worldData, spellId);
        var expectedPlayerDamage = CalculateExpectedChopPerimeterPlayerDamage(
            casterSession,
            primaryTarget,
            spell,
            worldData);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        Assert.True(expectedPlayerDamage > 1);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 830,
                Y: 587,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: spellId)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(832, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal(
            new[] { TargetPlayerUid, TargetPlayerUid + 1, checked((uint)insideSobNpc.Id) }.Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid &&
            target.Damage == expectedPlayerDamage &&
            !target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid + 1 &&
            target.Damage == expectedPlayerDamage &&
            !target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == checked((uint)insideSobNpc.Id) &&
            target.Damage == expectedSobNpcDamage &&
            target.Hit);
        Assert.DoesNotContain(animation.Targets, target =>
            target.Uid == TargetPlayerUid + 2 ||
            target.Uid == TargetPlayerUid + 3 ||
            target.Uid == checked((uint)outsideAngleSobNpc.Id) ||
            target.Uid == checked((uint)outsideDistanceSobNpc.Id));

        AssertPlayerHitpoints(sessions, playerConnections[TargetPlayerUid], 0);
        AssertPlayerHitpoints(sessions, playerConnections[TargetPlayerUid + 1], 100_000 - expectedPlayerDamage);
        AssertPlayerHitpoints(sessions, playerConnections[TargetPlayerUid + 2], 100_000);
        AssertPlayerHitpoints(sessions, playerConnections[TargetPlayerUid + 3], 100_000);
        Assert.Equal(
            insideSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(insideSobNpc).Life);
        Assert.Equal(outsideAngleSobNpc.Life, sobNpcStates.GetVisible(outsideAngleSobNpc).Life);
        Assert.Equal(outsideDistanceSobNpc.Life, sobNpcStates.GetVisible(outsideDistanceSobNpc).Life);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(0, updatedCaster.LegacyRandomSpell);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy LeftChop/RightChop Perimeter player/SOB magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("sort=78", output);
        Assert.Contains("targetX=832 targetY=584", output);
        Assert.Contains("sectorSize=60", output);
        Assert.Contains("sectorDistance=3", output);
        Assert.Contains("playerTargets=2", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("source=Perimeter.cs case 14570/14580", output);
    }

    [Theory]
    [InlineData(LeftChopPerimeterSpellId)]
    [InlineData(RightChopPerimeterSpellId)]
    public async Task HandlerChopPerimeterSobPrimaryUsesQueryDirectionAndPhysicalDamage(ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var primarySobNpc = CreateMoveLineSobNpc(id: 65_570, x: 832, y: 584);
        var insideSobNpc = CreateMoveLineSobNpc(id: 65_571, x: 833, y: 585);
        var outsideAngleSobNpc = CreateMoveLineSobNpc(id: 65_572, x: 830, y: 587);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            [primarySobNpc, insideSobNpc, outsideAngleSobNpc],
            [CreateChopPerimeterRow(spellId)]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection([]);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [spellId],
            legacyRandomSpell: spellId);
        var playerTarget = CreateSession(
            uid: TargetPlayerUid,
            name: "PerimeterSobCompanion",
            x: 833,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, playerTarget);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);
        var (_, spell) = ResolveChopPerimeterSpell(casterSession, worldData, spellId);
        var expectedPlayerDamage = CalculateExpectedChopPerimeterPlayerDamage(
            casterSession,
            playerTarget,
            spell,
            worldData);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)primarySobNpc.Id),
                X: checked((ushort)primarySobNpc.X),
                Y: checked((ushort)primarySobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: spellId)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(primarySobNpc.X, animation.X);
        Assert.Equal(primarySobNpc.Y, animation.Y);
        Assert.Equal(
            new[] { TargetPlayerUid, checked((uint)primarySobNpc.Id), checked((uint)insideSobNpc.Id) }.Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid &&
            target.Damage == expectedPlayerDamage &&
            !target.Hit);
        Assert.All(
            animation.Targets.Where(target => target.Uid != TargetPlayerUid),
            target =>
            {
                Assert.Equal(expectedSobNpcDamage, target.Damage);
                Assert.True(target.Hit);
            });
        Assert.DoesNotContain(animation.Targets, target => target.Uid == checked((uint)outsideAngleSobNpc.Id));

        AssertPlayerHitpoints(sessions, targetConnection, 100_000 - expectedPlayerDamage);
        Assert.Equal(
            primarySobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(primarySobNpc).Life);
        Assert.Equal(
            insideSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(insideSobNpc).Life);
        Assert.Equal(outsideAngleSobNpc.Life, sobNpcStates.GetVisible(outsideAngleSobNpc).Life);
        Assert.Contains("playerTargets=1", log.ToString());
        Assert.Contains("sobNpcTargets=2", log.ToString());
    }

    [Fact]
    public async Task HandlerSpecialPerimeter13040PlayerPrimaryReanchorsHitsMixedTargetsAndExcludesPlayerDamageFromProgress()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var insideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 13_040, boundX: 833, boundY: 584);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 13_041, boundX: 838, boundY: 584);
        var insideSobNpc = CreateMoveLineSobNpc(id: 63_040, x: 835, y: 584);
        var outsideSobNpc = CreateMoveLineSobNpc(id: 63_041, x: 838, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [insideSpawn, outsideSpawn],
            [insideSobNpc, outsideSobNpc],
            [
                CreateSpecialPerimeterRow(GaleBombardmentPerimeterSpellId, level: 0, experience: 1_000_000),
                CreateSpecialPerimeterRow(GaleBombardmentPerimeterSpellId, level: 1, experience: 1_000_000),
            ]);
        var insideMonster = CreateRuntimeMonsterInstance(worldData, insideSpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection([]);
        var insideConnection = CreateSendingConnection([]);
        var sourceWrapConnection = CreateSendingConnection([]);
        var outsideConnection = CreateSendingConnection([]);
        var caster = CreateSession(
            strength: 302,
            spellIds: [GaleBombardmentPerimeterSpellId]);
        var primaryPlayer = CreateSession(
            uid: TargetPlayerUid,
            name: "GalePrimary",
            x: 832,
            y: 584,
            minHitPoints: 100_000);
        var insidePlayer = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "GaleInside",
            x: 834,
            y: 584,
            minHitPoints: 100_000);
        var outsidePlayer = CreateSession(
            uid: TargetPlayerUid + 2,
            name: "GaleOutside",
            x: 838,
            y: 585,
            minHitPoints: 100_000);
        var sourceWrapPlayer = CreateSession(
            uid: TargetPlayerUid + 3,
            name: "GaleSourceWrapOnly",
            x: 827,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, caster);
        sessions.Set(primaryConnection, primaryPlayer);
        sessions.Set(insideConnection, insidePlayer);
        sessions.Set(sourceWrapConnection, sourceWrapPlayer);
        sessions.Set(outsideConnection, outsidePlayer);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var (_, spell) = ResolveSpecialPerimeterSpell(caster, worldData, GaleBombardmentPerimeterSpellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            caster,
            worldData,
            insideMonster,
            monsterFamily,
            spell);
        var expectedMonsterProgress = InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedMonsterDamage, insideMonster.MaxLife)),
            checked((int)caster.Character.Level),
            insideMonster.Level);
        var expectedPrimaryDamage = CalculateExpectedSpecialPerimeterPlayerBaseDamage(
            caster,
            primaryPlayer,
            spell,
            worldData);
        var expectedInsideDamage = CalculateExpectedSpecialPerimeterPlayerBaseDamage(
            caster,
            insidePlayer,
            spell,
            worldData);
        var expectedSourceWrapDamage = CalculateExpectedSpecialPerimeterPlayerBaseDamage(
            caster,
            sourceWrapPlayer,
            spell,
            worldData);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            caster,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSpecialPerimeterMagicQuery(
                GaleBombardmentPerimeterSpellId,
                TargetPlayerUid,
                x: 999,
                y: 998)).ToFrameBytes()));

        var animations = ParseSpellAnimations(casterSent);
        Assert.True(animations.Count == 1, log.ToString());
        var animation = Assert.Single(animations);
        Assert.Equal(832, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(GaleBombardmentPerimeterSpellId, animation.SpellId);
        Assert.Equal(
            new[]
            {
                insideMonster.Uid,
                TargetPlayerUid,
                TargetPlayerUid + 1,
                TargetPlayerUid + 3,
                checked((uint)insideSobNpc.Id),
            }.Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Contains(animation.Targets, target =>
            target.Uid == insideMonster.Uid &&
            target.Damage == expectedMonsterDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid &&
            target.Damage == expectedPrimaryDamage &&
            !target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid + 1 &&
            target.Damage == expectedInsideDamage &&
            !target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid + 3 &&
            target.Damage == expectedSourceWrapDamage &&
            !target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == checked((uint)insideSobNpc.Id) &&
            target.Damage == expectedSobNpcDamage &&
            target.Hit);
        Assert.DoesNotContain(animation.Targets, target =>
            target.Uid == outsideMonster.Uid ||
            target.Uid == TargetPlayerUid + 2 ||
            target.Uid == checked((uint)outsideSobNpc.Id));

        var interactions = ParseSpecialPerimeterPhysicalInteractions(casterSent);
        Assert.Equal(5, interactions.Count);
        Assert.Equal(
            animation.Targets.Select(target => target.Uid).Order().ToArray(),
            interactions.Select(entry => entry.Packet.Query.OpponentUid).Order().ToArray());
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == GaleBombardmentPerimeterSpellId);
        var spellProgressIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == GaleBombardmentPerimeterSpellId);
        Assert.All(interactions, entry => Assert.True(entry.Index < spellProgressIndex));
        Assert.True(spellProgressIndex < animationIndex);

        Assert.True(monsterStates.TryGetVisibleInstance(insideMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(insideMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.Equal(outsideMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.Equal(
            insideSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(insideSobNpc).Life);
        Assert.Equal(outsideSobNpc.Life, sobNpcStates.GetVisible(outsideSobNpc).Life);
        AssertPlayerHitpoints(sessions, primaryConnection, 100_000 - expectedPrimaryDamage);
        AssertPlayerHitpoints(sessions, insideConnection, 100_000 - expectedInsideDamage);
        AssertPlayerHitpoints(sessions, sourceWrapConnection, 100_000 - expectedSourceWrapDamage);
        AssertPlayerHitpoints(sessions, outsideConnection, 100_000);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(
            expectedMonsterProgress,
            Assert.Single(updatedCaster.PlayerSpells.Spells, entry => entry.Id == GaleBombardmentPerimeterSpellId).Experience);
        Assert.True(updatedCaster.Character.Experience > 0);
    }

    [Theory]
    [InlineData(ThunderstrikerLeftPerimeterSpellId)]
    [InlineData(ThunderstrikerRightPerimeterSpellId)]
    public async Task HandlerThunderstrikerPerimeterMonsterPrimaryUsesExact500SectorAndPostSoulClassScaling(
        ushort spellId)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var primarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: spellId, boundX: 830, boundY: 582);
        var rejectedEastSpawn = CreateSingleInstanceMonsterSpawn(generatorId: spellId + 1, boundX: 833, boundY: 584);
        var insideSobNpc = CreateMoveLineSobNpc(id: 65_670 + spellId % 10, x: 829, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [primarySpawn, rejectedEastSpawn],
            [insideSobNpc],
            [
                CreateSpecialPerimeterRow(spellId, level: 0, experience: 1_000_000),
                CreateSpecialPerimeterRow(spellId, level: 1, experience: 1_000_000),
            ]);
        var primaryMonster = CreateRuntimeMonsterInstance(worldData, primarySpawn, monsterFamily);
        var rejectedEastMonster = CreateRuntimeMonsterInstance(worldData, rejectedEastSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var trojanConnection = CreateSendingConnection([]);
        var nonTrojanConnection = CreateSendingConnection([]);
        var rejectedEastConnection = CreateSendingConnection([]);
        var caster = CreateSession(strength: 302, spellIds: [spellId]);
        var trojanTarget = CreateSession(
            uid: TargetPlayerUid,
            name: "PerimeterTrojan",
            x: 827,
            y: 584,
            minHitPoints: 100_000,
            classCode: 1_000);
        var nonTrojanTarget = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "PerimeterNonTrojan",
            x: 830,
            y: 581,
            minHitPoints: 100_000,
            classCode: 142);
        var rejectedEastTarget = CreateSession(
            uid: TargetPlayerUid + 2,
            name: "PerimeterRejectedEast",
            x: 833,
            y: 584,
            minHitPoints: 100_000,
            classCode: 142);
        sessions.Set(casterConnection, caster);
        sessions.Set(trojanConnection, trojanTarget);
        sessions.Set(nonTrojanConnection, nonTrojanTarget);
        sessions.Set(rejectedEastConnection, rejectedEastTarget);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var (_, spell) = ResolveSpecialPerimeterSpell(caster, worldData, spellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            caster,
            worldData,
            primaryMonster,
            monsterFamily,
            spell);
        var expectedMonsterProgress = InvokePrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedMonsterDamage, primaryMonster.MaxLife)),
            checked((int)caster.Character.Level),
            primaryMonster.Level);
        var trojanBaseDamage = CalculateExpectedSpecialPerimeterPlayerBaseDamage(
            caster,
            trojanTarget,
            spell,
            worldData);
        var nonTrojanBaseDamage = CalculateExpectedSpecialPerimeterPlayerBaseDamage(
            caster,
            nonTrojanTarget,
            spell,
            worldData);
        var expectedTrojanDamage = checked(trojanBaseDamage * 10U) / 7U;
        var expectedNonTrojanDamage = checked(nonTrojanBaseDamage * 10U) / 11U;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            caster,
            spell,
            worldData,
            1U);
        Assert.NotEqual((trojanBaseDamage / 7U) * 10U, expectedTrojanDamage);
        Assert.NotEqual((nonTrojanBaseDamage / 11U) * 10U, expectedNonTrojanDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSpecialPerimeterMagicQuery(
                spellId,
                primaryMonster.Uid,
                x: 999,
                y: 998)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(primaryMonster.X, animation.X);
        Assert.Equal(primaryMonster.Y, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal(
            new[]
            {
                primaryMonster.Uid,
                TargetPlayerUid,
                TargetPlayerUid + 1,
                checked((uint)insideSobNpc.Id),
            }.Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Contains(animation.Targets, target =>
            target.Uid == primaryMonster.Uid &&
            target.Damage == expectedMonsterDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid &&
            target.Damage == expectedTrojanDamage &&
            !target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid + 1 &&
            target.Damage == expectedNonTrojanDamage &&
            !target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == checked((uint)insideSobNpc.Id) &&
            target.Damage == expectedSobNpcDamage &&
            target.Hit);
        Assert.DoesNotContain(animation.Targets, target =>
            target.Uid == rejectedEastMonster.Uid ||
            target.Uid == TargetPlayerUid + 2);

        var interactions = ParseSpecialPerimeterPhysicalInteractions(casterSent);
        Assert.Equal(4, interactions.Count);
        var animationIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == spellId);
        var spellProgressIndex = SingleFrameIndex(
            casterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) && packet.Id == spellId);
        Assert.All(interactions, entry => Assert.True(entry.Index < spellProgressIndex));
        Assert.True(spellProgressIndex < animationIndex);

        Assert.True(monsterStates.TryGetVisibleInstance(primaryMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(rejectedEastMonster, DateTimeOffset.UtcNow, out var untouchedEastMonster));
        Assert.Equal(primaryMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.Equal(rejectedEastMonster.MaxLife, untouchedEastMonster.Hitpoints);
        AssertPlayerHitpoints(sessions, trojanConnection, 100_000 - expectedTrojanDamage);
        AssertPlayerHitpoints(sessions, nonTrojanConnection, 100_000 - expectedNonTrojanDamage);
        AssertPlayerHitpoints(sessions, rejectedEastConnection, 100_000);
        Assert.Equal(
            insideSobNpc.Life - checked((int)expectedSobNpcDamage),
            sobNpcStates.GetVisible(insideSobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(
            expectedMonsterProgress,
            Assert.Single(updatedCaster.PlayerSpells.Spells, entry => entry.Id == spellId).Experience);
    }

    [Theory]
    [InlineData(GaleBombardmentPerimeterSpellId)]
    [InlineData(ThunderstrikerLeftPerimeterSpellId)]
    [InlineData(ThunderstrikerRightPerimeterSpellId)]
    public async Task HandlerSpecialPerimeterWithNoAcceptedTargetsSendsNoAnimation(ushort spellId)
    {
        var worldData = CreateMagicCatalog(
            CreateHighLifeMonsterFamily(),
            [],
            CreateSpecialPerimeterRow(spellId));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var caster = CreateSession(strength: 302, spellIds: [spellId]);
        sessions.Set(connection, caster);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                caster,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore(),
                SobNpcStates: new SobNpcRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(CreateSpecialPerimeterMagicQuery(
                spellId,
                opponentUid: 0,
                x: 835,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseSpecialPerimeterPhysicalInteractions(sent));
        Assert.True(sessions.TryGet(connection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(0, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
    }

    [Theory]
    [InlineData(GaleBombardmentPerimeterSpellId)]
    [InlineData(ThunderstrikerLeftPerimeterSpellId)]
    [InlineData(ThunderstrikerRightPerimeterSpellId)]
    public async Task HandlerSpecialPerimeterManualCastUsesFiveHundredMillisecondAttackStampCadence(ushort spellId)
    {
        var worldData = CreateMagicCatalog(
            CreateHighLifeMonsterFamily(),
            [],
            CreateSpecialPerimeterRow(spellId));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var targetConnection = CreateSendingConnection([]);
        var caster = CreateSession(strength: 302, spellIds: [spellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "CadenceTarget",
            x: 832,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(connection, caster);
        sessions.Set(targetConnection, target);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var frame = ReadFrame(new MsgAttackPacket(CreateSpecialPerimeterMagicQuery(
            spellId,
            TargetPlayerUid,
            x: 999,
            y: 998)).ToFrameBytes());

        await PacketHandlers.HandleAsync(context, frame);

        Assert.True(sessions.TryGet(connection, out var firstCastCaster));
        Assert.NotNull(firstCastCaster);
        Assert.True(firstCastCaster.Character.Attributes.TryGetValue(
            RuntimeLegacyAttackStampUtcAttribute,
            out var firstAttackStamp));
        Assert.True(sessions.TryGet(targetConnection, out var firstCastTarget));
        Assert.NotNull(firstCastTarget);
        var firstCastFrameCount = sent.Count;

        await PacketHandlers.HandleAsync(context with { Session = firstCastCaster }, frame);

        Assert.Equal(firstCastFrameCount, sent.Count);
        Assert.Single(ParseSpellAnimations(sent));
        Assert.Single(ParseSpecialPerimeterPhysicalInteractions(sent));
        Assert.True(sessions.TryGet(connection, out var blockedCaster));
        Assert.NotNull(blockedCaster);
        Assert.Equal(firstAttackStamp, blockedCaster.Character.Attributes[RuntimeLegacyAttackStampUtcAttribute]);
        Assert.True(sessions.TryGet(targetConnection, out var blockedTarget));
        Assert.NotNull(blockedTarget);
        Assert.Equal(firstCastTarget.Character.MinHitPoints, blockedTarget.Character.MinHitPoints);
        Assert.Contains("attackStampRemainingMs=", log.ToString());
    }

    [Theory]
    [InlineData(GaleBombardmentPerimeterSpellId)]
    [InlineData(ThunderstrikerLeftPerimeterSpellId)]
    [InlineData(ThunderstrikerRightPerimeterSpellId)]
    public async Task HandlerSpecialPerimeterRejectsMismatchedPendingWeaponProc(ushort spellId)
    {
        const ushort otherPendingSpellId = 13_050;
        var worldData = CreateMagicCatalog(
            CreateHighLifeMonsterFamily(),
            [],
            CreateSpecialPerimeterRow(spellId));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var targetConnection = CreateSendingConnection([]);
        var caster = CreateSession(
            strength: 302,
            spellIds: [spellId],
            legacyRandomSpell: otherPendingSpellId);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "MismatchedProcTarget",
            x: 832,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(connection, caster);
        sessions.Set(targetConnection, target);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                caster,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore(),
                SobNpcStates: new SobNpcRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(CreateSpecialPerimeterMagicQuery(
                spellId,
                TargetPlayerUid,
                x: 999,
                y: 998)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseSpecialPerimeterPhysicalInteractions(sent));
        AssertPlayerHitpoints(sessions, targetConnection, 100_000);
        Assert.True(sessions.TryGet(connection, out var unchangedCaster));
        Assert.NotNull(unchangedCaster);
        Assert.Equal(otherPendingSpellId, unchangedCaster.LegacyRandomSpell);
        Assert.False(unchangedCaster.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
    }

    [Theory]
    [InlineData(GaleBombardmentPerimeterSpellId)]
    [InlineData(ThunderstrikerLeftPerimeterSpellId)]
    [InlineData(ThunderstrikerRightPerimeterSpellId)]
    public async Task HandlerD13ClientSelectedSpecialPerimeterBypassesAttackStampWithoutDuplicateDamage(
        ushort spellId)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: spellId, boundX: 833, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 66_000 + spellId % 100, x: 835, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [
                CreateSpecialPerimeterRow(spellId, level: 0, experience: 1_000_000),
                CreateSpecialPerimeterRow(spellId, level: 1, experience: 1_000_000),
            ]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var playerConnection = CreateSendingConnection([]);
        var attackStamp = DateTimeOffset.UtcNow.ToString("O");
        var caster = CreateSession(
            strength: 302,
            spellIds: [spellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyAttackStampUtcAttribute] = attackStamp,
            },
            playerItems: CreateSpecialPerimeterWeapon(spellId));
        var playerTarget = CreateSession(
            uid: TargetPlayerUid,
            name: "D13PerimeterCompanion",
            x: 834,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(connection, caster);
        sessions.Set(playerConnection, playerTarget);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates,
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient with
            {
                DisableLegacyWeaponProcMonsterAttacks = true,
            },
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var (_, spell) = ResolveSpecialPerimeterSpell(caster, worldData, spellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            caster,
            worldData,
            monster,
            monsterFamily,
            spell);
        var playerBaseDamage = CalculateExpectedSpecialPerimeterPlayerBaseDamage(
            caster,
            playerTarget,
            spell,
            worldData);
        var expectedPlayerDamage = spellId == GaleBombardmentPerimeterSpellId
            ? playerBaseDamage
            : checked(playerBaseDamage * 10U) / 11U;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            caster,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: spellId,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal(monster.X, animation.X);
        Assert.Equal(monster.Y, animation.Y);
        Assert.Equal(3, animation.Targets.Count);
        Assert.Equal(3, animation.Targets.Select(target => target.Uid).Distinct().Count());
        Assert.Contains(animation.Targets, target =>
            target.Uid == monster.Uid && target.Damage == expectedMonsterDamage);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid && target.Damage == expectedPlayerDamage);
        Assert.Contains(animation.Targets, target =>
            target.Uid == checked((uint)sobNpc.Id) && target.Damage == expectedSobNpcDamage);

        var interactions = ParseSpecialPerimeterPhysicalInteractions(sent);
        Assert.Equal(3, interactions.Count);
        Assert.Equal(3, interactions.Select(entry => entry.Packet.Query.OpponentUid).Distinct().Count());
        var animationIndex = SingleFrameIndex(
            sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == spellId);
        Assert.All(interactions, entry => Assert.True(entry.Index < animationIndex));

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        AssertPlayerHitpoints(sessions, playerConnection, 100_000 - expectedPlayerDamage);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);
        Assert.True(sessions.TryGet(connection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(attackStamp, updatedCaster.Character.Attributes[RuntimeLegacyAttackStampUtcAttribute]);
        Assert.Equal(0, updatedCaster.LegacyRandomSpell);
        Assert.Contains($"selector={spellId}", log.ToString());
        Assert.Contains("adapted D13 client-selected weapon proc", log.ToString());
    }

    [Fact]
    public async Task HandlerSpecialPerimeter13040Map1505MutatesMonsterThenReturnsBeforePlayerSobProgressAndAnimation()
    {
        const int sourceEarlyReturnMapId = 1_505;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 15_050, boundX: 833, boundY: 584) with
        {
            MapId = sourceEarlyReturnMapId,
            SourceRelativePath = "1505/1.txt",
        };
        var sobNpc = CreateMoveLineSobNpc(id: 65_050, x: 834, y: 584) with
        {
            MapId = sourceEarlyReturnMapId,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [
                CreateSpecialPerimeterRow(GaleBombardmentPerimeterSpellId, level: 0, experience: 1_000_000),
                CreateSpecialPerimeterRow(GaleBombardmentPerimeterSpellId, level: 1, experience: 1_000_000),
            ]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var playerConnection = CreateSendingConnection([]);
        var caster = CreateSession(
            strength: 302,
            spellIds: [GaleBombardmentPerimeterSpellId],
            mapId: sourceEarlyReturnMapId);
        var visiblePlayer = CreateSession(
            uid: TargetPlayerUid,
            name: "Map1505VisiblePlayer",
            x: 840,
            y: 584,
            minHitPoints: 100_000,
            mapId: sourceEarlyReturnMapId);
        sessions.Set(connection, caster);
        sessions.Set(playerConnection, visiblePlayer);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var (_, spell) = ResolveSpecialPerimeterSpell(caster, worldData, GaleBombardmentPerimeterSpellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            caster,
            worldData,
            monster,
            monsterFamily,
            spell);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSpecialPerimeterMagicQuery(
                GaleBombardmentPerimeterSpellId,
                monster.Uid,
                x: 999,
                y: 998)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        var interaction = Assert.Single(ParseSpecialPerimeterPhysicalInteractions(sent));
        Assert.Equal(monster.Uid, interaction.Packet.Query.OpponentUid);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        AssertPlayerHitpoints(sessions, playerConnection, 100_000);
        Assert.Equal(sobNpc.Life, sobNpcStates.GetVisible(sobNpc).Life);
        Assert.True(sessions.TryGet(connection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(
            0,
            Assert.Single(updatedCaster.PlayerSpells.Spells, entry => entry.Id == GaleBombardmentPerimeterSpellId).Experience);
        Assert.Equal(0, updatedCaster.Character.Experience);
    }

    [Theory]
    [InlineData(13_030)]
    [InlineData(13_041)]
    [InlineData(13_050)]
    [InlineData(15_660)]
    [InlineData(15_681)]
    [InlineData(15_690)]
    public async Task HandlerSpecialPerimeterDoesNotExpandToAdjacentIds(ushort adjacentSpellId)
    {
        var worldData = CreateMagicCatalog(
            CreateHighLifeMonsterFamily(),
            [],
            CreateSpecialPerimeterRow(adjacentSpellId, useMana: 7));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var targetConnection = CreateSendingConnection([]);
        var caster = CreateSession(strength: 302, spellIds: [adjacentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "AdjacentPerimeterTarget",
            x: 832,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(connection, caster);
        sessions.Set(targetConnection, target);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                caster,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore(),
                SobNpcStates: new SobNpcRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(CreateSpecialPerimeterMagicQuery(
                adjacentSpellId,
                TargetPlayerUid,
                x: 832,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseSpecialPerimeterPhysicalInteractions(sent));
        AssertPlayerHitpoints(sessions, targetConnection, 100_000);
        Assert.True(sessions.TryGet(connection, out var unchangedCaster));
        Assert.NotNull(unchangedCaster);
        Assert.Equal(500, unchangedCaster.Character.MinMana);
        Assert.False(unchangedCaster.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
    }

    private static MagicType CreateSpecialPerimeterRow(
        ushort spellId,
        byte level = 0,
        uint experience = 0,
        ushort useMana = 0)
    {
        var weaponTypes = spellId < 15_000
            ? GaleBombardmentPerimeterWeaponTypes
            : ThunderstrikerPerimeterWeaponTypes;
        return CreateMagicRow(
            spellId,
            sort: (byte)LegacyMagicSort.Perimeter,
            level,
            useMana,
            damage: 100,
            experience,
            maxTargets: 30,
            range: 0,
            weaponType: weaponTypes,
            useStamina: 0,
            coolDown: 0,
            coldTime: 0,
            damageOnMonster: 100,
            gDamage: 100);
    }

    private static MsgAttackQuery CreateSpecialPerimeterMagicQuery(
        ushort spellId,
        uint opponentUid,
        ushort x,
        ushort y)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
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
        ResolveSpecialPerimeterSpell(
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

    private static uint CalculateExpectedSpecialPerimeterPlayerBaseDamage(
        WorldSession caster,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog worldData)
    {
        return InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            caster,
            target,
            spell,
            worldData,
            1U);
    }

    private static PlayerItemSnapshot CreateSpecialPerimeterWeapon(ushort spellId)
    {
        var weaponType = spellId == GaleBombardmentPerimeterSpellId ? 626U : 681U;
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = weaponType * 1_000U + 439U,
                    Durability = 100,
                    MaximumDurability = 100,
                    Position = 4,
                },
            ],
            RuneItems: []);
    }

    private static List<(int Index, MsgAttackPacket Packet)> ParseSpecialPerimeterPhysicalInteractions(
        IReadOnlyList<byte[]> frames)
    {
        var interactions = new List<(int Index, MsgAttackPacket Packet)>();
        for (var index = 0; index < frames.Count; index++)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(frames[index]), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical)
            {
                interactions.Add((index, packet));
            }
        }

        return interactions;
    }

    private static MagicType CreateChopPerimeterRow(ushort spellId)
    {
        return CreateMagicRow(
            spellId,
            sort: 78,
            useMana: 0,
            damage: 0,
            maxTargets: 3,
            range: 0,
            weaponType: ChopPerimeterWeaponTypes,
            useStamina: 0,
            coolDown: 0,
            coldTime: 0,
            damageOnMonster: 100,
            gDamage: 100);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)
        ResolveChopPerimeterSpell(
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

    private static uint CalculateExpectedChopPerimeterPlayerDamage(
        WorldSession caster,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog worldData)
    {
        var effectiveSpell = spell with { Damage = 100 };
        return InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            caster,
            target,
            effectiveSpell,
            worldData,
            1U);
    }

    private static void AssertPlayerHitpoints(
        WorldSessionStore sessions,
        ConquerTcpConnection connection,
        long expectedHitpoints)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        Assert.Equal(expectedHitpoints, session.Character.MinHitPoints);
    }
}
