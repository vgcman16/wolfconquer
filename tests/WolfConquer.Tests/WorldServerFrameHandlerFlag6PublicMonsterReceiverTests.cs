using System.Collections.Concurrent;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFlag6PublicMonsterLoopDeathRetainsReceiverAcrossDeadTalk(bool originalVisible)
    {
        const uint uid = 1_293_600;
        const ushort x = 197;
        const ushort y = 230;
        var model = new Flag6PublicReceiverSupplementModel(originalVisible);
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(uid, uid: uid, mapId: 1011, x: x, y: y, minHitPoints: 1);
        // Declared MODEL monster data selects the legitimate generic PHYSICAL BOSS branch.
        // Life5000 excludes extra-boss copy rules; family910 has no special spell table.
        // Boss disables dodge; its REAL random minimum draw is NOT forced or seeded.
        var family = new Monster(910, "Pheasant", new Dictionary<string, string>
        {
            ["life"] = "5000", ["lookface"] = "0910", ["level"] = "0001", ["Boss"] = "1",
            ["attack_range"] = "0015", ["view_range"] = "0015",
            ["attack_speed"] = "1", ["move_speed"] = "1",
            ["attack_min"] = "2000", ["attack_max"] = "2000",
        }, []);
        var spawn = new MonsterSpawn(GeneratorId: 910, MapId: 1011, MonsterTypeId: 910,
            BoundX: x, BoundY: y, BoundWidth: 1, BoundHeight: 1, MaxNpc: 1,
            RestSeconds: 10, MaxPerGenerator: 1, TimerBegin: 0, TimerEnd: 0, BornX: 0, BornY: 0,
            SourceMapFolderId: 1011, SourceMonsterName: "Pheasant",
            SourceRelativePath: Path.Combine("1011", "Pheasant", "1.ini"),
            Attributes: new Dictionary<string, string>
                { ["id"] = "0910", ["mapid"] = "1011", ["npctype"] = "0910" });
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(monsters: [family], monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        Assert.True(monster.Boss);
        Assert.Equal(5000U, monster.MaxLife);
        Assert.False(LegacyMonsterFamilyRuntimeRules.UsesExtraBossCopyRules(family));
        Assert.False(LegacyMonsterFamilyRuntimeRules.IsExcludedFromGenericAggro(family));
        Assert.Equal((uint)910, monster.MonsterId);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(uid, "flag6-public-monster-model", ConquerRuntimeDefaults.WorldServer);
        using var log = new RuntimeThreadSafeStringWriter();
        using var cancel = new CancellationTokenSource();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot,
            playerItemsRoot: null, playerSpellsRoot: null, playerProficienciesRoot: null,
            playersCombatGearRoot: null, playerQuestsRoot: null), handoff, worldData,
            new LegacyRuntimeUpdateLoopOptions(Enabled: true, TimeSpan.FromMilliseconds(20),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(20))),
            log, clientCompatibility: LegacyClientCompatibilityOptions.DecompiledSourceCompatible);
        var captureGate = new object();
        var allFrames = new List<byte[]>();
        var callbackErrors = new ConcurrentQueue<Exception>();
        var finished = new TaskCompletionSource<Flag6MonsterDeathWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
        var talkCount = 0;
        var deathCount = 0;
        ConquerTcpConnection connection = null!;
        connection = new ConquerTcpConnection(null, null,
            sendAsync: async (memory, _) =>
            {
                try
                {
                    var bytes = memory.ToArray();
                    var frame = ReadFrame(bytes);
                    var isTerminalDeath = IsFlag6MonsterInteraction(bytes, monster.Uid, uid, MsgAttackType.Death);
                    byte[][]? terminalSnapshot = null;
                    lock (captureGate)
                    {
                        allFrames.Add(bytes);
                        if (isTerminalDeath) terminalSnapshot = allFrames.ToArray();
                    }
                    if (LegacyMsgMessagePacket.TryParse(frame, out var message) &&
                        message.Content.Contains(LegacyPlayerDeathState.DeadTalkText))
                    {
                        Assert.Equal(1, Interlocked.Increment(ref talkCount)); // Never ignore a second trigger.
                        byte[][] beforeSwap;
                        lock (captureGate) beforeSwap = allFrames.ToArray();
                        var incoming = Assert.Single(beforeSwap.Where(candidate =>
                            IsFlag6MonsterInteraction(candidate, monster.Uid, uid, MsgAttackType.Physical)));
                        Assert.True(MsgAttackPacket.TryParse(ReadFrame(incoming), out var attack));
                        Assert.True(attack.Query.Damage >= 1); // Captured positive/lethal value, NOT an exact RNG forecast.
                        var current = GetSession(handler, connection);
                        Assert.Equal(1, current.Character.MinHitPoints);
                        Assert.Same(model.Original, current.NativePlayer);
                        Assert.False(current.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
                        // Incoming2135 has ALREADY completed. Dead captured its instance before this awaited talk.
                        // No earlier2135 swap, Store.Set, UID recapture, private call or manual timer/target-lock injection.
                        await model.SwapAsync(connection, () => GetSession(handler, connection));
                    }
                    if (isTerminalDeath)
                    {
                        Assert.Equal(1, Interlocked.Increment(ref deathCount));
                        Assert.Equal(1, Volatile.Read(ref talkCount));
                        Assert.True(model.Swapped.IsCompletedSuccessfully);
                        var terminal = GetSession(handler, connection);
                        Assert.Same(model.Replacement, terminal.NativePlayer);
                        finished.TrySetResult(new Flag6MonsterDeathWindow(terminalSnapshot!, terminal,
                            terminal.Character.MinHitPoints,
                            terminal.Character.Attributes[LegacyPlayerDeathState.DeadKillerUidAttribute],
                            LegacyPlayerDeathState.CreateStatusFlagVector(terminal.Character.Attributes, 18)));
                    }
                }
                catch (Exception error)
                {
                    callbackErrors.Enqueue(error);
                    model.CaptureCallbackFailure(error);
                    finished.TrySetException(error); // Production loop may catch; test MUST surface callback errors.
                    throw;
                }
            }, nativeGameClient: model.Client);
        try
        {
            // Enter starts the production loop. Capture is armed BEFORE Enter, not after SetLocation:
            // legitimate startup/SetLocation interleavings cannot hide or discard the first attack/death.
            await handler.HandleFrameAsync(connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()), cancel.Token);
            var window = await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await model.Swapped.WaitAsync(TimeSpan.FromSeconds(3));
            // The snapshot is only a wire-window witness. These are LATER source-completion witnesses:
            // retaliation completed receive/death/counterpunch, then the global attack pass returned.
            await WaitUntilAsync(() => log.ToString().Contains("WorldServer sent legacy monster player death",
                    StringComparison.Ordinal) &&
                log.ToString().Contains("WorldServer sent proactive monster attacks", StringComparison.Ordinal),
                TimeSpan.FromSeconds(1));
            // Enter/loop is the producer; keep the real public action outside the closed death window.
            await handler.HandleFrameAsync(connection, ReadFrame(new MsgActionPacket
                { ObjId = uid, Timestamp = 1234, Type = MsgActionType.SetLocation }.ToFrameBytes()), cancel.Token);
            await cancel.CancelAsync();
            Assert.Empty(callbackErrors);
            Assert.DoesNotContain("PARITY-BUG world runtime loop exception", log.ToString());
            Assert.Equal(1, Volatile.Read(ref talkCount));
            Assert.Equal(1, Volatile.Read(ref deathCount));
            Assert.Equal(1, model.SwapCount);
            Assert.Equal(0, window.HitpointsAtTerminal);
            Assert.Equal(monster.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture), window.KillerAtTerminal);
            var endCompatibility = new ulong[18];
            endCompatibility[0] = 1UL << 5;
            Assert.Equal(endCompatibility, window.CompatibilityAtTerminal); // End witness is before future Ghost phase.
            model.AssertRetained(window.SessionAtTerminal);
            Assert.Same(model.Client, connection.NativeGameClient);
            Assert.Same(model.Replacement, GetSession(handler, connection).NativePlayer);

            var frames = window.Frames;
            var incomingIndex = Assert.Single(Enumerable.Range(0, frames.Length).Where(index =>
                IsFlag6MonsterInteraction(frames[index], monster.Uid, uid, MsgAttackType.Physical)));
            var talkIndex = Assert.Single(Enumerable.Range(0, frames.Length).Where(index =>
                LegacyMsgMessagePacket.TryParse(ReadFrame(frames[index]), out var message) &&
                message.Content.Contains(LegacyPlayerDeathState.DeadTalkText)));
            var endIndex = Assert.Single(Enumerable.Range(0, frames.Length).Where(index =>
                IsFlag6MonsterInteraction(frames[index], monster.Uid, uid, MsgAttackType.Death)));
            Assert.Equal(frames.Length - 1, endIndex); // Closed in the terminal send callback, not a later polling snapshot.
            Assert.True(incomingIndex < talkIndex && talkIndex < endIndex);
            Assert.Equal((ushort)2135, ReadFrame(frames[incomingIndex]).Header.Opcode);
            Assert.True(MsgAttackPacket.TryParse(ReadFrame(frames[incomingIndex]), out var incomingAttack));
            Assert.Equal(x, incomingAttack.Query.X);
            Assert.Equal(y, incomingAttack.Query.Y);
            Assert.True(incomingAttack.Query.Damage >= 1);
            Assert.Equal(0, incomingAttack.Query.KilledMonster);
            Assert.Equal(0U, incomingAttack.Query.ResponseDamage);
            Assert.Equal(MsgAttackEffect.None, incomingAttack.Query.Effect);
            Assert.Equal(0U, incomingAttack.Query.Unknown);
            Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(frames[talkIndex]), out var talk));
            Assert.Equal(new[] { "SYSTEM", "ALL", string.Empty, LegacyPlayerDeathState.DeadTalkText,
                string.Empty, string.Empty, string.Empty }, talk.Content);
            // Ignore earlier Enter/SetLocation traffic and unrelated tick kinds, but require the ENTIRE
            // relevant death subsequence and its exact ordered entries/values between these witnesses.
            AssertFlag6PublicMonsterDeathSubsequence(frames[talkIndex..(endIndex + 1)], uid, monster, x, y, model);
            GC.KeepAlive(new object[] { handler, connection, model, log, cancel });
        }
        finally
        {
            await cancel.CancelAsync();
            GC.KeepAlive(new object[] { handler, connection, model });
        }
    }

    private sealed record Flag6MonsterDeathWindow(byte[][] Frames, WorldSession SessionAtTerminal,
        long HitpointsAtTerminal, string KillerAtTerminal, ulong[] CompatibilityAtTerminal);

    private static bool IsFlag6MonsterInteraction(byte[] bytes, uint monsterUid, uint uid, MsgAttackType type) =>
        MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Query.Uid == monsterUid &&
        packet.Query.OpponentUid == uid && packet.Query.AttackType == type;

    private static void AssertFlag6PublicMonsterDeathSubsequence(byte[][] frames, uint uid,
        LegacyMonsterInstance monster, ushort x, ushort y, Flag6PublicReceiverSupplementModel model)
    {
        var hp = Enumerable.Range(0, frames.Length).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var packet) && packet.Uid == uid &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or
                LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(8, hp.Length); // Three real equipment-query pairs, then source HP0 setter pair.
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hp[0]]), out var firstMaximum));
        var maximumEntry = Assert.Single(firstMaximum.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximumEntry.Type);
        Assert.True(maximumEntry.Value > 1);
        for (var phase = 0; phase < 4; phase++)
        {
            var maximumIndex = hp[phase * 2];
            var hitpointIndex = hp[phase * 2 + 1];
            Assert.Equal(maximumIndex + 1, hitpointIndex);
            Assert.Equal((ushort)2176, ReadFrame(frames[maximumIndex]).Header.Opcode);
            Assert.Equal((ushort)2176, ReadFrame(frames[hitpointIndex]).Header.Opcode);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[maximumIndex]), out var maximum));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hitpointIndex]), out var current));
            Assert.Equal(uid, maximum.Uid);
            Assert.Equal(uid, current.Uid);
            Assert.Equal(new[] { new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maximumEntry.Value) },
                maximum.Updates);
            Assert.Equal(new[]
            {
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maximumEntry.Value),
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, phase == 3 ? 0UL : 1UL),
            }, current.Updates);
        }
        var query = Assert.Single(Enumerable.Range(0, frames.Length).Where(index =>
            LegacyShowEquipmentPacket.TryParse(ReadFrame(frames[index]), out _)));
        var status = Assert.Single(Enumerable.Range(0, frames.Length).Where(index =>
            LegacyPlayerStatusPacket.TryParse(ReadFrame(frames[index]), out var packet) && packet.Uid == uid));
        Assert.True(query < hp[0] && hp[1] < status && status < hp[2]);
        var vectors = Enumerable.Range(0, frames.Length).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var packet) && packet.Uid == uid &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).ToArray();
        Assert.Equal(2, vectors.Length);
        Assert.True(0 < vectors[0] && vectors[0] < query && hp[^1] < vectors[1] && vectors[1] < frames.Length - 1);
        model.AssertVector(frames[vectors[0]], uid, dead: false);
        model.AssertVector(frames[vectors[1]], uid, dead: true);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(frames[^1]), out var death));
        Assert.Equal((ushort)2135, ReadFrame(frames[^1]).Header.Opcode);
        Assert.Equal(new MsgAttackQuery(Uid: monster.Uid, OpponentUid: uid, X: x, Y: y,
            AttackType: MsgAttackType.Death, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
            Effect: MsgAttackEffect.None, Unknown: 0), death.Query);
    }
}
