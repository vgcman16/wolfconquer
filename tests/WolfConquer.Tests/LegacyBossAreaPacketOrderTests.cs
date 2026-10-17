using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossAreaPacketOrderTests
{
    private const uint FirstPlayerUid = 1_293_600;
    private const uint SecondPlayerUid = 1_293_601;
    private const uint MonsterUid = 400_001;
    private const ushort AreaSpellId = 7_013;
    private const int FrightenedFlag = LegacyBossStatusEffect.FrightenedFlag;
    private const int DeadFlag = LegacyPlayerDeathState.DeadStatusFlag;
    private static readonly uint[] PlayerUids = [FirstPlayerUid, SecondPlayerUid];

    [Fact]
    public async Task NonlethalForcedStatusSendsStatusVectorAndGameUpdateBeforeSharedAnimation()
    {
        var result = await SendForcedAreaRetaliationAsync(hitpoints: 5_000);
        var animationIndices = AssertFrameIndices(
            result.Frames,
            PlayerUids,
            frame => TryParseAreaAnimation(frame, result.Monster.Uid, PlayerUids));
        var sideEffectIndices = new List<int>();

        foreach (var playerUid in PlayerUids)
        {
            var updatedSession = result.UpdatedSessions[playerUid];
            Assert.Equal(2_000, updatedSession.Character.MinHitPoints);
            Assert.True(LegacyPlayerDeathState.HasStatusFlag(
                updatedSession.Character.Attributes,
                FrightenedFlag));

            var hitpointsIndices = AssertFrameIndices(
                result.Frames,
                [playerUid],
                frame => TryParseHitpoints(frame, playerUid, expectedHitpoints: 2_000));
            var statusVectorIndices = AssertFrameIndices(
                result.Frames,
                PlayerUids,
                frame => TryParseStatusVector(frame, playerUid, out var values) &&
                    HasStatusFlag(values, FrightenedFlag) &&
                    !HasStatusFlag(values, DeadFlag));
            var gameUpdateIndices = AssertFrameIndices(
                result.Frames,
                PlayerUids,
                frame => TryParseActiveFrightenedUpdate(frame, playerUid));

            AssertPacketGroupOrder(
                result.Log,
                (hitpointsIndices, $"hitpoints for {playerUid}"),
                (statusVectorIndices, $"status vector for {playerUid}"),
                (gameUpdateIndices, $"GameUpdate for {playerUid}"),
                (animationIndices, "MsgSpellAnimation"));
            sideEffectIndices.AddRange(hitpointsIndices);
            sideEffectIndices.AddRange(statusVectorIndices);
            sideEffectIndices.AddRange(gameUpdateIndices);
        }

        AssertSharedAnimationAfterAllTargetSideEffects(result.Log, sideEffectIndices, animationIndices);
    }

    [Fact]
    public async Task LethalForcedStatusCompletesDeathThenReaddsStatusBeforeSharedAnimation()
    {
        var result = await SendForcedAreaRetaliationAsync(hitpoints: 10);
        var animationIndices = AssertFrameIndices(
            result.Frames,
            PlayerUids,
            frame => TryParseAreaAnimation(frame, result.Monster.Uid, PlayerUids));
        var sideEffectIndices = new List<int>();

        foreach (var playerUid in PlayerUids)
        {
            var updatedSession = result.UpdatedSessions[playerUid];
            Assert.Equal(0, updatedSession.Character.MinHitPoints);
            Assert.True(LegacyPlayerDeathState.HasStatusFlag(
                updatedSession.Character.Attributes,
                DeadFlag));
            Assert.True(LegacyPlayerDeathState.HasStatusFlag(
                updatedSession.Character.Attributes,
                FrightenedFlag));
            Assert.True(updatedSession.Character.Attributes.ContainsKey(
                LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute));
            Assert.Equal(
                bool.TrueString,
                updatedSession.Character.Attributes[
                    LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute]);

            var hitpointsIndices = AssertFrameIndices(
                result.Frames,
                [playerUid],
                frame => TryParseHitpoints(frame, playerUid, expectedHitpoints: 0));
            var deadStatusVectorIndices = AssertFrameIndices(
                result.Frames,
                PlayerUids,
                frame => TryParseStatusVector(frame, playerUid, out var values) &&
                    HasStatusFlag(values, DeadFlag) &&
                    !HasStatusFlag(values, FrightenedFlag));
            var deathIndices = AssertFrameIndices(
                result.Frames,
                PlayerUids,
                frame => MsgAttackPacket.TryParse(ReadFrame(frame), out var packet) &&
                    packet.Query.Uid == result.Monster.Uid &&
                    packet.Query.OpponentUid == playerUid &&
                    packet.Query.AttackType == MsgAttackType.Death);
            var readdedStatusVectorIndices = AssertFrameIndices(
                result.Frames,
                PlayerUids,
                frame => TryParseStatusVector(frame, playerUid, out var values) &&
                    HasStatusFlag(values, DeadFlag) &&
                    HasStatusFlag(values, FrightenedFlag));
            var gameUpdateIndices = AssertFrameIndices(
                result.Frames,
                PlayerUids,
                frame => TryParseActiveFrightenedUpdate(frame, playerUid));

            AssertPacketGroupOrder(
                result.Log,
                (hitpointsIndices, $"hitpoints for {playerUid}"),
                (deadStatusVectorIndices, $"dead status vector for {playerUid}"),
                (deathIndices, $"death for {playerUid}"),
                (readdedStatusVectorIndices, $"post-death status vector for {playerUid}"),
                (gameUpdateIndices, $"GameUpdate for {playerUid}"),
                (animationIndices, "MsgSpellAnimation"));
            sideEffectIndices.AddRange(hitpointsIndices);
            sideEffectIndices.AddRange(deadStatusVectorIndices);
            sideEffectIndices.AddRange(deathIndices);
            sideEffectIndices.AddRange(readdedStatusVectorIndices);
            sideEffectIndices.AddRange(gameUpdateIndices);
        }

        AssertSharedAnimationAfterAllTargetSideEffects(result.Log, sideEffectIndices, animationIndices);
    }

    private static async Task<RetaliationResult> SendForcedAreaRetaliationAsync(long hitpoints)
    {
        var capture = new FrameCapture();
        var firstConnection = CreateSendingConnection(capture, FirstPlayerUid, remotePort: 9_958);
        var secondConnection = CreateSendingConnection(capture, SecondPlayerUid, remotePort: 9_959);
        var firstSession = CreateSession(FirstPlayerUid, accountId: 1, "FirstTarget", x: 94, hitpoints);
        var secondSession = CreateSession(SecondPlayerUid, accountId: 2, "SecondTarget", x: 95, hitpoints);
        var sessions = new WorldSessionStore();
        sessions.Set(firstConnection, firstSession);
        sessions.Set(secondConnection, secondSession);
        var monster = CreateAreaBoss();
        var monsterFamily = CreateAreaBossFamily();
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            firstConnection,
            log,
            firstSession,
            Sessions: sessions);

        await WorldPacketHandlers.TrySendLegacyMonsterRetaliationAsync(
            context,
            firstSession,
            monster,
            monsterFamily,
            CancellationToken.None);

        Assert.True(sessions.TryGet(firstConnection, out var updatedFirstSession));
        Assert.NotNull(updatedFirstSession);
        Assert.True(sessions.TryGet(secondConnection, out var updatedSecondSession));
        Assert.NotNull(updatedSecondSession);
        return new RetaliationResult(
            capture.Snapshot(),
            new Dictionary<uint, WorldSession>
            {
                [FirstPlayerUid] = updatedFirstSession,
                [SecondPlayerUid] = updatedSecondSession,
            },
            monster,
            log.ToString());
    }

    private static WorldSession CreateSession(
        uint playerUid,
        long accountId,
        string name,
        long x,
        long hitpoints)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyBossSpellIndex"] = "0",
            ["LegacyBossStatusRoll"] = "0",
            ["LegacyMonsterBossMinimumDamage"] = "3000",
        };
        var character = new CharacterSnapshot(
            Uid: playerUid,
            Name: name,
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: 1_002,
            X: x,
            Y: 354,
            PreviousMapId: 1_002,
            PreviousMapX: x,
            PreviousMapY: 354,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: hitpoints,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://legacy-boss-area-packet-order",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());

        return new WorldSession(
            AccountId: accountId,
            Username: name,
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static LegacyMonsterInstance CreateAreaBoss()
    {
        return new LegacyMonsterInstance(
            Uid: MonsterUid,
            MapId: 1_002,
            X: 94,
            Y: 354,
            Mesh: 900,
            Level: 120,
            Hitpoints: 70_000,
            MaxLife: 70_000,
            MonsterId: 213_883,
            Name: "AreaBoss",
            Boss: true,
            GeneratorId: 1,
            SpawnIndex: 0);
    }

    private static Monster CreateAreaBossFamily()
    {
        return new Monster(
            213_883,
            "AreaBoss",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["attack_min"] = "100",
                ["attack_max"] = "100",
            },
            []);
    }

    private static ConquerTcpConnection CreateSendingConnection(
        FrameCapture capture,
        uint recipientUid,
        int remotePort)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, remotePort),
            sendAsync: (frame, _) =>
            {
                capture.Add(recipientUid, frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static bool TryParseHitpoints(byte[] bytes, uint playerUid, ulong expectedHitpoints)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == playerUid &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.Hitpoints &&
                update.Value == expectedHitpoints);
    }

    private static bool TryParseStatusVector(
        byte[] bytes,
        uint playerUid,
        out IReadOnlyList<ulong> values)
    {
        values = [];
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) ||
            packet.Uid != playerUid ||
            packet.Updates.Count != 1 ||
            packet.Updates[0].Type != LegacyMsgUpdateDataType.StatusFlag ||
            packet.Updates[0].Values is not { } parsedValues)
        {
            return false;
        }

        values = parsedValues;
        return true;
    }

    private static bool TryParseActiveFrightenedUpdate(byte[] bytes, uint playerUid)
    {
        return LegacyGameUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == playerUid &&
            packet.Type == LegacyGameUpdateDataType.Flustered &&
            packet.Active &&
            packet.Time == 3;
    }

    private static bool TryParseAreaAnimation(
        byte[] bytes,
        uint monsterUid,
        IReadOnlyCollection<uint> expectedTargetUids)
    {
        return LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.AttackerUid == monsterUid &&
            packet.SpellId == AreaSpellId &&
            packet.Targets.Select(target => target.Uid).Order().SequenceEqual(expectedTargetUids.Order());
    }

    private static bool HasStatusFlag(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static IReadOnlyList<int> AssertFrameIndices(
        IReadOnlyList<CapturedFrame> frames,
        IReadOnlyCollection<uint> expectedRecipientUids,
        Func<byte[], bool> predicate)
    {
        var matches =
            frames
                .Select((frame, index) => (frame, index))
                .Where(item => predicate(item.frame.Bytes))
                .ToArray();
        Assert.Equal(
            expectedRecipientUids.Order(),
            matches.Select(item => item.frame.RecipientUid).Order());
        return matches.Select(item => item.index).ToArray();
    }

    private static void AssertPacketGroupOrder(
        string log,
        params (IReadOnlyList<int> Indices, string Name)[] expectedOrder)
    {
        for (var index = 1; index < expectedOrder.Length; index++)
        {
            var previous = expectedOrder[index - 1];
            var current = expectedOrder[index];
            Assert.True(
                previous.Indices.Max() < current.Indices.Min(),
                $"Expected {previous.Name} before {current.Name}. Log={log}");
        }
    }

    private static void AssertSharedAnimationAfterAllTargetSideEffects(
        string log,
        IReadOnlyList<int> sideEffectIndices,
        IReadOnlyList<int> animationIndices)
    {
        Assert.True(
            sideEffectIndices.Max() < animationIndices.Min(),
            "Expected the earliest shared MsgSpellAnimation only after every target's latest side effect. " +
            $"Log={log}");
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private sealed record RetaliationResult(
        IReadOnlyList<CapturedFrame> Frames,
        IReadOnlyDictionary<uint, WorldSession> UpdatedSessions,
        LegacyMonsterInstance Monster,
        string Log);

    private sealed record CapturedFrame(uint RecipientUid, byte[] Bytes);

    private sealed class FrameCapture
    {
        private readonly object _gate = new();
        private readonly List<CapturedFrame> _frames = [];

        public void Add(uint recipientUid, byte[] bytes)
        {
            lock (_gate)
            {
                _frames.Add(new CapturedFrame(recipientUid, bytes));
            }
        }

        public IReadOnlyList<CapturedFrame> Snapshot()
        {
            lock (_gate)
            {
                return _frames.ToArray();
            }
        }
    }
}
