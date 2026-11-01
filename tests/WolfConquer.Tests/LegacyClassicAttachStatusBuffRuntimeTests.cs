using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ClassicAttachStatusInvisibilitySpellId = 1_075;
    private const ushort ClassicAttachStatusStigmaSpellId = 1_095;
    private const int ClassicAttachStatusInvisibilityFlag = 0x16;
    private const int ClassicAttachStatusStigmaFlag = 0x09;
    private const uint ClassicAttachStatusDurationSeconds = 45;
    private const uint ClassicAttachStatusUnknownTargetUid = 799_999;
    private const string ClassicAttachStatusInvisibilityExpiresAtUtcAttribute =
        "LegacyInvisibilityExpiresAtUtc";
    private const string ClassicAttachStatusInvisibilityRemoveOnDeadAttribute =
        "LegacyInvisibilityRemoveOnDead";
    private const string ClassicAttachStatusAccuracyExpiresAtUtcAttribute =
        "LegacyAccuracyExpiresAtUtc";
    private const string ClassicAttachStatusAccuracyRemoveOnDeadAttribute =
        "LegacyAccuracyRemoveOnDead";
    private const string ClassicAttachStatusMagicShieldExpiresAtUtcAttribute =
        "LegacyMagicShieldExpiresAtUtc";
    private const string ClassicAttachStatusMagicShieldRemoveOnDeadAttribute =
        "LegacyMagicShieldRemoveOnDead";
    private const string ClassicAttachStatusStigmaExpiresAtUtcAttribute =
        "LegacyStigmaExpiresAtUtc";
    private const string ClassicAttachStatusStigmaRemoveOnDeadAttribute =
        "LegacyStigmaRemoveOnDead";

    [Theory]
    [InlineData(ClassicAttachStatusInvisibilitySpellId, ClassicAttachStatusInvisibilityFlag)]
    [InlineData(StarOfAccuracySpellId, LegacyStarOfAccuracyFlag)]
    [InlineData(MagicShieldSpellId, LegacyMagicShieldFlag)]
    [InlineData(ClassicAttachStatusStigmaSpellId, ClassicAttachStatusStigmaFlag)]
    public async Task HandlerClassicAttachStatusMonsterTargetSendsStatusBeforeProgressAndAnimation(
        ushort spellId,
        int statusFlag)
    {
        using var result = await RunClassicAttachStatusCastAsync(
            spellId,
            ClassicAttachStatusTargetKind.Monster);

        Assert.True(result.MonsterStates.TryGetTimedStatusFlag(
            result.Monster,
            statusFlag,
            result.AfterCast,
            out var status));
        Assert.Equal(statusFlag, status.StatusFlag);
        Assert.True(status.RemoveOnDead);
        Assert.InRange(
            status.ExpiresAt,
            result.BeforeCast.AddSeconds(ClassicAttachStatusDurationSeconds),
            result.AfterCast.AddSeconds(ClassicAttachStatusDurationSeconds));

        var runtimeVector = result.MonsterStates.CreateStatusFlagVector(
            result.Monster,
            result.AfterCast,
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, runtimeVector.Length);
        Assert.True(IsLegacyStatusFlagSet(runtimeVector, statusFlag));

        var frames = SnapshotFrames(result.CasterSent);
        var statusIndex = AssertClassicAttachStatusVectorFrame(
            frames,
            result.Monster.Uid,
            statusFlag,
            expectedSet: true);
        var progressIndex = AssertClassicAttachStatusProgressFrame(frames, spellId);
        var animationIndex = AssertClassicAttachStatusAnimationFrame(
            frames,
            spellId,
            result.Monster.Uid,
            result.Monster.X,
            result.Monster.Y);

        Assert.True(statusIndex < progressIndex);
        Assert.True(progressIndex < animationIndex);
    }

    [Fact]
    public async Task HandlerClassicMagicShieldAlreadyActiveMonsterSendsProgressAndEmptyAnimationOnly()
    {
        var seededAt = DateTimeOffset.UtcNow;
        var seededStatus = default(LegacyMonsterTimedStatusState);
        using var result = await RunClassicAttachStatusCastAsync(
            MagicShieldSpellId,
            ClassicAttachStatusTargetKind.Monster,
            arrangeMonster: (states, monster) =>
            {
                var applied = states.ApplyTimedStatusFlag(
                    monster,
                    LegacyMagicShieldFlag,
                    TimeSpan.FromMinutes(2),
                    seededAt,
                    refreshIfActive: false);
                Assert.True(applied.Applied);
                Assert.True(applied.Status.HasValue);
                seededStatus = applied.Status.Value;
            });

        Assert.True(result.MonsterStates.TryGetTimedStatusFlag(
            result.Monster,
            LegacyMagicShieldFlag,
            result.AfterCast,
            out var activeStatus));
        Assert.Equal(seededStatus, activeStatus);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == result.Monster.Uid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));

        var frames = SnapshotFrames(result.CasterSent);
        var progressIndex = AssertClassicAttachStatusProgressFrame(frames, MagicShieldSpellId);
        var animationIndex = AssertClassicAttachStatusAnimationFrame(
            frames,
            MagicShieldSpellId,
            expectedTargetUid: null,
            result.Monster.X,
            result.Monster.Y);

        Assert.True(progressIndex < animationIndex);
    }

    [Theory]
    [InlineData(ClassicAttachStatusInvisibilitySpellId, ClassicAttachStatusInvisibilityFlag)]
    [InlineData(StarOfAccuracySpellId, LegacyStarOfAccuracyFlag)]
    [InlineData(MagicShieldSpellId, LegacyMagicShieldFlag)]
    [InlineData(ClassicAttachStatusStigmaSpellId, ClassicAttachStatusStigmaFlag)]
    public async Task HandlerClassicAttachStatusUnknownTargetPreservesResourcesAndEmitsNoSuccess(
        ushort spellId,
        int statusFlag)
    {
        using var result = await RunClassicAttachStatusCastAsync(
            spellId,
            ClassicAttachStatusTargetKind.Unknown);

        Assert.False(result.MonsterStates.TryGetTimedStatusFlag(
            result.Monster,
            statusFlag,
            result.AfterCast,
            out _));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));

        Assert.Empty(SnapshotFrames(result.CasterSent));
        Assert.Equal("100", result.CasterSession.Character.Attributes["Stamina"]);
        Assert.Equal(0, Assert.Single(result.CasterSession.PlayerSpells.Spells, row => row.Id == spellId).Experience);
        Assert.Contains("classicTargetUnavailable", result.Log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClassicAttachStatusInvisibilitySpellId, ClassicAttachStatusInvisibilityFlag, ClassicAttachStatusInvisibilityExpiresAtUtcAttribute, ClassicAttachStatusInvisibilityRemoveOnDeadAttribute, true)]
    [InlineData(ClassicAttachStatusInvisibilitySpellId, ClassicAttachStatusInvisibilityFlag, ClassicAttachStatusInvisibilityExpiresAtUtcAttribute, ClassicAttachStatusInvisibilityRemoveOnDeadAttribute, false)]
    [InlineData(StarOfAccuracySpellId, LegacyStarOfAccuracyFlag, ClassicAttachStatusAccuracyExpiresAtUtcAttribute, ClassicAttachStatusAccuracyRemoveOnDeadAttribute, true)]
    [InlineData(StarOfAccuracySpellId, LegacyStarOfAccuracyFlag, ClassicAttachStatusAccuracyExpiresAtUtcAttribute, ClassicAttachStatusAccuracyRemoveOnDeadAttribute, false)]
    [InlineData(MagicShieldSpellId, LegacyMagicShieldFlag, ClassicAttachStatusMagicShieldExpiresAtUtcAttribute, ClassicAttachStatusMagicShieldRemoveOnDeadAttribute, true)]
    [InlineData(MagicShieldSpellId, LegacyMagicShieldFlag, ClassicAttachStatusMagicShieldExpiresAtUtcAttribute, ClassicAttachStatusMagicShieldRemoveOnDeadAttribute, false)]
    [InlineData(ClassicAttachStatusStigmaSpellId, ClassicAttachStatusStigmaFlag, ClassicAttachStatusStigmaExpiresAtUtcAttribute, ClassicAttachStatusStigmaRemoveOnDeadAttribute, true)]
    [InlineData(ClassicAttachStatusStigmaSpellId, ClassicAttachStatusStigmaFlag, ClassicAttachStatusStigmaExpiresAtUtcAttribute, ClassicAttachStatusStigmaRemoveOnDeadAttribute, false)]
    public async Task HandlerClassicAttachStatusPlayerAndSelfTargetsPersistSourceTimerMetadata(
        ushort spellId,
        int statusFlag,
        string expiresAtAttribute,
        string removeOnDeadAttribute,
        bool selfTarget)
    {
        using var result = await RunClassicAttachStatusCastAsync(
            spellId,
            selfTarget
                ? ClassicAttachStatusTargetKind.Self
                : ClassicAttachStatusTargetKind.Player);
        var recipient = selfTarget ? result.CasterSession : result.TargetSession;
        var recipientUid = checked((uint)recipient.Character.Uid);

        Assert.True(HasLegacyStatusFlag(recipient.Character.Attributes, statusFlag));
        AssertLegacyXpSkillExpiresAround(
            recipient.Character.Attributes,
            expiresAtAttribute,
            result.BeforeCast,
            result.AfterCast,
            checked((int)ClassicAttachStatusDurationSeconds));
        Assert.Equal(bool.TrueString, recipient.Character.Attributes[removeOnDeadAttribute]);

        var casterFrames = SnapshotFrames(result.CasterSent);
        var casterStatusIndex = AssertClassicAttachStatusVectorFrame(
            casterFrames,
            recipientUid,
            statusFlag,
            expectedSet: true);
        var progressIndex = AssertClassicAttachStatusProgressFrame(casterFrames, spellId);
        var casterAnimationIndex = AssertClassicAttachStatusAnimationFrame(
            casterFrames,
            spellId,
            recipientUid,
            checked((ushort)recipient.Character.X),
            checked((ushort)recipient.Character.Y));

        Assert.True(casterStatusIndex < progressIndex);
        Assert.True(progressIndex < casterAnimationIndex);

        if (!selfTarget)
        {
            var targetFrames = SnapshotFrames(result.TargetSent);
            var targetStatusIndex = AssertClassicAttachStatusVectorFrame(
                targetFrames,
                recipientUid,
                statusFlag,
                expectedSet: true);
            var targetAnimationIndex = AssertClassicAttachStatusAnimationFrame(
                targetFrames,
                spellId,
                recipientUid,
                checked((ushort)recipient.Character.X),
                checked((ushort)recipient.Character.Y));
            Assert.True(targetStatusIndex < targetAnimationIndex);
            Assert.Empty(ParseLegacySpellPackets(targetFrames));
        }
    }

    [Fact]
    public async Task HandlerRequestEntityReplaysActiveClassicMonsterStatusInSpawnVector()
    {
        using var requestLog = new StringWriter();
        using var result = await RunClassicAttachStatusCastAsync(
            ClassicAttachStatusStigmaSpellId,
            ClassicAttachStatusTargetKind.Monster);
        result.CasterSent.Clear();

        await PacketHandlers.HandleAsync(
            result.Context with
            {
                Log = requestLog,
                Session = result.CasterSession,
            },
            ReadFrame(new MsgActionPacket
            {
                ObjId = PlayerUid,
                DwParam = result.Monster.Uid,
                Timestamp = 1_234,
                Type = MsgActionType.RequestEntity,
                PositionX = result.Monster.X,
                PositionY = result.Monster.Y,
            }.ToFrameBytes()));

        var spawn = Assert.Single(ParseClassicAttachStatusMonsterSpawns(result.CasterSent));
        Assert.Equal(result.Monster.Uid, spawn.Uid);
        Assert.Equal(LegacyMonsterSpawnPacket.StatusFlagVectorLength, spawn.StatusFlags.Count);
        Assert.True(IsLegacyStatusFlagSet(spawn.StatusFlags, ClassicAttachStatusStigmaFlag));
    }

    [Fact]
    public async Task RuntimeLoopExpiresClassicMonsterStatusAndSendsClearedStatusVector()
    {
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateClassicAttachStatusCatalog(
            monsterFamily,
            spawn,
            ClassicAttachStatusInvisibilitySpellId);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var now = DateTimeOffset.UtcNow;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: worldData,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(ClassicAttachStatusDurationSeconds + 1))),
            log: log);
        var monsterStates = GetClassicAttachStatusMonsterStates(handler);
        var applied = monsterStates.ApplyTimedStatusFlag(
            monster,
            ClassicAttachStatusInvisibilityFlag,
            TimeSpan.FromSeconds(ClassicAttachStatusDurationSeconds),
            now);
        Assert.True(applied.Applied);

        var session = CreateSession(
            spellIds: [ClassicAttachStatusInvisibilitySpellId],
            mapId: spawn.MapId);
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () => ParseMsgUpdates(sent).Any(update =>
                    update.Uid == monster.Uid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        !IsLegacyStatusFlagSet(
                            entry.Values,
                            ClassicAttachStatusInvisibilityFlag))),
                TimeSpan.FromSeconds(1));

            Assert.False(monsterStates.TryGetTimedStatusFlag(
                monster,
                ClassicAttachStatusInvisibilityFlag,
                now.AddSeconds(ClassicAttachStatusDurationSeconds + 1),
                out _));
            var frames = SnapshotFrames(sent);
            AssertClassicAttachStatusVectorFrame(
                frames,
                monster.Uid,
                ClassicAttachStatusInvisibilityFlag,
                expectedSet: false);
            Assert.DoesNotContain(
                frames,
                bytes => ReadFrame(bytes).Header.Opcode == LegacySpellAnimationPacket.Opcode);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public Task RuntimeLoopExpiresClassicPlayerInvisibilityAndSendsClearedStatusVector()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var rows = new[] { (byte)0, (byte)1 }.Select(level => CreateMagicRow(
            ClassicAttachStatusInvisibilitySpellId, sort: 6, level: level, useMana: 7,
            useStamina: 4, experience: 1_000, range: 18, coolDown: 0, coldTime: 0,
            duration: ClassicAttachStatusDurationSeconds)).ToArray();
        var head = OwnedEquipmentBehaviorFixture.Item(111_003, "ClassicExpiryHead",
            new Dictionary<int, string> { [18] = "5000", [19] = "5000" });
        var worldData = CreateMagicCatalogCore(monsterFamily, [spawn], [], rows, items: [head]);
        var clock = new ManualRuntimeTimeProvider(DateTimeOffset.UtcNow);
        var items = PlayerItemSnapshot.Empty with
        {
            EquipmentItems = new[] { new PlayerItemSnapshotItem
            {
                Uid = PlayerUid + 70_000, ItemId = 111_003, Position = 1,
                Durability = 100, MaximumDurability = 100, StackSize = 1,
            } },
        };
        var declared = CreateSession(spellIds: [ClassicAttachStatusInvisibilitySpellId], mapId: spawn.MapId,
            minHitPoints: 1_000, minMana: 500, stamina: 100, playerItems: items);
        var options = new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromMilliseconds(10), clock);
        return OwnedEquipmentBehaviorFixture.RunWithEnabledSchedulingAsync(
            new(declared.Character, declared.PlayerItems, declared.PlayerSpells, declared.PlayerProficiencies),
            worldData, options, async f =>
        {
            await f.SendAsync(PlayerUid, new MsgActionPacket
                { ObjId = PlayerUid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
            var initial = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(initial.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Player.Alive);
            Assert.False(HasLegacyStatusFlag(initial.Character.Attributes, ClassicAttachStatusInvisibilityFlag));
            f.ActorFrames.Clear();
            var beforeRefusal = f.Data.Files();
            await f.SendAsync(PlayerUid, new MsgAttackPacket(new MsgAttackQuery(PlayerUid,
                ClassicAttachStatusUnknownTargetUid, 832, 584, MsgAttackType.Magic, 0, 0, 0,
                MsgAttackEffect.None, 0, SpellId: ClassicAttachStatusInvisibilitySpellId)).ToFrameBytes());
            Assert.Same(initial, f.Current()); f.Data.AssertFiles(beforeRefusal);
            Assert.Equal(500L, f.Current().Character.MinMana);
            Assert.Equal("100", f.Current().Character.Attributes["Stamina"]); Assert.Empty(f.ActorFrames);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));

            // A genuine cast owns the SET. Admission has already initialized
            // the native cache, so a tick need not resend an unchanged flag.
            var beforeCast = DateTimeOffset.UtcNow;
            await f.SendAsync(PlayerUid, new MsgAttackPacket(new MsgAttackQuery(PlayerUid, PlayerUid,
                checked((ushort)initial.Character.X), checked((ushort)initial.Character.Y), MsgAttackType.Magic,
                0, 0, 0, MsgAttackEffect.None, 0, SpellId: ClassicAttachStatusInvisibilitySpellId)).ToFrameBytes());
            var afterCast = DateTimeOffset.UtcNow;
            var cast = f.Current(); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(HasLegacyStatusFlag(cast.Character.Attributes, ClassicAttachStatusInvisibilityFlag), f.Log.ToString());
            Assert.Equal(493L, cast.Character.MinMana); Assert.Equal("96", cast.Character.Attributes["Stamina"]);
            var expiresAt = DateTimeOffset.Parse(cast.Character.Attributes[ClassicAttachStatusInvisibilityExpiresAtUtcAttribute],
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.InRange(expiresAt, beforeCast.AddSeconds(ClassicAttachStatusDurationSeconds),
                afterCast.AddSeconds(ClassicAttachStatusDurationSeconds));
            Assert.Equal(bool.TrueString, cast.Character.Attributes[ClassicAttachStatusInvisibilityRemoveOnDeadAttribute]);
            var castFrames = SnapshotFrames(f.ActorFrames);
            var set = AssertClassicAttachStatusVectorFrame(castFrames, PlayerUid, ClassicAttachStatusInvisibilityFlag, true);
            var progress = AssertClassicAttachStatusProgressFrame(castFrames, ClassicAttachStatusInvisibilitySpellId);
            var animation = AssertClassicAttachStatusAnimationFrame(castFrames, ClassicAttachStatusInvisibilitySpellId,
                PlayerUid, checked((ushort)initial.Character.X), checked((ushort)initial.Character.Y));
            Assert.True(set < progress && progress < animation);
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 2, TimeSpan.FromSeconds(2));
            clock.Advance(TimeSpan.FromMilliseconds(10));
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 2, TimeSpan.FromSeconds(2));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(HasLegacyStatusFlag(f.Current().Character.Attributes, ClassicAttachStatusInvisibilityFlag));
            Assert.DoesNotContain(ParseMsgUpdates(f.ActorFrames), update => update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, ClassicAttachStatusInvisibilityFlag)));
            Assert.Equal(expiresAt.ToString("O", CultureInfo.InvariantCulture),
                f.Current().Character.Attributes[ClassicAttachStatusInvisibilityExpiresAtUtcAttribute]);
            clock.Advance(expiresAt - clock.GetUtcNow() + TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => ParseMsgUpdates(f.ActorFrames).Any(update => update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, ClassicAttachStatusInvisibilityFlag))), TimeSpan.FromSeconds(2));
            var current = f.Current(); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.False(HasLegacyStatusFlag(current.Character.Attributes, ClassicAttachStatusInvisibilityFlag));
            Assert.DoesNotContain(ClassicAttachStatusInvisibilityExpiresAtUtcAttribute, current.Character.Attributes.Keys);
            Assert.DoesNotContain(ClassicAttachStatusInvisibilityRemoveOnDeadAttribute, current.Character.Attributes.Keys);
            var statuses = ParseMsgUpdates(f.ActorFrames).Where(update => update.Uid == PlayerUid)
                .SelectMany(update => update.Updates).Where(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag).ToArray();
            var setIndex = Array.FindIndex(statuses, entry => entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, ClassicAttachStatusInvisibilityFlag));
            var clearIndex = Array.FindIndex(statuses, entry => entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, ClassicAttachStatusInvisibilityFlag));
            Assert.True(setIndex >= 0 && clearIndex > setIndex);
        });
    }

    [Fact]
    public void PlayerDeathClearsClassicInvisibilityTimerMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var session = CreateSession(
            spellIds: [ClassicAttachStatusInvisibilitySpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(ClassicAttachStatusInvisibilityFlag),
                [ClassicAttachStatusInvisibilityExpiresAtUtcAttribute] =
                    now.AddMinutes(1).ToString("O", CultureInfo.InvariantCulture),
                [ClassicAttachStatusInvisibilityRemoveOnDeadAttribute] = bool.TrueString,
            });

        var death = LegacyPlayerDeathState.ApplyDeath(
            session.Character,
            now,
            checked((ushort)session.Character.X),
            checked((ushort)session.Character.Y));

        Assert.False(LegacyPlayerDeathState.HasStatusFlag(
            death.Character.Attributes,
            ClassicAttachStatusInvisibilityFlag));
        Assert.DoesNotContain(
            ClassicAttachStatusInvisibilityExpiresAtUtcAttribute,
            death.Character.Attributes.Keys);
        Assert.DoesNotContain(
            ClassicAttachStatusInvisibilityRemoveOnDeadAttribute,
            death.Character.Attributes.Keys);
    }

    private static async Task<ClassicAttachStatusCastResult> RunClassicAttachStatusCastAsync(
        ushort spellId,
        ClassicAttachStatusTargetKind targetKind,
        Action<MonsterRuntimeStateStore, LegacyMonsterInstance>? arrangeMonster = null)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateClassicAttachStatusCatalog(monsterFamily, spawn, spellId);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        arrangeMonster?.Invoke(monsterStates, monster);

        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellEntries: [CreatePlayerSpell(spellId)]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            spellIds: []);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        var (opponentUid, x, y) = targetKind switch
        {
            ClassicAttachStatusTargetKind.Monster => (monster.Uid, monster.X, monster.Y),
            ClassicAttachStatusTargetKind.Player =>
                (TargetPlayerUid, checked((ushort)targetSession.Character.X), checked((ushort)targetSession.Character.Y)),
            ClassicAttachStatusTargetKind.Self =>
                (PlayerUid, checked((ushort)casterSession.Character.X), checked((ushort)casterSession.Character.Y)),
            ClassicAttachStatusTargetKind.Unknown =>
                (ClassicAttachStatusUnknownTargetUid, (ushort)832, (ushort)584),
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Unknown classic buff target kind."),
        };

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId,
                x,
                y,
                opponentUid)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        return new ClassicAttachStatusCastResult(
            context,
            updatedCasterSession,
            updatedTargetSession,
            monsterStates,
            monster,
            casterSent,
            targetSent,
            beforeCast,
            afterCast,
            log.ToString(), combatSpellOwner);
    }

    private static WorldDataCatalog CreateClassicAttachStatusCatalog(
        Monster monsterFamily,
        MonsterSpawn spawn,
        ushort spellId)
    {
        var rows = Enumerable.Range(0, 2)
            .Select(level => CreateMagicRow(
                spellId,
                sort: 6,
                level: checked((byte)level),
                useMana: 0,
                experience: 1_000,
                range: 18,
                useStamina: 0,
                coolDown: 0,
                coldTime: 0,
                duration: ClassicAttachStatusDurationSeconds))
            .ToArray();
        return CreateMagicCatalog(monsterFamily, [spawn], rows);
    }

    private static int AssertClassicAttachStatusVectorFrame(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int statusFlag,
        bool expectedSet)
    {
        var index = SingleFrameIndex(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == uid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        var frame = ReadFrame(frames[index]);
        Assert.Equal(2_176, frame.Header.Opcode);
        Assert.Equal(LegacyMsgUpdatePacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var statusUpdate));
        var entry = Assert.Single(statusUpdate.Updates);
        Assert.Equal(0x19U, (uint)entry.Type);
        Assert.NotNull(entry.Values);
        Assert.Equal(LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength, entry.Values!.Count);
        Assert.Equal(expectedSet, IsLegacyStatusFlagSet(entry.Values, statusFlag));
        return index;
    }

    private static int AssertClassicAttachStatusProgressFrame(
        IReadOnlyList<byte[]> frames,
        ushort spellId)
    {
        var index = SingleFrameIndex(
            frames,
            frame => LegacySpellPacket.TryParse(frame, out var spell) && spell.Id == spellId);
        Assert.True(LegacySpellPacket.TryParse(ReadFrame(frames[index]), out var progress));
        Assert.Equal(spellId, progress.Id);
        Assert.Equal(0, progress.Level);
        Assert.Equal(checked((int)ClassicAttachStatusDurationSeconds), progress.Experience);
        return index;
    }

    private static int AssertClassicAttachStatusAnimationFrame(
        IReadOnlyList<byte[]> frames,
        ushort spellId,
        uint? expectedTargetUid,
        ushort expectedX,
        ushort expectedY)
    {
        var index = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == spellId);
        var frame = ReadFrame(frames[index]);
        Assert.Equal(2_239, frame.Header.Opcode);
        Assert.Equal(LegacySpellAnimationPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var animation));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(expectedX, animation.X);
        Assert.Equal(expectedY, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);

        if (expectedTargetUid is not { } targetUid)
        {
            Assert.Empty(animation.Targets);
            return index;
        }

        var target = Assert.Single(animation.Targets);
        Assert.Equal(targetUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        return index;
    }

    private static IReadOnlyList<LegacyMonsterSpawnPacket> ParseClassicAttachStatusMonsterSpawns(
        IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMonsterSpawnPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static MonsterRuntimeStateStore GetClassicAttachStatusMonsterStates(
        WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_monsterStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<MonsterRuntimeStateStore>(field.GetValue(handler));
    }

    private enum ClassicAttachStatusTargetKind
    {
        Monster,
        Player,
        Self,
        Unknown,
    }

    private sealed record ClassicAttachStatusCastResult(
        WorldPacketHandlerContext Context,
        WorldSession CasterSession,
        WorldSession TargetSession,
        MonsterRuntimeStateStore MonsterStates,
        LegacyMonsterInstance Monster,
        List<byte[]> CasterSent,
        List<byte[]> TargetSent,
        DateTimeOffset BeforeCast,
        DateTimeOffset AfterCast,
        string Log, LegacyCombatSpellTestOwner SpellOwner) : IDisposable
    {
        public void Dispose() => SpellOwner.Dispose();
    }
}
