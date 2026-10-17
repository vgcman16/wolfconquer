using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("incoming", "attacker", false)]
    [InlineData("incoming", "attacker", true)]
    [InlineData("incoming", "target", false)]
    [InlineData("incoming", "target", true)]
    [InlineData("opening", "attacker", false)]
    [InlineData("opening", "attacker", true)]
    [InlineData("opening", "target", false)]
    [InlineData("opening", "target", true)]
    public async Task DirectIdentityReceiveDoesNotResurrectRemovedOrOverwriteReplacedParticipant(
        string phase,
        string participant,
        bool replace)
    {
        var result = await DirectIdentityRunAsync(phase, participant, replace);

        Assert.Equal(1, result.MutationCount);
        if (replace)
        {
            Assert.Same(result.Replacement, result.RegisteredParticipant);
            Assert.Equal(77_777, result.RegisteredParticipant!.Character.MinHitPoints);
            Assert.Equal("replacement", result.RegisteredParticipant.Character.Attributes["DirectIdentityMarker"]);
        }
        else
        {
            Assert.Null(result.RegisteredParticipant);
        }

        Assert.NotNull(result.RegisteredOther);
        Assert.Equal(100_000, result.RegisteredOther.Character.MinHitPoints);
        // The old Player references held by this attack must not become a new
        // registration, and no final receive may hit a different UID occupant.
        Assert.DoesNotContain(ParseMsgUpdates(result.Frames), update =>
            update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.DoesNotContain(ParseMsgUpdates(result.Frames), update =>
            update.Uid == result.Replacement.Character.Uid);

        if (phase == "incoming")
        {
            // Source MsgAttackPacket.cs:657-659 retains the accepted Player
            // objects across InteractionCreate and ReceiveAttack.Player.Execute.
            // A disconnected/reused connection is not a replacement Player object.
            Assert.DoesNotContain(ParseMsgUpdates(result.Frames), update =>
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    (entry.Flag == StarArrowReceiveEnvelopeSolidFlag ||
                     entry.Flag == StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag)));
            Assert.DoesNotContain(ParseMsgUpdates(result.Frames), update =>
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    (IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeSolidFlag) ||
                     IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag))));
            Assert.False(HasLegacyStatusFlag(result.RegisteredOther.Character.Attributes,
                participant == "attacker"
                    ? StarArrowReceiveEnvelopeSolidFlag
                    : StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectIdentitySolidSendIdentityLossStopsBloodthirstBeforeItCanRepublishOldTarget(bool replace)
    {
        var result = await DirectIdentityRunAsync("opening", "target", replace,
            targetStartingHitpoints: 50_000);

        Assert.Equal(1, result.MutationCount);
        if (replace)
        {
            Assert.Same(result.Replacement, result.RegisteredParticipant);
            Assert.Equal(77_777, result.RegisteredParticipant!.Character.MinHitPoints);
        }
        else
        {
            Assert.Null(result.RegisteredParticipant);
        }
        Assert.NotNull(result.RegisteredOther);
        Assert.Equal(100_000, result.RegisteredOther.Character.MinHitPoints);
        // Bloodthirst is forced and this old target is below maximum HP. It must
        // not run after Solid's awaited archive loses the original registration.
        Assert.DoesNotContain(ParseMsgUpdates(result.Frames).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.BloodthirstElan);
        Assert.DoesNotContain(ParseMsgUpdates(result.Frames), update =>
            update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.DoesNotContain(ParseMsgUpdates(result.Frames), update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag));
    }

    [Fact]
    public async Task DirectIdentitySameUidAttackerAtZeroHitpointsAfterIncomingStillCompletesReceive()
    {
        var result = await DirectIdentityRunAsync("incoming", "attacker", replace: false,
            keepSameUidAtZeroHitpoints: true);

        Assert.Equal(1, result.MutationCount);
        Assert.NotNull(result.RegisteredParticipant);
        Assert.Equal(PlayerUid, result.RegisteredParticipant.Character.Uid);
        Assert.Equal(0, result.RegisteredParticipant.Character.MinHitPoints);
        Assert.Equal("same-uid-zero-hp", result.RegisteredParticipant.Character.Attributes["DirectIdentityMarker"]);
        Assert.True(HasLegacyStatusFlag(result.RegisteredParticipant.Character.Attributes,
            StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag));
        Assert.NotNull(result.RegisteredOther);
        Assert.Equal(TargetPlayerUid, result.RegisteredOther.Character.Uid);
        Assert.Equal(99_990, result.RegisteredOther.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.RegisteredOther.Character.Attributes,
            StarArrowReceiveEnvelopeSolidFlag));
        Assert.Contains(ParseMsgUpdates(result.Frames), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints && entry.Value == 99_990));
        Assert.DoesNotContain(ParseMsgUpdates(result.Frames).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.BloodthirstElan);
    }

    private static async Task<DirectIdentityResult> DirectIdentityRunAsync(
        string phase,
        string participant,
        bool replace,
        bool keepSameUidAtZeroHitpoints = false,
        long targetStartingHitpoints = 100_000)
    {
        var worldData = CreateMagicCatalogCore(CreateMonsterFamily(), spawns: [], sobNpcs: [], magicRows: [],
            gouyuImmortals: [CreateStarArrowReceiveEnvelopeGouyuImmortal()]);
        var attacker = CreateSession(mapId: 1_000, classCode: 5_005, minHitPoints: 100_000, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(LegacyHookMoonCasterFlag),
                ["HookMoonPower"] = "1000",
                ["LegacyNinjaLevels"] = "17",
                ["LegacyNinjaSageModeRoll"] = "0",
            });
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectIdentityTarget", mapId: 1_000,
            x: 832, y: 584, minHitPoints: targetStartingHitpoints, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(LegacyPhysicalSpellsShurikenVortexFlag),
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
                ["LegacySpeffectLevel.Bloodthirst"] = "4",
                ["LegacySpeffectRoll.Bloodthirst"] = "0",
            });
        var replacement = CreateSession(uid: TargetPlayerUid + 1_000, name: "DirectIdentityReplacement",
            mapId: 1_000, x: 832, minHitPoints: 77_777, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "77777",
                ["DirectIdentityMarker"] = "replacement",
            });
        var sessions = new WorldSessionStore();
        var frames = new ConcurrentQueue<byte[]>();
        var mutationCount = 0;
        ConquerTcpConnection? attackerConnection = null;
        ConquerTcpConnection? targetConnection = null;

        ValueTask CaptureAndMutate(ReadOnlyMemory<byte> frame)
        {
            var bytes = frame.ToArray();
            frames.Enqueue(bytes);
            var parsed = ReadFrame(bytes);
            var incoming = phase == "incoming" && MsgAttackPacket.TryParse(parsed, out var attack) &&
                attack.Query.AttackType == MsgAttackType.Physical && attack.Query.Uid == PlayerUid &&
                attack.Query.OpponentUid == TargetPlayerUid;
            var opening = phase == "opening" && LegacyMsgUpdatePacket.TryParse(parsed, out var update) &&
                update.Uid == (participant == "attacker" ? PlayerUid : TargetPlayerUid) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == (participant == "attacker"
                        ? StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag
                        : StarArrowReceiveEnvelopeSolidFlag));
            if ((incoming || opening) && Interlocked.CompareExchange(ref mutationCount, 1, 0) == 0)
            {
                var affectedConnection = participant == "attacker" ? attackerConnection! : targetConnection!;
                Assert.True(sessions.TryGet(affectedConnection, out var current));
                Assert.NotNull(current);
                if (keepSameUidAtZeroHitpoints)
                {
                    var attributes = new Dictionary<string, string>(current.Character.Attributes)
                    {
                        ["DirectIdentityMarker"] = "same-uid-zero-hp",
                    };
                    sessions.Set(affectedConnection, current with
                    {
                        Character = current.Character with { MinHitPoints = 0, Attributes = attributes },
                    });
                }
                else if (replace)
                {
                    sessions.Set(affectedConnection, replacement);
                }
                else
                {
                    Assert.True(sessions.TryRemove(affectedConnection, out var removed));
                    Assert.Same(current, removed);
                }
            }
            return ValueTask.CompletedTask;
        }

        attackerConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) => CaptureAndMutate(frame));
        targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: (frame, _) => CaptureAndMutate(frame));
        sessions.Set(attackerConnection, attacker);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(attackerConnection, TextWriter.Synchronized(log), attacker,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 832, Y: 584,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        var participantConnection = participant == "attacker" ? attackerConnection : targetConnection;
        var otherConnection = participant == "attacker" ? targetConnection : attackerConnection;
        sessions.TryGet(participantConnection, out var registeredParticipant);
        sessions.TryGet(otherConnection, out var registeredOther);
        return new DirectIdentityResult(registeredParticipant, registeredOther, replacement,
            frames.ToArray(), Volatile.Read(ref mutationCount));
    }

    private sealed record DirectIdentityResult(
        WorldSession? RegisteredParticipant,
        WorldSession? RegisteredOther,
        WorldSession Replacement,
        byte[][] Frames,
        int MutationCount);
}
