using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Declared DB/learned inputs, not stock6003/acquisition or global OnSoul proof.
    [Theory]
    [InlineData(1_000L)]
    [InlineData(1_039L)]
    public async Task NativeCounterKillSwitchAdmittedCostsPrecedeEachToggle(long map)
    {
        var free = map == 1_039;
        using var fixture = new CounterKillCanUseFixture(map: map,
            stamina: free ? 5UL : 100UL, mana: free ? 20 : 1_000);
        fixture.AssertValidSelection();
        fixture.AssertResources(free ? 5UL : 100UL, free ? 20 : 1_000);
        fixture.AssertNoColdDeadline();

        var start = fixture.FrameCount;
        await fixture.SwitchAsync();
        fixture.AssertActive(true);
        fixture.AssertResources(free ? 5UL : 90UL, free ? 20 : 960);
        fixture.AssertFrames(start, free ? [] :
            [(LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 960UL)], true);
        fixture.AssertNoColdDeadline();

        start = fixture.FrameCount;
        await fixture.SwitchAsync(); // Native switch has no manual-Magic AttackStamp cadence.
        fixture.AssertActive(false);
        fixture.AssertResources(free ? 5UL : 80UL, free ? 20 : 920);
        fixture.AssertFrames(start, free ? [] :
            [(LegacyMsgUpdateDataType.Stamina, 80UL), (LegacyMsgUpdateDataType.Mana, 920UL)], false);
        fixture.AssertNoColdDeadline();
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
    }

    [Fact]
    public async Task NativeCounterKillSwitchFailedStaminaNeverTogglesOrSpendsMana()
    {
        using var fixture = new CounterKillCanUseFixture(stamina: 5);
        fixture.AssertValidSelection();
        fixture.AssertResources(5, 1_000);
        fixture.AssertNoColdDeadline();
        var state = fixture.ActiveState;
        var start = fixture.FrameCount;

        await fixture.SwitchAsync();

        Assert.Equal(state, fixture.ActiveState);
        fixture.AssertResources(5, 1_000);
        fixture.AssertFrames(start, [], toggle: null);
        fixture.AssertNoColdDeadline();
    }

    [Fact]
    public async Task NativeCounterKillSwitchFailedManaRetainsEarlierStaminaSpendWithoutToggle()
    {
        using var fixture = new CounterKillCanUseFixture(mana: 20);
        fixture.AssertValidSelection();
        fixture.AssertResources(100, 20);
        fixture.AssertNoColdDeadline();
        var state = fixture.ActiveState;
        var start = fixture.FrameCount;

        await fixture.SwitchAsync();

        Assert.Equal(state, fixture.ActiveState);
        fixture.AssertResources(90, 20);
        fixture.AssertFrames(start, [(LegacyMsgUpdateDataType.Stamina, 90UL)], toggle: null);
        fixture.AssertNoColdDeadline();
    }

    [Theory]
    [InlineData(1_000L)]
    [InlineData(1_039L)]
    public async Task NativeCounterKillSwitchEarnedColdRejectsAfterCostsAndAdmitsExactDeadline(long map)
    {
        var free = map == 1_039;
        using var fixture = new CounterKillCanUseFixture(map: map, coldTime: 2_000);
        fixture.AssertValidSelection();
        fixture.AssertNoColdDeadline();
        var origin = fixture.Now;
        var start = fixture.FrameCount;

        await fixture.SwitchAsync(); // Earn the actual deadline; never seed MagicCooldowns.
        fixture.AssertActive(true);
        fixture.AssertResources(free ? 100UL : 90UL, free ? 1_000 : 960);
        fixture.AssertFrames(start, free ? [] :
            [(LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 960UL)], true);
        fixture.AssertColdDeadline(origin.AddMilliseconds(2_000));
        // If the first real admission is RED, later cold assertions are not claimed reached.
        fixture.Advance(TimeSpan.FromMilliseconds(1_999));
        var before = fixture.ActiveState;
        start = fixture.FrameCount;
        await fixture.SwitchAsync();
        Assert.Equal(before, fixture.ActiveState);
        fixture.AssertResources(free ? 100UL : 80UL, free ? 1_000 : 920);
        fixture.AssertFrames(start, free ? [] :
            [(LegacyMsgUpdateDataType.Stamina, 80UL), (LegacyMsgUpdateDataType.Mana, 920UL)], toggle: null);
        fixture.AssertColdDeadline(origin.AddMilliseconds(2_000)); // Rejection does not renew.

        fixture.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(origin.AddMilliseconds(2_000), fixture.Now);
        start = fixture.FrameCount;
        await fixture.SwitchAsync();
        fixture.AssertActive(false);
        fixture.AssertResources(free ? 100UL : 70UL, free ? 1_000 : 880);
        fixture.AssertFrames(start, free ? [] :
            [(LegacyMsgUpdateDataType.Stamina, 70UL), (LegacyMsgUpdateDataType.Mana, 880UL)], false);
        fixture.AssertColdDeadline(origin.AddMilliseconds(4_000));
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
    }

    [Theory]
    [InlineData("missing-book")]
    [InlineData("missing-catalog")]
    [InlineData("missing-level")]
    [InlineData("wrong-sort")]
    public async Task NativeCounterKillSwitchEarlySelectionDenialNeverEntersCanUse(string input)
    {
        using var fixture = new CounterKillCanUseFixture(input: input, coldTime: 2_000);
        fixture.AssertEarlyDenialSelection(input);
        fixture.AssertResources(100, 1_000);
        fixture.AssertNoColdDeadline();
        var state = fixture.ActiveState;
        var start = fixture.FrameCount;

        await fixture.SwitchAsync();

        Assert.Equal(state, fixture.ActiveState);
        fixture.AssertResources(100, 1_000);
        fixture.AssertFrames(start, [], toggle: null);
        fixture.AssertNoColdDeadline();
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
    }

    private sealed class CounterKillCanUseFixture : IDisposable
    {
        private const ushort Id = 6_003;
        private const ushort LearnedLevel = 2;
        private readonly WorldSessionStore sessions = new();
        private readonly List<byte[]> sent = [];
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConquerTcpConnection connection;
        private readonly WorldPacketHandlerContext context;
        private readonly CounterKillCanUseClock clock = new();
        private readonly string input;
        private readonly int coldTime;

        public CounterKillCanUseFixture(long map = 1_000, ulong stamina = 100,
            long mana = 1_000, int coldTime = 0, string input = "valid")
        {
            Assert.Contains(input, new[]
                { "valid", "missing-book", "missing-catalog", "missing-level", "wrong-sort" });
            this.input = input;
            this.coldTime = coldTime;
            // Level0 is a deliberate cheaper decoy, not an alternative legal level2 row.
            var rows = new List<MagicType>
            {
                CreateMagicRow(Id, sort: 29, level: 0, useMana: 1, useStamina: 1,
                    weaponType: 0, coolDown: 1_000, coldTime: 0),
            };
            if (input != "missing-level")
                rows.Add(CreateMagicRow(Id, sort: input == "wrong-sort" ? (byte)6 : (byte)29,
                    level: (byte)LearnedLevel, useMana: 40, useStamina: 10,
                    weaponType: 0, coolDown: 1_000, coldTime: coldTime));
            if (input == "missing-catalog")
                rows = [CreateMagicRow(6_004, sort: 29, useMana: 0, useStamina: 0, coldTime: 0)];
            WorldData = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], rows);
            var owner = CreateSession(mapId: map, classCode: 142, minHitPoints: 5_000,
                minMana: mana, stamina: stamina, spirit: 0, strength: 0,
                spellEntries: input == "missing-book" ? [] : [CreatePlayerSpell(Id, LearnedLevel)],
                playerItems: PlayerItemSnapshot.Empty);
            // Declared physical-client MODEL prestate; no socket/login acquisition claim.
            var physicalClient = new LegacyNativeGameClientRuntime();
            owner = owner with { NativeGameClient = physicalClient };
            connection = CreateCounterKillPhysicalModelConnection(sent, physicalClient);
            sessions.Set(connection, owner);
            context = new WorldPacketHandlerContext(connection, log, owner,
                WorldData: WorldData, Sessions: sessions, TimeProvider: clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Empty(sent);
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.Null(owner.MagicCooldowns);
            Assert.Empty(owner.PlayerItems.EquipmentItems);
            Assert.Empty(owner.PlayerItems.RuneItems);
            Assert.Equal((ushort)0, owner.LegacyRandomSpell);
        }

        public WorldDataCatalog WorldData { get; }
        public WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(connection, out var current) ? current : null);
        public int FrameCount => sent.Count;
        public DateTimeOffset Now => clock.GetUtcNow();
        public (bool Exists, string? Raw) ActiveState =>
            Owner.Character.Attributes.TryGetValue("LegacyCounterKillActive", out var value)
                ? (true, value) : (false, null);
        public void Advance(TimeSpan amount) => clock.Advance(amount);

        public void AssertValidSelection()
        {
            Assert.Equal("valid", input);
            AssertRawSelectedRow(expectedSort: "29");
            var book = Assert.Single(Owner.PlayerSpells.Spells);
            Assert.Equal(Id, book.Id);
            Assert.Equal(LearnedLevel, book.Level);
            Assert.Equal((byte)0, book.UseSpellSoul); // Declared learned input, not hidden OnSoul state.
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
                Owner.PlayerSpells, WorldData, Id, out var selectedBook, out var selected));
            Assert.Equal(book, selectedBook);
            Assert.Equal(Id, selected.Id);
            Assert.Equal((byte)LearnedLevel, selected.Level);
            Assert.Equal(LegacyMagicSort.CounterKill, selected.Sort);
            Assert.Equal((byte)10, selected.UseStamina);
            Assert.Equal((ushort)40, selected.UseMana);
            Assert.Equal(coldTime, selected.ColdTime);
            Assert.Equal((ushort)1_000, selected.CoolDown);
        }

        public void AssertEarlyDenialSelection(string expectedInput)
        {
            Assert.Equal(expectedInput, input);
            if (input == "missing-book")
            {
                Assert.Empty(Owner.PlayerSpells.Spells);
                AssertRawSelectedRow("29");
            }
            else
            {
                var book = Assert.Single(Owner.PlayerSpells.Spells);
                Assert.Equal(Id, book.Id);
                Assert.Equal(LearnedLevel, book.Level);
                if (input == "missing-catalog")
                    Assert.False(WorldData.MagicByTypeId.ContainsKey(Id));
                else if (input == "missing-level")
                    Assert.Equal("0", Assert.Single(WorldData.MagicByTypeId[Id]).Fields[8]);
                else
                    AssertRawSelectedRow("6");
            }
            var resolved = LegacyMagicRuntime.TryResolveLearnedSpell(
                Owner.PlayerSpells, WorldData, Id, out _, out var selected);
            Assert.Equal(input == "wrong-sort", resolved);
            if (resolved) Assert.Equal(LegacyMagicSort.AttachStatus, selected.Sort);
        }

        private void AssertRawSelectedRow(string expectedSort)
        {
            var rows = WorldData.MagicByTypeId[Id];
            Assert.Equal(2, rows.Count);
            var selected = Assert.Single(rows, row => row.Fields[8] == "2");
            Assert.Equal(Id, selected.TypeId);
            Assert.Equal("6003", selected.Fields[0]);
            Assert.Equal("6003", selected.Fields[1]);
            Assert.Equal(expectedSort, selected.Fields[2]);
            Assert.Equal("40", selected.Fields[9]);
            Assert.Equal("0", selected.Fields[22]); // No weapon-token producer input.
            Assert.Equal("10", selected.Fields[29]);
            Assert.Equal("1000", selected.Fields[33]);
            Assert.Equal(coldTime.ToString(CultureInfo.InvariantCulture), selected.Fields[47]);
        }

        public Task SwitchAsync()
        {
            var current = Owner;
            var query = new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                X: 830, Y: 584, AttackType: MsgAttackType.CounterKillSwitch,
                Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellLevel: 99, SpellId: 0);
            return PacketHandlers.HandleAsync(context with { Session = current },
                ReadFrame(new MsgAttackPacket(query).ToFrameBytes())).AsTask();
        }

        public void AssertResources(ulong stamina, long mana)
        {
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["Stamina"]);
            Assert.Equal(mana, Owner.Character.MinMana);
        }

        public void AssertActive(bool active) =>
            Assert.Equal(active ? "1" : "0", Owner.Character.Attributes["LegacyCounterKillActive"]);

        public void AssertNoColdDeadline() =>
            Assert.True(Owner.MagicCooldowns is null || !Owner.MagicCooldowns.ContainsKey(Id));

        public void AssertColdDeadline(DateTimeOffset expected)
        {
            Assert.NotNull(Owner.MagicCooldowns);
            Assert.Equal(expected, Owner.MagicCooldowns![Id]);
        }

        public void AssertFrames(int start,
            (LegacyMsgUpdateDataType Type, ulong Value)[] costs, bool? toggle)
        {
            var frames = sent.Skip(start).ToArray();
            Assert.Equal(costs.Length + (toggle.HasValue ? 1 : 0), frames.Length);
            for (var index = 0; index < costs.Length; index++)
            {
                Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var packet));
                Assert.Equal(PlayerUid, packet.Uid);
                var scalar = Assert.Single(packet.Updates);
                Assert.Equal(costs[index].Type, scalar.Type);
                Assert.Equal(costs[index].Value, scalar.Value);
            }
            if (toggle is { } active)
            {
                Assert.True(MsgAttackPacket.TryParse(ReadFrame(frames[^1]), out var packet));
                Assert.Equal(MsgAttackType.CounterKillSwitch, packet.Query.AttackType);
                Assert.Equal(PlayerUid, packet.Query.Uid);
                Assert.Equal(PlayerUid, packet.Query.OpponentUid);
                Assert.Equal(Id, packet.Query.SpellId);
                Assert.Equal(LearnedLevel, packet.Query.SpellLevel);
                Assert.Equal(active ? 1 : 0, packet.Query.Damage);
            }
            Assert.Empty(ParseMagicColdTimePackets(frames)); // Native ColdTime is a silent field.
            Assert.Empty(ParseSpellAnimations(frames));
        }

        public void Dispose() => log.Dispose();
    }

    // Declared physical-client MODEL prestate only; NOT socket birth or login acquisition.
    private static ConquerTcpConnection CreateCounterKillPhysicalModelConnection(
        List<byte[]> sent, LegacyNativeGameClientRuntime physicalClient)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                lock (sent)
                {
                    sent.Add(frame.ToArray());
                }

                return ValueTask.CompletedTask;
            },
            nativeGameClient: physicalClient);
    }

    private sealed class CounterKillCanUseClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount)
        {
            Assert.True(amount >= TimeSpan.Zero);
            now += amount; // Only later public input; no timer/runtime loop or cooldown seeding.
        }
    }
}
