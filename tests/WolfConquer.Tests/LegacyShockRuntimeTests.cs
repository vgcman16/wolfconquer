using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CrackingShockSpellId = 15_720;
    private const int CrackingShockWildPhoenixFlag = 0x170;

    [Fact]
    public async Task HandlerCrackingShockThunderstrikerHitsMixedTargetsAroundUnchangedAttackPoint()
    {
        const ushort casterX = 830;
        const ushort casterY = 584;
        const ushort clickedX = 836;
        const ushort clickedY = casterY;
        const ushort centerX = clickedX;
        const ushort centerY = clickedY;
        const uint edgePlayerUid = TargetPlayerUid + 1;
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-crackingshock-spell-xp-" + Guid.NewGuid().ToString("N"),
            "PlayersSpells");
        try
        {
            var monsterFamily = CreateMonsterFamily();
            var centerSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: centerX, boundY: centerY);
            var edgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: centerX + 6, boundY: centerY);
            var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: centerX + 7, boundY: centerY);
            var sobNpc = CreateMoveLineSobNpc(id: 65_080, x: centerX - 1, y: centerY + 2);
            var worldData = CreateMagicCatalogCore(
                monsterFamily,
                [centerSpawn, edgeSpawn, outsideSpawn],
                [sobNpc],
                [
                    CreateCrackingShockRow(experience: 100),
                    CreateCrackingShockRow(level: 1, experience: 200),
                ]);
            var centerMonster = CreateRuntimeMonsterInstance(worldData, centerSpawn, monsterFamily);
            var edgeMonster = CreateRuntimeMonsterInstance(worldData, edgeSpawn, monsterFamily);
            var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
            var allMonsters = new[] { centerMonster, edgeMonster, outsideMonster };
            var monsterStates = new MonsterRuntimeStateStore();
            var sobNpcStates = new SobNpcRuntimeStateStore();
            var sessions = new WorldSessionStore();
            var casterSent = new List<byte[]>();
            var targetSent = new List<byte[]>();
            var edgeTargetSent = new List<byte[]>();
            using var log = new StringWriter();
            var casterConnection = CreateSendingConnection(casterSent);
            var targetConnection = CreateSendingConnection(targetSent);
            var edgeTargetConnection = CreateSendingConnection(edgeTargetSent);
            var casterSession = CreateSession(
                x: casterX,
                y: casterY,
                minMana: 50,
                strength: 300,
                spirit: 0,
                stamina: 100,
                spellIds: [CrackingShockSpellId],
                classCode: 9_001);
            var targetSession = CreateSession(
                uid: TargetPlayerUid,
                name: "Target",
                x: centerX + 4,
                y: centerY,
                minHitPoints: 5_000);
            var edgeTargetSession = CreateSession(
                uid: edgePlayerUid,
                name: "EdgeTarget",
                x: centerX + 6,
                y: centerY,
                minHitPoints: 5_000);
            sessions.Set(casterConnection, casterSession);
            sessions.Set(targetConnection, targetSession);
            sessions.Set(edgeTargetConnection, edgeTargetSession);
            var context = new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates,
                PlayerSpellsRoot: spellsRoot);

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                    centerMonster,
                    legacyEncodedQuery: false,
                    spellId: CrackingShockSpellId,
                    x: clickedX,
                    y: clickedY)).ToFrameBytes()));

            var spell = Assert.Single(ParseSpellAnimations(casterSent));
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(clickedX, spell.X);
            Assert.Equal(clickedY, spell.Y);
            Assert.Equal(CrackingShockSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            Assert.Equal(0, spell.LevelHu);

            var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
            Assert.Equal(
                new[]
                {
                    centerMonster.Uid,
                    edgeMonster.Uid,
                    TargetPlayerUid,
                    edgePlayerUid,
                    checked((uint)sobNpc.Id),
                }.Order().ToArray(),
                damageByUid.Keys.Order().ToArray());
            Assert.DoesNotContain(spell.Targets, target => target.Uid == outsideMonster.Uid);
            // The matching player radius is inclusive six, and ordinary
            // integer-scaled zero hits are still present in the animation.
            Assert.Equal(0U, damageByUid[TargetPlayerUid]);
            Assert.Equal(0U, damageByUid[edgePlayerUid]);
            Assert.All(spell.Targets, target =>
            {
                if (target.Uid != TargetPlayerUid && target.Uid != edgePlayerUid)
                {
                    Assert.True(target.Damage > 0);
                }
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });

            foreach (var monster in allMonsters)
            {
                Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
                if (damageByUid.TryGetValue(monster.Uid, out var damage))
                {
                    Assert.Equal(monster.MaxLife - damage, visibleMonster.Hitpoints);
                }
                else
                {
                    Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
                }
            }

            Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
            Assert.NotNull(updatedCasterSession);
            Assert.Equal(43, updatedCasterSession.Character.MinMana);
            Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);
            var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
            Assert.Equal(CrackingShockSpellId, updatedSpell.Id);
            Assert.Equal(1, updatedSpell.Level);
            Assert.Equal(0, updatedSpell.Experience);

            Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
            Assert.NotNull(updatedTargetSession);
            Assert.Equal(5_000 - (long)damageByUid[TargetPlayerUid], updatedTargetSession.Character.MinHitPoints);
            Assert.True(sessions.TryGet(edgeTargetConnection, out var updatedEdgeTargetSession));
            Assert.NotNull(updatedEdgeTargetSession);
            Assert.Equal(5_000, updatedEdgeTargetSession.Character.MinHitPoints);

            var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
            Assert.Equal(sobNpc.MaxLife - checked((int)damageByUid[checked((uint)sobNpc.Id)]), visibleSobNpc.Life);

            var output = log.ToString();
            Assert.Contains("WorldServer handled legacy Shock/CrackingShock magic", output);
            Assert.Contains("spellId=15720", output);
            Assert.Contains("sort=34", output);
            Assert.Contains("targetX=836 targetY=584", output);
            Assert.Contains("radius=6", output);
            Assert.Contains("monsterTargets=2", output);
            Assert.Contains("playerTargets=2", output);
            Assert.Contains("sobNpcTargets=1", output);
            Assert.Contains("source=Shock.cs", output);
        }
        finally
        {
            var tempRoot = Path.GetDirectoryName(spellsRoot);
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerCrackingShockAgainstWildPhoenixPlayerConsumesDefenderChargeAndScalesDamageToZero()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateCrackingShockRow(),
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
            minMana: 50,
            strength: 300,
            spirit: 0,
            stamina: 100,
            spellIds: [CrackingShockSpellId],
            classCode: 9_001);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 840,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(CrackingShockWildPhoenixFlag),
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
        var wildPhoenixRawDamage = 1U;
        var soulDamage = wildPhoenixRawDamage;
        var expectedPlayerDamage = soulDamage * 75 / 100;
        Assert.Equal(0U, expectedPlayerDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CrackingShockSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(checked((ushort)targetSession.Character.X), animation.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), animation.Y);
            Assert.Equal(CrackingShockSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, CrackingShockWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == CrackingShockWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy Shock/CrackingShock magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("totalAppliedDamage=0", output);
        Assert.Contains("source=Shock.cs", output);
    }

    [Fact]
    public async Task HandlerCrackingShockThunderstrikerWithoutTargetsSpendsResourcesAndSendsEmptyAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 850, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateCrackingShockRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            x: 830,
            y: 584,
            minMana: 50,
            strength: 300,
            spirit: 0,
            stamina: 100,
            spellIds: [CrackingShockSpellId],
            classCode: 9_001);
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
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: CrackingShockSpellId,
                x: 850,
                y: 584)).ToFrameBytes()));

        var emptyAnimation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Empty(emptyAnimation.Targets);
        Assert.Equal((850, 584), (emptyAnimation.X, emptyAnimation.Y));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(43, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Shock/CrackingShock magic", output);
        Assert.Contains("targetX=850 targetY=584", output);
        Assert.Contains("targets=0", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("source=Shock.cs", output);
    }

    [Fact]
    public async Task HandlerCrackingShockNonThunderstrikerSpendsResourcesThenReturnsWithoutAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateCrackingShockRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            strength: 300,
            spirit: 0,
            stamina: 100,
            spellIds: [CrackingShockSpellId],
            classCode: 142);
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
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: CrackingShockSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(43, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("WorldServer source-backed returned legacy Shock/CrackingShock magic", output);
        Assert.Contains("spellId=15720", output);
        Assert.Contains("class=142", output);
        Assert.Contains("source Shock.cs returns after CanUseSpell.Verified", output);
    }

    private static MagicType CreateCrackingShockRow(
        byte level = 0,
        uint experience = 0,
        ushort useMana = 7,
        byte useStamina = 5,
        uint damage = 120)
    {
        return CreateMagicRow(
            CrackingShockSpellId,
            sort: (byte)LegacyMagicSort.Shock,
            level: level,
            useMana: useMana,
            damage: damage,
            experience: experience,
            maxTargets: 6,
            range: 15,
            useStamina: useStamina,
            coolDown: 0,
            coldTime: 0);
    }
}
