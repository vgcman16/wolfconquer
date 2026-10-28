using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort AttackStatus7000SpellId = 7_000;
    private const int AttackStatus7000WildPhoenixFlag = 0x170;

    [Fact]
    public async Task HandlerAttackStatus7000HitsVisibleMonsterPlayerAndSobInsideStrictSourceRange()
    {
        var monsterFamily = CreateMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584);
        var edgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 837, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_700, x: 836, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [inRangeSpawn, edgeSpawn],
            [sobNpc],
            [
                CreateAttackStatus7000Row(range: 7, experience: 1),
                CreateAttackStatus7000Row(level: 1, range: 7, experience: 100),
            ]);
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
        var edgeMonster = CreateRuntimeMonsterInstance(worldData, edgeSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
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
            spellIds: [AttackStatus7000SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 835,
            y: 584,
            minHitPoints: 5_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                inRangeMonster,
                legacyEncodedQuery: false,
                spellId: AttackStatus7000SpellId,
                x: 840,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(840, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(AttackStatus7000SpellId, spell.SpellId);

        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Contains(inRangeMonster.Uid, damageByUid.Keys);
        Assert.Contains(TargetPlayerUid, damageByUid.Keys);
        Assert.Contains(checked((uint)sobNpc.Id), damageByUid.Keys);
        Assert.DoesNotContain(edgeMonster.Uid, damageByUid.Keys);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(inRangeMonster.MaxLife - damageByUid[inRangeMonster.Uid], damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(edgeMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(edgeMonster.MaxLife, untouchedMonster.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)damageByUid[TargetPlayerUid], updatedTargetSession.Character.MinHitPoints);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(50_000 - (int)damageByUid[checked((uint)sobNpc.Id)], visibleSobNpc.Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(43, updatedCasterSession.Character.MinMana);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(AttackStatus7000SpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AttackStatus 7000 magic", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("source=AttackStatus.cs case 7000 Range.OnMonster/OnPlayer/OnNpcs slice", output);
    }

    [Fact]
    public async Task HandlerAttackStatus7000MonsterDamageAppliesMonsterHunterBonus()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateAttackStatus7000Row(range: 7),
            CreateMagicRow(MonsterHunterSpellId, sort: 1, useMana: 0, damage: 30_300, coldTime: 0));
        var baselineDamage = await RunAttackStatus7000MonsterDamageAsync(
            worldData,
            monsterFamily,
            spawn,
            CreateSession(
                x: 830,
                y: 584,
                minMana: 50,
                strength: 300,
                spellIds: [AttackStatus7000SpellId]));
        var boostedDamage = await RunAttackStatus7000MonsterDamageAsync(
            worldData,
            monsterFamily,
            spawn,
            CreateSession(
                x: 830,
                y: 584,
                minMana: 50,
                strength: 300,
                spellEntries:
                [
                    CreatePlayerSpell(AttackStatus7000SpellId),
                    CreatePlayerSpell(MonsterHunterSpellId),
                ]));

        Assert.True(
            boostedDamage > baselineDamage * 3,
            $"Expected MonsterHunter to add source Range.OnMonster damage percent. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public async Task HandlerAttackStatus7000PlayerOnlyDamageDoesNotAdvanceSpellExperience()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateAttackStatus7000Row(range: 7, experience: 1),
                CreateAttackStatus7000Row(level: 1, range: 7, experience: 100),
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
            spellIds: [AttackStatus7000SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 835,
            y: 584,
            minHitPoints: 5_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 840,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: AttackStatus7000SpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.True(target.Damage > 0);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)target.Damage, updatedTargetSession.Character.MinHitPoints);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        var playerSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(AttackStatus7000SpellId, playerSpell.Id);
        Assert.Equal(0, playerSpell.Level);
        Assert.Equal(0, playerSpell.Experience);

        var output = log.ToString();
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("totalMonsterDamage=0", output);
        Assert.Contains("totalSobNpcExperienceDamage=0", output);
        Assert.DoesNotContain("WorldServer updated legacy player magic spell", output);
    }

    [Fact]
    public async Task HandlerAttackStatus7000AgainstWildPhoenixPlayerConsumesDefenderChargeAndDealsOneDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateAttackStatus7000Row(range: 7, experience: 1),
                CreateAttackStatus7000Row(level: 1, range: 7, experience: 100),
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
            spellIds: [AttackStatus7000SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 835,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(AttackStatus7000WildPhoenixFlag),
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
        var expectedPlayerDamage = 1U;
        Assert.Equal(1U, expectedPlayerDamage);

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
                SpellId: AttackStatus7000SpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(checked((ushort)targetSession.Character.X), animation.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), animation.Y);
            Assert.Equal(AttackStatus7000SpellId, animation.SpellId);
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
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, AttackStatus7000WildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == AttackStatus7000WildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(43, updatedCasterSession.Character.MinMana);
        var playerSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(AttackStatus7000SpellId, playerSpell.Id);
        Assert.Equal(0, playerSpell.Level);
        Assert.Equal(0, playerSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy AttackStatus 7000 magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("totalMonsterDamage=0", output);
        Assert.Contains("totalSobNpcExperienceDamage=0", output);
    }

    private static MagicType CreateAttackStatus7000Row(
        byte level = 0,
        byte range = 7,
        uint experience = 0)
    {
        return CreateMagicRow(
            AttackStatus7000SpellId,
            sort: 16,
            level: level,
            useMana: 7,
            damage: 120,
            experience: experience,
            range: range,
            coldTime: 0);
    }

    private static async Task<uint> RunAttackStatus7000MonsterDamageAsync(
        WorldDataCatalog worldData,
        Monster monsterFamily,
        MonsterSpawn spawn,
        WorldSession session)
    {
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
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
                    spellId: AttackStatus7000SpellId,
                    x: 840,
                    y: 584)).ToFrameBytes()))
            .ConfigureAwait(false);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - target.Damage, damagedMonster.Hitpoints);
        Assert.Contains("WorldServer handled legacy AttackStatus 7000 magic", log.ToString());
        return target.Damage;
    }
}
