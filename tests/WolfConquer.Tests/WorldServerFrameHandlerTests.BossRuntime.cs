using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerRuntimeLoopDeliversModeledBossSpellOnlyAfterProactiveSchedulerTick()
    {
        const uint playerUid = 1_293_600;
        const int mapId = 1_015;
        const ushort x = 100;
        const ushort y = 100;
        const ushort expectedSpellId = 10_001;
        var tickInterval = TimeSpan.FromSeconds(1);

        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(
            playerUid,
            uid: playerUid,
            mapId: mapId,
            x: x,
            y: y,
            minHitPoints: 1_000_000,
            extraCharacterLines:
                DisableLegacyWeaponProcLines + "\r\n" +
                "LegacyBossSpellIndex=1\r\n" +
                "LegacyBossStatusRoll=0");

        var bossFamily = new Monster(
            7_981,
            "SchedulerBoss7981",
            new Dictionary<string, string>
            {
                ["life"] = "10000000",
                ["Boss"] = "1",
                ["lookface"] = "7981",
                ["level"] = "120",
                ["defense"] = "0",
                ["defense2"] = "10000",
                ["attack_min"] = "1000",
                ["attack_max"] = "1000",
                ["attack_range"] = "18",
                ["view_range"] = "18",
                ["attack_speed"] = "1000",
                ["move_speed"] = "1000",
            },
            []);
        var bossSpawn = new MonsterSpawn(
            GeneratorId: 7_981,
            MapId: mapId,
            MonsterTypeId: 7_981,
            BoundX: x,
            BoundY: y,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: mapId,
            SourceMonsterName: "SchedulerBoss7981",
            SourceRelativePath: Path.Combine(mapId.ToString(), "SchedulerBoss7981", "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = "7981",
                ["mapid"] = mapId.ToString(),
                ["npctype"] = "7981",
            });
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            monsters: [bossFamily],
            monsterSpawns: [bossSpawn]);
        var bossInstance = Assert.Single(LegacyMonsterInstance.FromGenerator(
            bossSpawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(bossSpawn)));
        Assert.True(bossInstance.Boss);

        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(playerUid, "user02", ConquerRuntimeDefaults.WorldServer);
        var timeProvider = new BossRuntimeManualTimeProvider(DateTimeOffset.UtcNow.AddSeconds(20));
        using var log = new RuntimeThreadSafeStringWriter();
        using var cancel = new CancellationTokenSource();
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                tickInterval,
                timeProvider),
            log);
        var sent = new RuntimeThreadSafeFrameCapture();
        var connection = CreateSendingConnection(sent);

        try
        {
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()),
                cancel.Token);
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgActionPacket
                {
                    ObjId = playerUid,
                    Timestamp = 1234,
                    Type = MsgActionType.SetLocation,
                }.ToFrameBytes()),
                cancel.Token);
            await WaitForMonsterRuntimeTimerRegistrationAsync(timeProvider);

            var monster = sent.ToArray()
                .Select(bytes => LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var parsed) ? parsed : null)
                .SingleOrDefault(packet => packet is not null && packet.Uid == bossInstance.Uid);
            Assert.NotNull(monster);

            var hitStart = sent.Count;
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: playerUid,
                    OpponentUid: monster.Uid,
                    X: monster.X,
                    Y: monster.Y,
                    AttackType: MsgAttackType.Physical,
                    Damage: 1,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0)).ToFrameBytes()),
                cancel.Token);
            var immediateEnd = sent.Count;

            var (playerHitIndex, _) = AssertServerAppliedSurvivingMonsterPhysicalHitAfter(
                sent,
                hitStart,
                playerUid,
                monster,
                log.ToString());
            Assert.InRange(playerHitIndex, hitStart, immediateEnd - 1);
            var immediateFrames = sent
                .Skip(hitStart)
                .Take(immediateEnd - hitStart)
                .ToArray();
            Assert.DoesNotContain(
                immediateFrames,
                bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell) &&
                    spell.AttackerUid == monster.Uid);
            Assert.DoesNotContain(
                immediateFrames,
                bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
                    attack.Query.Uid == monster.Uid &&
                    attack.Query.OpponentUid == playerUid);
            Assert.DoesNotContain(
                immediateFrames,
                bytes => LegacyGameUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                    update.Uid == playerUid &&
                    update.Type == LegacyGameUpdateDataType.Flustered);

            var afterPlayerHit = GetSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(
                LegacyPlayerSpawnPacket.FromSnapshot(afterPlayerHit.Character).StatusFlags,
                LegacyBossStatusEffect.FrightenedFlag));
            Assert.DoesNotContain("WorldServer sent proactive monster attacks", log.ToString());
            Assert.DoesNotContain("WorldServer sent legacy boss physical spell retaliation", log.ToString());

            var schedulerStart = immediateEnd;
            timeProvider.Advance(tickInterval);
            await WaitUntilAsync(
                () => FindFrameIndexAfter(
                    sent,
                    schedulerStart,
                    frame => LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
                        spell.AttackerUid == monster.Uid &&
                        spell.SpellId == expectedSpellId) >= schedulerStart,
                TimeSpan.FromSeconds(3));

            var spellIndex = FindFrameIndexAfter(
                sent,
                schedulerStart,
                frame => LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
                    spell.AttackerUid == monster.Uid &&
                    spell.SpellId == expectedSpellId);
            Assert.True(LegacySpellAnimationPacket.TryParse(ReadFrame(sent[spellIndex]), out var bossSpell));
            Assert.Equal(monster.Uid, bossSpell.AttackerUid);
            Assert.Equal(0U, bossSpell.AttackedUid);
            Assert.Equal(x, bossSpell.X);
            Assert.Equal(y, bossSpell.Y);
            Assert.Equal(expectedSpellId, bossSpell.SpellId);
            var spellTarget = Assert.Single(bossSpell.Targets);
            Assert.Equal(playerUid, spellTarget.Uid);
            Assert.InRange(spellTarget.Damage, 1U, 999_999U);
            Assert.True(spellTarget.Hit);

            await WaitUntilAsync(
                () => FindFrameIndexAfter(
                    sent,
                    spellIndex + 1,
                    frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                        update.Uid == playerUid &&
                        update.Updates.Any(entry =>
                            entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                            entry.Values is { } values &&
                            HasLegacyStatusFlag(values, LegacyBossStatusEffect.FrightenedFlag))) > spellIndex,
                TimeSpan.FromSeconds(3));
            var statusIndex = FindFrameIndexAfter(
                sent,
                spellIndex + 1,
                frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                    update.Uid == playerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is { } values &&
                        HasLegacyStatusFlag(values, LegacyBossStatusEffect.FrightenedFlag)));
            Assert.True(statusIndex > spellIndex, $"Expected Frightened status vector after boss spell. Log={log}");

            await WaitUntilAsync(
                () => FindFrameIndexAfter(
                    sent,
                    statusIndex + 1,
                    frame => LegacyGameUpdatePacket.TryParse(frame, out var update) &&
                        update.Uid == playerUid &&
                        update.Type == LegacyGameUpdateDataType.Flustered) > statusIndex,
                TimeSpan.FromSeconds(3));
            var gameUpdateIndex = FindFrameIndexAfter(
                sent,
                statusIndex + 1,
                frame => LegacyGameUpdatePacket.TryParse(frame, out var update) &&
                    update.Uid == playerUid &&
                    update.Type == LegacyGameUpdateDataType.Flustered);
            Assert.True(gameUpdateIndex > statusIndex, $"Expected Frightened GameUpdate after status vector. Log={log}");
            Assert.True(LegacyGameUpdatePacket.TryParse(ReadFrame(sent[gameUpdateIndex]), out var frightened));
            Assert.Equal(54U, (uint)frightened.Type);
            Assert.Equal(LegacyGameUpdateDataType.Flustered, frightened.Type);
            Assert.True(frightened.Active);
            Assert.Equal(0U, frightened.ShownAmount);
            Assert.Equal(6U, frightened.Time);
            Assert.Equal(0U, frightened.Amount);

            var afterScheduler = GetSession(handler, connection);
            Assert.Equal(
                afterPlayerHit.Character.MinHitPoints - spellTarget.Damage,
                afterScheduler.Character.MinHitPoints);
            Assert.True(HasLegacyStatusFlag(
                LegacyPlayerSpawnPacket.FromSnapshot(afterScheduler.Character).StatusFlags,
                LegacyBossStatusEffect.FrightenedFlag));
            await WaitUntilAsync(
                () => log.ToString().Contains("WorldServer sent proactive monster attacks", StringComparison.Ordinal),
                TimeSpan.FromSeconds(1));
            var output = log.ToString();
            Assert.Contains("WorldServer sent proactive monster attacks", output);
            Assert.Contains("WorldServer sent legacy boss physical spell retaliation", output);
            Assert.Contains("spellId=10001", output);
            Assert.Contains("statusApplied=True", output);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerRuntimeLoopAttacksHighHpBossTargetAtEighteenTilesWithoutChasing()
    {
        const uint playerUid = 1_293_600;
        const int mapId = 1_015;
        const ushort monsterX = 100;
        const ushort monsterY = 100;
        const ushort playerX = monsterX + 18;
        const ushort playerY = monsterY;
        const ushort expectedSpellId = 9_999;
        var tickInterval = TimeSpan.FromMilliseconds(100);

        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(
            playerUid,
            uid: playerUid,
            mapId: mapId,
            x: playerX,
            y: playerY,
            minHitPoints: 1_000_000,
            extraCharacterLines: DisableLegacyWeaponProcLines);

        var bossFamily = new Monster(
            9_180,
            "EighteenTileBoss",
            new Dictionary<string, string>
            {
                ["life"] = "3000000",
                ["Boss"] = "1",
                ["lookface"] = "9180",
                ["level"] = "120",
                ["defense"] = "0",
                ["defense2"] = "10000",
                ["attack_min"] = "1000",
                ["attack_max"] = "1000",
                ["attack_range"] = "1",
                ["view_range"] = "1",
                ["attack_speed"] = "100",
                ["move_speed"] = "100",
                ["magic_type"] = "0",
            },
            []);
        var bossSpawn = new MonsterSpawn(
            GeneratorId: 9_180,
            MapId: mapId,
            MonsterTypeId: 9_180,
            BoundX: monsterX,
            BoundY: monsterY,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: mapId,
            SourceMonsterName: "EighteenTileBoss",
            SourceRelativePath: Path.Combine(mapId.ToString(), "EighteenTileBoss", "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = "9180",
                ["mapid"] = mapId.ToString(),
                ["npctype"] = "9180",
            });
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            monsters: [bossFamily],
            monsterSpawns: [bossSpawn]);
        var bossInstance = Assert.Single(LegacyMonsterInstance.FromGenerator(
            bossSpawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(bossSpawn)));
        Assert.True(bossInstance.Boss);
        Assert.True(bossInstance.Hitpoints >= 2_000_000U);
        Assert.Equal(18, LegacyTestDistance(bossInstance.X, bossInstance.Y, playerX, playerY));

        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(playerUid, "user02", ConquerRuntimeDefaults.WorldServer);
        var timeProvider = new BossRuntimeManualTimeProvider(DateTimeOffset.UtcNow.AddSeconds(20));
        using var log = new RuntimeThreadSafeStringWriter();
        using var cancel = new CancellationTokenSource();
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            worldData,
            CreateDeterministicMonsterRuntimeLoopOptions(tickInterval, timeProvider),
            log);
        var sent = new RuntimeThreadSafeFrameCapture();
        var connection = CreateSendingConnection(sent);

        try
        {
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()),
                cancel.Token);
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgActionPacket
                {
                    ObjId = playerUid,
                    Timestamp = 1234,
                    Type = MsgActionType.SetLocation,
                }.ToFrameBytes()),
                cancel.Token);
            await WaitForMonsterRuntimeTimerRegistrationAsync(timeProvider);

            Assert.Contains(
                sent,
                frame => LegacyMonsterSpawnPacket.TryParse(ReadFrame(frame), out var spawn) &&
                    spawn.Uid == bossInstance.Uid);
            var schedulerStart = sent.Count;
            timeProvider.Advance(tickInterval);
            await WaitForProactiveMonsterActionAsync(
                () => FindFrameIndexAfter(
                    sent,
                    schedulerStart,
                    frame => LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
                        spell.AttackerUid == bossInstance.Uid &&
                        spell.SpellId == expectedSpellId) >= schedulerStart,
                log);

            var spellIndex = FindFrameIndexAfter(
                sent,
                schedulerStart,
                frame => LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
                    spell.AttackerUid == bossInstance.Uid &&
                    spell.SpellId == expectedSpellId);
            Assert.DoesNotContain(
                sent.ToArray().Skip(schedulerStart).Take(spellIndex - schedulerStart),
                frame => MsgMovementPacket.TryParse(ReadFrame(frame), out var movement) &&
                    movement.Uid == bossInstance.Uid);
            Assert.True(LegacySpellAnimationPacket.TryParse(ReadFrame(sent[spellIndex]), out var bossSpell));
            Assert.Equal(playerX, bossSpell.X);
            Assert.Equal(playerY, bossSpell.Y);
            var target = Assert.Single(bossSpell.Targets);
            Assert.Equal(playerUid, target.Uid);
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private sealed class BossRuntimeManualTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly HashSet<BossRuntimeManualTimer> _timers = [];
        private DateTimeOffset _utcNow;
        private long _timestamp;

        public BossRuntimeManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        public int ScheduledTimerCount
        {
            get
            {
                lock (_gate)
                {
                    return _timers.Count(timer => timer.IsScheduled);
                }
            }
        }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _utcNow;
            }
        }

        public override long GetTimestamp()
        {
            lock (_gate)
            {
                return _timestamp;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            var timer = new BossRuntimeManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Manual time cannot move backwards.");
            }

            BossRuntimeManualTimer[] dueTimers;
            lock (_gate)
            {
                _utcNow += amount;
                _timestamp = checked(_timestamp + amount.Ticks);
                dueTimers = _timers
                    .Where(timer => timer.PrepareToFire(_timestamp))
                    .ToArray();
            }

            foreach (var timer in dueTimers)
            {
                timer.Fire();
            }
        }

        private bool Change(
            BossRuntimeManualTimer timer,
            TimeSpan dueTime,
            TimeSpan period)
        {
            if (dueTime < Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            }

            if (period < Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(period));
            }

            lock (_gate)
            {
                if (timer.IsDisposed)
                {
                    return false;
                }

                _timers.Add(timer);
                timer.DueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : checked(_timestamp + dueTime.Ticks);
                timer.Period = period;
                return true;
            }
        }

        private void Dispose(BossRuntimeManualTimer timer)
        {
            lock (_gate)
            {
                timer.IsDisposed = true;
                timer.DueTimestamp = long.MaxValue;
                _timers.Remove(timer);
            }
        }

        private sealed class BossRuntimeManualTimer : ITimer
        {
            private readonly BossRuntimeManualTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;

            public BossRuntimeManualTimer(
                BossRuntimeManualTimeProvider owner,
                TimerCallback callback,
                object? state)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
                DueTimestamp = long.MaxValue;
                Period = Timeout.InfiniteTimeSpan;
            }

            public bool IsDisposed { get; set; }

            public long DueTimestamp { get; set; }

            public TimeSpan Period { get; set; }

            public bool IsScheduled => !IsDisposed && DueTimestamp != long.MaxValue;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return _owner.Change(this, dueTime, period);
            }

            public void Dispose()
            {
                _owner.Dispose(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public bool PrepareToFire(long timestamp)
            {
                if (!IsScheduled || DueTimestamp > timestamp)
                {
                    return false;
                }

                DueTimestamp = Period == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : checked(timestamp + Period.Ticks);
                return true;
            }

            public void Fire()
            {
                _callback(_state);
            }
        }
    }
}
