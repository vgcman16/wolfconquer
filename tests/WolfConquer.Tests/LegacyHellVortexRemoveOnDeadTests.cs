using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint HellVortexRemovalMaxLife = 1_000_000;

    [Theory]
    [InlineData(14_334, true)]
    [InlineData(14_335, true)]
    [InlineData(14_334, false)]
    [InlineData(14_335, false)]
    public async Task HandlerHellVortexDeadQuestCenterLeavesMapButOrdinarySameFamilyCorpseRemains(
        int familyId, bool questSpawn)
    {
        using var fixture = await CreateHellVortexRemovalFixtureAsync(familyId, questSpawn);
        var deathAt = DateTimeOffset.UtcNow;
        var death = fixture.States.ApplyDamage(fixture.Center, fixture.Center.MaxLife, deathAt);
        Assert.True(death.Killed);
        Assert.Equal(fixture.Center.MaxLife, death.DamageApplied);
        Assert.Equal(0U, death.RemainingHitpoints);
        await CastHellVortexRemovalAsync(fixture);
        uint[] expected = questSpawn ? [] : fixture.OrderedTargets
            .Where(target => target.Monster.Uid != fixture.Center.Uid)
            .Select(target => target.Monster.Uid).ToArray();
        AssertHellVortexRemovalCast(fixture, expected, centerKilledByCast: false);
        AssertHellVortexRemovalRegistration(fixture, removed: questSpawn);

        // LeaveMap is immediate, but the existing delayed RemoveEntity publication remains consumable.
        Assert.Contains(fixture.States.GetDynamicMonsterLifecycleInstances(1_020, 0),
            monster => monster.Uid == fixture.Center.Uid);
        Assert.True(fixture.States.TryConsumeDeathRemovalInstance(fixture.Center,
            deathAt.AddSeconds(9), out var removed));
        Assert.Equal(fixture.Center.Uid, removed.Uid);
        Assert.Equal(0U, removed.Hitpoints);
        Assert.False(fixture.States.TryConsumeDeathRemovalInstance(fixture.Center,
            deathAt.AddSeconds(10), out _));
        AssertHellVortexRemovalRegistration(fixture, removed: questSpawn);
        if (questSpawn)
        {
            Assert.False(fixture.States.TryConsumeRespawnedInstance(fixture.Center,
                deathAt.AddDays(1), out _));
            Assert.False(fixture.States.TryGetVisibleInstance(fixture.Center,
                deathAt.AddDays(1), out _));
            var replay = fixture.States.ApplyDamage(fixture.Center, 1, deathAt.AddDays(1));
            Assert.True(replay.AlreadyDead);
            Assert.False(replay.Killed);
            Assert.Equal(0U, replay.DamageApplied);
            Assert.Equal(0U, replay.RemainingHitpoints);
            AssertHellVortexRemovalRegistration(fixture, removed: true);
        }
    }

    [Theory]
    [InlineData(14_334, true)]
    [InlineData(14_335, true)]
    [InlineData(14_334, false)]
    [InlineData(14_335, false)]
    public async Task HandlerHellVortexCenterKilledFirstStopsOnlyRemoveOnDeadFanout(
        int familyId, bool questSpawn)
    {
        using var fixture = await CreateHellVortexRemovalFixtureAsync(familyId, questSpawn);
        Assert.Equal(fixture.Center.Uid, fixture.OrderedTargets[0].Monster.Uid);
        PrimeHellVortexRemovalCenter(fixture);

        await CastHellVortexRemovalAsync(fixture);

        // HellVortex.cs:31 resolves the raw selected center again for each monster recipient.
        uint[] expected = questSpawn ? [fixture.Center.Uid] : fixture.OrderedTargets
            .Select(target => target.Monster.Uid).ToArray();
        AssertHellVortexRemovalCast(fixture, expected, centerKilledByCast: true);
        AssertHellVortexRemovalRegistration(fixture, removed: questSpawn);
    }

    [Fact]
    public async Task HandlerHellVortexGuardAfterRemovedCenterDoesNotFlashCaster()
    {
        using var fixture = await CreateHellVortexRemovalFixtureAsync(14_335, questSpawn: true,
            laterGuard: true);
        Assert.Equal(fixture.Center.Uid, fixture.OrderedTargets[0].Monster.Uid);
        Assert.Equal("Guard1", fixture.OrderedTargets[^1].Family.Name);
        PrimeHellVortexRemovalCenter(fixture);

        await CastHellVortexRemovalAsync(fixture);

        Assert.True(fixture.Context.Sessions!.TryGet(fixture.Context.Connection, out var caster));
        Assert.NotNull(caster);
        Assert.False(HasLegacyStatusFlag(caster.Character.Attributes, LegacyFlashingNameFlag));
        Assert.DoesNotContain(ParseMsgUpdates(fixture.Sent), update => update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null && IsLegacyStatusFlagSet(entry.Values, LegacyFlashingNameFlag)));
        Assert.DoesNotContain("WorldServer applied legacy guard attack flashing-name", ((StringWriter)fixture.Context.Log).ToString());
        AssertHellVortexRemovalCast(fixture, [fixture.Center.Uid], centerKilledByCast: true);
        AssertHellVortexRemovalRegistration(fixture, removed: true);
    }

    [Fact]
    public async Task HandlerHellVortexRecipientBeforeRemovedCenterKeepsItsEarlierHit()
    {
        using var fixture = await CreateHellVortexRemovalFixtureAsync(14_335, questSpawn: true,
            earlierRecipient: true);
        Assert.NotEqual(fixture.Center.Uid, fixture.OrderedTargets[0].Monster.Uid);
        Assert.Equal(fixture.Center.Uid, fixture.OrderedTargets[1].Monster.Uid);
        PrimeHellVortexRemovalCenter(fixture);

        await CastHellVortexRemovalAsync(fixture);

        AssertHellVortexRemovalCast(fixture,
            [fixture.OrderedTargets[0].Monster.Uid, fixture.Center.Uid], centerKilledByCast: true);
        AssertHellVortexRemovalRegistration(fixture, removed: true);
    }

    private static void PrimeHellVortexRemovalCenter(HellVortexRemovalFixture fixture)
    {
        var priming = fixture.States.ApplyDamage(fixture.Center, fixture.Center.MaxLife - 1,
            DateTimeOffset.UtcNow);
        Assert.False(priming.Killed);
        Assert.Equal(1U, priming.RemainingHitpoints);
        Assert.True(fixture.States.TryGetVisibleInstance(fixture.Center, DateTimeOffset.UtcNow, out var current));
        Assert.Equal(1U, current.Hitpoints);
        Assert.InRange(CalculateHellVortexRemovalDamage(fixture, fixture.OrderedTargets
            .Single(target => target.Monster.Uid == fixture.Center.Uid)), 2U, 99_999U);
    }

    private static void AssertHellVortexRemovalRegistration(HellVortexRemovalFixture fixture, bool removed)
    {
        var selected = InvokeHellVortexSelectedMonster(fixture.Context, fixture.Caster, fixture.Center.Uid);
        if (removed)
        {
            Assert.Null(selected);
        }
        else
        {
            Assert.NotNull(selected);
            Assert.Equal(0U, selected.Hitpoints);
        }

        Assert.Equal(!removed, fixture.States.TryGetDynamicMonsterByUid(1_020, 0,
            fixture.Center.Uid, out _, out _));
        Assert.False(fixture.States.TryGetVisibleInstance(fixture.Center, DateTimeOffset.UtcNow, out _));
        // Instance.cs reserves this identity even after LeaveMap; otherwise replay can rematerialize it.
        Assert.True(fixture.States.TryGetDynamicMonsterBySpawnIdentity(1_020, 0,
            fixture.Center.GeneratorId, fixture.Center.SpawnIndex, out var reserved, out _));
        Assert.Equal(fixture.Center.Uid, reserved.Uid);
    }

    private static async Task CastHellVortexRemovalAsync(HellVortexRemovalFixture fixture)
    {
        await PacketHandlers.HandleAsync(fixture.Context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: fixture.Center.Uid, X: 900, Y: 901,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                SpellId: HellVortexSpellId)).ToFrameBytes()));
    }

    private static void AssertHellVortexRemovalCast(HellVortexRemovalFixture fixture,
        IReadOnlyList<uint> expectedHitUids, bool centerKilledByCast)
    {
        var animation = Assert.Single(ParseSpellAnimations(fixture.Sent));
        var animationIndex = SingleFrameIndex(fixture.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        Assert.Equal((ushort)2239, ReadFrame(fixture.Sent[animationIndex]).Header.Opcode);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((900, 901), (animation.X, animation.Y));
        Assert.Equal(HellVortexSpellId, animation.SpellId);
        Assert.Equal(3, animation.LevelHu);
        Assert.Equal(0, animation.BombFlag);
        Assert.Equal(expectedHitUids, animation.Targets.Select(target => target.Uid));
        foreach (var hit in animation.Targets)
        {
            var target = fixture.OrderedTargets.Single(candidate => candidate.Monster.Uid == hit.Uid);
            Assert.Equal(CalculateHellVortexRemovalDamage(fixture, target), hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
        }

        foreach (var target in fixture.OrderedTargets.Where(target => target.Monster.Uid != fixture.Center.Uid))
        {
            Assert.True(fixture.States.TryGetDynamicMonsterByUid(1_020, 0, target.Monster.Uid, out _, out _));
            Assert.True(fixture.States.TryGetVisibleInstance(target.Monster, DateTimeOffset.UtcNow, out var current));
            var expectedDamage = expectedHitUids.Contains(target.Monster.Uid)
                ? CalculateHellVortexRemovalDamage(fixture, target) : 0U;
            Assert.Equal(HellVortexRemovalMaxLife - expectedDamage, current.Hitpoints);
            if (expectedDamage != 0)
            {
                var hpIndex = SingleFrameIndex(fixture.Sent, frame =>
                    LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == target.Monster.Uid &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                        entry.Value == current.Hitpoints));
                Assert.Equal((ushort)2176, ReadFrame(fixture.Sent[hpIndex]).Header.Opcode);
                Assert.True(hpIndex < animationIndex);
            }
        }

        Assert.Equal(expectedHitUids.Where(uid => uid != fixture.Center.Uid),
            ParseMsgUpdates(fixture.Sent).Where(update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Hitpoints)).Select(update => update.Uid));
        if (centerKilledByCast)
        {
            var death = Assert.Single(ParseDeathAttacks(fixture.Sent));
            Assert.Equal(fixture.Center.Uid, death.Query.OpponentUid);
            var deathIndex = SingleFrameIndex(fixture.Sent, frame =>
                MsgAttackPacket.TryParse(frame, out var packet) && packet.Query.AttackType == MsgAttackType.Death);
            Assert.Equal((ushort)2135, ReadFrame(fixture.Sent[deathIndex]).Header.Opcode);
            Assert.True(deathIndex < animationIndex);
        }
        else
        {
            Assert.Empty(ParseDeathAttacks(fixture.Sent));
        }
    }

    private static uint CalculateHellVortexRemovalDamage(HellVortexRemovalFixture fixture,
        HellVortexRemovalTarget target)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(fixture.Caster.PlayerSpells, fixture.WorldData,
            HellVortexSpellId, out _, out var spell));
        return InvokePrivateUInt32("CalculateLegacyPlayerMonsterPhysicalSpellDamage", fixture.Caster,
            fixture.WorldData, target.Monster, target.Family, spell);
    }

    private static async Task<HellVortexRemovalFixture> CreateHellVortexRemovalFixtureAsync(
        int familyId, bool questSpawn, bool earlierRecipient = false, bool laterGuard = false)
    {
        var family = CreateMonsterFamily() with
        {
            Id = familyId,
            Name = "ApeRemovalFixture",
            Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
            {
                ["life"] = "1000000",
            },
        };
        // No static spawns: EnumerateVisibleMonsterTargets preserves this dynamic insertion order.
        var worldData = CreateMagicCatalog(family, [],
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 7, useStamina: 11,
                damage: 200, coldTime: 0, coolDown: 0, damage3: 30_030));
        var states = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var caster = CreateSession(mapId: 1_020, strength: 301, classCode: 5_005, stamina: 100,
            spellEntries: [CreatePlayerSpell(HellVortexSpellId) with { SoulLevel = 3, UseSpellSoul = 3 }],
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "0",
                ["PkMode"] = "0",
                ["QuestMultiple"] = "1",
                ["LegacyMap1020Family14334ApeLeaderRate"] = familyId == 14_335 ? "1" : "0",
                ["LegacyMonsterDropRoll"] = "40",
            });
        caster = caster with
        {
            Character = caster.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot([
                    new PlayerQuestSnapshotEntry(1_749, PlayerQuestStatus.Accepted, 0,
                        familyId == 14_334 ? [0U, 4U] : [0U, 0U]),
                ]),
            },
        };
        sessions.Set(connection, caster);
        var context = new WorldPacketHandlerContext(connection, new StringWriter(), caster,
            WorldData: worldData, Sessions: sessions, MonsterStates: states,
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        if (earlierRecipient)
        {
            RegisterHellVortexRemovalOrdinaryMonster(states, family, 831);
        }

        LegacyMonsterInstance center;
        if (questSpawn)
        {
            var previousUids = states.GetDynamicMonsterLifecycleInstances(1_020, 0)
                .Select(monster => monster.Uid).ToHashSet();
            var triggerFamily = family with { Id = 14_334 };
            var trigger = new LegacyMonsterInstance(400_123, 1_020, 830, 584, 100, 100,
                HellVortexRemovalMaxLife, HellVortexRemovalMaxLife, 14_334,
                triggerFamily.Name, false, 123, 0);
            var method = typeof(WorldPacketHandlers).GetMethod(
                "TryApplyLegacyMap1020Family14334Quest1749ApeLeaderSpawnAsync",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            caster = await Assert.IsType<ValueTask<WorldSession>>(method.Invoke(null,
                [context, caster, trigger, triggerFamily, CancellationToken.None]));
            var spawned = states.GetVisibleDynamicMonsters(1_020, 0, DateTimeOffset.UtcNow)
                .Where(entry => !previousUids.Contains(entry.Instance.Uid)).ToArray();
            Assert.Equal(familyId == 14_334 ? 5 : 1, spawned.Length);
            Assert.All(spawned, entry => Assert.Equal(checked((uint)familyId), entry.Instance.MonsterId));
            Assert.Contains("RemoveOnDead", ((StringWriter)context.Log).ToString());
            center = spawned[0].Instance;
        }
        else
        {
            center = RegisterHellVortexRemovalOrdinaryMonster(states, family, 830);
        }

        RegisterHellVortexRemovalOrdinaryMonster(states, family, 832);
        if (laterGuard)
        {
            var guardFamily = CreateGuardMonsterFamily() with
            {
                Name = "Guard1",
                Attributes = new Dictionary<string, string>(CreateGuardMonsterFamily().Attributes)
                {
                    ["life"] = "1000000",
                },
            };
            RegisterHellVortexRemovalOrdinaryMonster(states, guardFamily, 833);
        }
        // Spawn provenance remains on the monster; later kills must not repeat the quest spawn branch.
        caster = caster with { Character = caster.Character with { PlayerQuests = new PlayerQuestSnapshot([]) } };
        sessions.Set(connection, caster);
        context = context with { Session = caster };
        var ordered = states.GetVisibleDynamicMonsters(1_020, 0, DateTimeOffset.UtcNow)
            .Select(entry => new HellVortexRemovalTarget(entry.Instance, entry.MonsterFamily)).ToArray();
        Assert.True(ordered.Length >= 2);
        Assert.All(ordered, target => Assert.Equal(HellVortexRemovalMaxLife, target.Monster.Hitpoints));
        sent.Clear();
        ((StringWriter)context.Log).GetStringBuilder().Clear();
        return new(worldData, states, caster, context, center, ordered, sent);
    }

    private static LegacyMonsterInstance RegisterHellVortexRemovalOrdinaryMonster(
        MonsterRuntimeStateStore states, Monster family, ushort x)
    {
        var uid = states.NextLegacyDynamicMonsterUid();
        return states.RegisterDynamicMonster(new LegacyMonsterInstance(uid, 1_020, x, 584, 100, 100,
            HellVortexRemovalMaxLife, HellVortexRemovalMaxLife, checked((uint)family.Id),
            family.Name, false, checked((int)uid), 0), family);
    }

    private sealed record HellVortexRemovalTarget(LegacyMonsterInstance Monster, Monster Family);

    private sealed record HellVortexRemovalFixture(WorldDataCatalog WorldData,
        MonsterRuntimeStateStore States, WorldSession Caster, WorldPacketHandlerContext Context,
        LegacyMonsterInstance Center, IReadOnlyList<HellVortexRemovalTarget> OrderedTargets,
        List<byte[]> Sent) : IDisposable
    {
        public void Dispose() => Context.Log.Dispose();
    }
}
