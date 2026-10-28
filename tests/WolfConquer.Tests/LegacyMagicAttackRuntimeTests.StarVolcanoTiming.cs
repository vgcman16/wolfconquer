using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerCombatGearStarVolcanoFloorRuntimeRepeatsUntilSourceExpirationThenRemovesEffect()
    {
        const ushort StarVolcanoPassiveSpellId = 22_070;
        const uint StarVolcanoFloorItemId = 4_200;
        const uint FloorUid = 900_226;

        var monsterFamilyAttributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["life"] = "500000",
            ["defense"] = "0",
        };
        var monsterFamily = CreateMonsterFamily() with { Attributes = monsterFamilyAttributes };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 22072, boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                StarVolcanoPassiveSpellId,
                sort: 1,
                useMana: 0,
                damage: 10,
                maxTargets: 4,
                range: 15,
                coldTime: 0));
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        using var log = new StarVolcanoTimingLog();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var runtimeAnchor = DateTimeOffset.UtcNow.AddSeconds(5);
        var clock = new ManualRuntimeTimeProvider(runtimeAnchor);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                clock),
            log);
        var session = CreateSession(strength: 10, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(StarVolcanoPassiveSpellId),
        ]);
        SetHandlerSession(handler, connection, session);

        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            StarVolcanoFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900226");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                session.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    StarVolcanoPassiveSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    runtimeAnchor.AddMilliseconds(-1),
                    HitRadius: 4,
                    ExpiresAt: runtimeAnchor.AddMilliseconds(600),
                    RepeatMilliseconds: 80)),
            runtimeAnchor);

        Task[] workers = [];
        Exception? primary = null;
        var observationBudget = TimeSpan.FromSeconds(3);
        var observationClock = System.Diagnostics.Stopwatch.StartNew();

        TimeSpan RemainingObservationBudget()
        {
            var remaining = observationBudget - observationClock.Elapsed;
            if (remaining <= TimeSpan.Zero) throw new TimeoutException("StarVolcano exceeded its original 3-second observation budget.");
            return remaining;
        }

        int ActualPulseCount() => ParseSpellAnimations(SnapshotFrames(sent)).Count(spell =>
            spell.SpellId == StarVolcanoPassiveSpellId && spell.AttackerUid == FloorUid);

        bool ActualRemoveWasSent() => ParseLegacyFloorItems(SnapshotFrames(sent)).Any(floorItem =>
            floorItem.Uid == FloorUid && floorItem.ItemId == StarVolcanoFloorItemId &&
            floorItem.DropType == LegacyFloorItemDropType.RemoveEffect);

        async Task WaitForReadyTimersAsync() =>
            await WaitForScheduledManualTimerAsync(clock).WaitAsync(RemainingObservationBudget());

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);
            workers = handler.CaptureLegacyRuntimeWorkerTasks();
            Assert.NotEmpty(workers);
            await WaitForReadyTimersAsync();
            Assert.Equal(runtimeAnchor, clock.GetUtcNow());
            Assert.Equal(0, ActualPulseCount());
            Assert.False(ActualRemoveWasSent());
            Assert.True(GetHandlerFloorItems(handler).TryGetVisiblePacket(FloorUid, clock.GetUtcNow(), out _));

            // Real Task.Delay timers dispatch the actual player/global workers.
            // Host scheduling cannot advance this effect's virtual lifetime.
            clock.Advance(TimeSpan.FromMilliseconds(10));
            await WaitUntilAsync(() => ActualPulseCount() >= 1, RemainingObservationBudget());
            await WaitForReadyTimersAsync();
            Assert.False(ActualRemoveWasSent());

            // The real floor consumer records nextDue=now+80 after pulse1.
            clock.Advance(TimeSpan.FromMilliseconds(80));
            await WaitUntilAsync(() => ActualPulseCount() >= 2, RemainingObservationBudget());
            await WaitForReadyTimersAsync();
            Assert.Equal(runtimeAnchor.AddMilliseconds(90), clock.GetUtcNow());
            Assert.False(ActualRemoveWasSent());
            Assert.True(GetHandlerFloorItems(handler).TryGetVisiblePacket(FloorUid, clock.GetUtcNow(), out _));

            // Expiry is checked before another due pulse, at the exact source
            // 600ms boundary. A real scheduled timer, not a direct tick helper,
            // drives the removal and its actual connection send.
            clock.Advance(TimeSpan.FromMilliseconds(510));
            await WaitUntilAsync(ActualRemoveWasSent, RemainingObservationBudget());
            await WaitForReadyTimersAsync();
            Assert.Equal(runtimeAnchor.AddMilliseconds(600), clock.GetUtcNow());

            var sentSnapshot = SnapshotFrames(sent);
            var pulses = ParseSpellAnimations(sentSnapshot)
                .Where(spell => spell.SpellId == StarVolcanoPassiveSpellId && spell.AttackerUid == FloorUid)
                .ToArray();
            Assert.True(pulses.Length >= 2);
            Assert.All(pulses.Take(2), pulse =>
            {
                Assert.Equal(floorPacket.OwnerX, pulse.X);
                Assert.Equal(floorPacket.OwnerY, pulse.Y);
                Assert.Contains(pulse.Targets, target => target.Uid == monster.Uid && target.Damage > 0 && target.Hit);
            });

            var remove = Assert.Single(
                ParseLegacyFloorItems(sentSnapshot),
                floorItem => floorItem.Uid == FloorUid &&
                    floorItem.DropType == LegacyFloorItemDropType.RemoveEffect);
            Assert.Equal(StarVolcanoFloorItemId, remove.ItemId);
            Assert.False(GetHandlerFloorItems(handler).TryGetVisiblePacket(FloorUid, clock.GetUtcNow(), out _));
            Assert.DoesNotContain("PARITY-BUG world runtime loop exception", log.Snapshot());
        }
        catch (TimeoutException error)
        {
            var diagnostic = new TimeoutException(
                "StarVolcano real worker observation timed out; virtualElapsedMs=" +
                (clock.GetUtcNow() - runtimeAnchor).TotalMilliseconds +
                " scheduledTimers=" + clock.ScheduledTimerCount +
                " pulses=" + ActualPulseCount() + " removeSent=" + ActualRemoveWasSent() +
                " actualRuntimeLog:\n" + log.Snapshot(), error);
            primary = diagnostic;
            throw diagnostic;
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try
            {
                await cancel.CancelAsync();
                await Task.WhenAll(workers.Concat(handler.CaptureLegacyRuntimeWorkerTasks()).Distinct())
                    .WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(0L, handler.LegacyRuntimeFaultedWorkerCount);
                Assert.Equal(0, clock.ScheduledTimerCount);
            }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }
    }

    private sealed class StarVolcanoTimingLog : StringWriter
    {
        private readonly object gate = new();
        internal string Snapshot() { lock (gate) return base.ToString(); }
        public override void WriteLine(string? value) { lock (gate) base.WriteLine(value); }
        public override Task WriteLineAsync(string? value)
        {
            WriteLine(value);
            return Task.CompletedTask;
        }
    }
}
