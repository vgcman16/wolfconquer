using System.Collections.Concurrent;
using System.Globalization;
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
    [InlineData("circle-double")]
    [InlineData("circle-special-zero")]
    [InlineData("circle-weapon-last")]
    [InlineData("fury-zero")]
    [InlineData("fury-one")]
    [InlineData("vip-seven")]
    public async Task DirectQueryStagesExposeNonneutralSourceSettersInsteadOfReplayingFinalTotals(string scenario)
    {
        using var fixture = new DirectQueryStagesFixture(scenario);
        await fixture.QueryEquipmentAsync();
        (uint Maximum, uint Hitpoints)[] expected = scenario switch
        {
            // Source double45*1.4 truncates62, not integer-rational63.
            "circle-double" => [(62, 162), (607, 162), (607, 162), (607, 162)],
            // Fan clears enchant BEFORE Circle, but still performs its +=0 setter.
            "circle-special-zero" => [(0, 100), (500, 100), (500, 100), (500, 100)],
            // Input is weapon first; source dictionary appends weapons AFTER nonweapons.
            "circle-weapon-last" => [(62, 162), (669, 224), (1_414, 224), (1_414, 224), (1_414, 224)],
            "fury-zero" => [(500, 100), (500, 100), (500, 100), (500, 100)],
            "fury-one" => [(500, 50_100), (50_500, 50_100), (50_500, 50_100), (50_500, 50_100)],
            "vip-seven" => [(500, 3_100), (3_500, 3_100), (3_500, 3_100), (3_500, 3_100)],
            _ => throw new InvalidOperationException(scenario),
        };
        var ownerFrames = fixture.Order.ToArray().Where(frame => frame.Recipient == PlayerUid)
            .Select(frame => frame.Bytes).ToArray();
        DirectQueryStagesAssertPairs(ownerFrames, expected);
        Assert.Equal(expected.Select(phase => (phase.Maximum, (long)phase.Hitpoints)), fixture.HpObservations.ToArray());
        Assert.Equal((long)expected[^1].Hitpoints, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, expected[^1].Maximum);
        var statusBytes = Assert.Single(ownerFrames, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
        Assert.Equal(expected[^1].Maximum, status.MaxHP);
        var indices = ownerFrames.Select((bytes, index) => (bytes, index)).Where(entry =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(entry.bytes), out var update) && update.Uid == PlayerUid &&
            update.Updates.Any(value => value.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .Select(entry => entry.index).ToArray();
        var statusIndex = Array.FindIndex(ownerFrames, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(statusIndex > indices[^5] && statusIndex < indices[^4]); // Status before clamp and final explicit pair.
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain1StatusFlag));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectQueryStagesAppendKeepsEarnedPairUntilSourceResetAndRebuildsCapturedGearAfterReentry(
        bool equipInsideAppend)
    {
        var armed = false;
        var appendCallbacks = 0;
        LegacyPlayerStatusPacket? beforeReset = null;
        LegacyPlayerStatusPacket? afterNested = null;
        using var fixture = new DirectQueryStagesFixture("append", async (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid || !LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _) ||
                Interlocked.CompareExchange(ref appendCallbacks, 1, 0) != 0) return;
            beforeReset = await current.QueryStatusAsync(); // Read-only2301, NOT another equipment query.
            Assert.Equal(66U, beforeReset.Breakthrough);
            Assert.Equal(75U, beforeReset.Counteraction);
            if (equipInsideAppend)
            {
                await current.EquipReplacementAsync(); // Same original UID, actual legal item action.
                afterNested = await current.QueryStatusAsync();
                Assert.Equal(80U, afterNested.Breakthrough);
                Assert.Equal(70U, afterNested.Counteraction);
            }
        });
        await fixture.EarnPairAsync(); // Actual17490 Front Break then actual incoming Impregnability.
        var earned = await fixture.QueryStatusAsync();
        Assert.Equal(66U, earned.Breakthrough);
        Assert.Equal(75U, earned.Counteraction);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        armed = true;
        await fixture.QueryEquipmentAsync(); // Read-only query has no accepted-items mutation fence.
        armed = false;
        Assert.Equal(1, appendCallbacks);
        Assert.NotNull(beforeReset);
        var rebuilt = await fixture.QueryStatusAsync();
        Assert.Equal(60U, rebuilt.Breakthrough);
        Assert.Equal(50U, rebuilt.Counteraction);
        Assert.Equal(60U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).Breakthrough);
        Assert.Equal(50U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).Counteraction);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain1StatusFlag));
        if (equipInsideAppend)
        {
            Assert.NotNull(afterNested);
            var helmet = Assert.Single(fixture.Owner.PlayerItems.EquipmentItems, item => item.Position == 1);
            Assert.Equal(98_803U, helmet.Uid); // Outer captured gear must NOT restore old accepted items.
            Assert.Contains(fixture.Owner.PlayerItems.InventoryItems, item => item.Uid == 98_801);
            var freshRaw = LegacyPlayerStatusCalculator.CalculateFields(fixture.Owner.Character,
                fixture.Owner.PlayerItems, fixture.World, 100_000, 0);
            Assert.Equal(80U, freshRaw[LegacyPlayerStatusPacket.BreakthroughIndex]);
            Assert.Equal(70U, freshRaw[LegacyPlayerStatusPacket.CounteractionIndex]);
        }
        else
            Assert.Equal(98_801U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems, item => item.Position == 1).Uid);
        Assert.Equal(99_775, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 100_000);
    }

    private static void DirectQueryStagesAssertPairs(IEnumerable<byte[]> frames, (uint Maximum, uint Hitpoints)[] phases)
    {
        var packets = ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.Equal(phases.Length * 2, packets.Length);
        for (var index = 0; index < phases.Length; index++)
        {
            var phase = phases[index];
            var maximum = Assert.Single(packets[index * 2].Updates);
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
            Assert.Equal((ulong)phase.Maximum, maximum.Value);
            Assert.Collection(packets[index * 2 + 1].Updates,
                max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal((ulong)phase.Maximum, max.Value); },
                hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal((ulong)phase.Hitpoints, hp.Value); });
        }
    }

    private sealed class DirectQueryStagesFixture : IDisposable
    {
        private const uint HelmetId = 111_099, ArmorId = 130_099, ReplacementId = 111_100, DummyHelmetId = 111_101;
        private const uint CircleId = 4_036_006, FuryZeroId = 4_020_100;
        private readonly WorldSessionStore _sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly WorldPacketHandlerContext _context;
        private readonly StringWriter _log = new();
        private readonly bool _append;

        public DirectQueryStagesFixture(string scenario,
            Func<DirectQueryStagesFixture, uint, byte[], ValueTask>? onSend = null)
        {
            _append = scenario is "append" or "append-alt";
            var alternate = scenario == "append-alt";
            var circle = scenario.StartsWith("circle-", StringComparison.Ordinal);
            var special = scenario == "circle-special-zero";
            var weaponLast = scenario == "circle-weapon-last";
            const uint fanId = 201_003, weaponId = 410_003;
            ItemType HpItem(uint id, string name, ushort hp, uint critical = 0, uint bt = 0, uint ca = 0)
            {
                var row = DirectOverlayItem(id, name, critical, bt, ca);
                var fields = row.Fields.ToArray();
                fields[ItemType.ItemHPFieldIndex] = hp.ToString(CultureInfo.InvariantCulture);
                return row with { Fields = fields };
            }
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(DirectOverlaySpellId, sort: 99, useMana: 0, damage: 30_250, range: 4,
                    damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085, gDamage: 0,
                    coolDown: 0, coldTime: 0),
            ], items:
            [
                HpItem(HelmetId, "QueryStageHelmet", _append ? (ushort)50_000 : (ushort)500,
                    _append ? 1_000U : 0, _append ? 60U : 0, _append ? 50U : 0),
                HpItem(ArmorId, "QueryStageArmor", 49_100),
                HpItem(ReplacementId, "QueryStageReplacement", 50_000, 1_000, 80, 70),
                HpItem(DummyHelmetId, "QueryStageDummy", 50_000, 1_000, 100, 65),
                HpItem(fanId, "QueryStageFan", 500),
                HpItem(weaponId, "QueryStageSword", 700),
                DirectOverlayItem(CircleId, "Circle of Life (+6)"),
                DirectOverlayItem(FuryZeroId, "FuryStrike(+0)"),
                DirectOverlayItem(FuryZeroId + 1, "FuryStrike(+1)"),
                DirectOverlayItem(DirectOverlayFrontBreakRuneId, "Front Break (+9)"),
                DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)"),
            ]);
            World = world;
            PlayerItemSnapshotItem Item(uint uid, uint id, ushort position, byte enchant = 0) => new()
            {
                Uid = uid, ItemId = id, Position = position, Enchant = enchant,
                Durability = 100, MaximumDurability = 100,
            };
            PlayerItemSnapshotItem[] equipment = alternate
                ? [Item(98_803, ReplacementId, 1), Item(98_806, ArmorId, 3),
                   Item(98_801, HelmetId, 51), Item(98_802, ArmorId, 53)]
                : _append
                ? [Item(98_801, HelmetId, 1), Item(98_802, ArmorId, 3)]
                : special ? [Item(98_801, fanId, 10, 45)]
                : weaponLast ? [Item(98_802, weaponId, 4, 45), Item(98_801, HelmetId, 1, 45)]
                : [Item(98_801, HelmetId, 1, circle ? (byte)45 : (byte)0)];
            PlayerItemSnapshotItem[] runes = alternate ? [] : _append
                ? [Item(98_804, DirectOverlayFrontBreakRuneId, 103), Item(98_805, DirectOverlaySyntheticImpregnabilityRuneId, 104)]
                : circle ? [Item(98_804, CircleId, 103)]
                : scenario.StartsWith("fury-", StringComparison.Ordinal)
                    ? [Item(98_804, scenario == "fury-zero" ? FuryZeroId : FuryZeroId + 1, 102)]
                    : [];
            var attributes = _append ? CombatAttributes() : new Dictionary<string, string>();
            if (scenario.StartsWith("fury-", StringComparison.Ordinal))
                attributes["StatusFlags"] = BuildStatusFlags(IronGuardStatusFlag);
            if (scenario == "vip-seven") attributes["VipLevel"] = "7";
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: _append ? 300 : 0,
                spirit: 0, minHitPoints: _append ? 100_000 : 100, stamina: 100,
                spellIds: [DirectOverlaySpellId], playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = equipment,
                    RuneItems = runes,
                    InventoryItems = _append && !alternate ? [Item(98_803, ReplacementId, 0)] : [],
                }, attributes: attributes) with { AlternateEquipmentActive = alternate };
            if (_append) owner = owner with { CombatGear = Gear() };
            var dummy = CreateSession(uid: TargetPlayerUid, name: "QueryStageDummy", mapId: 1_000,
                x: 839, y: 584, classCode: 2_005, reborn: 2, strength: 300, spirit: 0,
                minHitPoints: 100_000, stamina: 100, spellIds: [DirectOverlaySpellId],
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [Item(99_801, DummyHelmetId, 1), Item(99_802, ArmorId, 3)],
                }, attributes: CombatAttributes()) with { CombatGear = Gear() };
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints"));
            Assert.Null(owner.EquipmentHitpointsStatus);
            Assert.Null(owner.PhysicalStatusOverlay);
            Assert.Equal(2, owner.Character.Reborn);
            if (_append)
            {
                Assert.Equal(100_000U, LegacyMaxHitpointsCalculator.Calculate(owner.Character, owner.PlayerItems,
                    owner.CombatGear!, world, alternateEquipmentActive: alternate).Total);
                var raw = LegacyPlayerStatusCalculator.CalculateFields(owner.Character, owner.PlayerItems, world,
                    100_000, 0, alternateEquipmentActive: alternate);
                Assert.Equal(300U, raw[LegacyPlayerStatusPacket.MinAttackIndex]);
                Assert.Equal(301U, raw[LegacyPlayerStatusPacket.MaxAttackIndex]);
                Assert.Equal(1_000U, raw[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
                Assert.Equal(60U, raw[LegacyPlayerStatusPacket.BreakthroughIndex]);
                Assert.Equal(50U, raw[LegacyPlayerStatusPacket.CounteractionIndex]);
                Assert.Equal(0U, raw[LegacyPlayerStatusPacket.DefenceIndex]);
                Assert.Equal(0U, raw[LegacyPlayerStatusPacket.ItemBlessIndex]);
            }
            else
            {
                Assert.Equal(circle ? (weaponLast ? 1_290U : special ? 500U : 545U) : 500U,
                    LegacyMaxHitpointsCalculator.CalculateEquipmentHitpoints(owner.PlayerItems,
                        world.ItemsById, world.ItemAddBonusByItemTypeIdAndLevel));
                if (circle)
                {
                    Assert.Equal(CircleId, Assert.Single(owner.PlayerItems.RuneItems).ItemId);
                    Assert.Equal((byte)45, owner.PlayerItems.EquipmentItems[0].Enchant);
                }
            }
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (!_append && uid == PlayerUid && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                            update.Uid == PlayerUid && update.Updates.Count == 1 &&
                            update.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints)
                        {
                            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(Owner, out var maximum));
                            HpObservations.Enqueue((maximum, Owner.Character.MinHitPoints));
                        }
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            }
            _sessions.Set(_connections[PlayerUid], owner);
            _sessions.Set(_connections[TargetPlayerUid], dummy);
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, TimeProvider: clock,
                MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public WorldDataCatalog World { get; }
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public ConcurrentQueue<(uint Maximum, long Hitpoints)> HpObservations { get; } = new();
        public WorldSession Owner => Current(PlayerUid);
        private WorldSession Current(uint uid)
        {
            Assert.True(_sessions.TryGet(_connections[uid], out var current));
            return Assert.IsType<WorldSession>(current);
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            _context with { Connection = _connections[uid], Session = Current(uid) };

        public async Task QueryEquipmentAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));

        public async Task<LegacyPlayerStatusPacket> QueryStatusAsync()
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(Context(PlayerUid),
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
            var bytes = Assert.Single(Order.ToArray().Skip(start), frame => frame.Recipient == PlayerUid &&
                LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes.Bytes), out var status));
            return status;
        }

        public async Task EarnPairAsync()
        {
            Assert.True(_append);
            await AttackAsync(PlayerUid, TargetPlayerUid);
            Assert.Equal(66U, (await QueryStatusAsync()).Breakthrough);
            await AttackAsync(TargetPlayerUid, PlayerUid);
            var earned = await QueryStatusAsync();
            Assert.Equal(66U, earned.Breakthrough);
            Assert.Equal(75U, earned.Counteraction);
            Assert.Equal(99_775, Owner.Character.MinHitPoints);
        }

        private async Task AttackAsync(uint attacker, uint target)
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(Context(attacker),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: attacker, OpponentUid: target,
                    X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
            var animation = Assert.Single(ParseSpellAnimations(Order.ToArray().Skip(start)
                .Where(frame => frame.Recipient == attacker).Select(frame => frame.Bytes)),
                packet => packet.SpellId == DirectOverlaySpellId);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(target, hit.Uid);
            Assert.Equal(225U, hit.Damage); // EqualBP:300 -> rb2 half150 -> no-break +75.
        }

        public async Task EquipReplacementAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(new MsgItemUsagePacket(98_803, 1, MsgItemUsageAction.Equip,
                Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes()));

        public async Task ToggleToNormalAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(new MsgItemUsagePacket(0, 0, MsgItemUsageAction.UnAlternante,
                Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes()));

        private static Dictionary<string, string> CombatAttributes() => new()
        {
            ["PkMode"] = "0", ["BattlePower"] = "120", ["LegacyCombatGearAttackSpellIndex"] = "3",
            ["LegacyCombatGearAttackRateRoll"] = "99", ["LegacyPhysicalRefineryRoll"] = "0",
            ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99", ["LegacyPhysicalPlayerCriticalRoll"] = "99",
            ["LegacyPhysicalDashRoll"] = "0", ["DashRate"] = "0",
            ["LegacyPhysicalBreakthroughRefineryRoll"] = "0", ["LegacyPhysicalImpregnabilityRoll"] = "0",
        };
        private static CombatGearSnapshot Gear() => CombatGearSnapshot.Empty with
        {
            Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
        };
        public void Dispose()
        {
            GC.KeepAlive(_connections);
            _log.Dispose();
        }
    }
}
