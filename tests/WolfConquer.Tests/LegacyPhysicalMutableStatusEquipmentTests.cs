using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectOverlayChangedStatEquipKeepsBothOldFieldsUntilItsRealQueryPoint(bool admitted)
    {
        var result = await DirectOverlayEquipmentRunAsync("distinct", admitted);
        Assert.True(result.ItemCallbackRan);
        Assert.True(result.AcceptedItemsObservedBeforeQuery);
        Assert.Equal(60U, result.BeforeQueryStatus.Breakthrough);
        Assert.Equal(50U, result.BeforeQueryStatus.Counteraction);
        Assert.Equal(10_000, result.BeforeQueryStamp); // Neither materialization nor first item send renews it.
        Assert.Equal(80U, result.RawAfterMutation[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(70U, result.RawAfterMutation[LegacyPlayerStatusPacket.CounteractionIndex]);
        Assert.Equal(admitted ? 80U : 60U, result.AfterMutationStatus.Breakthrough);
        Assert.Equal(admitted ? 70U : 50U, result.AfterMutationStatus.Counteraction);
        Assert.Equal(admitted ? 16_000 : 10_000, result.AfterMutationStamp);
        Assert.Equal(admitted ? 0 : 1, result.OperationWaitMessages);
        Assert.Equal(80U, result.FinalStatus.Breakthrough);
        Assert.Equal(70U, result.FinalStatus.Counteraction);
        Assert.Equal(16_000, result.FinalStamp);
        Assert.Equal(91_003U, Assert.Single(result.Owner.PlayerItems.EquipmentItems, item => item.Position == 1).Uid);
        Assert.Contains(result.Owner.PlayerItems.InventoryItems, item => item.Uid == 91_001);
    }

    [Theory]
    [InlineData("normal-equip-while-alternate", 60U, 50U, true)]
    [InlineData("inactive-alternate-equip", 60U, 75U, false)]
    [InlineData("active-alternate-equip", 80U, 70U, true)]
    [InlineData("inactive-alternate-unequip", 60U, 50U, true)]
    [InlineData("normal-unequip-while-alternate", 60U, 50U, true)]
    public async Task DirectOverlayEquipmentQueryPredicatesRetainTheSourceEquipUnequipAsymmetry(
        string scenario, uint expectedBreakthrough, uint expectedCounteraction, bool queried)
    {
        var result = await DirectOverlayEquipmentRunAsync(scenario, admitted: true);
        Assert.True(result.ItemCallbackRan);
        Assert.True(result.AcceptedItemsObservedBeforeQuery);
        Assert.Equal(60U, result.BeforeQueryStatus.Breakthrough);
        Assert.Equal(75U, result.BeforeQueryStatus.Counteraction); // Earned by an actual prior Imp receive.
        Assert.Equal(10_000, result.BeforeQueryStamp);
        Assert.Equal(expectedBreakthrough, result.AfterMutationStatus.Breakthrough);
        Assert.Equal(expectedCounteraction, result.AfterMutationStatus.Counteraction);
        Assert.Equal(queried ? 16_000 : 10_000, result.AfterMutationStamp);
        Assert.Equal(0, result.OperationWaitMessages);
        var equipment = result.Owner.PlayerItems.EquipmentItems;
        switch (scenario)
        {
            case "normal-equip-while-alternate":
                Assert.Equal(91_003U, Assert.Single(equipment, item => item.Position == 1).Uid);
                Assert.Equal(91_002U, Assert.Single(equipment, item => item.Position == 51).Uid);
                break;
            case "inactive-alternate-equip":
            case "active-alternate-equip":
                Assert.Equal(91_003U, Assert.Single(equipment, item => item.Position == 51).Uid);
                break;
            case "inactive-alternate-unequip":
                Assert.DoesNotContain(equipment, item => item.Position == 51);
                Assert.Contains(result.Owner.PlayerItems.InventoryItems, item => item.Uid == 91_002);
                break;
            case "normal-unequip-while-alternate":
                Assert.DoesNotContain(equipment, item => item.Position == 1);
                Assert.Contains(result.Owner.PlayerItems.InventoryItems, item => item.Uid == 91_001);
                break;
        }
        // Inactive alternate EQUIP alone skips QueryEquipment. Normal Equip
        // still queries while alternate is selected, and BOTH Unequips query.
    }

    private static async Task<DirectOverlayEquipmentResult> DirectOverlayEquipmentRunAsync(string scenario, bool admitted)
    {
        const uint oldNormal = 111_050, newNormal = 111_051, oldAlternate = 111_052, newAlternate = 111_053, strikerHelmet = 111_054;
        var distinct = scenario == "distinct";
        var alternate = scenario is "normal-equip-while-alternate" or "active-alternate-equip" or "normal-unequip-while-alternate";
        var alternateEquip = scenario is "inactive-alternate-equip" or "active-alternate-equip";
        var unequip = scenario.Contains("unequip", StringComparison.Ordinal);
        var position = (ushort)(alternateEquip || scenario == "inactive-alternate-unequip" ? 51 : 1);
        var removedUid = position == 51 ? 91_002U : 91_001U;
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
        [
            CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, useMana: 0, damage: 0,
                duration: 60, coolDown: 0, coldTime: 0),
            CreateMagicRow(DirectOverlaySpellId, sort: 99, useMana: 0, damage: 30_250,
                range: 4, damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085,
                gDamage: 0, coolDown: 0, coldTime: 0),
        ], items:
        [
            DirectOverlayItem(oldNormal, "OverlayEquipmentOldNormal", breakthrough: 60, counteraction: 50),
            DirectOverlayItem(newNormal, "OverlayEquipmentNewNormal", breakthrough: 80, counteraction: 70),
            DirectOverlayItem(oldAlternate, "OverlayEquipmentOldAlternate", breakthrough: 60, counteraction: 50),
            DirectOverlayItem(newAlternate, "OverlayEquipmentNewAlternate", breakthrough: 80, counteraction: 70),
            DirectOverlayItem(strikerHelmet, "OverlayEquipmentStriker", critical: 1_000, breakthrough: 100),
            DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)"),
        ]);
        PlayerItemSnapshotItem Item(uint uid, uint id, ushort slot) => new()
        {
            Uid = uid, ItemId = id, Position = slot, Durability = 100, MaximumDurability = 100,
        };
        var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2,
            strength: 0, spirit: 0, minHitPoints: 100_000, stamina: 100, spellIds: [FineRainAttachStatusSpellId],
            playerItems: PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [Item(91_001, oldNormal, 1), Item(91_002, oldAlternate, 51)],
                InventoryItems = [Item(91_003, alternateEquip ? newAlternate : newNormal, 0)],
                RuneItems = [Item(91_004, DirectOverlaySyntheticImpregnabilityRuneId, alternate ? (ushort)123 : (ushort)103)],
            },
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "200",
                ["LegacyPhysicalImpregnabilityRoll"] = "0",
            }) with { AlternateEquipmentActive = alternate };
        var striker = CreateSession(uid: TargetPlayerUid, mapId: 1_000, x: 839, y: 584, classCode: 2_005,
            strength: 300, spirit: 0, minHitPoints: 100_000, stamina: 100, spellIds: [DirectOverlaySpellId],
            playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = [Item(92_001, strikerHelmet, 1)] },
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "100",
                ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99",
                ["LegacyPhysicalPlayerCriticalRoll"] = "99", ["LegacyPhysicalDashRoll"] = "0", ["DashRate"] = "0",
                ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                    Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        var sessions = new WorldSessionStore();
        var captures = new ConcurrentQueue<byte[]>();
        var allFrames = new ConcurrentQueue<byte[]>();
        var clock = new DirectOverlayEquipmentTimeProvider();
        var signedClock = LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000);
        var connections = new Dictionary<uint, ConquerTcpConnection>();
        WorldPacketHandlerContext context = null!;
        var enabled = false;
        var itemCallback = 0;
        var acceptedBeforeQuery = false;
        var beforeQueryStamp = -1;
        var beforeQueryStatus = new LegacyPlayerStatusPacket(new uint[LegacyPlayerStatusPacket.FieldCount]);
        WorldSession CurrentOwner()
        {
            Assert.True(sessions.TryGet(connections[PlayerUid], out var current));
            Assert.NotNull(current);
            return current;
        }
        async Task<LegacyPlayerStatusPacket> QueryAsync()
        {
            var start = captures.Count;
            await PacketHandlers.HandleAsync(context with { Session = CurrentOwner() },
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
            var frame = Assert.Single(captures.ToArray().Skip(start),
                bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out var packet));
            return packet;
        }
        async ValueTask CaptureAsync(uint uid, ReadOnlyMemory<byte> memory)
        {
            var bytes = memory.ToArray();
            allFrames.Enqueue(bytes);
            if (uid == PlayerUid) captures.Enqueue(bytes);
            if (!enabled || uid != PlayerUid ||
                !MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var itemPacket) ||
                itemPacket.Action is not (MsgItemUsageAction.RemoveInventory or MsgItemUsageAction.Unequip) ||
                Interlocked.CompareExchange(ref itemCallback, 1, 0) != 0) return;
            var current = CurrentOwner();
            acceptedBeforeQuery = unequip
                ? current.PlayerItems.EquipmentItems.All(item => item.Uid != removedUid)
                : current.PlayerItems.EquipmentItems.Any(item => item.Uid == 91_003 && item.Position == position);
            beforeQueryStamp = LegacyFineRainEquipmentAdmission.ForOwner(current).StampMilliseconds;
            beforeQueryStatus = await QueryAsync();
        }
        connections[PlayerUid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (memory, _) => CaptureAsync(PlayerUid, memory));
        connections[TargetPlayerUid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (memory, _) => CaptureAsync(TargetPlayerUid, memory));
        sessions.Set(connections[PlayerUid], owner);
        sessions.Set(connections[TargetPlayerUid], striker);
        using var log = new StringWriter();
        context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log), owner,
            WorldData: world, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), TimeProvider: clock, LegacyClock: signedClock,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var initial = await QueryAsync();
        Assert.Equal(60U, initial.Breakthrough);
        Assert.Equal(50U, initial.Counteraction);
        // Real14500 raw0 normalizes to100 and owns the first guarded query/stamp.
        // This fixture starts no background runtime;60s is not shipped balance.
        await PacketHandlers.HandleAsync(context with { Session = CurrentOwner() },
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0,
                KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                SpellId: FineRainAttachStatusSpellId)).ToFrameBytes()));
        var fineRain = CurrentOwner();
        Assert.True(HasLegacyStatusFlag(fineRain.Character.Attributes, FineRain1StatusFlag));
        Assert.True(HasLegacyStatusFlag(fineRain.Character.Attributes, FineRain2StatusFlag));
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fineRain).StampMilliseconds);
        // Query rebuild materializes the captured equipment's absolute source pair.
        Assert.Equal((uint?)60, LegacyPhysicalStatusOverlay.ForOwner(fineRain).Breakthrough);
        Assert.Equal((uint?)50, LegacyPhysicalStatusOverlay.ForOwner(fineRain).Counteraction);
        if (!distinct)
        {
            Assert.True(sessions.TryGet(connections[TargetPlayerUid], out var currentStriker));
            Assert.NotNull(currentStriker);
            await PacketHandlers.HandleAsync(context with { Connection = connections[TargetPlayerUid], Session = currentStriker },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: PlayerUid,
                    X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
            Assert.Equal(75U, (await QueryAsync()).Counteraction);
        }
        clock.AdvanceMilliseconds(admitted ? 6_000 : 1_000);
        var operationStart = captures.Count;
        enabled = true;
        await PacketHandlers.HandleAsync(context with { Session = CurrentOwner() },
            ReadFrame(new MsgItemUsagePacket(unequip ? removedUid : 91_003,
                position, unequip ? MsgItemUsageAction.Unequip : MsgItemUsageAction.Equip, 0, 0, 0, 0).ToFrameBytes()));
        enabled = false;
        var operationFrames = captures.ToArray().Skip(operationStart).ToArray();
        var waits = operationFrames.Count(bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
            message.Content.Contains("You Shoud wait 6 seconds."));
        var afterMutation = await QueryAsync();
        var afterMutationStamp = LegacyFineRainEquipmentAdmission.ForOwner(CurrentOwner()).StampMilliseconds;
        var rawAfter = LegacyPlayerStatusCalculator.CalculateFields(CurrentOwner().Character,
            CurrentOwner().PlayerItems, world, 100_000, 0, alternateEquipmentActive: alternate);
        if (distinct && !admitted)
        {
            clock.AdvanceMilliseconds(5_000);
            var beforeQuery = captures.Count;
            await PacketHandlers.HandleAsync(context with { Session = CurrentOwner() },
                ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));
            Assert.DoesNotContain(captures.ToArray().Skip(beforeQuery), bytes =>
                LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
                message.Content.Contains("You Shoud wait 6 seconds."));
        }
        var finalStatus = await QueryAsync();
        var finalOwner = CurrentOwner();
        Assert.DoesNotContain(allFrames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
        return new DirectOverlayEquipmentResult(finalOwner, beforeQueryStatus, beforeQueryStamp,
            afterMutation, afterMutationStamp, finalStatus,
            LegacyFineRainEquipmentAdmission.ForOwner(finalOwner).StampMilliseconds, rawAfter,
            itemCallback != 0, acceptedBeforeQuery, waits);
    }

    private sealed class DirectOverlayEquipmentTimeProvider : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero).AddTicks(_ticks);
        public void AdvanceMilliseconds(long milliseconds) => _ticks =
            checked(_ticks + milliseconds * TimeSpan.TicksPerMillisecond);
    }

    private sealed record DirectOverlayEquipmentResult(WorldSession Owner, LegacyPlayerStatusPacket BeforeQueryStatus,
        int BeforeQueryStamp, LegacyPlayerStatusPacket AfterMutationStatus, int AfterMutationStamp,
        LegacyPlayerStatusPacket FinalStatus, int FinalStamp, uint[] RawAfterMutation,
        bool ItemCallbackRan, bool AcceptedItemsObservedBeforeQuery, int OperationWaitMessages);
}
