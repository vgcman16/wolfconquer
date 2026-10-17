using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort SectorBack18240SpellId = 18_240;
    private const int AttackInFlyLegacyFlyFlag = 0x1B;

    [Fact]
    public async Task HandlerSectorBack18240HitsMonsterAndSobWithSourceDamageAndDoubledAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 18_240, boundX: 836, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 68_240, x: 838, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [CreateSectorBack18240Row()]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SectorBack18240SpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var query = CreateMagicQuery(
            monster,
            legacyEncodedQuery: false,
            spellId: SectorBack18240SpellId,
            x: 842,
            y: 584);
        var (_, spell) = ResolveSectorBack18240Spell(session, worldData);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            session,
            worldData,
            monster,
            monsterFamily,
            spell);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            session,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(842, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(SectorBack18240SpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(0, animation.LevelHu);

        var damageByUid = animation.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { monster.Uid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage * 2, damageByUid[monster.Uid]);
        Assert.Equal(expectedSobNpcDamage * 2, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.All(animation.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife - expectedMonsterDamage, visibleMonster.Hitpoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("85", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CrescentShadow SectorBack magic", output);
        Assert.Contains("spellId=18240", output);
        Assert.Contains("sort=69", output);
        Assert.Contains("range=12", output);
        Assert.Contains("width=2", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=0", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"totalAppliedDamage={expectedMonsterDamage + expectedSobNpcDamage}", output);
        Assert.Contains("source=SectorBack.cs case 10990/18240", output);
    }

    [Fact]
    public async Task HandlerSectorBack18240ScalesPlayerDamageBySevenAndKeepsHitFalseAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateSectorBack18240Row());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SectorBack18240SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "SectorBack18240Target",
            x: 836,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: TargetPlayerUid,
            X: 842,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: SectorBack18240SpellId);
        var (_, spell) = ResolveSectorBack18240Spell(casterSession, worldData);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            7U);
        Assert.True(expectedPlayerDamage > 0);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(SectorBack18240SpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.False(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("85", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CrescentShadow SectorBack magic", output);
        Assert.Contains("spellId=18240", output);
        Assert.Contains("sort=69", output);
        Assert.Contains("range=12", output);
        Assert.Contains("width=2", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains($"totalAppliedDamage={expectedPlayerDamage}", output);
        Assert.Contains("source=SectorBack.cs case 10990/18240", output);
    }

    [Fact]
    public async Task HandlerSectorBack18240AgainstWildPhoenixPlayerConsumesDefenderChargeAndKeepsZeroDamageTarget()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateSectorBack18240Row(),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SectorBack18240SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "SectorBack18240WildPhoenixTarget",
            x: 836,
            y: 584,
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
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        const uint expectedPlayerDamage = 1U / 7;
        Assert.Equal(0U, expectedPlayerDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: SectorBack18240SpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(SectorBack18240SpellId, animation.SpellId);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.False(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("85", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("previousCharges=2", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("damageOverride=1", output);
        Assert.Contains("WorldServer handled legacy CrescentShadow SectorBack magic", output);
        Assert.Contains("spellId=18240", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("totalAppliedDamage=0", output);
        Assert.Contains("source=SectorBack.cs case 10990/18240", output);
    }

    [Fact]
    public async Task HandlerNebulousHuntSectorBackCanHitFlyingPlayerWhenAttackInFlyIsTrue()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateNebulousHuntRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [NebulousHuntSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FlyingNebulousHuntTarget",
            x: 836,
            y: 584,
            minHitPoints: 100_000,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(AttackInFlyLegacyFlyFlag),
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var (_, spell) = ResolveNebulousHuntSpell(casterSession, worldData);
        Assert.True(spell.AttackInFly);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            7U);
        Assert.True(expectedPlayerDamage > 0);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: NebulousHuntSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.False(target.Hit);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("WorldServer handled legacy NebulousHunt SectorBack magic", log.ToString());
    }

    [Fact]
    public async Task HandlerSectorBack18240SkipsFlyingPlayerBecauseAttackInFlyIsFalse()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateSectorBack18240Row());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SectorBack18240SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "FlyingSectorBack18240Target",
            x: 836,
            y: 584,
            minHitPoints: 100_000,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(AttackInFlyLegacyFlyFlag),
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var (_, spell) = ResolveSectorBack18240Spell(casterSession, worldData);
        Assert.False(spell.AttackInFly);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: SectorBack18240SpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Empty(casterSpell.Targets);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("WorldServer handled legacy CrescentShadow SectorBack magic", log.ToString());
        Assert.Contains("playerTargets=0", log.ToString());
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveSectorBack18240Spell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            SectorBack18240SpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateSectorBack18240Row()
    {
        return MagicTypeParser.ParseLine(
            "18240@@18240@@69@@CrescentShadow@@1@@1@@1@@4@@0@@0@@30300@@0@@100@@2@@12@@12@@315@@0@@" +
            "900000@@10000@@40@@0@@61606606@@0@@0@@0@@0@@0@@0@@15@@1@@0@@0@@800@@0@@30103@@" +
            "10300@@8@@0@@0@@10@@0@@0@@0@@45@@0@@0@@0@@1000@@0@@0@@0@@0@@");
    }
}
