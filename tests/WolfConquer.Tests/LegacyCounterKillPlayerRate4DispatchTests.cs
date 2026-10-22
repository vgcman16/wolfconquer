using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Declared physical-owner/session PRESTATE model; actual PUBLIC packet dispatch, not birth proof.
    [Theory]
    [InlineData(2_068L, 0, true, 100, 1_000, 0, 1)]
    [InlineData(1_000L, 0xDE, true, 100, 1_000, 0, 0)]
    [InlineData(1_039L, 0x3B, true, 100, 1_000, 0, 0)]
    [InlineData(1_000L, 0x3B, true, 100, 1_000, 1, 0)]
    [InlineData(1_000L, 0, false, 100, 1_000, 1, 0)]
    [InlineData(1_000L, 0, true, 9, 1_000, 1, 0)]
    [InlineData(1_000L, 0, true, 100, 39, 1, 1)]
    public async Task CounterKillRate4DeclaredGatesHaveNativeDrawAndWireOrder(
        long map, int flag, bool hasBook, int stamina, int mana, int draws, int frames)
    {
        using var fixture = new Rate4DispatchFixture(map, flag, hasBook, stamina, mana);
        var original = fixture.Owner;
        Assert.Equal(0L, fixture.Player.CompletedRateDraws);

        await fixture.SwitchAsync();

        Assert.Equal((long)draws, fixture.Player.CompletedRateDraws);
        Assert.Equal(new[] { 0L }, fixture.PrefixDraws);
        Assert.Equal(frames, fixture.Sent.Count);
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
        Assert.Null(fixture.Owner.MagicCooldowns);
        Assert.Equal(mana, fixture.Owner.Character.MinMana);
        Assert.Equal((mana == 39 ? 90 : stamina).ToString(CultureInfo.InvariantCulture),
            fixture.Owner.Character.Attributes["Stamina"]);
        if (map == 2_068)
        {
            Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(Assert.Single(fixture.Sent)), out var denial));
            Assert.Equal("Spells are not allowed on this area.", denial.Content[3]);
            Assert.Same(original, fixture.Owner);
        }
        else if (mana == 39)
            fixture.AssertCost(0, LegacyMsgUpdateDataType.Stamina, 90);
        else
            Assert.Empty(fixture.Sent);
    }

    [Fact]
    public async Task CounterKillRate4DeclaredCallsPrecedeCostsWithoutDurabilityPackets()
    {
        using var fixture = new Rate4DispatchFixture();
        var stream = fixture.Player.MyRandom;
        await fixture.SwitchAsync();
        fixture.AssertSuccess(0, 90, 960, true);
        Assert.Equal(new[] { 1L, 1L, 1L }, fixture.SendDraws);

        await fixture.SwitchAsync();

        fixture.AssertSuccess(3, 80, 920, false);
        Assert.Equal(new[] { 0L, 1L }, fixture.PrefixDraws);
        Assert.Equal(new[] { 1L, 1L, 1L, 2L, 2L, 2L }, fixture.SendDraws);
        Assert.Equal(2L, fixture.Player.CompletedRateDraws);
        Assert.Same(stream, fixture.Player.MyRandom);
        Assert.Equal(6, fixture.Sent.Count); // No CheckItems durability output in either result branch.
    }

    [Fact]
    public async Task CounterKillRate4DeclaredPrefixCopyRetainsPhysicalStream()
    {
        using var fixture = new Rate4DispatchFixture();
        var player = fixture.Player;
        var stream = player.MyRandom;
        var before = fixture.Owner;
        fixture.OnPrefix = () => fixture.Sessions.Set(fixture.Connection,
            fixture.Owner with { Character = fixture.Owner.Character with { X = 831 } });

        await fixture.SwitchAsync();

        Assert.NotSame(before, fixture.Owner);
        Assert.Equal(831, fixture.Owner.Character.X);
        Assert.Same(player, fixture.Owner.NativePlayer);
        Assert.Same(stream, fixture.Player.MyRandom);
        Assert.Equal(1L, player.CompletedRateDraws);
        fixture.AssertSuccess(0, 90, 960, true);
    }

    [Fact]
    public async Task CounterKillRate4DeclaredPrefixReplacementUsesCurrentPlayerOfOriginalClient()
    {
        using var fixture = new Rate4DispatchFixture();
        var oldPlayer = fixture.Player;
        LegacyNativePlayerRuntime? replacement = null;
        fixture.OnPrefix = () =>
        {
            Assert.Equal(0L, oldPlayer.CompletedRateDraws);
            replacement = new LegacyNativePlayerRuntime(fixture.Client);
            fixture.Client.Player = replacement; // Explicit model input, NOT a GetHeroInfo import proof.
        };

        await fixture.SwitchAsync();

        Assert.NotNull(replacement);
        Assert.NotSame(oldPlayer, replacement);
        Assert.Same(replacement, fixture.Owner.NativePlayer);
        Assert.Same(fixture.Client, replacement!.Owner);
        Assert.Equal(0L, oldPlayer.CompletedRateDraws);
        Assert.Equal(1L, replacement.CompletedRateDraws);
        fixture.AssertSuccess(0, 90, 960, true);
    }

    [Fact]
    public async Task CounterKillRate4DeclaredRetirementCannotRecaptureRegistrationAtDraw()
    {
        using var fixture = new Rate4DispatchFixture();
        var replacement = fixture.Owner with { Character = fixture.Owner.Character with { X = 832 } };
        fixture.OnPrefix = () =>
        {
            Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));
            fixture.Sessions.Set(fixture.Connection, replacement);
        };

        await fixture.SwitchAsync();

        Assert.Same(replacement, fixture.Owner);
        Assert.Equal(0L, fixture.Player.CompletedRateDraws);
        Assert.Empty(fixture.Sent);
        fixture.OnPrefix = null;
        await fixture.SwitchAsync(); // A separate fresh public entry may capture the new registration.
        Assert.Equal(1L, fixture.Player.CompletedRateDraws);
        fixture.AssertSuccess(0, 90, 960, true);
    }

    [Fact]
    public async Task CounterKillRate4DeclaredPostDrawRetirementDoesNotRefundOrReplay()
    {
        using var fixture = new Rate4DispatchFixture();
        var replacement = fixture.Owner with { Character = fixture.Owner.Character with { X = 833 } };
        var reached = 0;
        fixture.OnSend = () =>
        {
            if (++reached != 1) return;
            Assert.Equal(1L, fixture.Player.CompletedRateDraws);
            Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out _));
            fixture.Sessions.Set(fixture.Connection, replacement);
        };

        await fixture.SwitchAsync();

        Assert.Equal(1, reached);
        Assert.Same(replacement, fixture.Owner);
        Assert.Equal(1L, fixture.Player.CompletedRateDraws);
        fixture.AssertCost(0, LegacyMsgUpdateDataType.Stamina, 90);
        Assert.Single(fixture.Sent); // No resumed mana/toggle on the replacement.
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
    }

    [Fact]
    public async Task CounterKillRate4PubliclyEarnedColdDenialStillDrawsBeforeCostAndCold()
    {
        using var fixture = new Rate4DispatchFixture(cold: 2_000);
        await fixture.SwitchAsync(); // Must first earn the real cold deadline.
        fixture.AssertSuccess(0, 90, 960, true);
        Assert.NotNull(fixture.Owner.MagicCooldowns);
        var deadline = fixture.Owner.MagicCooldowns![6_003];
        Assert.Equal(fixture.Now.AddMilliseconds(2_000), deadline);
        Assert.Equal(1L, fixture.Player.CompletedRateDraws);

        fixture.Advance(1_999);
        await fixture.SwitchAsync();
        Assert.Equal(2L, fixture.Player.CompletedRateDraws);
        fixture.AssertCost(3, LegacyMsgUpdateDataType.Stamina, 80);
        fixture.AssertCost(4, LegacyMsgUpdateDataType.Mana, 920);
        Assert.Equal(5, fixture.Sent.Count);
        Assert.Equal("1", fixture.Owner.Character.Attributes["LegacyCounterKillActive"]);
        Assert.Equal(deadline, fixture.Owner.MagicCooldowns![6_003]);

        fixture.Advance(1);
        await fixture.SwitchAsync();
        fixture.AssertSuccess(5, 70, 880, false);
        Assert.Equal(3L, fixture.Player.CompletedRateDraws);
        Assert.Equal(fixture.Now.AddMilliseconds(2_000), fixture.Owner.MagicCooldowns![6_003]);
        Assert.Equal(8, fixture.Sent.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CounterKillRate4UnboundManualSessionHasNoInventedStream(bool withStore)
    {
        using var fixture = new Rate4DispatchFixture(bindPhysicalClient: false, withStore: withStore);
        var before = fixture.Owner;

        await fixture.SwitchAsync();

        Assert.Null(fixture.Connection.NativeGameClient);
        Assert.Null(fixture.Owner.NativePlayer);
        Assert.Same(before, fixture.Owner);
        Assert.Empty(fixture.Sent);
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
    }

    [Theory]
    [InlineData(1_000L, 0, 1, true)]
    [InlineData(1_039L, 0, 0, true)]
    [InlineData(2_068L, 0, 0, false)]
    [InlineData(1_000L, 0xDE, 0, false)]
    [InlineData(1_000L, 0x3B, 1, false)]
    public async Task CounterKillRate4DeclaredMissingCatalogIsHandledAfterNativeProcess(
        long map, int flag, int draws, bool catalogDiagnostic)
    {
        using var fixture = new Rate4DispatchFixture(map: map, flag: flag, withCatalog: false);
        var before = fixture.Owner;

        await fixture.SwitchAsync();

        Assert.Equal((long)draws, fixture.Player.CompletedRateDraws);
        Assert.Same(before, fixture.Owner);
        Assert.Equal(catalogDiagnostic, fixture.LogText.Contains(
            "world catalog was not available after Process admission.", StringComparison.Ordinal));
        Assert.Equal(map == 2_068 ? 1 : 0, fixture.Sent.Count);
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
    }

    [Fact]
    public async Task CounterKillRate4DeclaredNoStoreRequiresRealCapsuleAndOnlyPublishesLocalWire()
    {
        using var fixture = new Rate4DispatchFixture(withStore: false);
        var before = fixture.Owner;

        await fixture.SwitchAsync();

        Assert.Equal(1L, fixture.Player.CompletedRateDraws);
        fixture.AssertCost(0, LegacyMsgUpdateDataType.Stamina, 90);
        fixture.AssertCost(1, LegacyMsgUpdateDataType.Mana, 960);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(fixture.Sent[2]), out var toggle));
        Assert.Equal(MsgAttackType.CounterKillSwitch, toggle.Query.AttackType);
        Assert.Equal(1, toggle.Query.Damage);
        Assert.Equal(3, fixture.Sent.Count);
        Assert.Same(before, fixture.Owner); // No Store persistence is invented from local wire success.
        Assert.False(fixture.Owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
    }

    // This producer control uses the real legacy socket path + real 1052 load, NOT declared owners.
    [Theory]
    [InlineData(1_000L, 1)]
    [InlineData(1_039L, 0)]
    [InlineData(2_068L, 0)]
    public async Task CounterKillRate4RealGameLoginRoutesSocketBornPlayerBeforeMissingBook(long map, int draws)
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var files = new Rate4NetworkInput(map);
        using var log = new NativePlayerDiagnosticLog();
        var data = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [CreateMagicRow(6_003, sort: 29, level: 2, useStamina: 10, useMana: 40, coldTime: 0)]);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(files.Users),
            handoffService: null, worldData: data, runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled,
            log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer, async fixture =>
        {
            var wire = await fixture.ConnectGameAsync(false);
            var probe = await fixture.NextAsync();
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(probe.Connection.NativeGameClient);
            var player = client.Player;
            var stream = player.MyRandom;
            Assert.Null(probe.Before);
            Assert.Equal(0L, player.CompletedRateDraws);

            await wire.SendAsync(new LegacyMsgLoginGamePacket(Rate4NetworkInput.Uid).ToFrameBytes(), fixture.Token);
            var login = await fixture.NextAsync();
            Assert.Equal(LegacyMsgLoginGamePacket.Opcode, login.Opcode);
            Assert.True(log.Text.Contains(
                $"WorldServer legacy MsgLoginGame Success key={Rate4NetworkInput.Uid} uid={Rate4NetworkInput.Uid} map={map}.",
                StringComparison.Ordinal), log.Text);
            var accepted = Assert.IsType<WorldSession>(login.After);
            Assert.Equal(map, accepted.Character.MapId); // A loader/map failure is setup, not phase evidence.
            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, accepted.NativePlayer);
            Assert.DoesNotContain(accepted.PlayerSpells.Spells, row => row.Id == 6_003);
            Assert.Equal(0L, player.CompletedRateDraws);
            AssertNoRate4RuntimeError(log);

            await wire.SendAsync(Rate4Packet(Rate4NetworkInput.Uid).ToFrameBytes(), fixture.Token);
            var handled = await fixture.NextAsync();
            Assert.Equal(MsgAttackPacket.Opcode, handled.Opcode);
            var after = Assert.IsType<WorldSession>(handled.After);
            Assert.Same(probe.Connection, handled.Connection);
            Assert.Same(client, after.NativeGameClient);
            Assert.Same(player, after.NativePlayer);
            Assert.Same(stream, player.MyRandom);
            Assert.Equal((long)draws, player.CompletedRateDraws);
            Assert.False(after.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            AssertNoRate4RuntimeError(log);
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    [Fact]
    public async Task CounterKillRate4RealModernLoginDoesNotInventPhysicalStream()
    {
        using var files = new Rate4NetworkInput(1_000);
        using var log = new NativePlayerDiagnosticLog();
        var data = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [CreateMagicRow(6_003, sort: 29, level: 2, useStamina: 10, useMana: 40, coldTime: 0)]);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(files.Users),
            handoffService: null, worldData: data, runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled,
            log: log);
        await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.Modern, async fixture =>
        {
            var wire = await fixture.ConnectNongameAsync();
            await wire.SendAsync(new LegacyMsgLoginGamePacket(Rate4NetworkInput.Uid).ToFrameBytes(), fixture.Token);
            var login = await fixture.NextAsync();
            Assert.True(log.Text.Contains(
                $"WorldServer legacy MsgLoginGame Success key={Rate4NetworkInput.Uid} uid={Rate4NetworkInput.Uid} map=1000.",
                StringComparison.Ordinal), log.Text);
            var before = Assert.IsType<WorldSession>(login.After);
            Assert.Null(login.Connection.NativeGameClient);
            Assert.Null(before.NativePlayer);
            AssertNoRate4RuntimeError(log);

            await wire.SendAsync(Rate4Packet(Rate4NetworkInput.Uid).ToFrameBytes(), fixture.Token);
            var handled = await fixture.NextAsync();
            Assert.Equal(MsgAttackPacket.Opcode, handled.Opcode);
            Assert.Same(before, handled.After);
            Assert.Null(handled.After!.NativePlayer);
            AssertNoRate4RuntimeError(log);
        }, handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
    }

    private static void AssertNoRate4RuntimeError(NativePlayerDiagnosticLog log) =>
        log.AssertNoWorldRuntimeDiagnostics(); // Known real diagnostics, not universal zero-error proof.

    private static MsgAttackPacket Rate4Packet(uint uid) => new(new MsgAttackQuery(
        uid, uid, 830, 584, MsgAttackType.CounterKillSwitch, 0, 0, 0, MsgAttackEffect.None,
        Unknown: 0, SpellLevel: 99, SpellId: 0));

    private sealed class Rate4DispatchFixture : IDisposable
    {
        public readonly WorldSessionStore Sessions = new();
        public readonly List<byte[]> Sent = [];
        public readonly List<long> PrefixDraws = [];
        public readonly List<long> SendDraws = [];
        public readonly ConquerTcpConnection Connection;
        private readonly WorldPacketHandlerContext context;
        private readonly Rate4Clock clock = new();
        private readonly Rate4PrefixLog log;
        private readonly WorldSession initial;
        private readonly LegacyNativeGameClientRuntime? physical;
        public Action? OnPrefix;
        public Action? OnSend;

        public Rate4DispatchFixture(long map = 1_000, int flag = 0, bool hasBook = true,
            int stamina = 100, int mana = 1_000, int cold = 0, bool bindPhysicalClient = true, bool withStore = true,
            bool withCatalog = true)
        {
            var data = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(6_003, sort: 29, level: 2, useStamina: 10, useMana: 40,
                    weaponType: 0, coolDown: 1_000, coldTime: cold)]);
            physical = bindPhysicalClient ? new LegacyNativeGameClientRuntime() : null;
            var attrs = new Dictionary<string, string>();
            if (flag != 0) attrs["StatusFlags"] = BuildStatusFlags([flag]);
            initial = CreateSession(mapId: map, minMana: mana, stamina: (ulong)stamina,
                strength: 0, spirit: 0, attributes: attrs,
                spellEntries: hasBook ? [CreatePlayerSpell(6_003, 2)] : [],
                playerItems: PlayerItemSnapshot.Empty) with { NativeGameClient = physical };
            Connection = new ConquerTcpConnection(null, null, sendAsync: (frame, _) =>
            {
                Sent.Add(frame.ToArray());
                SendDraws.Add(physical?.Player.CompletedRateDraws ?? -1);
                OnSend?.Invoke();
                return ValueTask.CompletedTask;
            }, nativeGameClient: physical);
            if (withStore) Sessions.Set(Connection, initial);
            log = new Rate4PrefixLog(() =>
            {
                PrefixDraws.Add(physical?.Player.CompletedRateDraws ?? -1);
                OnPrefix?.Invoke();
            });
            context = new WorldPacketHandlerContext(Connection, log, initial,
                WorldData: withCatalog ? data : null, Sessions: withStore ? Sessions : null, TimeProvider: clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Null(initial.MagicCooldowns);
            Assert.Empty(Sent);
        }

        public LegacyNativeGameClientRuntime Client => Assert.IsType<LegacyNativeGameClientRuntime>(physical);
        public LegacyNativePlayerRuntime Player => Client.Player;
        public WorldSession Owner => context.Sessions is null ? initial :
            Assert.IsType<WorldSession>(Sessions.TryGet(Connection, out var current) ? current : null);
        public DateTimeOffset Now => clock.GetUtcNow();
        public string LogText => log.ToString();
        public void Advance(int milliseconds) => clock.Advance(milliseconds);
        public Task SwitchAsync() => PacketHandlers.HandleAsync(context with { Session = Owner },
            ReadFrame(Rate4Packet(PlayerUid).ToFrameBytes())).AsTask();

        public void AssertCost(int index, LegacyMsgUpdateDataType type, ulong value)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(Sent[index]), out var packet));
            Assert.Equal(PlayerUid, packet.Uid);
            var scalar = Assert.Single(packet.Updates);
            Assert.Equal(type, scalar.Type);
            Assert.Equal(value, scalar.Value);
        }

        public void AssertSuccess(int start, ulong stamina, ulong mana, bool active)
        {
            AssertCost(start, LegacyMsgUpdateDataType.Stamina, stamina);
            AssertCost(start + 1, LegacyMsgUpdateDataType.Mana, mana);
            Assert.True(MsgAttackPacket.TryParse(ReadFrame(Sent[start + 2]), out var toggle));
            Assert.Equal(MsgAttackType.CounterKillSwitch, toggle.Query.AttackType);
            Assert.Equal((ushort)6_003, toggle.Query.SpellId);
            Assert.Equal((ushort)2, toggle.Query.SpellLevel);
            Assert.Equal(active ? 1 : 0, toggle.Query.Damage);
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), Owner.Character.Attributes["Stamina"]);
            Assert.Equal((long)mana, Owner.Character.MinMana);
            Assert.Equal(active ? "1" : "0", Owner.Character.Attributes["LegacyCounterKillActive"]);
            Assert.Empty(ParseMagicColdTimePackets(Sent.Skip(start)));
            Assert.Empty(ParseSpellAnimations(Sent.Skip(start)));
        }

        public void Dispose() => log.Dispose();
    }

    private sealed class Rate4PrefixLog(Action onPrefix) : StringWriter(CultureInfo.InvariantCulture)
    {
        public override Task WriteLineAsync(string? value)
        {
            if (value?.StartsWith("WorldServer handled MsgAttack ", StringComparison.Ordinal) == true)
                onPrefix();
            return base.WriteLineAsync(value);
        }
    }

    private sealed class Rate4Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(int milliseconds) => now = now.AddMilliseconds(milliseconds);
    }

    private sealed class Rate4NetworkInput : IDisposable
    {
        internal const uint Uid = 1_294_600;
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-rate4-network-" + Guid.NewGuid().ToString("N"));
        public string Users => Path.Combine(root, "Users");
        public Rate4NetworkInput(long map)
        {
            Directory.CreateDirectory(Users);
            File.WriteAllText(Path.Combine(Users, Uid.ToString(CultureInfo.InvariantCulture) + ".ini"),
                $"""
                [Character]
                UID={Uid}
                Name=Rate4Network
                Body=1008
                Face=154
                Class=7051
                Map={map}
                X=830
                Y=584
                PMap=1000
                PMapX=500
                PMapY=650
                Level=140
                Experience=0
                Reborn=2
                FirstClass=0
                SecoundeClass=7050
                Haire=27
                Agility=36
                Strength=0
                Vitaliti=447
                Spirit=0
                Atributes=12
                MinHitPoints=61258
                MinMana=5000
                Money=1000
                ConquerPoints=0
                BankCps=0
                BoundConquerPoints=0
                PkPoints=0
                Online=0
                Angle=1
                Day={DateTime.Now.DayOfYear}
                """);
        }
        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
