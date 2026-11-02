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
    private sealed partial class RootMagicEntryFixture : IAsyncDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly RootRuntimeClock clock = new(RootMagicEntryOrigin);
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly WorldPacketHandlerContext context;
        private readonly bool hasRawRidingCrop;
        private readonly WorldServerFrameHandler? itemEffectRuntimeHandler;
        private readonly CancellationTokenSource? itemEffectRuntimeCancel;
        private readonly TextWriter? itemEffectRuntimeLog;
        private bool itemEffectRuntimeStarted;
        private readonly bool itemEffectXpControls;
        private bool itemEffectPlayerRuntimeStarted;
        private Exception? rootXpWaitFailure;
        private readonly bool nativeDeathCasterQueryControls;

        public RootMagicEntryFixture(ushort selectedId = 1_000, ushort rawCadence = 0,
            int coldTime = 0, bool passive = false, bool ridingCrop = false,
            ulong stamina = 100, long mana = 1_000,
            Func<RootMagicEntryFixture, uint, byte[], ValueTask>? onSend = null,
            bool catalogArrowWithoutLearnedBook = false,
            bool nativeCadenceControls = false, bool omitSelectedLearnedInput = false,
            bool ridingCropQueryControls = false,
            RootManualItemEffectInputs? itemEffects = null, bool itemEffectLifetimeControls = false,
            bool itemEffectXpControls = false, bool nativeDeathCasterQueryControls = false,
            bool playerEligibilityControls = false, uint shieldDuration = 30,
            ushort shieldMana = 0, byte shieldStamina = 0, bool opaqueCaseControls = false,
            ushort shieldCooldown = 0, int shieldColdTime = 0)
        {
            hasRawRidingCrop = ridingCrop;
            this.nativeDeathCasterQueryControls = nativeDeathCasterQueryControls;
            Assert.True(!nativeDeathCasterQueryControls || itemEffectLifetimeControls);
            this.itemEffectXpControls = itemEffectXpControls;
            Assert.True(!itemEffectXpControls || itemEffectLifetimeControls);
            var rows = new List<MagicType>
            {
                CreateMagicRow(1_000, sort: 1, useMana: 40, useStamina: 10,
                    damage: 100, range: 4, coolDown: rawCadence, coldTime: coldTime),
                CreateMagicRow(1_011, sort: 48, useMana: 40, useStamina: 10,
                    damage: 100, range: 4, coolDown: rawCadence, coldTime: coldTime),
                CreateMagicRow(1_010, sort: 5, useMana: 40, useStamina: 10,
                    damage: 100, range: 4, maxTargets: 3, coolDown: rawCadence, coldTime: coldTime),
                CreateMagicRow(1_005, sort: 2, useMana: 40, useStamina: 10,
                    damage: 100, range: 4, coolDown: rawCadence, coldTime: coldTime),
                CreateMagicRow(18_220, sort: 103, useMana: 40, useStamina: 10,
                    damage: 30_300, range: 18, maxTargets: 4, coolDown: rawCadence, coldTime: coldTime),
                CreateMagicRow(7_001, sort: 32, useMana: 0, useStamina: 0,
                    damage: 100, coolDown: 0, coldTime: 0),
            };
            if (playerEligibilityControls && !itemEffectLifetimeControls)
                rows.Add(CreateMagicRow(1_001, sort: 1, useMana: 40, useStamina: 10,
                    damage: 100, range: 4, coolDown: rawCadence, coldTime: coldTime));
            if (itemEffectLifetimeControls)
                rows.Add(CreateMagicRow(1_001, sort: 1, useMana: 0, useStamina: 0,
                    damage: nativeDeathCasterQueryControls ? 9_999U : 9_000U,
                    range: 18, coolDown: 0, coldTime: 0));
            if (itemEffectXpControls)
                rows.Add(CreateMagicRow(1_020, sort: 6, useMana: shieldMana, useStamina: shieldStamina,
                    damage: 100, duration: shieldDuration, range: 18, coolDown: shieldCooldown, coldTime: shieldColdTime));
            if (nativeCadenceControls)
                rows.Add(CreateMagicRow(1_115, sort: 1, useMana: 40, useStamina: 10,
                    damage: 100, range: 4, coolDown: rawCadence, coldTime: coldTime));
            if (ridingCropQueryControls)
                rows.Add(CreateMagicRow(14_500, sort: 6, useMana: 0, useStamina: 0,
                    damage: 1, duration: 60, range: 18, coolDown: 0, coldTime: 0));
            ItemType CropQueryInput(int id, string hp, string mana = "0")
            {
                var fields = Enumerable.Repeat("0", 60).ToArray();
                fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "NativeCrop" + id;
                fields[18] = hp;
                fields[19] = mana;
                return new ItemType(id, fields[1], fields);
            }
            IReadOnlyList<ItemType>? queryItems = ridingCrop
                ? [CropQueryInput(111_095, "5000"), CropQueryInput(203_003, "0")] : null;
            if (itemEffects is not null)
                queryItems =
                [
                    CropQueryInput(111_095, "5000", itemEffects.MaximumMana.ToString(CultureInfo.InvariantCulture)),
                    CropQueryInput(410_095, "0"), CropQueryInput(150_095, "0"), CropQueryInput(120_095, "0"),
                    .. (ridingCrop ? new[] { CropQueryInput(203_003, "0") } : Array.Empty<ItemType>()),
                ];
            if (nativeDeathCasterQueryControls)
                queryItems = [.. (queryItems ?? Array.Empty<ItemType>()),
                    CropQueryInput(111_005, "50000", "1000"), CropQueryInput(120_005, "50000")];
            var sourceArrowCatalog = catalogArrowWithoutLearnedBook ? CreateRootMissingArrowSourceGear() : null;
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], rows,
                items: queryItems,
                combatGear: sourceArrowCatalog is null ? null : [sourceArrowCatalog],
                magicEffects: passive ? MagicEffectParser.Parse(
                    $"[{selectedId * 100L}]\nName=RootEntry\nDescEx=Passive:~source~root~admission~control.\n",
                    "fixture://root-entry-MagicEffect.ini") : null);
            SelectedEffect = passive ? Assert.Single(world.MagicEffectsById[selectedId * 100L]) : null;
            SourceArrowCatalog = sourceArrowCatalog is null ? null : world.CombatGearByGearTypeAndLevel[(401, 1)];
            var equipment = ridingCrop
                ? new PlayerItemSnapshotItem[]
                {
                    new() { Uid = 99_180, ItemId = 203_003, Position = 18,
                        Durability = 100, MaximumDurability = 100 },
                    new() { Uid = 99_181, ItemId = 111_095, Position = 1,
                        Durability = 100, MaximumDurability = 100 },
                }
                : [];
            if (itemEffects is not null)
                equipment =
                [
                    .. equipment.Where(item => item.Position != 1),
                    new() { Uid = 99_181, ItemId = 111_095, Position = 1,
                        Durability = 100, MaximumDurability = 100 },
                    new() { Uid = 99_184, ItemId = 410_095, Position = 4, Effect = itemEffects.RightEffect,
                        Durability = 100, MaximumDurability = 100 },
                    new() { Uid = 99_186, ItemId = 150_095, Position = 6, Effect = itemEffects.RingEffect,
                        Durability = 100, MaximumDurability = 100 },
                    new() { Uid = 99_182, ItemId = 120_095, Position = 2, Effect = itemEffects.NeckEffect,
                        Durability = 100, MaximumDurability = 100 },
                ];
            var owner = CreateSession(mapId: 1_000, strength: 0, spirit: 0,
                classCode: catalogArrowWithoutLearnedBook ? 4_005 : 142,
                minHitPoints: 5_000, minMana: mana, stamina: stamina,
                spellIds: catalogArrowWithoutLearnedBook ? [1_000, 1_011, 1_010, 1_005, 7_001] :
                    [1_000, 1_011, 1_010, 1_005, 18_220, 7_001],
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = equipment,
                    InventoryItems = itemEffectXpControls
                        ? [new() { Uid = 99_183, ItemId = 120_095, Position = 0, Effect = 200,
                            Durability = 100, MaximumDurability = 100 }] : [],
                },
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["BattlePower"] = "120",
                    ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99",
                    ["LegacyPhysicalPlayerCriticalRoll"] = "99",
                });
            if (opaqueCaseControls)
            {
                var fields = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.Ordinal)
                    { ["Opaque"] = "one", ["opaque"] = "two" };
                owner = owner with { Character = owner.Character with { Attributes = fields } };
            }
            if (itemEffects is not null)
            {
                var inputs = LegacyCharacterAttributeMap.Copy(owner.Character.Attributes);
                LegacyCharacterAttributeMap.Set(inputs, "LegacyWeaponEffectRateRoll.1175", itemEffects.MpRoll.ToString(CultureInfo.InvariantCulture));
                LegacyCharacterAttributeMap.Set(inputs, "LegacyWeaponEffectRateRoll.1095", itemEffects.RingRoll.ToString(CultureInfo.InvariantCulture));
                LegacyCharacterAttributeMap.Set(inputs, "LegacyWeaponEffectRateRoll.1020", itemEffects.NeckRoll.ToString(CultureInfo.InvariantCulture));
                owner = owner with { Character = owner.Character with { Attributes = inputs } };
            }
            if (ridingCropQueryControls)
                owner = owner with { PlayerSpells = new PlayerSpellSnapshot(
                    [.. owner.PlayerSpells.Spells, CreatePlayerSpell(14_500)]) };
            if (nativeCadenceControls)
                owner = owner with { PlayerSpells = new PlayerSpellSnapshot(
                    [.. owner.PlayerSpells.Spells, CreatePlayerSpell(1_115)]) };
            if (itemEffectXpControls)
                owner = owner with { PlayerSpells = new PlayerSpellSnapshot(
                    [.. owner.PlayerSpells.Spells, CreatePlayerSpell(1_020)]) };
            if (playerEligibilityControls)
                owner = owner with { PlayerSpells = new PlayerSpellSnapshot(
                    [.. owner.PlayerSpells.Spells, CreatePlayerSpell(1_001)]) };
            if (omitSelectedLearnedInput)
                owner = owner with { PlayerSpells = new PlayerSpellSnapshot(
                    owner.PlayerSpells.Spells.Where(entry => entry.Id != selectedId).ToArray()) };
            if (catalogArrowWithoutLearnedBook)
                owner = owner with { CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1, Items = [new CombatGearSnapshotItem(401, Level: 1, Exp: 0, Equip: 1,
                        Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
                } };
            var peer = CreateSession(uid: TargetPlayerUid, name: "RootEntryPeer", mapId: 1_000,
                x: 832, y: 584, strength: 0, spirit: 0,
                minHitPoints: 100_000, minMana: 1_000, stamina: 100,
                spellIds: itemEffectLifetimeControls ? [1_001] : [],
                playerItems: nativeDeathCasterQueryControls ? PlayerItemSnapshot.Empty with
                {
                    EquipmentItems =
                    [
                        new() { Uid = 99_191, ItemId = 111_005, Position = 1,
                            Durability = 100, MaximumDurability = 100 },
                        new() { Uid = 99_192, ItemId = 120_005, Position = 2,
                            Durability = 100, MaximumDurability = 100 },
                    ],
                } : PlayerItemSnapshot.Empty,
                attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["BattlePower"] = "120" });
            if (itemEffectLifetimeControls)
            {
                Assert.NotNull(itemEffects); // Raw declared inputs only; the public query/cast must earn all state.
                itemEffectRuntimeLog = TextWriter.Synchronized(log);
                itemEffectRuntimeHandler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null,
                    worldData: world, runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                        true, TimeSpan.FromMilliseconds(20), clock), log: itemEffectRuntimeLog,
                    protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
                sessions = GetHandlerSessions(itemEffectRuntimeHandler);
                itemEffectRuntimeCancel = new CancellationTokenSource();
            }
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            sessions.Set(connections[PlayerUid], owner);
            sessions.Set(connections[TargetPlayerUid], peer);
            context = new WorldPacketHandlerContext(connections[PlayerUid], itemEffectRuntimeLog ?? log, owner,
                WorldData: world, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
                TimeProvider: clock, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            if (ridingCropQueryControls)
                context = context with { LegacyClock = LegacySignedMillisecondClock.FromTimeProvider(
                    clock, epochMilliseconds: 10_000) };
            Assert.Equal((ushort)0, Owner.LegacyRandomSpell);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 0x32));
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 333));
            Assert.Equal(ridingCrop ? 99_180U : 0U,
                LegacyShowEquipmentSlots.FromEquipmentItems(Owner.PlayerItems.EquipmentItems).RidingCrop);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(connections[PlayerUid], out var value) ? value : null);
        public WorldSession Peer => Assert.IsType<WorldSession>(
            sessions.TryGet(connections[TargetPlayerUid], out var value) ? value : null);
        public DateTimeOffset Now => clock.GetUtcNow();
        public MagicEffect? SelectedEffect { get; }
        public CombatGear? SourceArrowCatalog { get; }
        public WorldDataCatalog WorldData => Assert.IsType<WorldDataCatalog>(context.WorldData);
        public LegacyNativeMagicRow NativeRow(ushort id) =>
            Assert.IsType<LegacyNativeMagicRow>(WorldData.FindNativeMagicRow(id, 0));
        public byte[][] FramesSince(uint recipient, int start) => Order.ToArray().Skip(start)
            .Where(entry => entry.Recipient == recipient).Select(entry => entry.Bytes).ToArray();
        public void Advance(TimeSpan amount) => clock.Advance(amount); // No runtime loop is started.

        public Task CastAsync(ushort id, uint? target = null, ushort x = 832, ushort y = 584)
        {
            var current = Owner;
            var query = new MsgAttackQuery(Uid: PlayerUid,
                OpponentUid: target ?? (id is 1_005 or 7_001 ? PlayerUid : TargetPlayerUid),
                X: x, Y: y, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: id);
            return PacketHandlers.HandleAsync(context with { Session = current },
                ReadFrame(new MsgAttackPacket(query).ToFrameBytes())).AsTask();
        }

        public async Task CastShieldThroughFrameAsync()
        {
            var handler = Assert.IsType<WorldServerFrameHandler>(itemEffectRuntimeHandler);
            var query = new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid, X: 829, Y: 583,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 1_020);
            await handler.HandleFrameAsync(connections[PlayerUid], ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));
        }

        public void SetMalformedShieldInput(string kind)
        {
            var current = Owner;
            var fields = new Dictionary<string, string>(current.Character.Attributes, StringComparer.Ordinal);
            if (kind == "marker")
            {
                fields["LegacyRootItemEffectFlag.8.NativeOwned"] = "True";
                fields["legacyrootitemeffectflag.8.nativeowned"] = "True";
            }
            else if (kind == "status")
                fields["StatusFlags"] = fields["StatusFlags"] + "not-a-number/";
            else throw new ArgumentOutOfRangeException(nameof(kind));
            sessions.Set(connections[PlayerUid], current with { Character = current.Character with { Attributes = fields } });
        }

        public Task QueryRootRidingCropEquipmentAsync() => PacketHandlers.HandleAsync(context with { Session = Owner },
            ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes())).AsTask();
        public Task UnequipRootRidingCropAsync() => PacketHandlers.HandleAsync(context with { Session = Owner },
            ReadFrame(new MsgItemUsagePacket(99_180, 18, MsgItemUsageAction.Unequip, 0, 0, 0, 0)
                .ToFrameBytes())).AsTask();

        public Task QueryRootItemEffectsAsync() => QueryRootRidingCropEquipmentAsync();
        public async Task QueryRootItemEffectsThroughFrameAsync()
        {
            var handler = Assert.IsType<WorldServerFrameHandler>(itemEffectRuntimeHandler);
            // The real Frame dispatch registers this connection's transport
            // lifetime before routing the real equipment query. The fixture
            // still owns synthetic in-memory sessions; this is not saved login.
            int first = Order.Count;
            await handler.HandleFrameAsync(connections[PlayerUid],
                ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            Assert.Equal(200U, LegacyPhysicalStatusOverlay.ForOwner(Owner).NecklaceItemEffect);
            Assert.Contains(FramesSince(PlayerUid, first), bytes =>
                ReadFrame(bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode);
        }
        public Task UnequipRootItemEffectAsync(ushort position)
        {
            var item = Assert.Single(Owner.PlayerItems.EquipmentItems, value => value.Position == position);
            return PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgItemUsagePacket(item.Uid, position, MsgItemUsageAction.Unequip, 0, 0, 0, 0)
                    .ToFrameBytes())).AsTask();
        }
        public void MoveRootItemEffectOwner(ushort x, ushort y)
        {
            var current = Owner;
            Assert.Equal(PlayerUid, current.Character.Uid);
            sessions.Set(connections[PlayerUid], current with { Character = current.Character with { X = x, Y = y } });
        }
        public bool RootShieldClockExists() => LegacyRootItemEffectFlagClock.HasRegistration(
            sessions, connections[PlayerUid], PlayerUid, 8);
        public WorldSession? RootItemEffectSlot(uint uid) =>
            sessions.TryGet(connections[uid], out var current) ? current : null;
        public WorldSession? RetireRootItemEffectSubject(uint uid, bool rebind)
        {
            Assert.Contains(uid, new[] { PlayerUid, TargetPlayerUid });
            Assert.True(sessions.TryGet(connections[uid], out var current));
            Assert.NotNull(current);
            Assert.Equal((long)uid, current.Character.Uid);
            Assert.True(sessions.TryRemove(connections[uid], out _));
            if (!rebind) return null;
            var replacement = current with { Character = current.Character with
                { Uid = (long)uid + 9_000, Name = "RootItemEffectReplacement" } };
            sessions.Set(connections[uid], replacement);
            return replacement; // Only the original role identity changes; no cache/HP/flag/clock outcome is assigned.
        }

        public async Task MountAndWaitAsync(bool queryEquippedCrop = true)
        {
            if (hasRawRidingCrop && queryEquippedCrop)
                await QueryRootRidingCropEquipmentAsync(); // Real source query, not a desired cached crop value.
            await CastAsync(7_001); // Actual public Riding handler, never a seeded Ride bit/timer.
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, 0x32));
            Assert.Single(ParseSpellAnimations(FramesSince(PlayerUid, 0)), packet => packet.SpellId == 7_001);
            Advance(TimeSpan.FromMilliseconds(500)); // Source-natural later input, not a stamp mutation.
        }

        public async Task StartRootItemEffectRuntimeAsync()
        {
            Assert.False(itemEffectRuntimeStarted);
            var handler = Assert.IsType<WorldServerFrameHandler>(itemEffectRuntimeHandler);
            var cancel = Assert.IsType<CancellationTokenSource>(itemEffectRuntimeCancel);
            Assert.Equal(0, clock.ScheduledTimerCount);
            using (clock.Identify("global"))
                FineRainExpiryStartGlobal(handler, cancel.Token); // Actual global owner only, not a player/login ticker.
            itemEffectRuntimeStarted = true;
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
        }
        public async Task AdvanceRootItemEffectRuntimeAsync(TimeSpan amount)
        {
            Assert.True(itemEffectRuntimeStarted);
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
            clock.Advance(amount);
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
        }
        public async Task KillRootItemEffectOwnerAsync()
        {
            var query = new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: PlayerUid,
                X: checked((ushort)Owner.Character.X), Y: checked((ushort)Owner.Character.Y), AttackType: MsgAttackType.Magic,
                Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                Unknown: 0, SpellId: 1_001);
            await PacketHandlers.HandleAsync(context with
                { Connection = connections[TargetPlayerUid], Session = Peer },
                ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));
        }

        public Task EquipRootXpNeckAsync()
        {
            Assert.True(itemEffectXpControls);
            return PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgItemUsagePacket(99_183, 2, MsgItemUsageAction.Equip, 0, 0, 0, 0)
                    .ToFrameBytes())).AsTask();
        }

        public async Task EarnRootItemEffectXpListAsync()
        {
            Assert.True(itemEffectXpControls);
            Assert.False(itemEffectPlayerRuntimeStarted);
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRuntimeUpdateTicker.XpListFlag));
            Assert.False(Owner.Character.Attributes.TryGetValue("XPCount", out var initial) &&
                initial != "0"); // Read-only preflight; never seeds the runtime gauge or flag.
            var startedAt = Now;
            var first = Order.Count;
            await StartRootItemEffectRuntimeAsync();
            var handler = Assert.IsType<WorldServerFrameHandler>(itemEffectRuntimeHandler);
            var cancel = Assert.IsType<CancellationTokenSource>(itemEffectRuntimeCancel);
            using (clock.Identify("player"))
                StartHandlerRuntimeUpdateLoop(handler, connections[PlayerUid], Owner, cancel.Token);
            itemEffectPlayerRuntimeStarted = true;
            await WaitForRootXpRuntimeAsync("initial timers", 0, () =>
                clock.ActiveGeneration("global") != 0 && clock.ActiveGeneration("player") != 0);
            for (var count = 1; count <= 100; count++)
            {
                await AdvanceRootItemEffectXpRuntimeAsync(TimeSpan.FromSeconds(8), count);
                var expected = (count == 100 ? 0 : count).ToString(CultureInfo.InvariantCulture);
                // A scheduled timer is not a completed player tick. Observe
                // the actual ticker's state and corresponding outbound circle,
                // then wait until its next one-shot delay is armed before the
                // next eight-second advance. The global timer alone is no proof.
                await WaitForRootXpRuntimeAsync("XP state and frame", count, () =>
                    Owner.Character.Attributes.TryGetValue("XPCount", out var actual) && actual == expected &&
                    HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRuntimeUpdateTicker.XpListFlag) == (count == 100) &&
                    ParseMsgUpdates(FramesSince(PlayerUid, first)).Any(packet => packet.Uid == PlayerUid &&
                        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPCircle && entry.Value == (ulong)count)) &&
                    (count != 100 || ParseMsgUpdates(FramesSince(PlayerUid, first)).Any(packet => packet.Uid == PlayerUid &&
                        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPList && entry.Value == 1))) &&
                    clock.ActiveGeneration("global") != 0 && clock.ActiveGeneration("player") != 0);
                Assert.Equal(expected, Owner.Character.Attributes["XPCount"]);
                Assert.Equal(count == 100,
                    HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRuntimeUpdateTicker.XpListFlag));
            }
            Assert.Equal(startedAt.AddSeconds(800), Now); //100 separate real due calls, not one catch-up leap.
            Assert.Equal(Enumerable.Range(1, 100).Select(count => (ulong)count).ToArray(),
                ParseMsgUpdates(FramesSince(PlayerUid, first)).Where(packet => packet.Uid == PlayerUid)
                    .SelectMany(packet => packet.Updates).Where(entry => entry.Type == LegacyMsgUpdateDataType.XPCircle)
                    .Select(entry => entry.Value).ToArray());
            Assert.Contains(ParseMsgUpdates(FramesSince(PlayerUid, first)), packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPList && entry.Value == 1));
        }
        public async Task AdvanceRootItemEffectXpRuntimeAsync(TimeSpan amount, int count = 0)
        {
            Assert.True(itemEffectPlayerRuntimeStarted);
            await WaitForRootXpRuntimeAsync("before advance", count, () =>
                clock.ActiveGeneration("global") != 0 && clock.ActiveGeneration("player") != 0);
            long player = clock.ActiveGeneration("player"), global = clock.ActiveGeneration("global");
            clock.Advance(amount);
            // Each real Task.Delay is one-shot. A strictly newer timer from
            // each same worker proves that worker finished its prior iteration;
            // unrelated timers and the other worker cannot satisfy this wait.
            await WaitForRootXpRuntimeAsync("after advance", count, () =>
                clock.ActiveGeneration("player") > player && clock.ActiveGeneration("global") > global);
        }

        private async Task WaitForRootXpRuntimeAsync(string phase, int count, Func<bool> ready)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (!ready())
            {
                var handler = Assert.IsType<WorldServerFrameHandler>(itemEffectRuntimeHandler);
                if (FineRainExpiryGlobalStopped(handler) || DateTimeOffset.UtcNow >= deadline)
                {
                    var current = RootItemEffectSlot(PlayerUid);
                    string xp = current is not null && current.Character.Attributes.TryGetValue("XPCount", out var raw)
                        ? raw : "<absent>";
                    bool list = current is not null && HasLegacyStatusFlag(current.Character.Attributes, LegacyRuntimeUpdateTicker.XpListFlag);
                    var workers = handler.CaptureLegacyRuntimeWorkerTasks();
                    string runtimeLog;
                    lock (itemEffectRuntimeLog ?? log) runtimeLog = log.ToString();
                    if (runtimeLog.Length > 8_000) runtimeLog = runtimeLog[^8_000..];
                    var failure = new TimeoutException($"Root XP {phase}; step={count}; now={Now:O}; xp={xp}; xpList={list}; " +
                        $"frames={Order.Count}; scheduled={clock.ScheduledTimerCount}; timers=[{clock.Describe()}]; " +
                        $"globalStopped={FineRainExpiryGlobalStopped(handler)}; " +
                        $"workers=[{string.Join(",", workers.Select(task => task.Status))}]; " +
                        $"faultedWorkers={handler.LegacyRuntimeFaultedWorkerCount}; log={runtimeLog}");
                    rootXpWaitFailure = failure;
                    throw failure;
                }
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        public void AssertProducedStamp(DateTimeOffset expected) =>
            Assert.Equal(expected.ToString("O", CultureInfo.InvariantCulture),
                Owner.Character.Attributes["LegacyAttackStampUtc"]);

        public void AssertCostPairSince(int start, ulong stamina, ulong mana)
        {
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["Stamina"]);
            Assert.Equal(checked((long)mana), Owner.Character.MinMana);
            Assert.Equal(new[]
            {
                (LegacyMsgUpdateDataType.Stamina, stamina),
                (LegacyMsgUpdateDataType.Mana, mana),
            }, RootMagicEntryCosts(FramesSince(PlayerUid, start)));
            Assert.Empty(RootMagicEntryCosts(FramesSince(TargetPlayerUid, start)));
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (itemEffectRuntimeCancel is not null)
                {
                    await itemEffectRuntimeCancel.CancelAsync();
                    if (itemEffectRuntimeStarted)
                        await WaitUntilAsync(() => clock.ScheduledTimerCount == 0 &&
                            FineRainExpiryGlobalStopped(itemEffectRuntimeHandler!), TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception cleanup) when (rootXpWaitFailure is not null)
            {
                throw new AggregateException("Root XP runtime wait and cleanup failed.", rootXpWaitFailure, cleanup);
            }
            finally
            {
                itemEffectRuntimeCancel?.Dispose();
                GC.KeepAlive(connections);
                log.Dispose();
            }
        }
    }

    private sealed record RootManualItemEffectInputs(uint RightEffect = 202, uint RingEffect = 0,
        uint NeckEffect = 0, ushort MaximumMana = 1_000, uint MpRoll = 49,
        uint RingRoll = 19, uint NeckRoll = 19);

    private static readonly DateTimeOffset RootMagicEntryOrigin = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static (LegacyMsgUpdateDataType Type, ulong Value)[] RootMagicEntryCosts(IEnumerable<byte[]> frames) =>
        ParseMsgUpdates(frames).SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana)
            .Select(entry => (entry.Type, entry.Value)).ToArray();
}
