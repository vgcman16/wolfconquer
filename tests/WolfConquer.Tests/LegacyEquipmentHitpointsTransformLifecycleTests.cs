using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task DirectFineRainHpRealTransformQueryAndExpiryCannotRetainTheTransformedMaximum()
    {
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var signed = LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000);
        const uint helmetId = 111_080;
        var helmet = DirectOverlayItem(helmetId, "TransformHpLiteral500");
        var fields = helmet.Fields.ToArray();
        fields[18] = "500";
        helmet = helmet with { Fields = fields };
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
        [
            CreateMagicRow(TransformSpellId, sort: 19, level: 0, useMana: 0, useStamina: 0,
                damage: 100, duration: 2, experience: 1_000, coolDown: 0, coldTime: 0),
            CreateMagicRow(TransformSpellId, sort: 19, level: 1, useMana: 0, useStamina: 0,
                damage: 100, duration: 2, experience: 1_000, coolDown: 0, coldTime: 0),
        ], items: [helmet], transformInfo:
        [
            new TransformInfo(TransformSpellId, 0, "DivineHare", 207, 3_000,
                "fixture://TransformInfo.txt", LineNumber: 1, RawLine: "1350 0 DivineHare 207 3000",
                Fields: ["1350", "0", "DivineHare", "207", "3000"]),
        ]);
        var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
            minHitPoints: 300, stamina: 100, spellIds: [TransformSpellId],
            playerItems: PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [new PlayerItemSnapshotItem
                {
                    Uid = 97_001, ItemId = helmetId, Position = 1, Durability = 100, MaximumDurability = 100,
                }],
            }, attributes: new Dictionary<string, string> { ["MaxHitpoints"] = "500" });
        Assert.Equal(500U, LegacyMaxHitpointsCalculator.Calculate(owner.Character, owner.PlayerItems,
            CombatGearSnapshot.Empty, world).Total);
        Assert.Null(owner.EquipmentHitpointsStatus);
        var frames = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (memory, _) =>
            {
                frames.Enqueue(memory.ToArray());
                return ValueTask.CompletedTask;
            });
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: world,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromSeconds(1), clock, signed),
            log: TextWriter.Synchronized(log), protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var sessions = GetHandlerSessions(handler);
        sessions.Set(connection, owner);
        var context = new WorldPacketHandlerContext(connection, TextWriter.Synchronized(log), owner,
            WorldData: world, Sessions: sessions, TimeProvider: clock, LegacyClock: signed,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        WorldSession Current() => AssertHandlerSession(handler, connection);
        async Task<LegacyPlayerStatusPacket> QueryAsync()
        {
            var start = frames.Count;
            await PacketHandlers.HandleAsync(context with { Session = Current() },
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
            var bytes = Assert.Single(frames.ToArray().Skip(start), frame =>
                LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            return status;
        }

        // Real1350 and TransformInfo create the deadline/HP; no transform state,
        // cache answer, FineRain registration or stamp is manually installed.
        await PacketHandlers.HandleAsync(context, ReadFrame(CreateTransformAttack().ToFrameBytes()));
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(Current().Character, out var transformed));
        Assert.Equal(3_000U, transformed);
        Assert.Equal(3_000, Current().Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(Current().Character.Attributes, FineRain1StatusFlag));
        await PacketHandlers.HandleAsync(context with { Session = Current() },
            ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));
        Assert.NotNull(Current().EquipmentHitpointsStatus); // RealReborn2 query reached its cache owner.
        Assert.Equal(3_000U, (await QueryAsync()).MaxHP);
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(Current()).StampMilliseconds);

        StartHandlerRuntimeUpdateLoop(handler, connection, Current(), cancel.Token);
        try
        {
            await WaitForScheduledManualTimerAsync(clock);
            clock.Advance(TimeSpan.FromSeconds(2));
            await WaitForScheduledManualTimerAsync(clock);
            // SourceClientTransform.CheckUp uses STRICT > Stamp, not >=.
            Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(Current().Character, out _));
            Assert.Equal(3_000U, (await QueryAsync()).MaxHP);
            var expiryStart = frames.Count;
            clock.Advance(TimeSpan.FromSeconds(1));
            await WaitForScheduledManualTimerAsync(clock);
            Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(Current().Character, out _));
            Assert.Equal("0", Current().Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
            Assert.False(Current().Character.Attributes.ContainsKey(LegacyTransformState.ExpiresAtUtcAttribute));
            Assert.Equal(500, Current().Character.MinHitPoints);
            Assert.Equal(500U, (await QueryAsync()).MaxHP);
            // The real admitted FinishTransform query owns its replacement cache.
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(Current(), out var cached));
            Assert.Equal(500U, cached);
            DirectFineRainHpAssertMaximumUpdates(frames.ToArray().Skip(expiryStart), 500);
            Assert.Single(frames.ToArray().Skip(expiryStart), bytes =>
                MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
                action.ObjId == PlayerUid && action.Type == MsgActionType.FinishTransform);
            Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(Current()).StampMilliseconds);
        }
        finally
        {
            await cancel.CancelAsync();
            var global = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(global);
            await WaitUntilAsync(() => clock.ScheduledTimerCount == 0 && Equals(0, global.GetValue(handler)),
                TimeSpan.FromSeconds(3));
            GC.KeepAlive(connection);
        }
    }
}
