using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task StarVolcanoDirectPlayerPulseKeepsCapturedLevelAfterCatalogReplacementAndBookRemoval()
    {
        var family = CreateMonsterFamily();
        var data = CreateMagicCatalog(family, [], StarVolcano524Row(2, 120, 30_350));
        using var log = new StringWriter();
        var placed = await PlaceStarVolcano524Async(data, log);
        var target = CreateSession(uid: TargetPlayerUid, name: "RadiusFour", x: 837, y: 588, minHitPoints: 100_000);
        var outside = CreateSession(uid: TargetPlayerUid + 1, name: "RadiusFive", x: 838, y: 588, minHitPoints: 100_000);
        var targetConnection = CreateSendingConnection([]);
        var outsideConnection = CreateSendingConnection([]);
        placed.Context.Sessions!.Set(targetConnection, target);
        placed.Context.Sessions.Set(outsideConnection, outside);
        var changed = CreateMagicCatalog(family, [], StarVolcano524Row(0, 1, 30_010));
        var context = RemoveStarVolcano524Book(placed.Context) with { WorldData = changed };
        Assert.True(LegacyMagicRuntime.TryResolveSpell(changed, 22_070, 0, out var replacement));
        var captured = placed.Floor.Effect!.CapturedSpell!;
        var expected = InvokePrivateUInt32("CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            context.Session!, target, captured, changed, 1U);
        var wrongDefault = InvokePrivateUInt32("CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            context.Session!, target, replacement, changed, 1U);
        Assert.NotEqual(wrongDefault, expected);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(context,
            placed.Floor.Effect.DueAt.AddTicks(1), CancellationToken.None);
        Assert.True(context.Sessions!.TryGet(targetConnection, out var after));
        Assert.Equal(target.Character.MinHitPoints - expected, after!.Character.MinHitPoints);
        Assert.True(context.Sessions.TryGet(outsideConnection, out var outsideAfter));
        Assert.Equal(outside.Character.MinHitPoints, outsideAfter!.Character.MinHitPoints);
        var animation = StarVolcano524Pulse(placed.Sent, placed.Floor);
        var hit = Assert.Single(animation.Targets, value => value.Uid == TargetPlayerUid);
        Assert.Equal(expected, hit.Damage); // 22070 does not use the 21580-only 75% display fraction.
        Assert.True(hit.Hit);
        Assert.DoesNotContain(animation.Targets, value => value.Uid == TargetPlayerUid + 1);
        Assert.Equal((byte)2, animation.SpellLevel);
        Assert.Equal((byte)4, animation.LevelHu);
    }

    [Fact]
    public async Task StarVolcanoPublicMonsterPulseSurvivesCatalogAndBookRemoval()
    {
        var family = CreateMonsterFamily();
        family = family with { Attributes = new Dictionary<string, string>(family.Attributes)
            { ["life"] = "100000", ["defense"] = "0" } };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 52_401, boundX: 837, boundY: 588);
        var data = CreateMagicCatalog(family, [spawn], StarVolcano524Row(2, 120, 30_350));
        using var log = new StringWriter();
        var placed = await PlaceStarVolcano524Async(data, log);
        var monster = CreateRuntimeMonsterInstance(data, spawn, family);
        var states = new MonsterRuntimeStateStore();
        var changed = CreateMagicCatalog(family, [spawn]);
        var context = RemoveStarVolcano524Book(placed.Context) with { WorldData = changed, MonsterStates = states };
        Assert.False(LegacyMagicRuntime.TryResolveSpell(changed, 22_070, 0, out _));
        Assert.False(LegacyMagicRuntime.TryResolveSpell(changed, 22_070, 2, out _));
        var expected = InvokePrivateUInt32("CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            context.Session!, changed, monster, family, placed.Floor.Effect!.CapturedSpell!);
        Assert.InRange(expected, 1U, monster.MaxLife - 1);
        var now = placed.Floor.Effect.DueAt.AddTicks(1);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(context, now, CancellationToken.None);
        Assert.True(states.TryGetVisibleInstance(monster, now, out var after));
        Assert.Equal(monster.MaxLife - expected, after.Hitpoints);
        var animation = StarVolcano524Pulse(placed.Sent, placed.Floor);
        Assert.Equal(expected, Assert.Single(animation.Targets, value => value.Uid == monster.Uid).Damage);
        Assert.Equal((byte)2, animation.SpellLevel);
        Assert.Equal((byte)4, animation.LevelHu);
    }

    [Fact]
    public async Task StarVolcanoPublicSobPulseSurvivesCatalogAndBookRemoval()
    {
        var family = CreateMonsterFamily();
        var sob = CreateMoveLineSobNpc(65_524, 837, 588);
        var data = CreateMagicCatalogCore(family, [], [sob], [StarVolcano524Row(2, 120, 30_350)]);
        using var log = new StringWriter();
        var placed = await PlaceStarVolcano524Async(data, log);
        var states = new SobNpcRuntimeStateStore();
        var changed = CreateMagicCatalogCore(family, [], [sob], []);
        var context = RemoveStarVolcano524Book(placed.Context) with { WorldData = changed, SobNpcStates = states };
        Assert.False(LegacyMagicRuntime.TryResolveSpell(changed, 22_070, 0, out _));
        var expected = InvokePrivateUInt32("CalculateLegacySobNpcPhysicalSpellDamage",
            context.Session!, placed.Floor.Effect!.CapturedSpell!, changed, 1U);
        Assert.InRange(expected, 1U, (uint)sob.Life - 1);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(context,
            placed.Floor.Effect.DueAt.AddTicks(1), CancellationToken.None);
        Assert.Equal(sob.Life - expected, states.GetVisible(sob).Life);
        var animation = StarVolcano524Pulse(placed.Sent, placed.Floor);
        Assert.Equal(expected, Assert.Single(animation.Targets, value => value.Uid == (uint)sob.Id).Damage);
        Assert.Equal((byte)2, animation.SpellLevel);
        Assert.Equal((byte)4, animation.LevelHu);
    }

    [Fact]
    public async Task StarVolcanoPublicPhysicalPirateProcCapturesBookAndPaysResourcesBeforePulse()
    {
        var family = CreateMonsterFamily();
        var data = CreateMagicCatalog(family, [], StarVolcano524Row(2, 120, 30_350, useMana: 7, useStamina: 5, rate: 100));
        using var log = new StringWriter();
        var placed = await PlaceStarVolcano524Async(data, log, physicalProc: true);
        var context = RemoveStarVolcano524Book(placed.Context) with { WorldData = CreateMagicCatalog(family, []) };
        var target = placed.PhysicalTarget!;
        var targetConnection = placed.PhysicalTargetConnection!;
        var expected = InvokePrivateUInt32("CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            context.Session!, target, placed.Floor.Effect!.CapturedSpell!, context.WorldData!, 1U);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(context,
            placed.Floor.Effect.DueAt.AddTicks(1), CancellationToken.None);
        Assert.True(context.Sessions!.TryGet(targetConnection, out var after));
        Assert.Equal(target.Character.MinHitPoints - expected, after!.Character.MinHitPoints);
        var animation = StarVolcano524Pulse(placed.Sent, placed.Floor);
        Assert.Equal(expected, Assert.Single(animation.Targets, value => value.Uid == TargetPlayerUid).Damage);
        Assert.Equal((byte)2, animation.SpellLevel);
        Assert.Equal((byte)4, animation.LevelHu);
    }

    [Fact]
    public async Task StarVolcanoPublicGroupKeepsFirstAnimationStateAndEachFloorSpellUntilEmpty()
    {
        var family = CreateMonsterFamily();
        family = family with { Attributes = new Dictionary<string, string>(family.Attributes)
            { ["life"] = "100000", ["defense"] = "0" } };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 52_402, boundX: 833, boundY: 584);
        var data = CreateMagicCatalog(family, [spawn],
            StarVolcano524Row(2, 120, 30_350), StarVolcano524Row(1, 37, 30_025));
        using var log = new StringWriter();
        var placed = await PlaceStarVolcano524Async(data, log);
        var first = placed.Floor;
        var owner = placed.Context.Session! with
        {
            Character = placed.Context.Session!.Character with { X = 831, Y = 585 },
            PlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(22_070, level: 1) with { SoulLevel = 8, UseSpellSoul = 1 }]),
        };
        placed.Context.Sessions!.Set(placed.Context.Connection, owner);
        var context = placed.Context with { Session = owner };
        await CastStarVolcano524Async(context, physicalProc: false);
        var second = Assert.Single(context.FloorItems!.SnapshotVisible(DateTimeOffset.UtcNow), value => value.Packet.Uid != first.Packet.Uid);
        Assert.Equal((byte)1, second.Effect!.CapturedSpell!.Level);
        Assert.Equal(37U, second.Effect.CapturedSpell.Damage);
        Assert.Equal((byte)2, second.Effect.SpellLevel);
        Assert.Equal((byte)4, second.Effect.LevelHu);
        Assert.Equal((ushort)830, second.Effect.AnimationOriginX);
        Assert.Equal((ushort)584, second.Effect.AnimationOriginY);
        Assert.Equal((ushort)831, second.Effect.OriginX);
        Assert.Equal((ushort)585, second.Effect.OriginY);
        var monster = CreateRuntimeMonsterInstance(data, spawn, family);
        var states = new MonsterRuntimeStateStore();
        var changed = CreateMagicCatalog(family, [spawn]);
        Assert.True(context.Sessions.TryGet(context.Connection, out var pulseOwner));
        var firstDamage = InvokePrivateUInt32("CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            pulseOwner!, changed, monster, family, first.Effect!.CapturedSpell!);
        var secondDamage = InvokePrivateUInt32("CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            pulseOwner!, changed, monster, family, second.Effect.CapturedSpell);
        Assert.NotEqual(firstDamage, secondDamage);
        var now = (first.Effect.DueAt > second.Effect.DueAt ? first.Effect.DueAt : second.Effect.DueAt).AddTicks(1);
        Assert.True(now < first.Effect.ExpiresAt!.Value && now < second.Effect.ExpiresAt!.Value);
        await WorldPacketHandlers.SendDueLegacyCombatGearFloorSpellsAsync(
            context with { WorldData = changed, MonsterStates = states }, now, CancellationToken.None);
        Assert.True(states.TryGetVisibleInstance(monster, now, out var damaged));
        Assert.Equal(monster.MaxLife - firstDamage - secondDamage, damaged.Hitpoints);
        var firstPulse = StarVolcano524Pulse(placed.Sent, first);
        var secondPulse = StarVolcano524Pulse(placed.Sent, second);
        Assert.Equal(firstDamage, Assert.Single(firstPulse.Targets, value => value.Uid == monster.Uid).Damage);
        Assert.Equal(secondDamage, Assert.Single(secondPulse.Targets, value => value.Uid == monster.Uid).Damage);
        Assert.Equal((byte)2, secondPulse.SpellLevel);
        Assert.Equal((byte)4, secondPulse.LevelHu);
        Assert.True(context.FloorItems.TryRemoveVisible(first.Packet.Uid, out _));
        Assert.True(context.Sessions.TryGet(context.Connection, out var current));
        await CastStarVolcano524Async(context with { Session = current! }, physicalProc: false);
        var third = Assert.Single(context.FloorItems.SnapshotVisible(DateTimeOffset.UtcNow), value => value.Packet.Uid != second.Packet.Uid);
        Assert.Equal((byte)4, third.Effect!.LevelHu);
        Assert.Equal((byte)2, third.Effect.SpellLevel);
        Assert.True(context.FloorItems.TryRemoveVisible(second.Packet.Uid, out _));
        Assert.True(context.FloorItems.TryRemoveVisible(third.Packet.Uid, out _));
        Assert.True(context.Sessions.TryGet(context.Connection, out current));
        await CastStarVolcano524Async(context with { Session = current! }, physicalProc: false);
        var fresh = Assert.Single(context.FloorItems.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal((byte)1, fresh.Effect!.SpellLevel);
        Assert.Equal((byte)8, fresh.Effect.LevelHu);
        Assert.Equal((ushort)831, fresh.Effect.AnimationOriginX);
        Assert.Equal((ushort)585, fresh.Effect.AnimationOriginY);
    }

    private static MagicType StarVolcano524Row(
        byte level, uint damage, int damageOnHuman, ushort useMana = 0, byte useStamina = 0, int rate = 0) =>
        CreateMagicRow(22_070, sort: 21, level: level, useMana: useMana, useStamina: useStamina,
            damage: damage, maxTargets: 5, range: 12, coolDown: 0, coldTime: 0,
            damageOnHuman: damageOnHuman, gDamage: (uint)rate);

    private static async Task<(WorldPacketHandlerContext Context, FloorItemRuntimeEntry Floor, List<byte[]> Sent,
        WorldSession? PhysicalTarget, ConquerTcpConnection? PhysicalTargetConnection)> PlaceStarVolcano524Async(
        WorldDataCatalog data, TextWriter log, bool physicalProc = false)
    {
        var learned = CreatePlayerSpell(22_070, level: 2) with { SoulLevel = 4, UseSpellSoul = 1 };
        var owner = CreateSession(strength: 300, stamina: 100, minMana: 500, spellEntries: [learned],
            classCode: physicalProc ? 7_051 : 142, attributes: physicalProc ? new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["LegacyCombatGearAttackSpellIndex.0"] = "4",
                ["LegacyCombatGearAttackRateRoll.22070"] = "0",
            } : null);
        if (physicalProc)
        {
            owner = owner with { CombatGear = CombatGearSnapshot.Empty with { Count = 1, Items =
            [
                new CombatGearSnapshotItem(701, Level: 1, Exp: 0, Equip: 1,
                    Transform: 0, MasteryScore: 0, Animas: [], BlackJades: []),
            ] } };
        }
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, owner);
        var floors = new FloorItemRuntimeStateStore();
        var target = physicalProc ? CreateSession(uid: TargetPlayerUid, name: "StarVolcanoProc", x: 831, y: 584, minHitPoints: 100_000) : null;
        var targetConnection = physicalProc ? CreateSendingConnection([]) : null;
        if (target is not null) sessions.Set(targetConnection!, target);
        var context = new WorldPacketHandlerContext(connection, log, owner,
            WorldData: data, Sessions: sessions, FloorItems: floors);
        await CastStarVolcano524Async(context, physicalProc);
        Assert.True(sessions.TryGet(connection, out var prepared));
        Assert.Equal(physicalProc ? 493 : 500, prepared!.Character.MinMana);
        Assert.Equal(physicalProc ? "95" : "100", prepared.Character.Attributes["Stamina"]);
        if (target is not null)
        {
            Assert.True(sessions.TryGet(targetConnection!, out var untouched));
            Assert.Equal(target.Character.MinHitPoints, untouched!.Character.MinHitPoints);
        }
        var floor = Assert.Single(floors.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Equal(4_200U, floor.Packet.ItemId);
        Assert.Equal((byte)14, floor.Packet.PlusOrColor);
        Assert.Equal(4d, floor.Effect!.HitRadius);
        Assert.Equal((byte)2, floor.Effect.SpellLevel);
        Assert.Equal((byte)4, floor.Effect.LevelHu);
        Assert.NotNull(floor.Effect.CapturedSpell);
        Assert.Equal((byte)2, floor.Effect.CapturedSpell.Level);
        Assert.Equal((byte)1, Assert.Single(ParseSpellAnimations(sent),
            value => value.AttackerUid == PlayerUid && value.SpellId == 22_070).LevelHu);
        return (context with { Session = prepared }, floor, sent, target, targetConnection);
    }

    private static async Task CastStarVolcano524Async(WorldPacketHandlerContext context, bool physicalProc) =>
        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: physicalProc ? TargetPlayerUid : 0,
            X: physicalProc ? (ushort)831 : (ushort)833, Y: 584,
            AttackType: physicalProc ? MsgAttackType.Physical : MsgAttackType.Magic,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
            Unknown: 0, SpellId: physicalProc ? (ushort)0 : (ushort)22_070)).ToFrameBytes()));

    private static WorldPacketHandlerContext RemoveStarVolcano524Book(WorldPacketHandlerContext context)
    {
        Assert.True(context.Sessions!.TryGet(context.Connection, out var current));
        var withoutBook = current! with { PlayerSpells = PlayerSpellSnapshot.Empty };
        context.Sessions.Set(context.Connection, withoutBook);
        return context with { Session = withoutBook };
    }

    private static LegacySpellAnimationPacket StarVolcano524Pulse(List<byte[]> sent, FloorItemRuntimeEntry floor) =>
        Assert.Single(ParseSpellAnimations(sent), value => value.AttackerUid == floor.Packet.Uid && value.SpellId == 22_070);
}
