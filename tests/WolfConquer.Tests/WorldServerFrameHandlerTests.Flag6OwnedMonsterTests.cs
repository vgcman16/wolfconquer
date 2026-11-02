using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NativeFlag6PublicMonsterLoopRetainsReceiverAcrossActivePick(bool originalVisible) =>
        RunOwnedMonsterReceiverAsync(activePick: true, replacePlayer: true, originalVisible);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NativeFlag6PublicMonsterLoopDeathRetainsReceiverAcrossDeadTalk(bool originalVisible) =>
        RunOwnedMonsterReceiverAsync(activePick: false, replacePlayer: true, originalVisible);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task OwnedMonsterDeathCompletesWithCurrentPhysicalReceiver(bool activePick, bool originalVisible) =>
        RunOwnedMonsterReceiverAsync(activePick, replacePlayer: false, originalVisible);

    private static async Task RunOwnedMonsterReceiverAsync(bool activePick, bool replacePlayer, bool originalVisible)
    {
        const uint uid = OwnedEquipmentBehaviorFixture.Actor;
        var family = new Monster(910, "Pheasant", new Dictionary<string, string>
        {
            ["id"] = "910", ["life"] = "5000", ["lookface"] = "910", ["level"] = "1", ["Boss"] = "1",
            ["attack_range"] = "15", ["view_range"] = "15", ["attack_speed"] = "1", ["move_speed"] = "1",
            ["attack_min"] = "2000", ["attack_max"] = "2000",
        }, []);
        var spawn = new MonsterSpawn(910, 1011, 910, 197, 230, 1, 1, 1, 10, 1, 0, 0, 0, 0,
            1011, "Pheasant", "fixture://owned-flag6/1.ini", new Dictionary<string, string>
            { ["id"] = "910", ["mapid"] = "1011", ["npctype"] = "910" });
        var catalog = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(monsters: new[] { family }, monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, catalog.GetLegacyMonsterRuntimeUidBase(spawn)));
        var actor = OwnedEquipmentBehaviorFixture.Character(uid, mapId: 1011, x: 197, y: 230, minHitPoints: 1) with
        {
            Attributes = activePick ? new Dictionary<string, string> { ["ActivePick"] = "1" } : new Dictionary<string, string>(),
        };
        var interval = TimeSpan.FromMilliseconds(100);
        var clock = new BossRuntimeManualTimeProvider(DateTimeOffset.UtcNow.AddSeconds(20));
        await OwnedEquipmentBehaviorFixture.RunWithEnabledSchedulingAsync(new(actor, PlayerItemSnapshot.Empty), catalog,
            CreateDeterministicMonsterRuntimeLoopOptions(interval, clock), async owner =>
        {
            var client = owner.Connection(uid).NativeGameClient!;
            var original = client.Player;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            var replacement = new LegacyNativePlayerRuntime(client);
            if (originalVisible) Assert.True(NativeFlag6TestModel.AddFlagFixture(original, Flag6PublicReceiverSupplementModel.Origin));
            else Assert.True(NativeFlag6TestModel.AddFlagFixture(replacement, Flag6PublicReceiverSupplementModel.Origin));
            var originalFlag = NativeFlag6TestModel.Current(original);
            var replacementFlag = NativeFlag6TestModel.Current(replacement);
            var capture = new RuntimeThreadSafeFrameCapture();
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            WorldSession? atBoundary = null;
            Dictionary<string, byte[]>? files = null;
            var boundaryCount = 0;
            var boundaryFrameCount = 0;
            var peer = owner.WirePeer(uid);
            var fillerStarted = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            var boundaryRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var armed = 0;
            if (replacePlayer)
            {
                // Arm the actual TCP gate on receipt of the attack, before the
                // server's receipt wait resumes. The next source boundary is
                // then held behind a real backpressured write.
                owner.WireFrameRecorded = (_, bytes) =>
                {
                    if (!IsFlag6MonsterInteraction(bytes, monster.Uid, uid, MsgAttackType.Physical) ||
                        Interlocked.CompareExchange(ref armed, 1, 0) != 0) return;
                    fillerStarted.TrySetResult(peer.StartBackpressuredWrite());
                };
                peer.OrdinaryWriteRequested = memory =>
                {
                    var frame = ReadFrame(memory.ToArray());
                    var boundary = activePick
                        ? MsgActionPacket.TryParse(frame, out var action) && action.ObjId == uid && action.Type == MsgActionType.ActionPick
                        : LegacyMsgMessagePacket.TryParse(frame, out var talk) && talk.Content.Contains(LegacyPlayerDeathState.DeadTalkText);
                    if (boundary) boundaryRequested.TrySetResult();
                };
            }
            owner.BeforeSendAsync = (_, bytes) =>
            {
                capture.Add(bytes);
                var frame = ReadFrame(bytes);
                var boundary = activePick
                    ? MsgActionPacket.TryParse(frame, out var action) && action.ObjId == uid && action.Type == MsgActionType.ActionPick
                    : LegacyMsgMessagePacket.TryParse(frame, out var talk) && talk.Content.Contains(LegacyPlayerDeathState.DeadTalkText);
                if (boundary)
                {
                    Assert.Equal(1, Interlocked.Increment(ref boundaryCount));
                    if (!replacePlayer) Assert.Same(original, client.Player);
                    boundaryFrameCount = capture.Count;
                    reached.TrySetResult();
                }
                return ValueTask.CompletedTask;
            };
            try
            {
                await owner.SendAsync(uid, new MsgActionPacket { ObjId = uid, Timestamp = 1234, Type = MsgActionType.SetLocation }.ToFrameBytes());
                await WaitForMonsterRuntimeTimerRegistrationAsync(clock);
                Assert.True(GetMonsterStates(owner.Handler).TryGetVisibleInstance(monster, clock.GetUtcNow(), out var visible));
                Assert.True(owner.Store.TryReadMonsterPhysicalRange(visible, family, out var birthRange));
                Assert.Equal(3, birthRange);
                Assert.DoesNotContain(capture, bytes => IsFlag6MonsterInteraction(bytes, monster.Uid, uid, MsgAttackType.Physical));
                clock.Advance(interval);
                if (replacePlayer)
                {
                    var filler = await fillerStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
                    await peer.WaitForBackpressure();
                    await boundaryRequested.Task.WaitAsync(TimeSpan.FromSeconds(3));
                    Assert.True(peer.ActualBackpressure);
                    Assert.Same(original, client.Player);
                    atBoundary = owner.Current(uid);
                    files = owner.Data.Files();
                    client.Player = replacement;
                    Assert.False(owner.Store.IsBoundSpellOwnerCurrent(binding));
                    await peer.DrainBackpressuredWrite();
                    await filler.WaitAsync(TimeSpan.FromSeconds(5));
                }
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await WaitUntilAsync(() => owner.Log.ToString().Contains("WorldServer sent proactive monster attacks", StringComparison.Ordinal), TimeSpan.FromSeconds(3));
                await owner.DrainWireObservationsAsync();
                var frames = capture.ToArray();
                Assert.Same(originalFlag, NativeFlag6TestModel.Current(original));
                Assert.Same(replacementFlag, NativeFlag6TestModel.Current(replacement));
                Assert.Equal(originalVisible, NativeFlag6TestModel.Visible(original));
                Assert.Equal(!originalVisible, NativeFlag6TestModel.Visible(replacement));
                Assert.Single(frames, bytes => IsFlag6MonsterInteraction(bytes, monster.Uid, uid, MsgAttackType.Physical));
                Assert.Equal(1, boundaryCount);
                var attackIndex = FindFrameIndexAfter(frames, 0, frame =>
                    MsgAttackPacket.TryParse(frame, out var attack) && attack.Query.Uid == monster.Uid &&
                    attack.Query.OpponentUid == uid && attack.Query.AttackType == MsgAttackType.Physical);
                var boundaryIndex = FindFrameIndexAfter(frames, attackIndex + 1, frame => activePick
                    ? MsgActionPacket.TryParse(frame, out var pick) && pick.ObjId == uid && pick.Type == MsgActionType.ActionPick
                    : LegacyMsgMessagePacket.TryParse(frame, out var talk) && talk.Content.Contains(LegacyPlayerDeathState.DeadTalkText));
                Assert.True(attackIndex >= 0 && boundaryIndex > attackIndex);
                if (replacePlayer)
                {
                    Assert.Equal(boundaryFrameCount, frames.Length);
                    Assert.Same(atBoundary, owner.Current(uid));
                    owner.Data.AssertFiles(Assert.IsType<Dictionary<string, byte[]>>(files));
                    Assert.DoesNotContain(frames, bytes => IsFlag6MonsterInteraction(bytes, monster.Uid, uid, MsgAttackType.Death));
                    Assert.Equal(1, owner.Current(uid).Character.MinHitPoints);
                    Assert.Equal(1, original.HitPoints);
                    Assert.False(owner.Current(uid).Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
                }
                else
                {
                    Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
                    Assert.Single(frames, bytes => IsFlag6MonsterInteraction(bytes, monster.Uid, uid, MsgAttackType.Death));
                    var talkIndex = FindFrameIndexAfter(frames, attackIndex + 1, frame =>
                        LegacyMsgMessagePacket.TryParse(frame, out var talk) && talk.Content.Contains(LegacyPlayerDeathState.DeadTalkText));
                    var deathIndex = FindFrameIndexAfter(frames, talkIndex + 1, frame =>
                        MsgAttackPacket.TryParse(frame, out var death) && death.Query.Uid == monster.Uid &&
                        death.Query.OpponentUid == uid && death.Query.AttackType == MsgAttackType.Death);
                    Assert.True(talkIndex > attackIndex && deathIndex > talkIndex);
                    if (activePick) Assert.True(boundaryIndex < talkIndex);
                    Assert.Equal(0, owner.Current(uid).Character.MinHitPoints);
                }
            }
            finally
            {
                owner.BeforeSendAsync = null;
                owner.WireFrameRecorded = null;
                peer.OrdinaryWriteRequested = null;
                peer.ReleaseBackpressuredWrite();
                client.Player = original;
            }
        });
    }
}
