using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMonsterDeathDeferredMoneyTests
{
    private const uint PlayerUid = 1_293_600;
    private const string MoneyAttribute = "LegacyMonsterDropMoneyAmount";
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> Handlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData(41, 1)]
    [InlineData(49, 4)]
    public async Task D13PhysicalKillDispatcherKeepsForcedAmountOnEveryRealMoneyPile(int roll, int count)
    {
        using var fixture = new Fixture(roll, [50_000]);
        var primed = fixture.Monsters.ApplyDamage(fixture.Monster, 4_999, DateTimeOffset.UtcNow);
        Assert.False(primed.Killed);

        await Handlers.HandleAsync(fixture.Context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: fixture.Monster.Uid,
            X: fixture.Monster.X, Y: fixture.Monster.Y,
            AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
            ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));

        Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, fixture.Context.ProtocolProfile);
        Assert.Contains(fixture.Sent, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Query.AttackType == MsgAttackType.Death && packet.Query.OpponentUid == fixture.Monster.Uid);
        AssertMoneyPiles(fixture, Enumerable.Repeat(50_000L, count).ToArray(),
            Enumerable.Repeat(1_091_020u, count).ToArray());
        Assert.Contains($"kind=Money amount=50000 count={count}", fixture.Log.ToString());
    }

    [Fact]
    public async Task ActualDropBodyDemandsEachMoneyPayloadOnlyAfterItsOwnTerrainPlacement()
    {
        using var fixture = new Fixture(49, [50, 200, 6_000, 50_000]);
        int priorCellReads = 0;
        fixture.Attributes.BeforeMoneyRead = call =>
        {
            Assert.True(fixture.Cells.Reads > priorCellReads, "This pile must check real terrain before reading money.");
            priorCellReads = fixture.Cells.Reads;
            var visible = fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow);
            Assert.Equal(call - 1, visible.Count);
            Assert.Equal(call - 1, FloorPackets(fixture).Count);
            Assert.All(visible, entry =>
            {
                Assert.Equal(FloorItemRuntimePickupKind.Money, entry.Kind);
                Assert.True(entry.Packet.ItemId > 0);
                Assert.True(entry.Amount > 0); // No draft/placeholder entered the visible index.
            });
        };

        await InvokeActualDropBodyAsync(fixture);

        Assert.Equal(4, fixture.Attributes.MoneyReads);
        AssertMoneyPiles(fixture, [50, 200, 6_000, 50_000],
            [1_090_000u, 1_090_010u, 1_091_000u, 1_091_020u]);
        Assert.Equal(new[] { (10, 10), (9, 9), (8, 8), (7, 7) },
            FloorPackets(fixture).Select(packet => ((int)packet.X, (int)packet.Y)).ToArray());
    }

    [Fact]
    public async Task ActualDropBodyRejectsBlockedTerrainWithoutDemandingMoney()
    {
        using var fixture = new Fixture(49, [50_000], ground: static (_, _) => false);

        await InvokeActualDropBodyAsync(fixture);

        Assert.True(fixture.Cells.Reads > 0);
        Assert.Equal(0, fixture.Attributes.MoneyReads);
        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.DoesNotContain("sent legacy monster death drop", fixture.Log.ToString());
    }

    [Fact]
    public async Task ActualDropBodyDoesNotReadAmountsForUnplaceableLaterPiles()
    {
        using var fixture = new Fixture(49, [200, 50_000],
            ground: static (x, y) => x == 10 && y == 10);

        await InvokeActualDropBodyAsync(fixture);

        Assert.Equal(1, fixture.Attributes.MoneyReads);
        AssertMoneyPiles(fixture, [200], [1_090_010u]);
        Assert.Contains("kind=Money amount=200 count=1", fixture.Log.ToString());
    }

    [Fact]
    public async Task ActualDropBodyRetriesRemainingPilesFromClearedOriginAfterFailedPlacement()
    {
        using var fixture = new Fixture(49, [6_000, 50_000],
            ground: static (x, y) => x == 1 && y == 1);
        int cellReadsAtDemand = 0;
        fixture.Attributes.BeforeMoneyRead = call =>
        {
            Assert.Equal(1, call);
            Assert.True(fixture.Cells.Reads > 1);
            cellReadsAtDemand = fixture.Cells.Reads;
            Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
            Assert.Empty(FloorPackets(fixture));
        };

        await InvokeActualDropBodyAsync(fixture);

        Assert.Equal(1, fixture.Attributes.MoneyReads);
        AssertMoneyPiles(fixture, [6_000], [1_091_000u]);
        var packet = Assert.Single(FloorPackets(fixture));
        Assert.Equal((ushort)1, packet.X);
        Assert.Equal((ushort)1, packet.Y);
        Assert.True(fixture.Cells.Reads > cellReadsAtDemand);
        Assert.Contains("kind=Money amount=6000 count=1", fixture.Log.ToString());
    }

    [Fact]
    public async Task ActualMoneyRetryRejectsZeroEdgesEvenWhenTerrainMarksThoseCellsWalkable()
    {
        using var fixture = new Fixture(49, [6_000, 50_000],
            ground: static (x, y) => x <= 1 && y <= 1);

        await InvokeActualDropBodyAsync(fixture);

        Assert.Equal(1, fixture.Attributes.MoneyReads);
        AssertMoneyPiles(fixture, [6_000], [1_091_000u]);
        var packet = Assert.Single(FloorPackets(fixture));
        Assert.Equal((ushort)1, packet.X);
        Assert.Equal((ushort)1, packet.Y);
        Assert.All(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow), entry =>
        {
            Assert.True(entry.Packet.X > 0);
            Assert.True(entry.Packet.Y > 0);
        });
    }

    [Fact]
    public async Task ActualDropBodyDoesNotDemandMoneyWhenPinnedCapacityRejectsPlacement()
    {
        var floor = new FloorItemRuntimeStateStore(capacity: 1);
        using var fixture = new Fixture(49, [50_000], floor: floor);
        var packet = LegacyFloorItemPacket.FromMonsterDrop(1_090_010, 31, 31, fixture.Session.Character, 80_000_001);
        var seed = FloorItemRuntimeEntry.ForMoney(1002, packet, 125, ownerOnly: true);
        floor.AddVisible(seed);
        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            floor.TryReservePickup(seed, PlayerUid, false, DateTimeOffset.UtcNow, out var reservation));
        using var pinned = Assert.IsType<FloorItemRuntimeStateStore.PickupReservation>(reservation);

        await InvokeActualDropBodyAsync(fixture);

        Assert.Equal(0, fixture.Attributes.MoneyReads);
        Assert.Equal(0, fixture.Cells.Reads); // Capacity gate precedes terrain and amount generation.
        Assert.Empty(FloorPackets(fixture));
        Assert.Same(seed, Assert.Single(floor.SnapshotVisible(DateTimeOffset.UtcNow)));
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(501, 1)]
    public async Task ActualNonMoneyDropBranchesNeverDemandMoneyAmount(int roll, int expectedItems)
    {
        using var fixture = new Fixture(roll, [50_000]);

        await InvokeActualDropBodyAsync(fixture);

        Assert.Equal(0, fixture.Attributes.MoneyReads);
        var packets = FloorPackets(fixture);
        Assert.Equal(expectedItems, packets.Count);
        Assert.All(packets, packet => Assert.Equal(1_001_000u, packet.ItemId));
        var visible = fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow);
        Assert.Equal(expectedItems, visible.Count);
        Assert.All(visible, entry =>
        {
            Assert.Equal(FloorItemRuntimePickupKind.Item, entry.Kind);
            Assert.Equal(0, entry.Amount);
        });
    }

    [Fact]
    public async Task ActualBlockedMapEarlyReturnDoesNotDemandMoney()
    {
        using var fixture = new Fixture(49, [50_000], mapId: 10_250);

        await InvokeActualDropBodyAsync(fixture);

        Assert.Equal(0, fixture.Attributes.MoneyReads);
        Assert.Equal(0, fixture.Cells.Reads);
        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ActualMissingWorldDataEarlyReturnDoesNotDemandMoney()
    {
        using var fixture = new Fixture(49, [50_000], worldDataLoaded: false);

        await InvokeActualDropBodyAsync(fixture);

        Assert.Equal(0, fixture.Attributes.MoneyReads);
        Assert.Equal(0, fixture.Cells.Reads);
        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Contains("world data is not loaded", fixture.Log.ToString());
    }

    [Fact]
    public async Task ActualMissingMoneyCatalogTriesFourPayloadsWithoutPublishingOrRedrawingForDiagnostics()
    {
        using var fixture = new Fixture(49, [50, 200, 6_000, 50_000]);
        var context = fixture.Context with
        {
            WorldData = LegacyItemWorldDataFixture.Create(new ItemType(1_001_000, "LifePotion", []))
        };
        fixture.Attributes.BeforeMoneyRead = call =>
        {
            Assert.Equal(call, fixture.Cells.Reads); // Each attempt placed before catalog validation.
            Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
            Assert.Empty(FloorPackets(fixture));
        };

        await InvokeActualDropBodyAsync(fixture, context);

        Assert.Equal(4, fixture.Attributes.MoneyReads);
        Assert.Equal(4, fixture.Cells.Reads);
        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Contains("source money drop item id 1091020 is not present in the local itemtype catalog",
            fixture.Log.ToString());
        Assert.Contains("itemId=1091020", fixture.Log.ToString());
        Assert.DoesNotContain("sent legacy monster death drop", fixture.Log.ToString());
    }

    [Fact]
    public async Task ActualMissingFloorRuntimeKeepsUntrackedCompatibilitySendsAndWarnsInsteadOfClaimingPickupTracking()
    {
        using var fixture = new Fixture(49, [50, 200, 6_000, 50_000]);
        var context = fixture.Context with { FloorItems = null };
        fixture.Attributes.BeforeMoneyRead = call =>
        {
            Assert.Equal(call - 1, FloorPackets(fixture).Count);
            Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        };

        await InvokeActualDropBodyAsync(fixture, context);

        Assert.Equal(4, fixture.Attributes.MoneyReads);
        Assert.Equal(0, fixture.Cells.Reads); // This compatibility path does not own actual placement.
        var packets = FloorPackets(fixture);
        Assert.Equal(new[] { 1_090_000u, 1_090_010u, 1_091_000u, 1_091_020u },
            packets.Select(packet => packet.ItemId).ToArray());
        Assert.Equal(4, packets.Select(packet => packet.Uid).Distinct().Count());
        Assert.All(packets, packet =>
        {
            Assert.Equal(LegacyFloorItemDropType.Visible, packet.DropType);
            Assert.Equal(PlayerUid, packet.ItemOwnerUid);
            Assert.Equal((ushort)10, packet.X);
            Assert.Equal((ushort)10, packet.Y);
        });
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        const string warning = "monster death drop was sent but floor item runtime state is not loaded for pickup tracking";
        Assert.Single(fixture.Log.ToString().Split(Environment.NewLine), line => line.Contains(warning, StringComparison.Ordinal));
        Assert.Contains("kind=Money amount=50000 count=4", fixture.Log.ToString());
    }

    // Edge/order controls invoke the actual async handler body, not a copied
    // selector or RNG helper. They are not dispatcher-entry or live-client proof.
    private static async ValueTask InvokeActualDropBodyAsync(Fixture fixture, WorldPacketHandlerContext? context = null)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("TrySendLegacyMonsterDeathDropAsync",
            BindingFlags.Static | BindingFlags.NonPublic, binder: null,
            [typeof(WorldPacketHandlerContext), typeof(WorldSession), typeof(LegacyMonsterInstance),
                typeof(Monster), typeof(CancellationToken)], modifiers: null);
        Assert.NotNull(method);
        var invoked = method.Invoke(null,
            [context ?? fixture.Context, fixture.Session, fixture.Monster, fixture.Family, CancellationToken.None]);
        await Assert.IsType<ValueTask>(invoked);
    }

    private static void AssertMoneyPiles(Fixture fixture, IReadOnlyList<long> amounts, IReadOnlyList<uint> ids)
    {
        var packets = FloorPackets(fixture);
        Assert.Equal(amounts.Count, packets.Count);
        Assert.Equal(amounts.Count, fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow).Count);
        Assert.Equal(packets.Count, packets.Select(packet => packet.Uid).Distinct().Count());
        for (int index = 0; index < packets.Count; index++)
        {
            var packet = packets[index];
            Assert.Equal(ids[index], packet.ItemId);
            Assert.Equal(LegacyFloorItemDropType.Visible, packet.DropType);
            Assert.Equal(PlayerUid, packet.ItemOwnerUid);
            Assert.Equal((ushort)40, packet.MaxLife);
            Assert.True(fixture.Floor.TryGetVisibleEntry(packet.Uid, out var entry));
            Assert.Equal(FloorItemRuntimePickupKind.Money, entry.Kind);
            Assert.Equal(amounts[index], entry.Amount);
            Assert.Equal(packet, entry.Packet);
            Assert.True(entry.OwnerOnly);
            Assert.Null(entry.Item);
        }
    }

    private static List<LegacyFloorItemPacket> FloorPackets(Fixture fixture)
    {
        var result = new List<LegacyFloorItemPacket>();
        foreach (var bytes in fixture.Sent.ToArray())
            if (LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet)) result.Add(packet);
        return result;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly ConcurrentQueue<byte[]> Sent = new();
        internal readonly StringWriter Log = new();
        internal readonly MonsterRuntimeStateStore Monsters = new();
        internal readonly WorldSessionStore Sessions = new();
        internal readonly MoneyAttributes Attributes;
        internal readonly CountingCells Cells;
        internal readonly FloorItemRuntimeStateStore Floor;
        internal readonly WorldSession Session;
        internal readonly Monster Family;
        internal readonly LegacyMonsterInstance Monster;
        internal readonly WorldPacketHandlerContext Context;

        internal Fixture(int roll, uint[] amounts, Func<int, int, bool>? ground = null,
            FloorItemRuntimeStateStore? floor = null, int mapId = 1002, bool worldDataLoaded = true,
            uint monsterDynamicId = 0, uint killerDynamicId = 0)
        {
            Attributes = new MoneyAttributes(roll, amounts, killerDynamicId);
            Cells = new CountingCells(ground ?? (static (_, _) => true));
            Floor = floor ?? new FloorItemRuntimeStateStore();
            Family = new Monster(9_901, "DeferredMoneyFixture", new Dictionary<string, string>
            {
                ["life"] = "5000", ["lookface"] = "100", ["level"] = "1",
                ["defense"] = "0", ["defence"] = "0", ["defense2"] = "10000",
                ["attack_range"] = "1", ["drop_hp"] = "1001000"
            }, []);
            Monster = new LegacyMonsterInstance(450_000, mapId, 10, 10, 100, 1, 5_000, 5_000,
                9_901, "DeferredMoneyFixture", false, 1, 0, DynamicId: monsterDynamicId);
            Monsters.RegisterDynamicMonster(Monster, Family);
            Session = new WorldSession(1, "deferred-money", new CharacterSnapshot(
                Uid: PlayerUid, Name: "DeferredMoney", Body: 1003, Face: 0, Hair: 0,
                Class: 1000, FirstClass: 0, SecondClass: 0, Reborn: 0, Level: 120,
                Experience: 0, MapId: mapId, X: 9, Y: 10, PreviousMapId: mapId,
                PreviousMapX: 9, PreviousMapY: 10, Strength: 1000, Agility: 60,
                Vitality: 100, Spirit: 0, AttributePoints: 0, MinHitPoints: 5000,
                MinMana: 0, Money: 0, ConquerPoints: 0, BoundConquerPoints: 0,
                BankConquerPoints: 0, PkPoints: 0, Online: 1,
                SourceFile: "fixture://deferred-money", Attributes: Attributes,
                PaidNobility: new Dictionary<string, string>()),
                PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) =>
                {
                    Sent.Enqueue(frame.ToArray());
                    return ValueTask.CompletedTask;
                });
            Sessions.Set(connection, Session);
            var catalog = worldDataLoaded ? LegacyItemWorldDataFixture.Create(
                new ItemType(1_090_000, "MoneySmall", []), new ItemType(1_090_010, "MoneyMedium", []),
                new ItemType(1_090_020, "MoneyLarge", []), new ItemType(1_091_000, "Money6000", []),
                new ItemType(1_091_010, "Money10000", []), new ItemType(1_091_020, "Money50000", []),
                new ItemType(1_001_000, "LifePotion", [])) : null;
            Context = new WorldPacketHandlerContext(connection, Log, Session, WorldData: catalog,
                Sessions: Sessions, MonsterStates: Monsters, FloorItems: Floor,
                Terrain: new LegacyGameMapTerrainQuery([new LegacyDMapTerrain(mapId,
                    "fixture://deferred-money-terrain", 32, 32, Cells)]),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public void Dispose() => Log.Dispose();
    }

    // Instrument existing forced attributes, not production RNG. Enumeration,
    // keys and values expose the same current dictionary state without reads.
    private sealed class MoneyAttributes : IReadOnlyDictionary<string, string>
    {
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        private readonly uint[] amounts;
        internal int MoneyReads { get; private set; }
        internal Action<int>? BeforeMoneyRead { get; set; }

        internal MoneyAttributes(int roll, uint[] amounts, uint dynamicId = 0)
        {
            Assert.NotEmpty(amounts);
            this.amounts = amounts;
            values["LegacyMonsterDropRoll"] = roll.ToString(CultureInfo.InvariantCulture);
            values[MoneyAttribute] = amounts[0].ToString(CultureInfo.InvariantCulture);
            values["PkMode"] = "3";
            values["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture);
        }

        public bool TryGetValue(string key, out string value)
        {
            if (string.Equals(key, MoneyAttribute, StringComparison.OrdinalIgnoreCase))
            {
                MoneyReads++;
                BeforeMoneyRead?.Invoke(MoneyReads);
                values[MoneyAttribute] = amounts[Math.Min(MoneyReads - 1, amounts.Length - 1)]
                    .ToString(CultureInfo.InvariantCulture);
            }
            return values.TryGetValue(key, out value!);
        }

        public string this[string key] => values[key];
        public IEnumerable<string> Keys => values.Keys;
        public IEnumerable<string> Values => values.Values;
        public int Count => values.Count;
        public bool ContainsKey(string key) => values.ContainsKey(key);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CountingCells : IReadOnlyList<LegacyDMapCell>
    {
        private readonly LegacyDMapCell[] cells;
        internal int Reads { get; private set; }

        internal CountingCells(Func<int, int, bool> ground) => cells = Enumerable.Range(0, 32 * 32)
            .Select(index => new LegacyDMapCell(index % 32, index / 32,
                (short)(ground(index % 32, index / 32) ? 0 : 1), 0, 0)).ToArray();

        public LegacyDMapCell this[int index]
        {
            get { Reads++; return cells[index]; }
        }
        public int Count => cells.Length;
        public IEnumerator<LegacyDMapCell> GetEnumerator() => ((IEnumerable<LegacyDMapCell>)cells).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
