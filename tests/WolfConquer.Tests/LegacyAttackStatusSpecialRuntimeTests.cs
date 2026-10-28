using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort AttackStatus10800SpellId = 10_800;
    private const ushort AttackStatus15880SpellId = 15_880;
    private const int AttackStatus10800ShieldOfTruthFlag = 0x138;
    private const int AttackStatus15880SongofPhoenixFlag = 0x101;

    [Fact]
    public async Task HandlerAttackStatus10800AppliesShieldOfTruthToInRangeTeamMembers()
    {
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateMagicRow(AttackStatus10800SpellId, sort: 16, useMana: 9, experience: 1, coldTime: 0),
                CreateMagicRow(AttackStatus10800SpellId, sort: 16, level: 1, useMana: 9, experience: 100, coldTime: 0),
            ]);
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var casterSent = new List<byte[]>();
        var memberSent = new List<byte[]>();
        var farMemberSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var memberConnection = CreateSendingConnection(memberSent);
        var farMemberConnection = CreateSendingConnection(farMemberSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 50,
            spellIds: [AttackStatus10800SpellId],
            attributes: new Dictionary<string, string> { ["Dodge"] = "100" });
        var memberSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Member",
            x: 835,
            y: 584,
            attributes: new Dictionary<string, string> { ["Dodge"] = "100" });
        var farMemberSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "FarMember",
            x: 900,
            y: 584,
            attributes: new Dictionary<string, string> { ["Dodge"] = "100" });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(memberConnection, memberSession);
        sessions.Set(farMemberConnection, farMemberSession);
        teamStates.CreateTeam(casterConnection, PlayerUid);
        Assert.True(teamStates.TryAddMember(casterConnection, memberConnection, TargetPlayerUid));
        Assert.True(teamStates.TryAddMember(casterConnection, farMemberConnection, TargetPlayerUid + 1));
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: 840,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: AttackStatus10800SpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(41, updatedCasterSession.Character.MinMana);
        Assert.Equal("160", updatedCasterSession.Character.Attributes["Dodge"]);
        Assert.True(updatedCasterSession.AlternateEquipmentActive);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, AttackStatus10800ShieldOfTruthFlag));
        Assert.Contains("LegacyShieldofTruthExpiresAtUtc", updatedCasterSession.Character.Attributes.Keys);
        Assert.Equal(1, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Level);

        Assert.True(sessions.TryGet(memberConnection, out var updatedMemberSession));
        Assert.NotNull(updatedMemberSession);
        Assert.Equal("160", updatedMemberSession.Character.Attributes["Dodge"]);
        Assert.True(updatedMemberSession.AlternateEquipmentActive);
        Assert.True(HasLegacyStatusFlag(updatedMemberSession.Character.Attributes, AttackStatus10800ShieldOfTruthFlag));
        Assert.Contains("LegacyShieldofTruthExpiresAtUtc", updatedMemberSession.Character.Attributes.Keys);

        Assert.True(sessions.TryGet(farMemberConnection, out var updatedFarMemberSession));
        Assert.NotNull(updatedFarMemberSession);
        Assert.Equal("100", updatedFarMemberSession.Character.Attributes["Dodge"]);
        Assert.False(updatedFarMemberSession.AlternateEquipmentActive);
        Assert.False(HasLegacyStatusFlag(updatedFarMemberSession.Character.Attributes, AttackStatus10800ShieldOfTruthFlag));

        Assert.Contains(
            ParseShowEquipmentRefreshes(casterSent),
            packet => packet.Action == LegacyShowEquipmentPacket.ShowAction && packet.Alternante == 1);
        Assert.Contains(
            ParseShowEquipmentRefreshes(memberSent),
            packet => packet.Action == LegacyShowEquipmentPacket.ShowAction && packet.Alternante == 1);
        Assert.DoesNotContain(
            ParseShowEquipmentRefreshes(farMemberSent),
            packet => packet.Action == LegacyShowEquipmentPacket.ShowAction && packet.Alternante == 1);

        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, AttackStatus10800ShieldOfTruthFlag)));
        Assert.Contains(
            ParseMsgUpdates(memberSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, AttackStatus10800ShieldOfTruthFlag)));
        Assert.DoesNotContain(
            ParseMsgUpdates(farMemberSent),
            update => update.Uid == TargetPlayerUid + 1 &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, AttackStatus10800ShieldOfTruthFlag)));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(AttackStatus10800SpellId, spell.SpellId);
        Assert.Equal(840, spell.X);
        Assert.Equal(584, spell.Y);
        var animationTarget = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, animationTarget.Uid);
        Assert.Equal(0U, animationTarget.Damage);
        Assert.True(animationTarget.Hit);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AttackStatus 10800 magic", output);
        Assert.Contains("teamMembersApplied=2", output);
        Assert.Contains("teamMembersOutOfRange=1", output);
        Assert.Contains("equipmentRefreshPackets=2", output);
        Assert.Contains("statusFlagRefreshPackets=2", output);
        Assert.Contains("source=AttackStatus.cs case 10800", output);
        Assert.DoesNotContain("PARITY-GAP legacy AttackStatus 10800", output);
    }

    [Fact]
    public async Task HandlerAttackStatus15880UsesMatchingCasterCenteredLoopsScalingProgressOrderAndNoStatus()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 832, boundY: 584);
        var boundarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 835, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_880, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [inRangeSpawn, boundarySpawn],
            [sobNpc],
            [
                CreateAttackStatus15880Row(experience: 1),
                CreateAttackStatus15880Row(level: 1, experience: 100),
            ]);
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
        var boundaryMonster = CreateRuntimeMonsterInstance(worldData, boundarySpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var playerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var playerConnection = CreateSendingConnection(playerSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minMana: 50,
            strength: 300,
            spellIds: [AttackStatus15880SpellId]);
        var playerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 833,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(playerConnection, playerSession);
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
        var (_, spell) = ResolveAttackStatus15880Spell(casterSession, worldData);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            playerSession,
            spell,
            worldData,
            1U) * 7 / 13;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacyAttackStatus15880SobNpcRangeDamage",
            casterSession,
            spell,
            worldData) * 50 / 100;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: boundaryMonster.Uid,
                X: 840,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: AttackStatus15880SpellId)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(840, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(AttackStatus15880SpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(0, animation.LevelHu);
        Assert.Equal(
            new[] { inRangeMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) },
            animation.Targets.Select(target => target.Uid).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        var runtimeMonsterDamage = inRangeMonster.MaxLife - damagedMonster.Hitpoints;
        Assert.True(runtimeMonsterDamage > 0);
        Assert.Equal(runtimeMonsterDamage * 60 / 100, animation.Targets[0].Damage);
        Assert.True(animation.Targets[0].Hit);

        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.True(animation.Targets[1].Hit);
        Assert.Equal(MsgAttackEffect.None, animation.Targets[1].Effect);
        Assert.True(sessions.TryGet(playerConnection, out var updatedPlayerSession));
        Assert.NotNull(updatedPlayerSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedPlayerSession.Character.MinHitPoints);

        Assert.Equal(expectedSobNpcDamage, animation.Targets[2].Damage);
        Assert.True(animation.Targets[2].Hit);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(monsterStates.TryGetVisibleInstance(boundaryMonster, DateTimeOffset.UtcNow, out var untouchedBoundaryMonster));
        Assert.Equal(boundaryMonster.MaxLife, untouchedBoundaryMonster.Hitpoints);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(50, updatedCasterSession.Character.MinMana);
        Assert.Equal(1, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Level);
        AssertNoAttackStatus15880SongofPhoenixState(updatedCasterSession, casterSent);

        var progressFrameIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacySpellPacket.TryParse(ReadFrame(casterSent[index]), out var packet) &&
                packet.Id == AttackStatus15880SpellId);
        var animationFrameIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out var packet) &&
                packet.SpellId == AttackStatus15880SpellId);
        Assert.True(progressFrameIndex < animationFrameIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AttackStatus 15880 magic", output);
        Assert.Contains("targets=3", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("songOfPhoenixStatusApplied=false", output);
        Assert.Contains("source=matching DecompiledSource AttackStatus.cs case 15880 lines 108-153", output);
    }

    [Fact]
    public async Task HandlerAttackStatus15880MonsterRangeDamageInheritsCase7000MonsterHunterBonus()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateAttackStatus15880Row(),
            CreateMagicRow(MonsterHunterSpellId, sort: 1, useMana: 0, damage: 30_300, coldTime: 0));
        var baselineDamage = await RunAttackStatus15880MonsterDamageAsync(
            worldData,
            monsterFamily,
            spawn,
            CreateSession(
                x: 830,
                y: 584,
                minMana: 50,
                strength: 300,
                spellIds: [AttackStatus15880SpellId]));
        var learnedMonsterHunterDamage = await RunAttackStatus15880MonsterDamageAsync(
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
                    CreatePlayerSpell(AttackStatus15880SpellId),
                    CreatePlayerSpell(MonsterHunterSpellId),
                ]));

        Assert.True(
            learnedMonsterHunterDamage > baselineDamage,
            $"Expected shared case-7000 Range.OnMonster to apply MonsterHunter. Baseline={baselineDamage}, boosted={learnedMonsterHunterDamage}.");
    }

    [Fact]
    public void CalculateAttackStatus15880SobNpcRangeDamageDelegatesToCase7000RangeSemantics()
    {
        var session = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [AttackStatus15880SpellId]);
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            [],
            [],
            [CreateAttackStatus15880Row()]);
        var (_, spell) = ResolveAttackStatus15880Spell(session, worldData);

        var case7000RangeDamage = InvokePrivateUInt32(
            "CalculateLegacyAttackStatus7000SobNpcRangeDamage",
            session,
            spell,
            worldData);
        var case15880RangeDamage = InvokePrivateUInt32(
            "CalculateLegacyAttackStatus15880SobNpcRangeDamage",
            session,
            spell,
            worldData);

        Assert.Equal(case7000RangeDamage, case15880RangeDamage);
    }

    [Fact]
    public void CalculateAttackStatus15880PlayerTargetUsesMatchingPhysicalDamageOnHumanSelector()
    {
        var attacker = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [AttackStatus15880SpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 833,
            y: 584);
        var baselineWorldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            [],
            [],
            [CreateAttackStatus15880Row()]);
        var boostedWorldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            [],
            [],
            [CreateAttackStatus15880Row(damageOnHuman: 30_180)]);
        var (_, baselineSpell) = ResolveAttackStatus15880Spell(attacker, baselineWorldData);
        var (_, boostedSpell) = ResolveAttackStatus15880Spell(attacker, boostedWorldData);
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(135U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(180U, boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected matching Physical.OnPlayer to select DamageOnHuman % 1000. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public async Task HandlerAttackStatus15880WildPhoenixPlayerScalesOneDamageToZeroAndAppliesNoSongofPhoenixStatus()
    {
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            [],
            [],
            [
                CreateAttackStatus15880Row(),
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
            spellIds: [AttackStatus15880SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 833,
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

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 840,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: AttackStatus15880SpellId)).ToFrameBytes()));

        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterAnimation, targetAnimation })
        {
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveFrameIndex = Assert.Single(
            Enumerable.Range(0, targetSent.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(targetSent[index]), out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == LegacyWildPhoenixFlag));
        var animationFrameIndex = Assert.Single(
            Enumerable.Range(0, targetSent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(targetSent[index]), out var animation) &&
                animation.SpellId == AttackStatus15880SpellId);
        Assert.True(archiveFrameIndex < animationFrameIndex);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(50, updatedCasterSession.Character.MinMana);
        AssertNoAttackStatus15880SongofPhoenixState(updatedCasterSession, casterSent);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy AttackStatus 15880 magic", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("songOfPhoenixStatusApplied=false", output);
    }

    private static async Task<uint> RunAttackStatus15880MonsterDamageAsync(
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
        var (_, runtimeSpell) = ResolveAttackStatus15880Spell(session, worldData);
        var expectedRangeDamage = InvokePrivateUInt32(
            "CalculateLegacyAttackStatus15880MonsterRangeDamage",
            session,
            worldData,
            monster,
            monsterFamily,
            runtimeSpell);

        await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid,
                    OpponentUid: 0,
                    X: 840,
                    Y: 584,
                    AttackType: MsgAttackType.Magic,
                    Damage: 0,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0,
                    SpellId: AttackStatus15880SpellId)).ToFrameBytes()))
            .ConfigureAwait(false);

        var animation = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        var runtimeDamage = monster.MaxLife - damagedMonster.Hitpoints;
        Assert.Equal(expectedRangeDamage, runtimeDamage);
        Assert.Equal(runtimeDamage * 60 / 100, target.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        AssertNoAttackStatus15880SongofPhoenixState(updatedSession, sent);
        return runtimeDamage;
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveAttackStatus15880Spell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            AttackStatus15880SpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateAttackStatus15880Row(
        byte level = 0,
        uint experience = 0,
        int damageOnHuman = 30_135,
        int damage3 = 10)
    {
        return CreateMagicRow(
            AttackStatus15880SpellId,
            sort: 16,
            level: level,
            useMana: 0,
            damage: 30_300,
            experience: experience,
            range: 5,
            coldTime: 0,
            duration: 20,
            damageOnHuman: damageOnHuman,
            damage3: damage3);
    }

    private static void AssertNoAttackStatus15880SongofPhoenixState(
        WorldSession session,
        IReadOnlyList<byte[]> frames)
    {
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, AttackStatus15880SongofPhoenixFlag));
        Assert.DoesNotContain("SongofPhoenixPower", session.Character.Attributes.Keys);
        Assert.DoesNotContain("LegacySongofPhoenixPower", session.Character.Attributes.Keys);
        Assert.DoesNotContain("SongofPhoenixExpiresAtUtc", session.Character.Attributes.Keys);
        Assert.DoesNotContain("LegacySongofPhoenixExpiresAtUtc", session.Character.Attributes.Keys);
        Assert.DoesNotContain(
            ParseMsgUpdates(frames),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, AttackStatus15880SongofPhoenixFlag)));
    }

    private static IReadOnlyList<LegacyShowEquipmentPacket> ParseShowEquipmentRefreshes(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyShowEquipmentPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
