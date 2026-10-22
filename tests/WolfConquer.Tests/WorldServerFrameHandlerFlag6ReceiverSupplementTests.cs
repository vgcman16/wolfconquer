using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFlag6SupplementMagicPrayRetainsReceiverAcrossFirstMaximumSend(bool originalVisible)
    {
        var model = new Flag6PublicReceiverSupplementModel(originalVisible);
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        Assert.True(File.Exists(Path.Combine(seedPath, "manifest.json")),
            "Required source-backed magic world seed is absent; this receiver test must not silently return.");
        var dbRoot = Path.Combine(_usersRoot, "flag6-pray-model");
        var usersRoot = Path.Combine(dbRoot, "Users");
        var spellsRoot = Path.Combine(dbRoot, "PlayersSpells");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(spellsRoot);
        try
        {
            WriteSnapshot(usersRoot, 1_293_600, uid: 1_293_600, mapId: 1015, x: 830, y: 584);
            WriteSnapshot(usersRoot, 1_293_601, uid: 1_293_601, mapId: 1015, x: 832, y: 584,
                name: "Flag6PrayTarget", minHitPoints: 0, statusFlags: BuildStatusFlags(5, 10),
                extraCharacterLines: "XPCount=88\r\nStamina=12\r\n");
            File.WriteAllBytes(Path.Combine(spellsRoot, "1293600.bin"),
                PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([1100]));
            var worldData = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var casterTicket = handoff.Issue(1_293_600, "flag6-pray-caster", ConquerRuntimeDefaults.WorldServer);
            var targetTicket = handoff.Issue(1_293_601, "flag6-pray-target", ConquerRuntimeDefaults.WorldServer);
            using var log = new RuntimeThreadSafeStringWriter();
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff, worldData, log);
            var casterFrames = new RuntimeThreadSafeFrameCapture();
            var targetFrames = new RuntimeThreadSafeFrameCapture();
            var casterClient = new LegacyNativeGameClientRuntime();
            var caster = new ConquerTcpConnection(null, null,
                sendAsync: (memory, _) => { casterFrames.Add(memory.ToArray()); return ValueTask.CompletedTask; },
                nativeGameClient: casterClient);
            var armed = false;
            var matchingSends = 0;
            ConquerTcpConnection target = null!;
            target = new ConquerTcpConnection(null, null,
                sendAsync: async (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    targetFrames.Add(bytes);
                    if (armed && Flag6PublicReceiverSupplementModel.FirstMaximum(bytes, 1_293_601))
                    {
                        try
                        {
                            Assert.Equal(1, Interlocked.Increment(ref matchingSends));
                            await model.SwapAsync(target, () => GetSession(handler, target));
                        }
                        catch (Exception error) { model.CaptureCallbackFailure(error); throw; }
                    }
                }, nativeGameClient: model.Client);
            await handler.HandleFrameAsync(caster,
                ReadFrame(new InternalWorldEnterRequestPacket(casterTicket.Token).ToFrameBytes()));
            await handler.HandleFrameAsync(target,
                ReadFrame(new InternalWorldEnterRequestPacket(targetTicket.Token).ToFrameBytes()));
            Assert.Equal(0, GetSession(handler, target).Character.MinHitPoints);
            Assert.Same(model.Original, GetSession(handler, target).NativePlayer);
            var targetStart = targetFrames.Count;
            var casterStart = casterFrames.Count;
            armed = true;

            await handler.HandleFrameAsync(caster, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: 1_293_600, OpponentUid: 1_293_601, X: 830, Y: 584,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 1100)).ToFrameBytes()));
            await model.Swapped.WaitAsync(TimeSpan.FromSeconds(2));
            armed = false;

            var frames = targetFrames.ToArray().Skip(targetStart).ToArray();
            var maximum = Assert.Single(ParseFlag6SupplementUpdates(frames).Where(packet =>
                packet.Uid == 1_293_601 && packet.Updates.Count == 1 &&
                packet.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints));
            var max = Assert.Single(maximum.Updates).Value;
            Assert.True(max > 0);
            var phase = Array.FindIndex(frames, bytes => Flag6PublicReceiverSupplementModel.FirstMaximum(bytes, 1_293_601));
            Assert.Equal(0, phase); // First target publication inside ApplyLegacyReviveToPlayerAsync.
            AssertFlag6SupplementHpPair(frames, phase, 1_293_601, max);
            AssertSingleUpdate(frames[phase + 2], 1_293_601, LegacyMsgUpdateDataType.XPCircle, 0);
            AssertSingleUpdate(frames[phase + 3], 1_293_601, LegacyMsgUpdateDataType.Stamina, 100);
            model.AssertVector(frames[phase + 4], 1_293_601, dead: false);
            AssertSingleUpdate(frames[phase + 5], 1_293_601, LegacyMsgUpdateDataType.Mesh, 1_541_008);
            Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(frames[phase + 6]), out var map));
            Assert.Equal((uint)1015, map.Id);
            var spell = Assert.Single(frames.Select(ReadFrame).Select(frame =>
                LegacySpellAnimationPacket.TryParse(frame, out var packet) ? packet : null)
                .OfType<LegacySpellAnimationPacket>().Where(packet => packet.SpellId == 1100));
            Assert.Equal(1_293_600U, spell.AttackerUid);
            var revivedTarget = Assert.Single(spell.Targets);
            Assert.Equal(1_293_601U, revivedTarget.Uid);
            Assert.Equal(0U, revivedTarget.Damage);
            Assert.True(revivedTarget.Hit);
            Assert.True(Array.FindIndex(frames, bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes),
                out var packet) && packet.SpellId == 1100) > phase + 6);
            Assert.Contains(casterFrames.ToArray().Skip(casterStart), bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 1100 &&
                packet.AttackerUid == 1_293_600 && packet.Targets.Count == 1 && packet.Targets[0].Uid == 1_293_601);
            // Receiver subsequence only. Peer publication has separate OUTCOME-NEUTRAL coverage;
            // the missing-spawn diagnosis was withdrawn; no full native wire parity is inferred.
            Assert.Equal(1, matchingSends);
            var revived = GetSession(handler, target);
            Assert.Equal((long)max, revived.Character.MinHitPoints);
            Assert.Equal("0", revived.Character.Attributes["XPCount"]);
            Assert.Equal("100", revived.Character.Attributes["Stamina"]);
            Assert.Contains("WorldServer handled legacy revive magic", log.ToString());
            model.AssertRetained(revived);
            GC.KeepAlive(new[] { caster, target });
        }
        finally
        {
            if (Directory.Exists(dbRoot)) Directory.Delete(dbRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFlag6SupplementGuardReviverRetainsReceiverAcrossFirstMaximumSend(bool originalVisible)
    {
        var model = new Flag6PublicReceiverSupplementModel(originalVisible);
        var deathAt = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600, uid: 1_293_600, mapId: 1015, x: 197, y: 230,
            minHitPoints: 0, statusFlags: BuildStatusFlags(5, 10),
            extraCharacterLines: $"DeadStamp={deathAt:O}\r\nGhostStamp={deathAt.AddSeconds(1):O}\r\nDead_X=197\r\nDead_Y=230");
        var family = new Monster(10000, "Reviver", new Dictionary<string, string>
        {
            ["life"] = "70000", ["lookface"] = "10000", ["level"] = "0200",
            ["attack_range"] = "0015", ["view_range"] = "0015",
            ["attack_speed"] = "1", ["move_speed"] = "1",
        }, []);
        var spawn = new MonsterSpawn(GeneratorId: 110, MapId: 1015, MonsterTypeId: 10000,
            BoundX: 199, BoundY: 230, BoundWidth: 1, BoundHeight: 1, MaxNpc: 1,
            RestSeconds: 10, MaxPerGenerator: 1, TimerBegin: 0, TimerEnd: 0, BornX: 0, BornY: 0,
            SourceMapFolderId: 1015, SourceMonsterName: "Reviver",
            SourceRelativePath: Path.Combine("1015", "Reviver", "1.ini"),
            Attributes: new Dictionary<string, string> { ["id"] = "0110", ["mapid"] = "1015", ["npctype"] = "10000" });
        var world = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(monsters: [family], monsterSpawns: [spawn]);
        var instance = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family,
            world.GetLegacyMonsterRuntimeUidBase(spawn)));
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "flag6-guard-target", ConquerRuntimeDefaults.WorldServer);
        using var log = new RuntimeThreadSafeStringWriter();
        using var cancel = new CancellationTokenSource();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot,
            playerItemsRoot: null, playerSpellsRoot: null, playerProficienciesRoot: null,
            playersCombatGearRoot: null, playerQuestsRoot: null), handoff, world,
            new LegacyRuntimeUpdateLoopOptions(Enabled: true, TimeSpan.FromMilliseconds(20),
                new AnchoredRuntimeTimeProvider(deathAt.AddSeconds(2))), log);
        var captureGate = new object();
        var sent = new List<byte[]>();
        var callbackErrors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var publication = new TaskCompletionSource<byte[][]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = true; // Enter itself starts the enabled producer; no post-Enter discard window.
        var startupPrestateObserved = 0;
        var reviveFrameIndex = -1;
        var matchingSends = 0;
        var terminalAnimations = 0;
        ConquerTcpConnection connection = null!;
        connection = new ConquerTcpConnection(null, null,
            sendAsync: async (memory, _) =>
            {
                try
                {
                    var bytes = memory.ToArray();
                    var terminalAnimation = armed &&
                        LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                        animation.AttackerUid == instance.Uid && animation.SpellId == 1100;
                    int frameIndex;
                    byte[][]? terminalSnapshot = null;
                    lock (captureGate)
                    {
                        sent.Add(bytes);
                        frameIndex = sent.Count - 1;
                        if (terminalAnimation) terminalSnapshot = sent.ToArray();
                    }
                    if (armed && Flag6PublicReceiverSupplementModel.FirstMaximum(bytes, 1_293_600))
                    {
                        var current = GetSession(handler, connection);
                        if (current.Character.MinHitPoints <= 0)
                        {
                            // Startup equipment-query HP frames precede loop start; they are NOT Reviver.
                            Assert.Equal(0, GetSession(handler, connection).Character.MinHitPoints);
                            Assert.Same(model.Original, GetSession(handler, connection).NativePlayer);
                            Interlocked.Exchange(ref startupPrestateObserved, 1);
                            return;
                        }
                        // ApplyLegacyReviverReviveAsync commits positive HP BEFORE its first owner send.
                        Assert.Equal(1, Volatile.Read(ref startupPrestateObserved));
                        Assert.Equal(1, Interlocked.Increment(ref matchingSends));
                        Assert.Equal(-1, Interlocked.CompareExchange(ref reviveFrameIndex, frameIndex, -1));
                        await model.SwapAsync(connection, () => GetSession(handler, connection));
                    }
                    if (terminalAnimation)
                    {
                        Assert.Equal(1, Interlocked.Increment(ref terminalAnimations));
                        Assert.Equal(1, Volatile.Read(ref matchingSends));
                        Assert.True(model.Swapped.IsCompletedSuccessfully);
                        publication.TrySetResult(terminalSnapshot!);
                    }
                }
                catch (Exception error)
                {
                    callbackErrors.Enqueue(error);
                    model.CaptureCallbackFailure(error);
                    publication.TrySetException(error); // Includes parsing before/after the swap.
                    throw;
                }
            }, nativeGameClient: model.Client);
        try
        {
            await handler.HandleFrameAsync(connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()), cancel.Token);
            Assert.Equal(1, Volatile.Read(ref startupPrestateObserved));
            var frames = await publication.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await model.Swapped.WaitAsync(TimeSpan.FromSeconds(2));
            // The terminal callback closes a wire window, NOT the complete Reviver operation.
            await WaitUntilAsync(() =>
            {
                if (callbackErrors.TryPeek(out var error))
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
                return log.ToString().Contains("WorldServer sent legacy Reviver revive");
            }, TimeSpan.FromSeconds(2));
            Assert.Empty(callbackErrors);
            Assert.Equal(1, Volatile.Read(ref terminalAnimations));
            // Enter+enabled loop is the actual producer ingress. Keep public SetLocation AFTER
            // completion so its legitimate responses cannot interleave the selected Reviver window.
            armed = false;
            await handler.HandleFrameAsync(connection, ReadFrame(new MsgActionPacket
                { ObjId = 1_293_600, Timestamp = 1234, Type = MsgActionType.SetLocation }.ToFrameBytes()), cancel.Token);
            await cancel.CancelAsync();
            Assert.Empty(callbackErrors); // Includes callbacks during post-completion SetLocation/cancel.

            var phase = Volatile.Read(ref reviveFrameIndex);
            Assert.True(phase >= 0);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[phase]), out var maximum));
            var max = Assert.Single(maximum.Updates).Value;
            Assert.True(max > 0);
            AssertFlag6SupplementHpPair(frames, phase, 1_293_600, max);
            AssertSingleUpdate(frames[phase + 2], 1_293_600, LegacyMsgUpdateDataType.Stamina, 100);
            model.AssertVector(frames[phase + 3], 1_293_600, dead: false);
            AssertSingleUpdate(frames[phase + 4], 1_293_600, LegacyMsgUpdateDataType.Mesh, 1_541_008);
            Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(frames[phase + 5]), out var map));
            Assert.Equal(new LegacyMapStatusPacket(1015, 1015, Status: 0), map);
            var spellIndex = Array.FindIndex(frames, phase + 6, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.AttackerUid == instance.Uid && packet.SpellId == 1100);
            Assert.True(spellIndex >= phase + 6);
            Assert.Equal(frames.Length - 1, spellIndex); // Atomic prefix closed AT this NPC animation.
            Assert.True(LegacySpellAnimationPacket.TryParse(ReadFrame(frames[spellIndex]), out var spell));
            Assert.Equal(instance.Uid, spell.AttackerUid);
            Assert.Equal((ushort)1100, spell.SpellId);
            Assert.Equal(199, spell.X);
            Assert.Equal(230, spell.Y);
            var revivedTarget = Assert.Single(spell.Targets);
            Assert.Equal(1_293_600U, revivedTarget.Uid);
            Assert.Equal(0U, revivedTarget.Damage);
            Assert.Equal(MsgAttackEffect.None, revivedTarget.Effect);
            Assert.Single(ParseFlag6SupplementUpdates(frames.Skip(phase).Take(6)).Where(packet =>
                packet.Uid == 1_293_600 && packet.Updates.Count == 1 &&
                packet.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag));
            Assert.DoesNotContain(frames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.Uid == instance.Uid && packet.Query.AttackType == MsgAttackType.Physical);
            Assert.Equal(1, matchingSends);
            var revived = GetSession(handler, connection);
            Assert.Equal((long)max, revived.Character.MinHitPoints);
            model.AssertRetained(revived);
            GC.KeepAlive(connection);
        }
        finally
        {
            armed = false;
            await cancel.CancelAsync();
            GC.KeepAlive(new object[] { handler, connection, model });
        }
    }

    private static LegacyMsgUpdatePacket[] ParseFlag6SupplementUpdates(IEnumerable<byte[]> frames) =>
        frames.Select(ReadFrame).Select(frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) ? packet : null)
            .OfType<LegacyMsgUpdatePacket>().ToArray();

    private static void AssertFlag6SupplementHpPair(byte[][] frames, int phase, uint uid, ulong maximum)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[phase]), out var max));
        Assert.Equal(uid, max.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maximum)], max.Updates);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[phase + 1]), out var hp));
        Assert.Equal(uid, hp.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maximum),
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, maximum)], hp.Updates);
    }
}
