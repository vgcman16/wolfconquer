using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int DirectPreparationMudWallFlag = 0x112;

    [Fact]
    public async Task DirectPreparationMudWallPrecedesDisplayedDamageAndRunsExactlyOnce()
    {
        var result = await DirectPreparationRunAsync(mudWallPower: 1_700, hawksAmbition: true);

        // Fixed unarmed [300,301) attack and zero defense give 300 in this bounded branch.
        // Physical.cs:983-985: 300 - floor(300 * (1700/100) / 100) = 249.
        // MsgAttackPacket.cs:649-656 displays floor(249*22/27)=202, before Receive.Player.
        // Receive.Player:138-142 then consumes Hawks25%: 249-floor(249*25/100)=187.
        Assert.Equal(202, DirectPreparationAssertIncoming(result.CasterSent).Query.Damage);
        Assert.Equal(202, DirectPreparationAssertIncoming(result.TargetSent).Query.Damage);
        Assert.Equal(99_813, result.Target.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyHawksAmbitionFlag));
        DirectPreparationAssertPublishedHitpoints(result.TargetSent, 99_813);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectPreparationFixedOneStatusOrWildPhoenixBypassesPhysicalMudWall(bool wildPhoenixOnly)
    {
        var result = await DirectPreparationRunAsync(
            mudWallPower: 10_000,
            statusDefense: !wildPhoenixOnly,
            wildPhoenix: true);

        // Physical.cs:157-165 returns1 before BackDmg/MudWall; use100%, since a50% floor
        // reduction of1 would subtract0 and would not distinguish a wrongly entered tail.
        Assert.Equal(0, DirectPreparationAssertIncoming(result.CasterSent).Query.Damage);
        Assert.Equal(0, DirectPreparationAssertIncoming(result.TargetSent).Query.Damage);
        Assert.Equal(99_999, result.Target.Character.MinHitPoints);
        Assert.Equal(wildPhoenixOnly ? "1" : "2",
            result.Target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        DirectPreparationAssertPublishedHitpoints(result.TargetSent, 99_999);
    }

    [Fact]
    public async Task DirectPreparationIncomingSendReentryPreservesBothCurrentSessionsForActualReceive()
    {
        var result = await DirectPreparationRunAsync(rewriteOnIncoming: true);

        // The incoming physical packet precedes Receive.Player, so its display stays
        // floor(300*22/27)=244 even when its send callback updates receive-time state.
        Assert.True(result.Reentered);
        Assert.Equal(100_000, result.TargetHitpointsObservedOnIncoming);
        Assert.Equal(244, DirectPreparationAssertIncoming(result.CasterSent).Query.Damage);
        Assert.Equal(244, DirectPreparationAssertIncoming(result.TargetSent).Query.Damage);
        Assert.Equal("current-attacker", result.Caster.Character.Attributes["DirectPreparationMarker"]);
        Assert.Equal("current-target", result.Target.Character.Attributes["DirectPreparationMarker"]);
        Assert.Equal(4_500, result.Caster.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Caster.Character.Attributes, LegacyCleanSweepFlag));
        Assert.False(HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyHawksAmbitionFlag));

        // Current same-UID target starts receive at80000. Source Receive.Player:130-142
        // applies new CleanSweep50% then Hawks25%: 300+150-floor(450*25/100)=338.
        Assert.Equal(79_662, result.Target.Character.MinHitPoints);
        DirectPreparationAssertPublishedHitpoints(result.TargetSent, 79_662);
    }

    private static async Task<DirectPreparationRunResult> DirectPreparationRunAsync(
        uint mudWallPower = 0,
        bool hawksAmbition = false,
        bool statusDefense = false,
        bool wildPhoenix = false,
        bool rewriteOnIncoming = false)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), []);
        var caster = CreateSession(strength: 300, spirit: 0, classCode: 9_001,
            mapId: 1_000, spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
                ["HawksAmbitionPower"] = hawksAmbition ? "25" : "0",
            });
        var targetFlags = new List<int>();
        if (mudWallPower != 0)
        {
            targetFlags.Add(DirectPreparationMudWallFlag);
        }
        if (hawksAmbition)
        {
            targetFlags.Add(LegacyHawksAmbitionFlag);
        }
        if (statusDefense)
        {
            targetFlags.Add(LegacyShurikenVortexFlag);
        }
        if (wildPhoenix)
        {
            targetFlags.Add(LegacyWildPhoenixFlag);
        }
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectPreparationTarget", x: 832,
            mapId: 1_000, classCode: 5_005, minHitPoints: 100_000, spirit: 0, spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray()),
                ["LegacyMudSigilImpeccablePower"] = mudWallPower.ToString(CultureInfo.InvariantCulture),
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = wildPhoenix ? "2" : "0",
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
            });
        var sessions = new WorldSessionStore();
        var casterSent = new ConcurrentQueue<byte[]>();
        var targetSent = new ConcurrentQueue<byte[]>();
        var targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                targetSent.Enqueue(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var reentered = false;
        var observedHitpoints = -1L;
        ConquerTcpConnection? casterConnection = null;
        casterConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                casterSent.Enqueue(bytes);
                var parsed = ReadFrame(bytes);
                if (rewriteOnIncoming && !reentered && parsed.Header.Opcode == 2135 &&
                    MsgAttackPacket.TryParse(parsed, out var attack) &&
                    attack.Query.AttackType == MsgAttackType.Physical &&
                    attack.Query.Uid == PlayerUid && attack.Query.OpponentUid == TargetPlayerUid)
                {
                    reentered = true;
                    Assert.True(sessions.TryGet(casterConnection!, out var currentCaster));
                    Assert.NotNull(currentCaster);
                    Assert.True(sessions.TryGet(targetConnection, out var currentTarget));
                    Assert.NotNull(currentTarget);
                    observedHitpoints = currentTarget.Character.MinHitPoints;
                    var casterAttributes = new Dictionary<string, string>(currentCaster.Character.Attributes)
                    {
                        ["DirectPreparationMarker"] = "current-attacker",
                        ["StatusFlags"] = BuildStatusFlags(LegacyCleanSweepFlag),
                        ["CleanSweepPower"] = "50",
                        ["HawksAmbitionPower"] = "25",
                    };
                    sessions.Set(casterConnection!, currentCaster with
                    {
                        Character = currentCaster.Character with
                        {
                            MinHitPoints = 4_500,
                            Attributes = casterAttributes,
                        },
                    });
                    var targetAttributes = new Dictionary<string, string>(currentTarget.Character.Attributes)
                    {
                        ["DirectPreparationMarker"] = "current-target",
                        ["StatusFlags"] = BuildStatusFlags(LegacyHawksAmbitionFlag),
                    };
                    sessions.Set(targetConnection, currentTarget with
                    {
                        Character = currentTarget.Character with
                        {
                            MinHitPoints = 80_000,
                            Attributes = targetAttributes,
                        },
                    });
                }
                return ValueTask.CompletedTask;
            });
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();

        await PacketHandlers.HandleAsync(new WorldPacketHandlerContext(
                casterConnection, TextWriter.Synchronized(log), caster, WorldData: worldData, Sessions: sessions,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 832, Y: 584,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        Assert.Contains("WorldServer handled legacy direct player physical", log.ToString());
        return new(finalCaster, finalTarget, casterSent.ToArray(), targetSent.ToArray(), reentered, observedHitpoints);
    }

    private static MsgAttackPacket DirectPreparationAssertIncoming(IReadOnlyList<byte[]> frames)
    {
        var incoming = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            var frame = ReadFrame(bytes);
            if (frame.Header.Opcode == 2135 && MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical &&
                packet.Query.Uid == PlayerUid && packet.Query.OpponentUid == TargetPlayerUid)
            {
                incoming.Add(packet);
            }
        }
        return Assert.Single(incoming);
    }

    private static void DirectPreparationAssertPublishedHitpoints(IReadOnlyList<byte[]> frames, ulong expected)
    {
        var hitpoints = new List<ulong>();
        foreach (var bytes in frames)
        {
            var frame = ReadFrame(bytes);
            if (frame.Header.Opcode == 2176 && LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == TargetPlayerUid)
            {
                hitpoints.AddRange(update.Updates.Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints)
                    .Select(entry => entry.Value));
            }
        }
        Assert.Equal(expected, Assert.Single(hitpoints));
    }

    private sealed record DirectPreparationRunResult(
        WorldSession Caster,
        WorldSession Target,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        bool Reentered,
        long TargetHitpointsObservedOnIncoming);
}
