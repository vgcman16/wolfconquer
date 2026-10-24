using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeCounterKillStaminaSendRetirementDoesNotReachReplacement(bool rebindSameUid)
    {
        using var fixture = new CounterKillOwnerCallbackFixture();
        fixture.AssertSelection();
        var reached = 0;
        WorldSession? retired = null;
        fixture.OnSend = (current, bytes) =>
        {
            if (!CounterKillOwnerScalar(bytes, LegacyMsgUpdateDataType.Stamina, 90)) return;
            Assert.Equal(1, ++reached);
            current.AssertCommittedBeforeToggle(current.Owner, 90, 960);
            retired = current.Retire(rebindSameUid);
        };

        await fixture.SwitchAsync();

        Assert.Equal(1, reached); // A missing resource callback is NOT an ownership pass.
        Assert.NotNull(retired);
        fixture.AssertCommittedBeforeToggle(retired, 90, 960); // Accepted admission is not refunded after delivery retirement.
        fixture.AssertFrames([(LegacyMsgUpdateDataType.Stamina, 90UL)], toggle: null);
        if (rebindSameUid) fixture.AssertReplacementUntouched();
        else Assert.False(fixture.TryOwner(out _));
    }

    [Fact]
    public async Task NativeCounterKillStaminaSendSameClientCopyCannotRepriceCommittedAdmission()
    {
        using var fixture = new CounterKillOwnerCallbackFixture();
        fixture.AssertSelection();
        var reached = 0;
        fixture.OnSend = (current, bytes) =>
        {
            if (!CounterKillOwnerScalar(bytes, LegacyMsgUpdateDataType.Stamina, 90)) return;
            Assert.Equal(1, ++reached);
            current.AssertCommittedBeforeToggle(current.Owner, 90, 960);
            current.CopySameClientWithEligibleMpMaster();
        };

        await fixture.SwitchAsync();

        Assert.Equal(1, reached);
        var owner = fixture.Owner;
        Assert.Equal("90", owner.Character.Attributes["Stamina"]);
        Assert.Equal(7, owner.Character.MinMana); // The later input is preserved, not charged again using a new rune.
        Assert.Equal(4_444, owner.Character.MinHitPoints);
        Assert.Equal("keep-copy", owner.Character.Attributes["CounterKillOwnerControl"]);
        Assert.Equal("2", owner.Character.Attributes["Reborn"]);
        Assert.Equal("15", owner.Character.Attributes["Level"]);
        Assert.Equal((uint)4_036_109, Assert.Single(owner.PlayerItems.RuneItems).ItemId);
        Assert.Equal("1", owner.Character.Attributes["LegacyCounterKillActive"]);
        Assert.Equal(fixture.Now.AddMilliseconds(2_000), owner.MagicCooldowns![6_003]);
        fixture.AssertFrames([(LegacyMsgUpdateDataType.Stamina, 90UL),
            (LegacyMsgUpdateDataType.Mana, 960UL)], toggle: true); // Immutable prepared cost, not a fresh debit.
    }

    [Fact]
    public async Task NativeCounterKillPostFocusedPrefixRetirementNeverAdmitsOrMutatesReplacement()
    {
        using var fixture = new CounterKillOwnerCallbackFixture(focusedInput: true);
        fixture.AssertSelection();
        var reached = 0;
        WorldSession? retired = null;
        fixture.OnLog = (current, line) =>
        {
            if (!line.StartsWith("WorldServer handled MsgAttack ", StringComparison.Ordinal)) return;
            Assert.Equal(1, ++reached);
            var prepared = current.Owner;
            // Earned by the public prefix, not a seeded hidden doFocus marker.
            Assert.False(HasLegacyStatusFlag(prepared.Character.Attributes, LegacyFocusedFlag));
            Assert.Equal("true", prepared.Character.Attributes["doFocus"]);
            current.AssertBeforeColdAndToggle(prepared, 100, 1_000);
            retired = current.Retire(rebindSameUid: true);
        };

        await fixture.SwitchAsync();

        Assert.Equal(1, reached);
        Assert.NotNull(retired);
        fixture.AssertBeforeColdAndToggle(retired, 100, 1_000);
        Assert.Equal("true", retired.Character.Attributes["doFocus"]);
        fixture.AssertReplacementUntouched();
        var bytes = Assert.Single(fixture.Frames);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var vector));
        Assert.Equal(PlayerUid, vector.Uid);
        var entry = Assert.Single(vector.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, entry.Type);
        Assert.NotNull(entry.Values);
        Assert.False(IsLegacyStatusFlagSet(entry.Values!, LegacyFocusedFlag));
        // Whole wire is ONLY the original public prefix's flag removal; no cost/toggle/ColdTime.
        Assert.Empty(ParseMagicColdTimePackets(fixture.Frames));
        Assert.Empty(ParseSpellAnimations(fixture.Frames));
    }

    [Fact]
    public async Task NativeCounterKillSynchronousToggleRetirementDoesNotSendSuccessOrRepublishOwner()
    {
        using var fixture = new CounterKillOwnerCallbackFixture();
        fixture.AssertSelection();
        var reached = 0;
        WorldSession? retired = null;
        fixture.OnSessionChanged = (current, previous, published) =>
        {
            if (!published.Character.Attributes.TryGetValue("LegacyCounterKillActive", out var active) ||
                active != "1") return;
            Assert.Equal(1, ++reached);
            Assert.NotNull(previous);
            Assert.False(previous.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.Equal("90", published.Character.Attributes["Stamina"]);
            Assert.Equal(960, published.Character.MinMana);
            Assert.Equal(current.Now.AddMilliseconds(2_000), published.MagicCooldowns![6_003]);
            // Real Store callback after the real toggle, inside the same reentrant gate.
            retired = current.Retire(rebindSameUid: true);
            Assert.Same(published, retired);
        };

        await fixture.SwitchAsync();

        Assert.Equal(1, reached);
        Assert.NotNull(retired);
        Assert.Equal("1", retired.Character.Attributes["LegacyCounterKillActive"]);
        fixture.AssertReplacementUntouched();
        fixture.AssertFrames([(LegacyMsgUpdateDataType.Stamina, 90UL),
            (LegacyMsgUpdateDataType.Mana, 960UL)], toggle: null);
    }

    private static bool CounterKillOwnerScalar(byte[] bytes, LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Count == 1 && packet.Updates[0].Type == type && packet.Updates[0].Value == value;

    private sealed class CounterKillOwnerCallbackFixture : IDisposable
    {
        private const ushort Id = 6_003;
        private const ushort LearnedLevel = 2;
        private const uint MpMaster = 4_036_109;
        private readonly WorldSessionStore sessions;
        private readonly ConquerTcpConnection connection;
        private readonly WorldPacketHandlerContext context;
        private readonly CounterKillOwnerCallbackLog log;
        private readonly CounterKillOwnerCallbackClock clock = new();
        private readonly List<byte[]> frames = [];

        public CounterKillOwnerCallbackFixture(bool focusedInput = false)
        {
            WorldData = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(Id, sort: 29, level: 0, useMana: 1, useStamina: 1, coldTime: 0),
                 CreateMagicRow(Id, sort: 29, level: (byte)LearnedLevel, useMana: 40,
                     useStamina: 10, coolDown: 1_000, coldTime: 2_000)],
                items: [CreateItemType((int)MpMaster, "MPMaster(+9)")]);
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Reborn"] = "0", ["Level"] = "120",
            };
            if (focusedInput) attributes["StatusFlags"] = BuildStatusFlags(LegacyFocusedFlag);
            var owner = CreateSession(mapId: 1_000, classCode: 142, minHitPoints: 5_000,
                minMana: 1_000, stamina: 100, spirit: 0, strength: 0,
                spellEntries: [CreatePlayerSpell(Id, LearnedLevel)], attributes: attributes);
            Replacement = CreateSession(uid: PlayerUid, name: "CounterKillOwnerReplacement",
                mapId: 1_000, minHitPoints: 77_777, minMana: 77, stamina: 33,
                spellEntries: [CreatePlayerSpell(Id, LearnedLevel)],
                attributes: new Dictionary<string, string> { ["CounterKillOwnerControl"] = "replacement" });
            sessions = new WorldSessionStore((_, previous, published) =>
                OnSessionChanged?.Invoke(this, previous, published));
            // Declared same-physical-client MODEL; Store retirement is NOT Player replacement.
            var physicalClient = new LegacyNativeGameClientRuntime();
            owner = owner with { NativeGameClient = physicalClient };
            Replacement = Replacement with { NativeGameClient = physicalClient };
            connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9961),
                sendAsync: (frame, _) =>
                {
                    var bytes = frame.ToArray();
                    frames.Add(bytes); // Triggering delivery is real and precedes the callback.
                    OnSend?.Invoke(this, bytes);
                    return ValueTask.CompletedTask;
                }, nativeGameClient: physicalClient);
            sessions.Set(connection, owner);
            log = new CounterKillOwnerCallbackLog(line => OnLog?.Invoke(this, line));
            context = new WorldPacketHandlerContext(connection, log, owner,
                WorldData: WorldData, Sessions: sessions, TimeProvider: clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Empty(frames);
            AssertBeforeColdAndToggle(owner, 100, 1_000);
            Assert.False(owner.Character.Attributes.ContainsKey("doFocus"));
            Assert.False(owner.Character.Attributes.ContainsKey("DoFocus"));
            Assert.Equal(focusedInput, HasLegacyStatusFlag(owner.Character.Attributes, LegacyFocusedFlag));
        }

        public Action<CounterKillOwnerCallbackFixture, byte[]>? OnSend { get; set; }
        public Action<CounterKillOwnerCallbackFixture, string>? OnLog { get; set; }
        public Action<CounterKillOwnerCallbackFixture, WorldSession?, WorldSession>? OnSessionChanged { get; set; }
        public WorldDataCatalog WorldData { get; }
        public WorldSession Replacement { get; }
        public WorldSession Owner => Assert.IsType<WorldSession>(TryOwner(out var owner) ? owner : null);
        public byte[][] Frames => frames.ToArray();
        public DateTimeOffset Now => clock.GetUtcNow();
        public bool TryOwner(out WorldSession? owner) => sessions.TryGet(connection, out owner);

        public void AssertSelection()
        {
            var book = Assert.Single(Owner.PlayerSpells.Spells);
            Assert.Equal(Id, book.Id);
            Assert.Equal(LearnedLevel, book.Level);
            Assert.Equal((byte)0, book.UseSpellSoul);
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
                Owner.PlayerSpells, WorldData, Id, out var selectedBook, out var selected));
            Assert.Equal(book, selectedBook);
            Assert.Equal(Id, selected.Id);
            Assert.Equal((byte)LearnedLevel, selected.Level);
            Assert.Equal(LegacyMagicSort.CounterKill, selected.Sort);
            Assert.Equal((byte)10, selected.UseStamina);
            Assert.Equal((ushort)40, selected.UseMana);
            Assert.Equal(2_000, selected.ColdTime);
            Assert.Equal((ushort)1_000, selected.CoolDown);
            var raw = Assert.Single(WorldData.MagicByTypeId[Id], row => row.Fields[8] == "2");
            Assert.Equal(Id, raw.TypeId);
            Assert.Equal("6003", raw.Fields[0]);
            Assert.Equal("6003", raw.Fields[1]);
            Assert.Equal("29", raw.Fields[2]);
            Assert.Equal("0", raw.Fields[22]);
            Assert.Empty(Owner.PlayerItems.RuneItems);
            Assert.Empty(Owner.PlayerItems.EquipmentItems);
            Assert.Equal((ushort)0, Owner.LegacyRandomSpell);
        }

        public WorldSession Retire(bool rebindSameUid)
        {
            var current = Owner;
            Assert.True(sessions.TryRemove(connection, out var removed));
            Assert.Same(current, removed);
            if (rebindSameUid)
            {
                sessions.Set(connection, Replacement);
                Assert.Same(Replacement, Owner);
                Assert.Equal(current.Character.Uid, Replacement.Character.Uid);
                Assert.NotSame(current, Replacement);
            }
            return current;
        }

        public void CopySameClientWithEligibleMpMaster()
        {
            var current = Owner;
            var attributes = new Dictionary<string, string>(current.Character.Attributes)
            {
                ["Reborn"] = "2", ["Level"] = "15", ["CounterKillOwnerControl"] = "keep-copy",
            };
            var copy = current with
            {
                Character = current.Character with
                {
                    MinMana = 7, MinHitPoints = 4_444, Reborn = 2, Level = 15, Attributes = attributes,
                },
                PlayerItems = CreatePlayerItems(new PlayerItemSnapshotItem { ItemId = MpMaster, Position = 103 }),
            };
            sessions.Set(connection, copy); // NO removal: same original native client, new immutable record.
            Assert.Same(copy, Owner);
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
                copy.PlayerSpells, WorldData, Id, out _, out var spell));
            var cost = LegacyMagicResourceCostCalculator.Calculate(spell, copy.PlayerItems, WorldData,
                copy.Character.Attributes, copy.AlternateEquipmentActive, copy.Character.Class);
            Assert.Equal((byte)9, cost.MpMasterLevel);
            Assert.Equal((ushort)4, cost.UseMana); // Setup guard, not a hand-written magic-cost substitute.
            Assert.True(copy.Character.MinMana < spell.UseMana);
            Assert.True(copy.Character.MinMana >= cost.UseMana);
        }

        public void AssertBeforeColdAndToggle(WorldSession owner, ulong stamina, long mana)
        {
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), owner.Character.Attributes["Stamina"]);
            Assert.Equal(mana, owner.Character.MinMana);
            Assert.True(owner.MagicCooldowns is null || !owner.MagicCooldowns.ContainsKey(Id));
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        }

        public void AssertCommittedBeforeToggle(WorldSession owner, ulong stamina, long mana)
        {
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), owner.Character.Attributes["Stamina"]);
            Assert.Equal(mana, owner.Character.MinMana);
            Assert.Equal(Now.AddMilliseconds(2_000), owner.MagicCooldowns![Id]);
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        }

        public void AssertReplacementUntouched()
        {
            Assert.Same(Replacement, Owner); // Even an equivalent replacement record is not a valid overwrite.
            AssertBeforeColdAndToggle(Owner, 33, 77);
            Assert.Equal(77_777, Owner.Character.MinHitPoints);
            Assert.Equal("replacement", Owner.Character.Attributes["CounterKillOwnerControl"]);
            Assert.False(Owner.Character.Attributes.ContainsKey("doFocus"));
            Assert.False(Owner.Character.Attributes.ContainsKey("DoFocus"));
        }

        public void AssertFrames((LegacyMsgUpdateDataType Type, ulong Value)[] costs, bool? toggle)
        {
            var observed = Frames;
            Assert.Equal(costs.Length + (toggle.HasValue ? 1 : 0), observed.Length);
            for (var index = 0; index < costs.Length; index++)
                Assert.True(CounterKillOwnerScalar(observed[index], costs[index].Type, costs[index].Value));
            if (toggle is { } active)
            {
                Assert.True(MsgAttackPacket.TryParse(ReadFrame(observed[^1]), out var response));
                Assert.Equal(MsgAttackType.CounterKillSwitch, response.Query.AttackType);
                Assert.Equal(PlayerUid, response.Query.Uid);
                Assert.Equal(PlayerUid, response.Query.OpponentUid);
                Assert.Equal(Id, response.Query.SpellId);
                Assert.Equal(LearnedLevel, response.Query.SpellLevel);
                Assert.Equal(active ? 1 : 0, response.Query.Damage);
            }
            Assert.Empty(ParseMagicColdTimePackets(observed));
            Assert.Empty(ParseSpellAnimations(observed));
        }

        public Task SwitchAsync() => PacketHandlers.HandleAsync(context with { Session = Owner },
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: PlayerUid, X: 830, Y: 584,
                AttackType: MsgAttackType.CounterKillSwitch, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                SpellLevel: 99, SpellId: 0)).ToFrameBytes())).AsTask();

        public void Dispose() => log.Dispose();
    }

    private sealed class CounterKillOwnerCallbackClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class CounterKillOwnerCallbackLog(Action<string> onLine) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value)
        {
            onLine(value ?? string.Empty);
            return Task.CompletedTask;
        }
    }
}
