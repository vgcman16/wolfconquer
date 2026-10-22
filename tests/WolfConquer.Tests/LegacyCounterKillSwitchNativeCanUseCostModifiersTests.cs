using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(15UL, false)]
    [InlineData(16UL, true)]
    public async Task NativeCounterKillScurvySurchargeUsesExactSixStaminaBoundary(
        ulong stamina, bool admitted)
    {
        using var fixture = new CounterKillCostModifiersFixture(
            stamina: stamina, mana: 37, scurvy: true);
        fixture.AssertInputs();
        fixture.AssertNoColdDeadline();
        var before = fixture.ActiveState;
        var start = fixture.FrameCount;

        await fixture.SwitchAsync();

        if (admitted)
        {
            fixture.AssertActive(true);
            fixture.AssertResources(0, 0);
            fixture.AssertFrames(start,
                [(LegacyMsgUpdateDataType.Stamina, 0UL), (LegacyMsgUpdateDataType.Mana, 0UL)], true);
        }
        else
        {
            Assert.Equal(before, fixture.ActiveState);
            fixture.AssertResources(15, 37);
            fixture.AssertFrames(start, [], toggle: null);
        }
        fixture.AssertNoColdDeadline();
        fixture.AssertScurvyUnchanged();
    }

    [Theory]
    [InlineData(1, 34)]
    [InlineData(2, 32)]
    [InlineData(3, 28)]
    [InlineData(7, 10)]
    [InlineData(8, 6)]
    [InlineData(9, 4)]
    public async Task NativeCounterKillEquippedMpMasterUsesNativeTruncatedDiscountBeforeManaGate(
        byte runeLevel, long expectedCost)
    {
        // Native is 37 - trunc(37 * percent / 100), NOT trunc(37 * remainingPercent / 100).
        using var fixture = new CounterKillCostModifiersFixture(
            mana: expectedCost, runeLevel: runeLevel);
        fixture.AssertInputs();
        fixture.AssertResources(100, expectedCost);
        fixture.AssertNoColdDeadline();
        var start = fixture.FrameCount;

        await fixture.SwitchAsync();

        fixture.AssertActive(true);
        fixture.AssertResources(90, 0);
        fixture.AssertFrames(start,
            [(LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 0UL)], true);
        fixture.AssertNoColdDeadline();
        fixture.AssertScurvyUnchanged();
    }

    [Fact]
    public async Task NativeCounterKillInactiveAlternateMpMasterCannotDiscountMana()
    {
        using var fixture = new CounterKillCostModifiersFixture(
            mana: 4, runeLevel: 9, runePosition: 123);
        fixture.AssertInputs();
        Assert.False(fixture.Owner.AlternateEquipmentActive);
        var before = fixture.ActiveState;
        var start = fixture.FrameCount;

        await fixture.SwitchAsync();

        // The same named/level9 item exists, but only inactive-alternate slot123 owns it.
        // Base mana37 is insufficient: native keeps the earlier stamina debit, with no toggle.
        Assert.Equal(before, fixture.ActiveState);
        fixture.AssertResources(90, 4);
        fixture.AssertFrames(start, [(LegacyMsgUpdateDataType.Stamina, 90UL)], toggle: null);
        fixture.AssertNoColdDeadline();
    }

    [Theory]
    [InlineData(1_000L)]
    [InlineData(1_039L)]
    public async Task NativeCounterKillModifiersSpendBeforeEarnedColdExceptMap1039(long map)
    {
        var free = map == 1_039;
        using var fixture = new CounterKillCostModifiersFixture(
            map: map, stamina: free ? 0UL : 100UL, mana: free ? 0 : 100,
            scurvy: true, runeLevel: 3, coldTime: 2_000);
        fixture.AssertInputs();
        fixture.AssertNoColdDeadline();
        fixture.AssertResources(free ? 0UL : 100UL, free ? 0 : 100);
        var origin = fixture.Now;
        var start = fixture.FrameCount;

        await fixture.SwitchAsync(); // This admission must EARN the real cooldown.
        fixture.AssertActive(true);
        fixture.AssertResources(free ? 0UL : 84UL, free ? 0 : 72);
        fixture.AssertFrames(start, free ? [] :
            [(LegacyMsgUpdateDataType.Stamina, 84UL), (LegacyMsgUpdateDataType.Mana, 72UL)], true);
        fixture.AssertColdDeadline(origin.AddMilliseconds(2_000));

        // No later assertion is claimed reached if the first public admission failed.
        fixture.Advance(TimeSpan.FromMilliseconds(1_999));
        var before = fixture.ActiveState;
        start = fixture.FrameCount;
        await fixture.SwitchAsync();
        Assert.Equal(before, fixture.ActiveState);
        fixture.AssertResources(free ? 0UL : 68UL, free ? 0 : 44);
        fixture.AssertFrames(start, free ? [] :
            [(LegacyMsgUpdateDataType.Stamina, 68UL), (LegacyMsgUpdateDataType.Mana, 44UL)], toggle: null);
        fixture.AssertColdDeadline(origin.AddMilliseconds(2_000)); // Denial cannot renew it.

        fixture.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(origin.AddMilliseconds(2_000), fixture.Now);
        start = fixture.FrameCount;
        await fixture.SwitchAsync();
        fixture.AssertActive(false);
        fixture.AssertResources(free ? 0UL : 52UL, free ? 0 : 16);
        fixture.AssertFrames(start, free ? [] :
            [(LegacyMsgUpdateDataType.Stamina, 52UL), (LegacyMsgUpdateDataType.Mana, 16UL)], false);
        fixture.AssertColdDeadline(origin.AddMilliseconds(4_000));
        fixture.AssertScurvyUnchanged();
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
    }

    private sealed class CounterKillCostModifiersFixture : IDisposable
    {
        private const ushort Id = 6_003;
        private const ushort LearnedLevel = 2;
        private const int ScurvyFlag = 0x60;
        private readonly WorldSessionStore sessions = new();
        private readonly List<byte[]> sent = [];
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConquerTcpConnection connection;
        private readonly WorldPacketHandlerContext context;
        private readonly CounterKillCostModifiersClock clock = new();
        private readonly WorldDataCatalog world;
        private readonly bool scurvy;
        private readonly byte? runeLevel;
        private readonly ushort runePosition;
        private readonly int coldTime;

        public CounterKillCostModifiersFixture(long map = 1_000, ulong stamina = 100,
            long mana = 1_000, bool scurvy = false, byte? runeLevel = null,
            ushort runePosition = 103, int coldTime = 0)
        {
            this.scurvy = scurvy;
            this.runeLevel = runeLevel;
            this.runePosition = runePosition;
            this.coldTime = coldTime;
            var runeId = runeLevel is { } level ? 4_030_800U + level : 0U;
            var items = runeLevel is { } selectedLevel
                ? new[] { new ItemType((int)runeId, $"MPMaster(+{selectedLevel})",
                    [runeId.ToString(CultureInfo.InvariantCulture), $"MPMaster(+{selectedLevel})", "0"]) }
                : Array.Empty<ItemType>();
            world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [
                    CreateMagicRow(Id, sort: 29, level: 0, useMana: 1,
                        useStamina: 1, coolDown: 1_000, coldTime: 0),
                    CreateMagicRow(Id, sort: 29, level: (byte)LearnedLevel, useMana: 37,
                        useStamina: 10, weaponType: 0, coolDown: 1_000, coldTime: coldTime),
                ], items: items);
            var owner = CreateSession(mapId: map, classCode: 142, reborn: 2,
                minHitPoints: 5_000, minMana: mana, stamina: stamina, spirit: 0, strength: 0,
                spellEntries: [CreatePlayerSpell(Id, LearnedLevel)],
                attributes: new Dictionary<string, string>
                {
                    ["Reborn"] = "2", ["Level"] = "120",
                    // Declared public status input, not an earned11040/acquisition claim.
                    ["StatusFlags"] = BuildStatusFlags(scurvy ? [ScurvyFlag] : []),
                },
                playerItems: runeLevel is null ? PlayerItemSnapshot.Empty :
                    CreatePlayerItems(new PlayerItemSnapshotItem
                    {
                        Uid = 98_603, ItemId = runeId, Position = runePosition,
                        Durability = 100, MaximumDurability = 100,
                    }));
            // Declared physical-client MODEL prestate; no socket/login acquisition claim.
            var physicalClient = new LegacyNativeGameClientRuntime();
            owner = owner with { NativeGameClient = physicalClient };
            connection = CreateCounterKillPhysicalModelConnection(sent, physicalClient);
            sessions.Set(connection, owner);
            context = new WorldPacketHandlerContext(connection, log, owner,
                WorldData: world, Sessions: sessions, TimeProvider: clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Empty(sent);
            Assert.Null(owner.MagicCooldowns);
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.Empty(owner.PlayerItems.EquipmentItems);
            Assert.Equal((ushort)0, owner.LegacyRandomSpell);
        }

        public WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(connection, out var current) ? current : null);
        public int FrameCount => sent.Count;
        public DateTimeOffset Now => clock.GetUtcNow();
        public (bool Exists, string? Raw) ActiveState =>
            Owner.Character.Attributes.TryGetValue("LegacyCounterKillActive", out var value)
                ? (true, value) : (false, null);

        public void AssertInputs()
        {
            var rawRows = world.MagicByTypeId[Id];
            Assert.Equal(2, rawRows.Count);
            var raw = Assert.Single(rawRows, row => row.Fields[8] == "2");
            Assert.Equal(Id, raw.TypeId);
            Assert.Equal("6003", raw.Fields[0]);
            Assert.Equal("6003", raw.Fields[1]);
            Assert.Equal("29", raw.Fields[2]);
            Assert.Equal("37", raw.Fields[9]);
            Assert.Equal("0", raw.Fields[22]);
            Assert.Equal("10", raw.Fields[29]);
            Assert.Equal("1000", raw.Fields[33]);
            Assert.Equal(coldTime.ToString(CultureInfo.InvariantCulture), raw.Fields[47]);
            var book = Assert.Single(Owner.PlayerSpells.Spells);
            Assert.Equal(Id, book.Id);
            Assert.Equal(LearnedLevel, book.Level);
            Assert.Equal((byte)0, book.UseSpellSoul);
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
                Owner.PlayerSpells, world, Id, out var selectedBook, out var selected));
            Assert.Equal(book, selectedBook);
            Assert.Equal(LegacyMagicSort.CounterKill, selected.Sort);
            Assert.Equal((byte)LearnedLevel, selected.Level);
            Assert.Equal((byte)10, selected.UseStamina);
            Assert.Equal((ushort)37, selected.UseMana);
            Assert.Equal(coldTime, selected.ColdTime);
            Assert.Equal("2", Owner.Character.Attributes["Reborn"]);
            Assert.Equal("120", Owner.Character.Attributes["Level"]);
            Assert.False(Owner.AlternateEquipmentActive);
            AssertScurvyUnchanged();
            if (runeLevel is { } level)
            {
                var rune = Assert.Single(Owner.PlayerItems.RuneItems);
                Assert.Equal(98_603U, rune.Uid);
                Assert.Equal(4_030_800U + level, rune.ItemId);
                Assert.Equal(runePosition, rune.Position);
                var type = Assert.Single(world.ItemsById[(int)rune.ItemId]);
                Assert.Equal($"MPMaster(+{level})", type.Name);
                Assert.Equal(0U, type.RequiredClass);
                Assert.Equal(level, (byte)(rune.ItemId % 100));
                Assert.Contains(runePosition, new ushort[] { 103, 123 });
            }
            else Assert.Empty(Owner.PlayerItems.RuneItems);
        }

        public Task SwitchAsync()
        {
            var query = new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                X: 830, Y: 584, AttackType: MsgAttackType.CounterKillSwitch,
                Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellLevel: 99, SpellId: 0);
            return PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(query).ToFrameBytes())).AsTask();
        }

        public void Advance(TimeSpan amount) => clock.Advance(amount);
        public void AssertActive(bool active) =>
            Assert.Equal(active ? "1" : "0", Owner.Character.Attributes["LegacyCounterKillActive"]);
        public void AssertResources(ulong stamina, long mana)
        {
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["Stamina"]);
            Assert.Equal(mana, Owner.Character.MinMana);
        }
        public void AssertScurvyUnchanged() =>
            Assert.Equal(BuildStatusFlags(scurvy ? [ScurvyFlag] : []), Owner.Character.Attributes["StatusFlags"]);
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
                Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update));
                Assert.Equal(PlayerUid, update.Uid);
                var scalar = Assert.Single(update.Updates);
                Assert.Equal(costs[index].Type, scalar.Type);
                Assert.Equal(costs[index].Value, scalar.Value);
            }
            if (toggle is { } active)
            {
                Assert.True(MsgAttackPacket.TryParse(ReadFrame(frames[^1]), out var response));
                Assert.Equal(PlayerUid, response.Query.Uid);
                Assert.Equal(PlayerUid, response.Query.OpponentUid);
                Assert.Equal(MsgAttackType.CounterKillSwitch, response.Query.AttackType);
                Assert.Equal(Id, response.Query.SpellId);
                Assert.Equal(LearnedLevel, response.Query.SpellLevel);
                Assert.Equal(active ? 1 : 0, response.Query.Damage);
            }
            Assert.Empty(ParseMagicColdTimePackets(frames));
            Assert.Empty(ParseSpellAnimations(frames));
        }

        public void Dispose() => log.Dispose();
    }

    private sealed class CounterKillCostModifiersClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount)
        {
            Assert.True(amount >= TimeSpan.Zero);
            now += amount;
        }
    }
}
