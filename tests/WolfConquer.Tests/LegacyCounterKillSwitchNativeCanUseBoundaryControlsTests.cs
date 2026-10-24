using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task NativeCounterKillExplicitNoStorePublishesLocalCostsWithoutInventedPersistence()
    {
        using var fixture = new CounterKillBoundaryFixture(withStore: false, registered: false);
        fixture.AssertSelection();
        Assert.False(fixture.HasStore);
        Assert.Null(fixture.StoredOwner);
        var original = fixture.Initial;
        fixture.AssertUntouched(original);

        await fixture.SwitchAsync();

        fixture.AssertFrames(0, [(LegacyMsgUpdateDataType.Stamina, 90UL),
            (LegacyMsgUpdateDataType.Mana, 960UL)], toggle: true);
        Assert.Null(fixture.StoredOwner);
        Assert.Same(original, fixture.Owner); // Public ValueTask returns no replacement local record.
        fixture.AssertUntouched(original); // No Store persistence is invented from wire delivery.
        Assert.Equal(3, fixture.FrameCount);
    }

    [Fact]
    public async Task NativeCounterKillStorePresentUnregisteredIsNotNoStoreFallback()
    {
        using var fixture = new CounterKillBoundaryFixture(withStore: true, registered: false,
            coldTime: 2_000);
        fixture.AssertSelection();
        Assert.True(fixture.HasStore);
        Assert.Null(fixture.StoredOwner);
        var original = fixture.Initial;
        fixture.AssertUntouched(original);

        await fixture.SwitchAsync();

        Assert.Null(fixture.StoredOwner);
        Assert.Same(original, fixture.Owner);
        fixture.AssertUntouched(original);
        fixture.AssertFrames(0, [], toggle: null);
        Assert.Equal(0, fixture.FrameCount);
    }

    [Fact]
    public async Task NativeCounterKillRegisteredZeroHpHasNoInventedAliveAdmissionGate()
    {
        using var fixture = new CounterKillBoundaryFixture(hitPoints: 0, coldTime: 2_000);
        fixture.AssertSelection();
        Assert.Same(fixture.Initial, fixture.StoredOwner);
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x05)); // Declared HP0, not Dead.
        fixture.AssertUntouched(fixture.Owner);
        var origin = fixture.Now;

        await fixture.SwitchAsync();

        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x05));
        fixture.AssertResources(90, 960);
        fixture.AssertActive(true);
        fixture.AssertCold(origin.AddMilliseconds(2_000));
        fixture.AssertFrames(0, [(LegacyMsgUpdateDataType.Stamina, 90UL),
            (LegacyMsgUpdateDataType.Mana, 960UL)], toggle: true);
        Assert.Equal(3, fixture.FrameCount);
    }

    [Theory]
    [InlineData(0x78)] // Native CannonBarrage120.
    [InlineData(0x79)] // Native BlackbeardsRage121.
    public async Task NativeCounterKillVisibleColdExclusionSkipsAndDoesNotRenewEarnedDeadline(int flag)
    {
        using var fixture = new CounterKillBoundaryFixture(coldTime: 2_000);
        fixture.AssertSelection();
        fixture.AssertUntouched(fixture.Owner);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, flag));
        var origin = fixture.Now;

        await fixture.SwitchAsync(); // FIRST earn a real deadline through the public entry.
        fixture.AssertResources(90, 960);
        fixture.AssertActive(true);
        fixture.AssertCold(origin.AddMilliseconds(2_000));
        fixture.AssertFrames(0, [(LegacyMsgUpdateDataType.Stamina, 90UL),
            (LegacyMsgUpdateDataType.Mana, 960UL)], toggle: true);

        fixture.Advance(TimeSpan.FromMilliseconds(500));
        fixture.SetVisibleColdExclusion(flag); // Declared visible input, NOT acquisition/hidden cold seeding.
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, flag));
        Assert.True(fixture.Now < origin.AddMilliseconds(2_000));
        var start = fixture.FrameCount;
        await fixture.SwitchAsync();
        fixture.AssertResources(80, 920); // Exclusion bypasses ONLY cold; costs are still first.
        fixture.AssertActive(false);
        fixture.AssertCold(origin.AddMilliseconds(2_000)); // No renewal to origin+2500.
        fixture.AssertFrames(start, [(LegacyMsgUpdateDataType.Stamina, 80UL),
            (LegacyMsgUpdateDataType.Mana, 920UL)], toggle: false);

        fixture.SetVisibleColdExclusion(null);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x78));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x79));
        Assert.Equal(origin.AddMilliseconds(500), fixture.Now);
        start = fixture.FrameCount;
        var original = fixture.Owner;
        var originalValues = System.Text.Json.JsonSerializer.Serialize(original);
        await fixture.SwitchAsync(); // The same earned deadline still refuses without spending either resource.
        Assert.Same(original, fixture.Owner);
        Assert.Equal(originalValues, System.Text.Json.JsonSerializer.Serialize(fixture.Owner));
        fixture.AssertResources(80, 920);
        fixture.AssertActive(false);
        fixture.AssertCold(origin.AddMilliseconds(2_000));
        fixture.AssertFrames(start, [], toggle: null);
        Assert.Equal(6, fixture.FrameCount);
    }

    private sealed class CounterKillBoundaryFixture : IDisposable
    {
        private const ushort Id = 6_003;
        private const ushort Level = 2;
        private readonly WorldSessionStore? sessions;
        private readonly ConquerTcpConnection connection;
        private readonly WorldPacketHandlerContext context;
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly CounterKillBoundaryClock clock = new();
        private readonly List<byte[]> frames = [];
        private readonly int coldTime;

        public CounterKillBoundaryFixture(bool withStore = true, bool registered = true,
            long hitPoints = 5_000, int coldTime = 0)
        {
            Assert.True(withStore || !registered);
            this.coldTime = coldTime;
            WorldData = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(Id, sort: 29, level: 0, useMana: 1, useStamina: 1,
                     weaponType: 0, coolDown: 1_000, coldTime: 0),
                 CreateMagicRow(Id, sort: 29, level: (byte)Level, useMana: 40, useStamina: 10,
                     weaponType: 0, coolDown: 1_000, coldTime: coldTime)]);
            Initial = CreateSession(mapId: 1_000, classCode: 142, minHitPoints: hitPoints,
                minMana: 1_000, stamina: 100, spirit: 0, strength: 0,
                spellEntries: [CreatePlayerSpell(Id, Level)], playerItems: PlayerItemSnapshot.Empty);
            // Declared physical-client MODEL prestate; Store-unregistered stays unregistered.
            var physicalClient = new LegacyNativeGameClientRuntime();
            Initial = Initial with { NativeGameClient = physicalClient };
            connection = CreateCounterKillPhysicalModelConnection(frames, physicalClient);
            sessions = withStore ? new WorldSessionStore() : null;
            if (registered) sessions!.Set(connection, Initial);
            context = new WorldPacketHandlerContext(connection, log, Initial,
                WorldData: WorldData, Sessions: sessions, TimeProvider: clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Empty(frames);
            AssertUntouched(Initial);
            Assert.False(HasLegacyStatusFlag(Initial.Character.Attributes, 0x78));
            Assert.False(HasLegacyStatusFlag(Initial.Character.Attributes, 0x79));
            Assert.False(HasLegacyStatusFlag(Initial.Character.Attributes, LegacyFocusedFlag));
        }

        public WorldDataCatalog WorldData { get; }
        public WorldSession Initial { get; }
        public bool HasStore => sessions is not null;
        public WorldSession? StoredOwner =>
            sessions is { } store && store.TryGet(connection, out var current) ? current : null;
        public WorldSession Owner => StoredOwner ?? Initial;
        public DateTimeOffset Now => clock.GetUtcNow();
        public int FrameCount => frames.Count;

        public void AssertSelection()
        {
            var learned = Assert.Single(Owner.PlayerSpells.Spells);
            Assert.Equal(Id, learned.Id);
            Assert.Equal(Level, learned.Level);
            Assert.Equal((byte)0, learned.UseSpellSoul);
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
                Owner.PlayerSpells, WorldData, Id, out var book, out var spell));
            Assert.Equal(learned, book);
            Assert.Equal(Id, spell.Id);
            Assert.Equal((byte)Level, spell.Level);
            Assert.Equal(LegacyMagicSort.CounterKill, spell.Sort);
            Assert.Equal((byte)10, spell.UseStamina);
            Assert.Equal((ushort)40, spell.UseMana);
            Assert.Equal(coldTime, spell.ColdTime);
            Assert.Equal((ushort)1_000, spell.CoolDown);
            Assert.Equal(2, WorldData.MagicByTypeId[Id].Count);
            var raw = Assert.Single(WorldData.MagicByTypeId[Id], row => row.Fields[8] == "2");
            Assert.Equal(Id, raw.TypeId);
            Assert.Equal("6003", raw.Fields[0]);
            Assert.Equal("6003", raw.Fields[1]);
            Assert.Equal("29", raw.Fields[2]);
            Assert.Equal("40", raw.Fields[9]);
            Assert.Equal("0", raw.Fields[22]);
            Assert.Equal("10", raw.Fields[29]);
            Assert.Equal("1000", raw.Fields[33]);
            Assert.Equal(coldTime.ToString(CultureInfo.InvariantCulture), raw.Fields[47]);
            Assert.Empty(Owner.PlayerItems.EquipmentItems);
            Assert.Empty(Owner.PlayerItems.RuneItems);
            Assert.Equal((ushort)0, Owner.LegacyRandomSpell);
        }

        public void AssertUntouched(WorldSession owner)
        {
            Assert.Equal("100", owner.Character.Attributes["Stamina"]);
            Assert.Equal(1_000, owner.Character.MinMana);
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.False(owner.Character.Attributes.ContainsKey("doFocus"));
            Assert.False(owner.Character.Attributes.ContainsKey("DoFocus"));
            Assert.Null(owner.MagicCooldowns);
        }

        public void AssertResources(ulong stamina, long mana)
        {
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["Stamina"]);
            Assert.Equal(mana, Owner.Character.MinMana);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        }

        public void AssertActive(bool active) =>
            Assert.Equal(active ? "1" : "0", Owner.Character.Attributes["LegacyCounterKillActive"]);

        public void AssertCold(DateTimeOffset expected)
        {
            Assert.NotNull(Owner.MagicCooldowns);
            Assert.Equal(expected, Owner.MagicCooldowns![Id]);
            Assert.Single(Owner.MagicCooldowns);
        }

        public void Advance(TimeSpan amount) => clock.Advance(amount);

        public void SetVisibleColdExclusion(int? flag)
        {
            Assert.True(flag is null or 0x78 or 0x79);
            Assert.NotNull(sessions);
            var current = Assert.IsType<WorldSession>(StoredOwner);
            Assert.NotNull(current.MagicCooldowns); // Must already be publicly earned before flag input.
            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.Ordinal);
            if (flag is { } present) attributes["StatusFlags"] = BuildStatusFlags(present);
            else Assert.True(attributes.Remove("StatusFlags"));
            var copy = current with { Character = current.Character with { Attributes = attributes } };
            sessions!.Set(connection, copy); // NO Remove; retain original native client holder.
            Assert.Same(copy, StoredOwner);
            Assert.Same(current.MagicCooldowns, copy.MagicCooldowns);
            Assert.Equal(current.Character.MinMana, copy.Character.MinMana);
            Assert.Equal(current.Character.Attributes["Stamina"], copy.Character.Attributes["Stamina"]);
        }

        public Task SwitchAsync()
        {
            var packet = new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: PlayerUid, X: 830, Y: 584,
                AttackType: MsgAttackType.CounterKillSwitch, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                SpellLevel: 99, SpellId: 0));
            return PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(packet.ToFrameBytes())).AsTask();
        }

        public void AssertFrames(int start,
            (LegacyMsgUpdateDataType Type, ulong Value)[] costs, bool? toggle)
        {
            var observed = frames.Skip(start).ToArray();
            Assert.Equal(costs.Length + (toggle.HasValue ? 1 : 0), observed.Length);
            for (var index = 0; index < costs.Length; index++)
            {
                Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(observed[index]), out var packet));
                Assert.Equal(PlayerUid, packet.Uid);
                var scalar = Assert.Single(packet.Updates);
                Assert.Equal(costs[index].Type, scalar.Type);
                Assert.Equal(costs[index].Value, scalar.Value);
            }
            if (toggle is { } active)
            {
                Assert.True(MsgAttackPacket.TryParse(ReadFrame(observed[^1]), out var packet));
                Assert.Equal(MsgAttackType.CounterKillSwitch, packet.Query.AttackType);
                Assert.Equal(PlayerUid, packet.Query.Uid);
                Assert.Equal(PlayerUid, packet.Query.OpponentUid);
                Assert.Equal(Id, packet.Query.SpellId);
                Assert.Equal(Level, packet.Query.SpellLevel);
                Assert.Equal(active ? 1 : 0, packet.Query.Damage);
            }
            Assert.Empty(ParseMagicColdTimePackets(observed));
            Assert.Empty(ParseSpellAnimations(observed));
        }

        public void Dispose() => log.Dispose();
    }

    private sealed class CounterKillBoundaryClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount)
        {
            Assert.True(amount >= TimeSpan.Zero);
            now += amount; // Declared deterministic public-input time, not an observed run timestamp.
        }
    }
}
