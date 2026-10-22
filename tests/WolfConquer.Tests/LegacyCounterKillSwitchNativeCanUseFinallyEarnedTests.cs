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
    private static readonly TimeSpan CounterKillFinallyGateTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task NativeCounterKillRetiredFinallyCannotClearReplacementPublicEarnedFocusedMarker()
    {
        using var fixture = new CounterKillFinallyEarnedFixture();
        fixture.AssertInitialSelection();
        Task? originalInvocation = null;
        var failures = new List<Exception>();
        try
        {
            originalInvocation = fixture.InvokeAsync(); // Retain A, not only its WaitAsync proxy.
            await originalInvocation.WaitAsync(CounterKillFinallyGateTimeout);
            fixture.AssertReplacementStillPubliclyPreparedAfterOuterCompleted();
        }
        catch (Exception error)
        {
            AddCounterKillFinallyFailure(failures, error);
        }
        finally
        {
            try { await fixture.ReleaseAndDrainInvocationsAsync(originalInvocation, failures); }
            catch (Exception error) { AddCounterKillFinallyFailure(failures, error); }
        }
        ThrowCounterKillFinallyFailures(failures);
        fixture.AssertReplacementCompletedNaturally();
    }

    private static void AddCounterKillFinallyFailure(List<Exception> failures, Exception error)
    {
        if (!failures.Exists(existing => ReferenceEquals(existing, error))) failures.Add(error);
    }

    private static void ThrowCounterKillFinallyFailures(List<Exception> failures)
    {
        if (failures.Count == 0) return;
        if (failures.Count == 1)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException(
            "CounterKill finally control failed; primary body error is first, followed by original A/B cleanup errors.",
            failures);
    }

    private sealed class CounterKillFinallyEarnedFixture : IDisposable
    {
        private const ushort Id = 6_003;
        private const ushort LearnedLevel = 2;
        private readonly WorldSessionStore sessions;
        private readonly ConquerTcpConnection connection;
        private readonly WorldPacketHandlerContext context;
        private readonly CounterKillFinallyEarnedLog log;
        private readonly List<byte[]> frames = [];
        private readonly List<WorldSession> publicationsAfterReplacementHeld = [];
        private readonly TaskCompletionSource<bool> replacementReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> releaseReplacement =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly WorldSession replacementSeed;
        private readonly WorldDataCatalog worldData;
        private readonly CounterKillFinallyEarnedClock clock = new();
        private WorldSession? retiredOriginal;
        private WorldSession? heldReplacement;
        private Task? replacementInvocation;
        private int handledLogs;

        public CounterKillFinallyEarnedFixture()
        {
            worldData = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(Id, sort: 29, level: 0, useMana: 1, useStamina: 1, coldTime: 0),
                 CreateMagicRow(Id, sort: 29, level: (byte)LearnedLevel, useMana: 40,
                     useStamina: 10, coolDown: 1_000, coldTime: 2_000)]);
            var original = CreateSession(mapId: 1_000, classCode: 142, minHitPoints: 5_000,
                minMana: 1_000, stamina: 100, spirit: 0, strength: 0,
                spellEntries: [CreatePlayerSpell(Id, LearnedLevel)],
                attributes: new Dictionary<string, string>
                {
                    ["StatusFlags"] = BuildStatusFlags(LegacyFocusedFlag),
                    ["Reborn"] = "0", ["Level"] = "120", ["FinallyControl"] = "original",
                });
            replacementSeed = CreateSession(uid: PlayerUid, name: "CounterKillFinallyReplacement",
                mapId: 1_000, classCode: 142, minHitPoints: 77_777, minMana: 77,
                stamina: 33, spirit: 0, strength: 0,
                spellEntries: [CreatePlayerSpell(Id, LearnedLevel)],
                attributes: new Dictionary<string, string>
                {
                    ["StatusFlags"] = BuildStatusFlags(LegacyFocusedFlag),
                    ["Reborn"] = "0", ["Level"] = "120", ["FinallyControl"] = "replacement",
                });
            sessions = new WorldSessionStore((_, _, published) =>
            {
                if (heldReplacement is not null) publicationsAfterReplacementHeld.Add(published);
            });
            // Declared same-physical-client MODEL; replacement B earns its marker via public entry.
            var physicalClient = new LegacyNativeGameClientRuntime();
            original = original with { NativeGameClient = physicalClient };
            replacementSeed = replacementSeed with { NativeGameClient = physicalClient };
            connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9962),
                sendAsync: (frame, _) =>
                {
                    frames.Add(frame.ToArray());
                    return ValueTask.CompletedTask;
                }, nativeGameClient: physicalClient);
            sessions.Set(connection, original);
            log = new CounterKillFinallyEarnedLog(OnLogAsync);
            context = new WorldPacketHandlerContext(connection, log, original,
                WorldData: worldData, Sessions: sessions, TimeProvider: clock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            AssertSeed(original, 100, 1_000, "original");
            AssertSeed(replacementSeed, 33, 77, "replacement");
            Assert.Empty(frames);
        }

        private WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(connection, out var owner) ? owner : null);

        public void AssertInitialSelection() => AssertSelection(Owner);

        private void AssertSelection(WorldSession owner)
        {
            var learned = Assert.Single(owner.PlayerSpells.Spells);
            Assert.Equal(Id, learned.Id);
            Assert.Equal(LearnedLevel, learned.Level);
            Assert.Equal((byte)0, learned.UseSpellSoul);
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
                owner.PlayerSpells, worldData, Id, out var selectedBook, out var selected));
            Assert.Equal(learned, selectedBook);
            Assert.Equal(Id, selected.Id);
            Assert.Equal((byte)LearnedLevel, selected.Level);
            Assert.Equal(LegacyMagicSort.CounterKill, selected.Sort);
            Assert.Equal((byte)10, selected.UseStamina);
            Assert.Equal((ushort)40, selected.UseMana);
            Assert.Equal(2_000, selected.ColdTime);
            Assert.Equal((ushort)1_000, selected.CoolDown);
            var row = Assert.Single(worldData.MagicByTypeId[Id], value => value.Fields[8] == "2");
            Assert.Equal("6003", row.Fields[0]);
            Assert.Equal("6003", row.Fields[1]);
            Assert.Equal("29", row.Fields[2]);
            Assert.Equal("0", row.Fields[22]);
            Assert.Empty(owner.PlayerItems.EquipmentItems);
            Assert.Empty(owner.PlayerItems.RuneItems);
            Assert.Equal((ushort)0, owner.LegacyRandomSpell);
        }

        private static void AssertSeed(WorldSession owner, int stamina, long mana, string marker)
        {
            Assert.True(HasLegacyStatusFlag(owner.Character.Attributes, LegacyFocusedFlag));
            Assert.False(owner.Character.Attributes.ContainsKey("doFocus"));
            Assert.False(owner.Character.Attributes.ContainsKey("DoFocus"));
            AssertUnspent(owner, stamina, mana, marker);
        }

        private static void AssertUnspent(WorldSession owner, int stamina, long mana, string marker)
        {
            Assert.Equal(stamina.ToString(CultureInfo.InvariantCulture), owner.Character.Attributes["Stamina"]);
            Assert.Equal(mana, owner.Character.MinMana);
            Assert.Equal(marker, owner.Character.Attributes["FinallyControl"]);
            Assert.True(owner.MagicCooldowns is null || !owner.MagicCooldowns.ContainsKey(Id));
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyCounterKillActive"));
            Assert.False(owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        }

        private async Task OnLogAsync(string? text)
        {
            if (!(text ?? string.Empty).StartsWith("WorldServer handled MsgAttack ", StringComparison.Ordinal))
                return;
            var ordinal = ++handledLogs;
            if (ordinal == 1)
            {
                var original = Owner;
                Assert.False(HasLegacyStatusFlag(original.Character.Attributes, LegacyFocusedFlag));
                Assert.Equal("true", original.Character.Attributes["doFocus"]);
                AssertUnspent(original, 100, 1_000, "original");
                Assert.True(sessions.TryRemove(connection, out retiredOriginal));
                Assert.Same(original, retiredOriginal);
                AssertSeed(replacementSeed, 33, 77, "replacement");
                sessions.Set(connection, replacementSeed); // Real retirement/new registration, not a copy.
                Assert.Same(replacementSeed, Owner);
                Assert.NotSame(original, Owner);
                Assert.Equal(original.Character.Uid, Owner.Character.Uid);
                AssertSelection(Owner);
                replacementInvocation = InvokeAsync(); // REAL second serialized public entry.
                var first = await Task.WhenAny(replacementReady.Task, replacementInvocation)
                    .WaitAsync(CounterKillFinallyGateTimeout);
                // A readiness assertion must not hide B's original fault/message/stack.
                if (ReferenceEquals(first, replacementInvocation)) await replacementInvocation;
                Assert.Same(replacementReady.Task, first);
                Assert.True(replacementReady.Task.IsCompletedSuccessfully);
                Assert.False(replacementInvocation.IsCompleted);
                return; // A may now resume and run its own finally while B remains suspended.
            }
            Assert.Equal(2, ordinal);
            var replacement = Owner;
            Assert.Equal("replacement", replacement.Character.Attributes["FinallyControl"]);
            Assert.False(HasLegacyStatusFlag(replacement.Character.Attributes, LegacyFocusedFlag));
            Assert.Equal("true", replacement.Character.Attributes["doFocus"]); // PUBLIC prefix earned this.
            Assert.False(replacement.Character.Attributes.Keys.Contains("DoFocus", StringComparer.Ordinal));
            AssertUnspent(replacement, 33, 77, "replacement");
            Assert.Equal(77_777, replacement.Character.MinHitPoints);
            heldReplacement = replacement;
            Assert.Empty(publicationsAfterReplacementHeld);
            replacementReady.SetResult(true);
            await releaseReplacement.Task; // B cannot admit/cost/toggle/own-finally before test releases it.
        }

        public void AssertReplacementStillPubliclyPreparedAfterOuterCompleted()
        {
            Assert.Equal(2, handledLogs);
            Assert.True(replacementReady.Task.IsCompletedSuccessfully);
            Assert.NotNull(replacementInvocation);
            Assert.False(replacementInvocation.IsCompleted);
            // FIRST state oracle: unlike the old prefix control, B actually earned this marker.
            Assert.True(Owner.Character.Attributes.TryGetValue("doFocus", out var marker) && marker == "true");
            Assert.False(Owner.Character.Attributes.Keys.Contains("DoFocus", StringComparer.Ordinal));
            Assert.Same(heldReplacement, Owner);
            Assert.Empty(publicationsAfterReplacementHeld);
            AssertUnspent(Owner, 33, 77, "replacement");
            Assert.Equal(77_777, Owner.Character.MinHitPoints);
            Assert.NotNull(retiredOriginal);
            AssertUnspent(retiredOriginal, 100, 1_000, "original");
            Assert.Equal(2, frames.Count); // NON-vacuous exact two genuine prefix vectors, no cost/toggle.
            AssertFocusedCleared(frames[0]);
            AssertFocusedCleared(frames[1]);
            Assert.Empty(ParseMagicColdTimePackets(frames));
            Assert.Empty(ParseSpellAnimations(frames));
        }

        public async Task ReleaseAndDrainInvocationsAsync(Task? originalInvocation, List<Exception> failures)
        {
            releaseReplacement.TrySetResult(true); // Also releases B if it reaches the gate after timeout.
            if (originalInvocation is not null)
                await DrainInvocationAsync("original A", originalInvocation, failures);
            var replacement = replacementInvocation; // Read AFTER A's drain, so late registration is included.
            if (replacement is not null && !ReferenceEquals(replacement, originalInvocation))
                await DrainInvocationAsync("replacement B", replacement, failures);
        }

        private static async Task DrainInvocationAsync(string role, Task invocation, List<Exception> failures)
        {
            try { await invocation.WaitAsync(CounterKillFinallyGateTimeout); }
            catch (TimeoutException error) when (!invocation.IsCompleted)
            {
                AddCounterKillFinallyFailure(failures, new TimeoutException(
                    $"{role} remained pending at the bounded cleanup deadline; release was signalled, not cancellation. " +
                    "Its retained fault observer remains installed; this test cannot pass or claim terminal cleanup.",
                    error));
            }
            catch (Exception error) { AddCounterKillFinallyFailure(failures, error); }
            finally
            {
                if (invocation.Exception is { } aggregate)
                    foreach (var error in aggregate.InnerExceptions)
                        AddCounterKillFinallyFailure(failures, error);
            }
        }

        private static void ObserveInvocationFault(Task invocation)
        {
            _ = invocation.ContinueWith(
                static completed => { _ = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default); // Per-task observation only; no global event or fire-and-forget entry.
        }

        public void AssertReplacementCompletedNaturally()
        {
            Assert.NotNull(replacementInvocation);
            Assert.True(replacementInvocation.IsCompletedSuccessfully);
            Assert.Equal(2, handledLogs);
            Assert.False(Owner.Character.Attributes.ContainsKey("doFocus"));
            Assert.False(Owner.Character.Attributes.ContainsKey("DoFocus")); // B's OWN finally is positive control.
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyFocusedFlag));
            Assert.Equal("23", Owner.Character.Attributes["Stamina"]);
            Assert.Equal(37, Owner.Character.MinMana);
            Assert.Equal(77_777, Owner.Character.MinHitPoints);
            Assert.Equal("replacement", Owner.Character.Attributes["FinallyControl"]);
            Assert.Equal("1", Owner.Character.Attributes["LegacyCounterKillActive"]);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.Equal(clock.GetUtcNow().AddMilliseconds(2_000), Owner.MagicCooldowns![Id]);
            Assert.NotEmpty(publicationsAfterReplacementHeld);
            Assert.Equal(5, frames.Count);
            AssertFocusedCleared(frames[0]);
            AssertFocusedCleared(frames[1]);
            AssertScalar(frames[2], LegacyMsgUpdateDataType.Stamina, 23);
            AssertScalar(frames[3], LegacyMsgUpdateDataType.Mana, 37);
            Assert.True(MsgAttackPacket.TryParse(ReadFrame(frames[4]), out var response));
            Assert.Equal(MsgAttackType.CounterKillSwitch, response.Query.AttackType);
            Assert.Equal(PlayerUid, response.Query.Uid);
            Assert.Equal(PlayerUid, response.Query.OpponentUid);
            Assert.Equal(Id, response.Query.SpellId);
            Assert.Equal(LearnedLevel, response.Query.SpellLevel);
            Assert.Equal(1, response.Query.Damage);
            Assert.Empty(ParseMagicColdTimePackets(frames));
            Assert.Empty(ParseSpellAnimations(frames));
        }

        private static void AssertFocusedCleared(byte[] bytes)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
            Assert.Equal(PlayerUid, packet.Uid);
            var update = Assert.Single(packet.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, update.Type);
            Assert.NotNull(update.Values);
            Assert.False(IsLegacyStatusFlagSet(update.Values!, LegacyFocusedFlag));
        }

        private static void AssertScalar(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
            Assert.Equal(PlayerUid, packet.Uid);
            var update = Assert.Single(packet.Updates);
            Assert.Equal(type, update.Type);
            Assert.Equal(value, update.Value);
        }

        public Task InvokeAsync()
        {
            var invocation = PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid, OpponentUid: PlayerUid, X: 830, Y: 584,
                    AttackType: MsgAttackType.CounterKillSwitch, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellLevel: 99, SpellId: 0)).ToFrameBytes())).AsTask();
            ObserveInvocationFault(invocation); // Both retained A and B get an observer BEFORE returning.
            return invocation;
        }

        public void Dispose() => log.Dispose();
    }

    private sealed class CounterKillFinallyEarnedClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class CounterKillFinallyEarnedLog(Func<string?, Task> onLine) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value) => onLine(value);
    }
}
