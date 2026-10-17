using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint DirectFuryDisplayRuneItemId = 4_020_101;
    private const int DirectFuryDisplayMudWallFlag = 0x112;

    [Theory]
    [InlineData("eligible", 285, 300U)]
    [InlineData("missing-iron-guard", 244, 300U)]
    [InlineData("missing-rune", 244, 300U)]
    [InlineData("wrong-job", 244, 300U)]
    [InlineData("inactive-alternate", 244, 300U)]
    [InlineData("active-alternate", 285, 300U)]
    [InlineData("reborn-one", 244, 300U)]
    [InlineData("level-fourteen", 244, 300U)]
    [InlineData("level-fifteen", 285, 300U)]
    [InlineData("rune-level-zero", 285, 300U)]
    [InlineData("highest-eligible-job", 285, 300U)]
    [InlineData("job-above-source-limit", 244, 300U)]
    [InlineData("byte-projected-power", 285, 300U)]
    [InlineData("mud-wall", 236, 249U)]
    public async Task DirectFuryDisplayUsesEligibleIronGuardBonusAfterPhysicalDisplayFloorWithoutChangingReceive(
        string scenario,
        int expectedDisplayedDamage,
        uint expectedReceivedDamage)
    {
        var result = await DirectFuryDisplayRunAsync(scenario);

        Assert.Equal(2, result.IncomingObservedTargetHitpoints.Length);
        Assert.All(result.IncomingObservedTargetHitpoints, hitpoints => Assert.Equal(100_000, hitpoints));
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var interaction = AssertSinglePhysicalAttack(frames, TargetPlayerUid);
            Assert.Equal(PlayerUid, interaction.Query.Uid);
            Assert.Equal(expectedDisplayedDamage, interaction.Query.Damage);
            Assert.Equal(MsgAttackEffect.None, interaction.Query.Effect);
            Assert.Equal((120, 121), ((int)interaction.Query.X, (int)interaction.Query.Y));
            var incomingIndex = SingleFrameIndex(frames, frame =>
                MsgAttackPacket.TryParse(frame, out var attack) &&
                attack.Query.AttackType == MsgAttackType.Physical &&
                attack.Query.Uid == PlayerUid && attack.Query.OpponentUid == TargetPlayerUid);
            Assert.Equal((ushort)2135, ReadFrame(frames[incomingIndex]).Header.Opcode);
            if (expectedDisplayedDamage == 285)
            {
                // Source MsgAttackPacket.cs:649-650 floors 300*22/27 to 244,
                // then adds floor(244*17/100)=41. Combining fractions gives 286.
                Assert.NotEqual(286, interaction.Query.Damage);
            }

            var hitpointIndices = Enumerable.Range(0, frames.Length).Where(index =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type is
                    LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints)).ToArray();
            if (ReferenceEquals(frames, result.CasterSent))
            {
                // Ordinary player HP setters publish their pair to the owner,
                // while both participants receive the incoming interaction.
                Assert.Empty(hitpointIndices);
                continue;
            }
            Assert.Equal(2, hitpointIndices.Length);
            Assert.All(hitpointIndices, index => Assert.True(incomingIndex < index));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hitpointIndices[0]]), out var maximum));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[hitpointIndices[1]]), out var hitpoints));
            Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], maximum.Updates.Select(entry => entry.Type));
            Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
                hitpoints.Updates.Select(entry => entry.Type));
            Assert.Equal(100_000UL, maximum.Updates[0].Value);
            Assert.Equal(100_000UL, hitpoints.Updates[0].Value);
            Assert.Equal(100_000UL - expectedReceivedDamage, hitpoints.Updates[1].Value);
        }

        // FuryStrike changes only the interaction display. The original prepared
        // physical SpellObj still enters ReceiveAttack.Player at source line 659.
        Assert.Equal(100_000L - expectedReceivedDamage, result.Target.Character.MinHitPoints);
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(scenario == "byte-projected-power" ? "273" : "17",
            result.Caster.Character.Attributes["FuryStrikeAttack"]);
        if (scenario == "mud-wall")
        {
            // Physical.cs:983-985 reduces 300 by 17% once to 249. Its display is
            // floor(249*22/27)=202, plus floor(202*17/100)=34, for 236; HP loses 249.
            Assert.Equal(99_751, result.Target.Character.MinHitPoints);
        }
        else
        {
            Assert.Equal(99_700, result.Target.Character.MinHitPoints);
        }
    }

    private static async Task<DirectFuryDisplayResult> DirectFuryDisplayRunAsync(string scenario)
    {
        var runeId = scenario == "rune-level-zero" ? DirectFuryDisplayRuneItemId - 1 : DirectFuryDisplayRuneItemId;
        var requiredClass = scenario == "wrong-job" ? 5_000U : 2_000U;
        var runeFields = Enumerable.Repeat("0", ItemType.RequiredClassFieldIndex + 1).ToArray();
        runeFields[0] = runeId.ToString(CultureInfo.InvariantCulture);
        runeFields[1] = "FuryStrike(+" + (runeId % 100).ToString(CultureInfo.InvariantCulture) + ")";
        runeFields[ItemType.RequiredClassFieldIndex] = requiredClass.ToString(CultureInfo.InvariantCulture);
        var rune = new ItemType(checked((int)runeId), runeFields[1], runeFields);
        var worldData = CreateMagicCatalogCore(CreateMonsterFamily(), spawns: [], sobNpcs: [], magicRows: [],
            items: [rune]);
        var runeItems = scenario == "missing-rune"
            ? Array.Empty<PlayerItemSnapshotItem>()
            : new[]
            {
                new PlayerItemSnapshotItem
                {
                    Uid = 7_900_001,
                    ItemId = runeId,
                    Position = scenario is "inactive-alternate" or "active-alternate" ? (ushort)122 : (ushort)102,
                },
            };
        var caster = CreateSession(mapId: 1_000, x: 830, y: 584, strength: 300, spirit: 0,
            classCode: scenario switch
            {
                "highest-eligible-job" => 2_051,
                "job-above-source-limit" => 2_052,
                _ => 2_005,
            },
            reborn: scenario == "reborn-one" ? 1 : 2, minHitPoints: 100_000,
            spellIds: [], playerItems: CreatePlayerItems(runeItems),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(scenario == "missing-iron-guard" ? [] : [IronGuardStatusFlag]),
                ["FuryStrikeAttack"] = scenario == "byte-projected-power" ? "273" : "17",
            });
        caster = caster with
        {
            Character = caster.Character with
            {
                Level = scenario switch
                {
                    "level-fourteen" => 14,
                    "level-fifteen" => 15,
                    _ => 120,
                },
            },
            AlternateEquipmentActive = scenario == "active-alternate",
        };
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectFuryDisplayTarget", mapId: 1_000,
            x: 832, y: 584, minHitPoints: 100_000, spirit: 0, classCode: 5_005, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(scenario == "mud-wall" ? [DirectFuryDisplayMudWallFlag] : []),
                ["LegacyMudSigilImpeccablePower"] = scenario == "mud-wall" ? "1700" : "0",
            });
        var sessions = new WorldSessionStore();
        var casterSent = new ConcurrentQueue<byte[]>();
        var targetSent = new ConcurrentQueue<byte[]>();
        var incomingObservedTargetHitpoints = new ConcurrentQueue<long>();
        ConquerTcpConnection? targetConnection = null;

        ValueTask Capture(ConcurrentQueue<byte[]> sent, ReadOnlyMemory<byte> frame)
        {
            var bytes = frame.ToArray();
            sent.Enqueue(bytes);
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.Physical && attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == TargetPlayerUid)
            {
                Assert.True(sessions.TryGet(targetConnection!, out var currentTarget));
                Assert.NotNull(currentTarget);
                incomingObservedTargetHitpoints.Enqueue(currentTarget.Character.MinHitPoints);
            }
            return ValueTask.CompletedTask;
        }

        var casterConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) => Capture(casterSent, frame));
        targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: (frame, _) => Capture(targetSent, frame));
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.Contains("WorldServer handled legacy direct player physical", log.ToString());
        return new DirectFuryDisplayResult(finalCaster, finalTarget, casterSent.ToArray(), targetSent.ToArray(),
            incomingObservedTargetHitpoints.ToArray());
    }

    private sealed record DirectFuryDisplayResult(
        WorldSession Caster,
        WorldSession Target,
        byte[][] CasterSent,
        byte[][] TargetSent,
        long[] IncomingObservedTargetHitpoints);
}
