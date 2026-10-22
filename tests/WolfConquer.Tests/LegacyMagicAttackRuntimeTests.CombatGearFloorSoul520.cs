using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static readonly DateTimeOffset FloorSoul520Now = DateTimeOffset.Parse("2030-01-01T00:00:00Z");

    [Fact]
    public Task PeriodicPheasantBeakPreservesActualPlayerDamageAndDisplaysSeventyFivePercent() =>
        AssertFloorSoul520PlayerAsync(21_580, levelHu: 4);

    [Fact]
    public Task PeriodicDrukylePreservesActualPlayerDamageAndDisplaysFullReceiveDamage() =>
        AssertFloorSoul520PlayerAsync(22_100, levelHu: 4);

    [Fact]
    public Task PeriodicPheasantBeakOverkillDisplaysRequestedReceiveNotCappedHitpointLoss() =>
        AssertFloorSoul520PlayerAsync(21_580, levelHu: 4, overkill: true);

    [Fact]
    public Task PeriodicDrukyleOverkillDisplaysRequestedReceiveNotCappedHitpointLoss() =>
        AssertFloorSoul520PlayerAsync(22_100, levelHu: 4, overkill: true);

    [Fact]
    public Task PeriodicPheasantBeakZeroSoulStillHasSourcePlayerDisplayFraction() =>
        AssertFloorSoul520PlayerAsync(21_580, levelHu: 0);

    [Fact]
    public Task StarVolcanoKeepsCapturedPlayerDamageAndFirstGroupAnimationSoul() =>
        AssertFloorSoul520PlayerAsync(22_070, levelHu: 4);

    [Fact]
    public Task PeriodicPheasantBeakKeepsLineGeometryRatherThanAnimationOriginRadius() =>
        AssertFloorSoul520PlayerAsync(21_580, levelHu: 4, offLine: true);

    [Fact]
    public Task PeriodicPheasantBeakMonsterOverkillKeepsSoulIdentityAndRequestedDisplay() =>
        AssertFloorSoul520NonPlayerAsync(21_580, monsterTarget: true);

    [Fact]
    public Task PeriodicDrukyleMonsterOverkillKeepsSoulIdentityAndRequestedDisplay() =>
        AssertFloorSoul520NonPlayerAsync(22_100, monsterTarget: true);

    [Fact]
    public Task PeriodicPheasantBeakSobOverkillHasNoPlayerDisplayFraction() =>
        AssertFloorSoul520NonPlayerAsync(21_580, monsterTarget: false);

    [Fact]
    public Task PeriodicDrukyleSobOverkillKeepsSoulIdentityAndRequestedDisplay() =>
        AssertFloorSoul520NonPlayerAsync(22_100, monsterTarget: false);

    [Fact]
    public Task PheasantBeakHandlerCapturesSoulLevelNotUseSpellSoul() =>
        AssertFloorSoul520PlacementAsync(21_580);

    [Fact]
    public Task DrukyleHandlerCapturesSoulLevelNotUseSpellSoul() =>
        AssertFloorSoul520PlacementAsync(22_100);

    [Fact]
    public Task PheasantBeakPassivePublicPhysicalProcCapturesSoulLevelNotUseSpellSoul() =>
        AssertFloorSoul520PlacementAsync(22_100, physicalProc: true);

    [Fact]
    public Task DirectPheasantBeakPassiveRejectsInsufficientStaminaBeforeFloorCreation() =>
        AssertFloorSoul523DirectDeniedAsync(learned: true);

    [Fact]
    public Task DirectPheasantBeakPassiveRejectsUnlearnedBookBeforeFloorCreation() =>
        AssertFloorSoul523DirectDeniedAsync(learned: false);

    [Fact]
    public void FloorLineGroupKeepsFirstAnimationStateButEachEntriesDamageAndGeometry()
    {
        var store = new FloorItemRuntimeStateStore();
        var spell = ResolveFloorSoul520Spell(CreateFloorSoul520Catalog(21_580), 21_580);
        var first = FloorSoul520Entry(900_520, spell, levelHu: 4);
        store.AddVisible(first, FloorSoul520Now);
        var later = FloorSoul520Entry(900_521, spell with { Level = 1, Damage = 321 }, levelHu: 8) with
        {
            Packet = first.Packet with { Uid = 900_521, OwnerX = 840, OwnerY = 600 },
            Effect = first.Effect! with
            {
                OriginX = 840, OriginY = 600, SpellLevel = 1, LevelHu = 8,
                AnimationOriginX = 840, AnimationOriginY = 600,
                CapturedSpell = spell with { Level = 1, Damage = 321 },
                ExpiresAt = FloorSoul520Now.AddSeconds(20),
            },
        };
        store.AddVisible(later, FloorSoul520Now);
        Assert.True(store.TryGetVisibleEntry(900_521, FloorSoul520Now, out var stored));
        Assert.Equal((byte)4, stored.Effect!.LevelHu);
        Assert.Equal((byte)2, stored.Effect.SpellLevel);
        Assert.Equal((ushort)830, stored.Effect.AnimationOriginX);
        Assert.Equal((ushort)584, stored.Effect.AnimationOriginY);
        Assert.Equal((ushort)840, stored.Effect.OriginX);
        Assert.Equal((ushort)600, stored.Effect.OriginY);
        Assert.Equal(321U, stored.Effect.CapturedSpell!.Damage);

        // Removal of the first floor is not removal of the owner/spell group.
        var expired = store.ConsumeDueRepeatingEffects(21_580, FloorSoul520Now.AddSeconds(10));
        Assert.Single(expired.Expired);
        store.AddVisible(FloorSoul520Entry(900_522, spell, levelHu: 9, FloorSoul520Now.AddSeconds(10)), FloorSoul520Now.AddSeconds(10));
        Assert.True(store.TryGetVisibleEntry(900_522, FloorSoul520Now.AddSeconds(10), out var inherited));
        Assert.Equal((byte)4, inherited.Effect!.LevelHu);
        Assert.True(store.TryRemoveVisible(900_521, out _));
        Assert.True(store.TryRemoveVisible(900_522, out _));
        store.AddVisible(FloorSoul520Entry(900_523, spell, levelHu: 9, FloorSoul520Now.AddSeconds(11)), FloorSoul520Now.AddSeconds(11));
        Assert.True(store.TryGetVisibleEntry(900_523, FloorSoul520Now.AddSeconds(11), out var fresh));
        Assert.Equal((byte)9, fresh.Effect!.LevelHu);
    }

    [Fact]
    public void FloorLineGroupStateDoesNotLeakAcrossOwnersOrSpellIds()
    {
        var store = new FloorItemRuntimeStateStore();
        var firstSpell = ResolveFloorSoul520Spell(CreateFloorSoul520Catalog(21_580), 21_580);
        store.AddVisible(FloorSoul520Entry(900_524, firstSpell, levelHu: 4), FloorSoul520Now);
        var otherOwner = FloorSoul520Entry(900_525, firstSpell, levelHu: 7);
        otherOwner = otherOwner with { Packet = otherOwner.Packet with { ItemOwnerUid = TargetPlayerUid } };
        store.AddVisible(otherOwner, FloorSoul520Now);
        var otherSpell = ResolveFloorSoul520Spell(CreateFloorSoul520Catalog(22_100), 22_100);
        store.AddVisible(FloorSoul520Entry(900_526, otherSpell, levelHu: 8), FloorSoul520Now);
        Assert.True(store.TryGetVisibleEntry(900_525, FloorSoul520Now, out var ownerResult));
        Assert.Equal((byte)7, ownerResult.Effect!.LevelHu);
        Assert.True(store.TryGetVisibleEntry(900_526, FloorSoul520Now, out var spellResult));
        Assert.Equal((byte)8, spellResult.Effect!.LevelHu);
    }

    [Fact]
    public async Task OwnerMissingPulseDoesNotDamageAndExpiredCapturedFloorIsRemoved()
    {
        var data = CreateFloorSoul520Catalog(21_580);
        var spell = ResolveFloorSoul520Spell(data, 21_580);
        var floors = new FloorItemRuntimeStateStore();
        floors.AddVisible(FloorSoul520Entry(900_527, spell, levelHu: 4), FloorSoul520Now);
        var target = CreateSession(uid: TargetPlayerUid, name: "Target", x: 827, y: 584);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connection, log, target,
            WorldData: data, Sessions: sessions, FloorItems: floors);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(
            context, FloorSoul520Now.AddSeconds(1), CancellationToken.None);
        Assert.True(sessions.TryGet(connection, out var after));
        Assert.Equal(target.Character.MinHitPoints, after!.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(sent));
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(
            context, FloorSoul520Now.AddSeconds(10), CancellationToken.None);
        Assert.False(floors.TryGetVisibleEntry(900_527, FloorSoul520Now.AddSeconds(10), out _));
        Assert.Empty(ParseSpellAnimations(sent));
    }

    private static WorldDataCatalog CreateFloorSoul520Catalog(
        ushort spellId, ushort useMana = 0, byte useStamina = 0, uint procRate = 0) =>
        CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateMagicRow(spellId, sort: 21, useMana: useMana, damage: 1, maxTargets: 5, range: 12,
                useStamina: useStamina, coolDown: 0, coldTime: 0, damageOnHuman: 30_010, gDamage: procRate),
            CreateMagicRow(spellId, sort: 21, level: 2, useMana: useMana, damage: 120, maxTargets: 5, range: 12,
                useStamina: useStamina, coolDown: 0, coldTime: 0, damageOnHuman: 30_350, gDamage: procRate));

    private static LegacyMagicRuntimeSpell ResolveFloorSoul520Spell(WorldDataCatalog data, ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveSpell(data, spellId, 2, out var spell));
        return spell;
    }

    private static FloorItemRuntimeEntry FloorSoul520Entry(
        uint uid, LegacyMagicRuntimeSpell spell, byte levelHu, DateTimeOffset? createdAt = null)
    {
        var now = createdAt ?? FloorSoul520Now;
        var itemId = spell.Id == 21_580 ? 4_170U : spell.Id == 22_070 ? 4_200U : 4_210U;
        var packet = new LegacyFloorItemPacket(uid, itemId, 833, 584,
            MaxLife: 0, LegacyFloorItemDropType.Effect, Life: 0,
            PlusOrColor: spell.Id == 22_070 ? (byte)14 : (byte)30,
            ItemOwnerUid: PlayerUid, GuildId: 0, FlowerType: 0, Timer: 0, OwnerX: 830, OwnerY: 584,
            Name: "STR_TRAP_ID_@@" + uid);
        return FloorItemRuntimeEntry.ForEffect(1_015, packet,
            new FloorItemRuntimeEffect(spell.Id, packet.OwnerX, packet.OwnerY, now,
                Distance: 12, HitRadius: spell.Id == 22_070 ? 4 : 5, ExpiresAt: now.AddSeconds(10),
                RepeatMilliseconds: 900, SpellLevel: spell.Level, LevelHu: levelHu,
                CapturedSpell: spell, AnimationOriginX: 830, AnimationOriginY: 584));
    }

    private static async Task AssertFloorSoul520PlayerAsync(
        ushort spellId, byte levelHu, bool overkill = false, bool offLine = false)
    {
        var data = CreateFloorSoul520Catalog(spellId);
        var captured = ResolveFloorSoul520Spell(data, spellId);
        // Current learned level remains zero: the floor's captured level-two damage must win.
        var owner = CreateSession(strength: 300, stamina: 100, spellIds: [spellId]);
        var target = CreateSession(uid: TargetPlayerUid, name: "Target", x: spellId == 22_070 ? 829 : 827,
            y: offLine ? 590 : 584, minHitPoints: overkill ? 1 : 100_000);
        var expectedReceive = InvokePrivateUInt32("CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            owner, target, captured, data, 1U);
        Assert.True(expectedReceive > 4);
        var sessions = new WorldSessionStore();
        var ownerSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var ownerConnection = CreateSendingConnection(ownerSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(ownerConnection, owner);
        sessions.Set(targetConnection, target);
        var floors = new FloorItemRuntimeStateStore();
        var entry = FloorSoul520Entry(900_528, captured, levelHu);
        floors.AddVisible(entry, FloorSoul520Now);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(ownerConnection, log, owner,
            WorldData: data, Sessions: sessions, FloorItems: floors);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(
            context, FloorSoul520Now.AddSeconds(1), CancellationToken.None);
        Assert.True(sessions.TryGet(targetConnection, out var after));
        var animation = Assert.Single(ParseSpellAnimations(ownerSent),
            value => value.AttackerUid == entry.Packet.Uid && value.SpellId == spellId);
        if (offLine)
        {
            Assert.Equal(target.Character.MinHitPoints, after!.Character.MinHitPoints);
            Assert.DoesNotContain(animation.Targets, value => value.Uid == TargetPlayerUid);
            return;
        }

        // The calculator is an independent baseline for this caller-order test, not a new damage rule.
        Assert.Equal(Math.Max(0, target.Character.MinHitPoints - expectedReceive), after!.Character.MinHitPoints);
        var hit = Assert.Single(animation.Targets, value => value.Uid == TargetPlayerUid);
        var expectedDisplay = spellId == 21_580 ? unchecked(expectedReceive * 75u) / 100u : expectedReceive;
        Assert.Equal(expectedDisplay, hit.Damage);
        Assert.True(hit.Hit);
        Assert.Equal(levelHu, animation.LevelHu);
        Assert.Equal((byte)2, animation.SpellLevel);
        if (overkill)
        {
            Assert.True(hit.Damage > 1);
            Assert.Equal(1, target.Character.MinHitPoints - after.Character.MinHitPoints);
        }
    }

    private static async Task AssertFloorSoul520NonPlayerAsync(ushort spellId, bool monsterTarget)
    {
        var family = CreateMonsterFamily();
        family = family with { Attributes = new Dictionary<string, string>(family.Attributes)
            { ["life"] = "1", ["defense"] = "0" } };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 52_001, boundX: 827, boundY: 584);
        var sob = CreateMoveLineSobNpc(65_520, 827, 584) with { Life = 1, MaxLife = 1 };
        var rows = new[] { CreateMagicRow(spellId, sort: 21, level: 2,
            useMana: 0, damage: 120, maxTargets: 5, range: 12, coolDown: 0, coldTime: 0) };
        // No level-zero row exists: ticking must use the captured per-floor DB spell.
        var data = CreateMagicCatalogCore(family, monsterTarget ? [spawn] : [],
            monsterTarget ? [] : [sob], rows);
        var captured = ResolveFloorSoul520Spell(data, spellId);
        var owner = CreateSession(strength: 300, stamina: 100, spellIds: [spellId]);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, owner);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobStates = new SobNpcRuntimeStateStore();
        var monster = monsterTarget ? CreateRuntimeMonsterInstance(data, spawn, family) : null;
        var expectedDamage = monsterTarget
            ? InvokePrivateUInt32("CalculateLegacyPlayerMonsterPhysicalSpellDamage",
                owner, data, monster!, family, captured)
            : InvokePrivateUInt32("CalculateLegacySobNpcPhysicalSpellDamage", owner, captured, data, 1U);
        Assert.True(expectedDamage > 1);
        var floors = new FloorItemRuntimeStateStore();
        var entry = FloorSoul520Entry(900_529, captured, levelHu: 4);
        floors.AddVisible(entry, FloorSoul520Now);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connection, log, owner,
            WorldData: data, Sessions: sessions, FloorItems: floors,
            MonsterStates: monsterTarget ? monsterStates : null,
            SobNpcStates: monsterTarget ? null : sobStates);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(
            context, FloorSoul520Now.AddSeconds(1), CancellationToken.None);
        var animation = Assert.Single(ParseSpellAnimations(sent),
            value => value.AttackerUid == entry.Packet.Uid && value.SpellId == spellId);
        var uid = monsterTarget ? monster!.Uid : (uint)sob.Id;
        Assert.Equal(expectedDamage, Assert.Single(animation.Targets, value => value.Uid == uid).Damage);
        Assert.Equal((byte)4, animation.LevelHu);
        if (monsterTarget)
        {
            Assert.False(monsterStates.TryGetVisibleInstance(monster!, FloorSoul520Now.AddSeconds(1), out _));
        }
        else
        {
            Assert.Equal(0, sobStates.GetVisible(sob).Life);
        }
    }

    private static async Task AssertFloorSoul520PlacementAsync(ushort spellId, bool physicalProc = false)
    {
        var data = physicalProc
            ? CreateFloorSoul520Catalog(spellId, useMana: 7, useStamina: 5, procRate: 100)
            : CreateFloorSoul520Catalog(spellId);
        var learned = CreatePlayerSpell(spellId, level: 2) with { SoulLevel = 4, UseSpellSoul = 1 };
        var owner = CreateSession(strength: 300, stamina: 100, spellEntries: [learned],
            classCode: physicalProc ? 7_051 : 142,
            attributes: physicalProc ? new Dictionary<string, string>
            {
                ["PkMode"] = "0",
                ["LegacyCombatGearAttackSpellIndex.0"] = "7",
                ["LegacyCombatGearAttackRateRoll.22100"] = "0",
            } : null);
        if (physicalProc)
        {
            owner = owner with
            {
                CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1,
                    Items =
                    [
                        new CombatGearSnapshotItem(701, Level: 1, Exp: 0, Equip: 1,
                            Transform: 0, MasteryScore: 0, Animas: [], BlackJades: []),
                    ],
                },
            };
        }
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, owner);
        var floors = new FloorItemRuntimeStateStore();
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connection, log, owner,
            WorldData: data, Sessions: sessions, FloorItems: floors);
        var target = CreateSession(uid: TargetPlayerUid, name: "FloorSoulProcTarget", x: 831, y: 584);
        var targetConnection = CreateSendingConnection([]);
        if (physicalProc)
        {
            sessions.Set(targetConnection, target);
        }

        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: physicalProc ? TargetPlayerUid : 0,
            X: physicalProc ? (ushort)831 : (ushort)833, Y: 584,
            AttackType: physicalProc ? MsgAttackType.Physical : MsgAttackType.Magic,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
            Unknown: 0, SpellId: physicalProc ? (ushort)0 : spellId)).ToFrameBytes()));
        Assert.True(sessions.TryGet(connection, out var prepared));
        Assert.Equal(owner.Character.MinMana - (physicalProc ? 7 : 0), prepared!.Character.MinMana);
        Assert.Equal(physicalProc ? "95" : "100", prepared.Character.Attributes["Stamina"]);
        Assert.Equal(learned, Assert.Single(prepared.PlayerSpells.Spells));
        if (physicalProc)
        {
            Assert.True(sessions.TryGet(targetConnection, out var unchangedTarget));
            Assert.Equal(target.Character.MinHitPoints, unchangedTarget!.Character.MinHitPoints);
        }

        var floor = Assert.Single(floors.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal((byte)4, floor.Effect!.LevelHu);
        Assert.Equal((byte)2, floor.Effect.SpellLevel);
        Assert.Equal((ushort)830, floor.Effect.AnimationOriginX);
        Assert.Equal((ushort)584, floor.Effect.AnimationOriginY);
        Assert.Equal((ushort)830, floor.Effect.OriginX);
        Assert.Equal((ushort)584, floor.Effect.OriginY);
        Assert.NotNull(floor.Effect.CapturedSpell);
        Assert.Equal((byte)2, floor.Effect.CapturedSpell.Level);
        Assert.Equal((byte)1, Assert.Single(ParseSpellAnimations(sent),
            value => value.AttackerUid == PlayerUid && value.SpellId == spellId).LevelHu);
    }

    private static async Task AssertFloorSoul523DirectDeniedAsync(bool learned)
    {
        const ushort spellId = 22_100;
        var data = CreateFloorSoul520Catalog(spellId, useMana: 7, useStamina: 5);
        var book = CreatePlayerSpell(spellId, level: 2) with { SoulLevel = 4, UseSpellSoul = 1 };
        var owner = CreateSession(strength: 300, minMana: 500, stamina: learned ? 4UL : 100UL,
            spellEntries: learned ? [book] : []);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, owner);
        var floors = new FloorItemRuntimeStateStore();
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connection, log, owner,
            WorldData: data, Sessions: sessions, FloorItems: floors);
        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: 0, X: 833, Y: 584, AttackType: MsgAttackType.Magic,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
            Unknown: 0, SpellId: spellId)).ToFrameBytes()));
        Assert.Empty(floors.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var after));
        Assert.Equal(owner.Character.MinMana, after!.Character.MinMana);
        Assert.Equal(learned ? "4" : "100", after.Character.Attributes["Stamina"]);
        Assert.Equal(learned ? 1 : 0, after.PlayerSpells.Spells.Count);
    }
}
