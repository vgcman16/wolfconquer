using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task NativeCounterKillProcessMapDenialPrecedesSilentFlagDenials(bool neptune, bool freeze)
    {
        var flags = new List<int>();
        if (neptune) flags.Add(0xDE);
        if (freeze) flags.Add(0x3B);
        using var fixture = new CounterKillProcessAdmissionFixture(2_068, flags.ToArray());
        fixture.AssertValidSelection();
        var before = fixture.Owner;

        await fixture.SwitchAsync();

        fixture.AssertDenied(before, message: true);
    }

    [Fact]
    public async Task NativeCounterKillProcessMapDenialPrecedesLearnedSelection()
    {
        using var fixture = new CounterKillProcessAdmissionFixture(2_068, hasBook: false);
        Assert.Empty(fixture.Owner.PlayerSpells.Spells);
        Assert.False(LegacyMagicRuntime.TryResolveLearnedSpell(
            fixture.Owner.PlayerSpells, fixture.WorldData, 6_003, out _, out _));
        var before = fixture.Owner;

        await fixture.SwitchAsync();

        fixture.AssertDenied(before, message: true);
    }

    [Theory]
    [InlineData(1_000L, 0xDE)]
    [InlineData(1_000L, 0x3B)]
    [InlineData(1_039L, 0xDE)]
    [InlineData(1_039L, 0x3B)]
    public async Task NativeCounterKillProcessSilentFlagsDenyBeforeCanUse(long map, int flag)
    {
        using var fixture = new CounterKillProcessAdmissionFixture(map, [flag]);
        fixture.AssertValidSelection();
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, flag));
        var before = fixture.Owner;

        await fixture.SwitchAsync();

        fixture.AssertDenied(before, message: false);
    }

    [Theory]
    [InlineData("arena")]
    [InlineData("team-arena")]
    public async Task NativeCounterKillProcessUnqualifiedWatcherOn700GetsMapDenial(string watching)
    {
        using var fixture = new CounterKillProcessAdmissionFixture(700);
        fixture.AddWatcher(watching);
        fixture.AssertValidSelection();
        fixture.AssertWatching(watching);
        fixture.AssertNoQualifier();
        var before = fixture.Owner;

        await fixture.SwitchAsync();

        fixture.AssertDenied(before, message: true);
    }

    [Theory]
    [InlineData("arena")]
    [InlineData("team-arena-member")]
    [InlineData("elite")]
    [InlineData("team-pk-member")]
    public async Task NativeCounterKillProcessQualifiedWatcherOn700MayReachCanUse(string qualifier)
    {
        using var fixture = new CounterKillProcessAdmissionFixture(700);
        var watching = qualifier == "arena" ? "team-arena" : "arena";
        fixture.AddWatcher(watching);
        fixture.AddQualifier(qualifier);
        fixture.AssertValidSelection();
        fixture.AssertWatching(watching);

        await fixture.SwitchAsync();

        fixture.AssertAdmitted();
    }

    [Theory]
    [InlineData(1_000L, "arena")]
    [InlineData(1_000L, "team-arena")]
    [InlineData(700L, "none")]
    public async Task NativeCounterKillProcessDoesNotBanAllWatchersOrAll700Players(long map, string watching)
    {
        using var fixture = new CounterKillProcessAdmissionFixture(map);
        if (watching != "none") fixture.AddWatcher(watching);
        fixture.AssertValidSelection();
        fixture.AssertWatching(watching);
        fixture.AssertNoQualifier();

        await fixture.SwitchAsync();

        fixture.AssertAdmitted();
    }

    private sealed class CounterKillProcessAdmissionFixture : IDisposable
    {
        private readonly WorldSessionStore sessions = new();
        private readonly List<byte[]> sent = [];
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConquerTcpConnection connection;
        private readonly WorldPacketHandlerContext context;
        private readonly ArenaSessionStateStore arena = new();
        private readonly TeamArenaSessionStateStore teamArena = new();
        private readonly LegacyElitePkSessionStateStore elite = new();
        private readonly LegacyTeamPkSessionStateStore teamPk = new();
        private readonly TeamSessionStateStore teams = new();
        private readonly CounterKillProcessAdmissionClock clock = new();

        public CounterKillProcessAdmissionFixture(long map, int[]? flags = null, bool hasBook = true)
        {
            WorldData = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(6_003, sort: 29, level: 2, useStamina: 10, useMana: 40,
                    weaponType: 0, coolDown: 1_000, coldTime: 2_000)]);
            var attributes = new Dictionary<string, string>();
            if (flags is { Length: > 0 }) attributes["StatusFlags"] = BuildStatusFlags(flags);
            var owner = CreateSession(mapId: map, minMana: 1_000, stamina: 100,
                strength: 0, spirit: 0, attributes: attributes,
                spellEntries: hasBook ? [CreatePlayerSpell(6_003, 2)] : [],
                playerItems: PlayerItemSnapshot.Empty);
            // Declared physical-client MODEL prestate; no socket/login acquisition claim.
            var physicalClient = new LegacyNativeGameClientRuntime();
            owner = owner with { NativeGameClient = physicalClient };
            connection = CreateCounterKillPhysicalModelConnection(sent, physicalClient);
            sessions.Set(connection, owner);
            context = new WorldPacketHandlerContext(connection, log, owner,
                WorldData: WorldData, Sessions: sessions, ArenaStates: arena,
                TeamArenaStates: teamArena, ElitePkStates: elite, TeamPkStates: teamPk,
                TeamStates: teams, TimeProvider: clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Empty(sent);
            Assert.False(HasLegacyStatusFlag(owner.Character.Attributes, LegacyFocusedFlag));
            Assert.False(owner.Character.Attributes.ContainsKey("InUseIntensify"));
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

        public void AssertValidSelection()
        {
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
                Owner.PlayerSpells, WorldData, 6_003, out var book, out var spell));
            Assert.Equal((ushort)6_003, book.Id);
            Assert.Equal((ushort)2, book.Level);
            Assert.Equal((ushort)6_003, spell.Id);
            Assert.Equal((byte)2, spell.Level);
            Assert.Equal(LegacyMagicSort.CounterKill, spell.Sort);
            Assert.Equal((byte)10, spell.UseStamina);
            Assert.Equal((ushort)40, spell.UseMana);
            Assert.Equal(2_000, spell.ColdTime);
            Assert.Equal("100", Owner.Character.Attributes["Stamina"]);
            Assert.Equal(1_000, Owner.Character.MinMana);
            Assert.Empty(sent);
        }

        public void AddWatcher(string kind)
        {
            var first = NewPeer();
            var second = NewPeer();
            if (kind == "arena")
            {
                Assert.False(arena.RegisterWaiting(first, 1_000, out _));
                Assert.True(arena.RegisterWaiting(second, 1_000, out _));
                Assert.False(arena.TryAcceptCountdown(first, out _));
                Assert.True(arena.TryAcceptCountdown(second, out var pair));
                Assert.True(arena.TryAddWatcher(connection, pair, out var update));
                Assert.Same(connection, Assert.Single(update.Watchers));
            }
            else
            {
                Assert.Equal("team-arena", kind);
                Assert.False(teamArena.RegisterWaiting(first, 1_000, out _));
                Assert.True(teamArena.RegisterWaiting(second, 1_000, out _));
                Assert.False(teamArena.TryAcceptCountdown(first, out _));
                Assert.True(teamArena.TryAcceptCountdown(second, out var pair));
                Assert.True(teamArena.TryAddWatcher(connection, pair, out var update));
                Assert.Same(connection, Assert.Single(update.Watchers));
            }
            Assert.Empty(sent); // Declared concrete match state, not watcher acquisition/wire proof.
        }

        public void AssertWatching(string kind)
        {
            Assert.Equal(kind == "arena", arena.IsWatcher(connection));
            Assert.Equal(kind == "team-arena", teamArena.IsWatcher(connection));
        }

        public void AssertNoQualifier()
        {
            Assert.False(arena.IsQualifierFighter(connection));
            Assert.False(teamArena.IsQualifierTeam(connection));
            Assert.False(elite.IsQualifierFighter(connection));
            Assert.False(teamPk.IsQualifierTeam(connection));
            Assert.False(teams.HasTeam(connection));
        }

        public void AddQualifier(string kind)
        {
            var first = kind == "arena" || kind == "elite" ? connection : NewPeer();
            var second = NewPeer();
            if (kind == "arena")
            {
                Assert.False(arena.RegisterWaiting(first, 1_000, out _));
                Assert.True(arena.RegisterWaiting(second, 1_000, out _));
                Assert.False(arena.TryAcceptCountdown(first, out _));
                Assert.True(arena.TryAcceptCountdown(second, out _));
                Assert.True(arena.IsQualifierFighter(connection));
            }
            else if (kind == "elite")
            {
                elite.ActivateMatch(first, second, dynamicId: 42);
                Assert.True(elite.IsQualifierFighter(connection));
            }
            else
            {
                Assert.Contains(kind, new[] { "team-arena-member", "team-pk-member" });
                teams.CreateTeam(first, PlayerUid + 1L);
                Assert.True(teams.TryAddMember(first, connection, PlayerUid));
                Assert.Contains(connection, teams.GetConnections(first));
                if (kind == "team-arena-member")
                {
                    Assert.False(teamArena.RegisterWaiting(first, 1_000, out _));
                    Assert.True(teamArena.RegisterWaiting(second, 1_000, out _));
                    Assert.False(teamArena.TryAcceptCountdown(first, out _));
                    Assert.True(teamArena.TryAcceptCountdown(second, out _));
                    Assert.True(teamArena.IsQualifierTeam(first));
                    Assert.False(teamArena.IsQualifierTeam(connection));
                }
                else
                {
                    teamPk.ActivateMatch(first, second, dynamicId: 42);
                    Assert.True(teamPk.IsQualifierTeam(first));
                    Assert.False(teamPk.IsQualifierTeam(connection));
                }
            }
            Assert.Empty(sent);
        }

        public Task SwitchAsync()
        {
            var query = new MsgAttackQuery(PlayerUid, PlayerUid, 830, 584,
                MsgAttackType.CounterKillSwitch, 0, 0, 0, MsgAttackEffect.None,
                Unknown: 0, SpellLevel: 99, SpellId: 0);
            return PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(query).ToFrameBytes())).AsTask();
        }

        public void AssertDenied(WorldSession before, bool message)
        {
            Assert.Same(before, Owner);
            Assert.Equal("100", Owner.Character.Attributes["Stamina"]);
            Assert.Equal(1_000, Owner.Character.MinMana);
            Assert.Null(Owner.MagicCooldowns);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            if (!message)
            {
                Assert.Empty(sent); // Exact whole owner wire, not just absence of a toggle.
                return;
            }
            Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(Assert.Single(sent)), out var packet));
            Assert.Equal(16_711_680U, packet.Color);
            Assert.Equal(2_005U, packet.ChatType);
            Assert.Equal(0U, packet.MessageUid1);
            Assert.Equal(0U, packet.MessageUid2);
            Assert.Equal(0U, packet.ServerId);
            Assert.Equal(0U, packet.Mesh);
            Assert.Equal(0U, packet.UnionType);
            Assert.Equal(1U, packet.Count);
            Assert.Equal(0U, packet.New1);
            Assert.Equal(0U, packet.New2);
            Assert.Equal(new[] { "SYSTEM", "ALL", "", "Spells are not allowed on this area.", "", "", "" },
                packet.Content);
        }

        public void AssertAdmitted()
        {
            Assert.Equal("90", Owner.Character.Attributes["Stamina"]);
            Assert.Equal(960, Owner.Character.MinMana);
            Assert.Equal("1", Owner.Character.Attributes["LegacyCounterKillActive"]);
            var cold = Assert.Single(Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(
                Owner.MagicCooldowns));
            Assert.Equal((ushort)6_003, cold.Key);
            Assert.Equal(clock.GetUtcNow().AddMilliseconds(2_000), cold.Value);
            Assert.Equal(3, sent.Count);
            var expected = new[] { (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, 960UL) };
            for (var index = 0; index < 2; index++)
            {
                Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update));
                Assert.Equal(PlayerUid, update.Uid);
                var scalar = Assert.Single(update.Updates);
                Assert.Equal(expected[index].Item1, scalar.Type);
                Assert.Equal(expected[index].Item2, scalar.Value);
            }
            Assert.True(MsgAttackPacket.TryParse(ReadFrame(sent[2]), out var toggle));
            Assert.Equal(MsgAttackType.CounterKillSwitch, toggle.Query.AttackType);
            Assert.Equal(PlayerUid, toggle.Query.Uid);
            Assert.Equal(PlayerUid, toggle.Query.OpponentUid);
            Assert.Equal((ushort)6_003, toggle.Query.SpellId);
            Assert.Equal((ushort)2, toggle.Query.SpellLevel);
            Assert.Equal(1, toggle.Query.Damage);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        }

        private static ConquerTcpConnection NewPeer() => CreateSendingConnection([]);
        public void Dispose() => log.Dispose();
    }

    private sealed class CounterKillProcessAdmissionClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    }
}
