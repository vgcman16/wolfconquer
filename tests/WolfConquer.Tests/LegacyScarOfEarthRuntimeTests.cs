using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ScarOfEarthRuntimeSpellId = 12_670;
    private const int ScarOfEarthRuntimeFlag = 180;

    [Fact]
    public async Task HandlerScarOfEarthSortSiblingSpendsResourcesThenSilentlyStops()
    {
        const ushort siblingSpellId = 31_082;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                siblingSpellId,
                sort: 82,
                useMana: 7,
                damage: 200,
                coolDown: 0,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [siblingSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
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
                SpellId: siblingSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(493, updated.Character.MinMana);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(sent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public async Task HandlerScarOfEarthHitsOnlyItsSourceLineAndAppliesNonRefreshingStatusesBeforeAnimation()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var insideSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 32_670,
            boundX: 833,
            boundY: 584);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 32_671,
            boundX: 833,
            boundY: 590);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [insideSpawn, outsideSpawn],
            CreateMagicRow(
                ScarOfEarthRuntimeSpellId,
                sort: 82,
                useMana: 7,
                damage: 200,
                experience: 1_000_000,
                range: 0,
                coolDown: 0,
                coldTime: 0,
                damage2: 77));
        var insideMonster = CreateRuntimeMonsterInstance(worldData, insideSpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var caster = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [ScarOfEarthRuntimeSpellId],
            attributes: new Dictionary<string, string>
            {
                [WorldPacketHandlers.RuntimeLegacyScarOfEarthRateRollAttribute] = "0",
            });
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "ScarTarget",
            x: 834,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
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
                SpellId: ScarOfEarthRuntimeSpellId)).ToFrameBytes()));

        Assert.True(monsterStates.TryGetVisibleInstance(
            insideMonster,
            DateTimeOffset.UtcNow,
            out var damagedInsideMonster));
        Assert.True(damagedInsideMonster.Hitpoints < insideMonster.MaxLife);
        Assert.True(monsterStates.TryGetVisibleInstance(
            outsideMonster,
            DateTimeOffset.UtcNow,
            out var untouchedOutsideMonster));
        Assert.Equal(outsideMonster.MaxLife, untouchedOutsideMonster.Hitpoints);
        var monsterFlags = monsterStates.CreateStatusFlagVector(
            insideMonster,
            DateTimeOffset.UtcNow,
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        Assert.True(IsLegacyStatusFlagSet(monsterFlags, ScarOfEarthRuntimeFlag));

        Assert.True(sessions.TryGet(targetConnection, out var damagedTarget));
        Assert.NotNull(damagedTarget);
        Assert.True(damagedTarget.Character.MinHitPoints < target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(damagedTarget.Character.Attributes, ScarOfEarthRuntimeFlag));
        Assert.True(damagedTarget.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyScarOfEarthExpiresAtUtcAttribute));
        Assert.True(damagedTarget.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute));

        var animationIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out _));
        Assert.Equal(casterSent.Count - 1, animationIndex);
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(ScarOfEarthRuntimeSpellId, animation.SpellId);
        Assert.Equal(835, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(0, animation.BombFlag);
        Assert.Contains(animation.Targets, entry =>
            entry.Uid == insideMonster.Uid && entry.Damage > 0 && entry.Hit);
        Assert.Contains(animation.Targets, entry =>
            entry.Uid == TargetPlayerUid && entry.Damage > 0 && !entry.Hit);
        Assert.DoesNotContain(animation.Targets, entry => entry.Uid == outsideMonster.Uid);
        Assert.Empty(ParseProficiencyPackets(casterSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(493, updatedCaster.Character.MinMana);
        Assert.Contains("monsterTargets=1 playerTargets=1", log.ToString());
    }

    [Fact]
    public void ScarOfEarthMonsterStatePreservesTimerOnRecastTicksInclusivelyAndExpiresBeforeFinalMonsterTick()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 32_672,
            boundX: 833,
            boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var store = new MonsterRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-07-19T20:00:00Z", CultureInfo.InvariantCulture);

        var first = store.ApplyScarOfEarth(
            monster,
            ScarOfEarthRuntimeFlag,
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(2),
            now,
            casterUid: PlayerUid,
            spellId: ScarOfEarthRuntimeSpellId,
            spellLevel: 0,
            damage: 77);
        Assert.True(first.Applied);
        Assert.False(first.AlreadyActive);
        Assert.Equal(now.AddSeconds(2), first.State!.Value.NextInvokeAtUtc);

        var recast = store.ApplyScarOfEarth(
            monster,
            ScarOfEarthRuntimeFlag,
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(2),
            now.AddSeconds(1),
            casterUid: TargetPlayerUid,
            spellId: ScarOfEarthRuntimeSpellId,
            spellLevel: 1,
            damage: 99);
        Assert.False(recast.Applied);
        Assert.True(recast.AlreadyActive);
        Assert.Equal(now.AddSeconds(2), recast.State!.Value.NextInvokeAtUtc);
        Assert.Equal(TargetPlayerUid, recast.State.Value.CasterUid);
        Assert.Equal(99U, recast.State.Value.Damage);

        var due = store.ConsumeScarOfEarthTick(
            monster,
            ScarOfEarthRuntimeFlag,
            now.AddSeconds(2),
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        Assert.True(due.Due);
        Assert.False(due.Expired);
        Assert.Equal(now.AddSeconds(4), due.State!.Value.NextInvokeAtUtc);

        var expired = store.ConsumeScarOfEarthTick(
            monster,
            ScarOfEarthRuntimeFlag,
            now.AddSeconds(12),
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        Assert.True(expired.Expired);
        Assert.False(expired.Due);
        Assert.False(IsLegacyStatusFlagSet(expired.StatusFlags, ScarOfEarthRuntimeFlag));

        var reapplied = store.ApplyScarOfEarth(
            monster,
            ScarOfEarthRuntimeFlag,
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(2),
            now.AddSeconds(20),
            casterUid: PlayerUid,
            spellId: ScarOfEarthRuntimeSpellId,
            spellLevel: 0,
            damage: 77);
        Assert.True(reapplied.Applied);
        var killed = store.ApplyDamage(monster, monster.MaxLife, now.AddSeconds(21));
        Assert.True(killed.Killed);
        Assert.False(store.ConsumeScarOfEarthTick(
            monster,
            ScarOfEarthRuntimeFlag,
            now.AddSeconds(22),
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength).Due);
        Assert.True(store.TryConsumeRespawnedInstance(monster, now.AddSeconds(100), out var respawned));
        var afterRespawn = store.ApplyScarOfEarth(
            respawned,
            ScarOfEarthRuntimeFlag,
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(2),
            now.AddSeconds(100),
            casterUid: PlayerUid,
            spellId: ScarOfEarthRuntimeSpellId,
            spellLevel: 0,
            damage: 77);
        Assert.True(afterRespawn.Applied);
        Assert.False(afterRespawn.AlreadyActive);
    }

    [Fact]
    public async Task DueScarOfEarthPlayerFinalBoundaryRemovesStatusThenSendsVictimAuthoredBombOneTick()
    {
        var now = DateTimeOffset.Parse("2026-07-19T20:30:00Z", CultureInfo.InvariantCulture);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var victimSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var victimConnection = CreateSendingConnection(victimSent);
        var caster = CreateSession(x: 830, y: 584, spellIds: [ScarOfEarthRuntimeSpellId]);
        var victim = CreateSession(
            uid: TargetPlayerUid,
            name: "ScarVictim",
            x: 831,
            y: 584,
            minHitPoints: 5_000,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(ScarOfEarthRuntimeFlag),
                [WorldPacketHandlers.RuntimeLegacyScarOfEarthExpiresAtUtcAttribute] = now.ToString("O", CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute] = now.ToString("O", CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyScarOfEarthCasterUidAttribute] = PlayerUid.ToString(CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyScarOfEarthSpellIdAttribute] = ScarOfEarthRuntimeSpellId.ToString(CultureInfo.InvariantCulture),
                [WorldPacketHandlers.RuntimeLegacyScarOfEarthSpellLevelAttribute] = "0",
                [WorldPacketHandlers.RuntimeLegacyScarOfEarthDamage2Attribute] = "77",
            });
        sessions.Set(casterConnection, caster);
        sessions.Set(victimConnection, victim);
        var context = new WorldPacketHandlerContext(
            victimConnection,
            log,
            victim,
            Sessions: sessions);

        Assert.True(await WorldPacketHandlers.TrySendDueLegacyPlayerScarOfEarthTickAsync(
            context,
            now,
            CancellationToken.None));

        Assert.True(sessions.TryGet(victimConnection, out var updatedVictim));
        Assert.NotNull(updatedVictim);
        Assert.Equal(4_923, updatedVictim.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(updatedVictim.Character.Attributes, ScarOfEarthRuntimeFlag));
        Assert.False(updatedVictim.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyScarOfEarthExpiresAtUtcAttribute));
        Assert.False(updatedVictim.Character.Attributes.ContainsKey(
            WorldPacketHandlers.RuntimeLegacyScarOfEarthNextInvokeAtUtcAttribute));

        var status = Assert.Single(ParseMsgUpdates(victimSent), update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(status.Updates, entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, ScarOfEarthRuntimeFlag));

        var animation = Assert.Single(ParseSpellAnimations(victimSent));
        Assert.Equal(TargetPlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(ScarOfEarthRuntimeSpellId, animation.SpellId);
        Assert.Equal(1, animation.BombFlag);
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, animationTarget.Uid);
        Assert.Equal(77U, animationTarget.Damage);
        Assert.True(animationTarget.Hit);
        Assert.Equal(0U, animationTarget.Unknown);
    }

    [Fact]
    public void ClassifyScarOfEarthAndHellVortexOnlyForTheirExactMatchingSourceIds()
    {
        var scar = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)82, ScarOfEarthRuntimeSpellId);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, scar.Status);
        Assert.True(scar.Supported);
        Assert.Equal("scarofearth-runtime", scar.NextSeam);
        Assert.Contains("non-refreshing flag 180", scar.Reason);
        Assert.Contains("SectorPasive.cs", scar.SourceEvidence);

        var adjacentScar = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)82, 12_671);
        Assert.False(adjacentScar.Supported);
        Assert.NotEqual("scarofearth-runtime", adjacentScar.NextSeam);

        var hellVortex = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.HellVortex, 16_370);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, hellVortex.Status);
        Assert.Equal("hellvortex-16370-runtime", hellVortex.NextSeam);
        Assert.Contains("selected monster UID", hellVortex.Reason);
        Assert.Contains("no pushback or movement", hellVortex.Reason);
        Assert.Contains("bomb flag 0", hellVortex.Reason);
        Assert.Contains("not Source7770", hellVortex.SourceEvidence);

        var adjacentHellVortex = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.HellVortex, 16_371);
        Assert.NotEqual("hellvortex-16370-runtime", adjacentHellVortex.NextSeam);
    }
}
