using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort FineRainAttachStatusSpellId = 14_500;
    private const int FineRain1StatusFlag = 0xD4;
    private const int FineRain2StatusFlag = 0xD5;
    private const int FineRainDeadStatusFlag = 0x05;

    [Fact]
    public async Task HandlerFineRain14500SelfCastAppliesStatusToCasterAndInRangeLivingTeamMembers()
    {
        const uint duration = 35;
        const uint fineRainPower = 477;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                FineRainAttachStatusSpellId,
                sort: 6,
                level: 0,
                experience: 50,
                damage: fineRainPower,
                duration: duration,
                coldTime: 0),
            CreateMagicRow(
                FineRainAttachStatusSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                damage: fineRainPower,
                duration: duration,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var casterSent = new List<byte[]>();
        var memberSent = new List<byte[]>();
        var farMemberSent = new List<byte[]>();
        var deadMemberSent = new List<byte[]>();
        var laterMemberSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var memberConnection = CreateSendingConnection(memberSent);
        var farMemberConnection = CreateSendingConnection(farMemberSent);
        var deadMemberConnection = CreateSendingConnection(deadMemberSent);
        var laterMemberConnection = CreateSendingConnection(laterMemberSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            minHitPoints: 3_000,
            spellIds: [FineRainAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "8000",
            });
        var memberSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Member",
            x: 835,
            y: 584,
            minHitPoints: 2_000,
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "7000",
            });
        var farMemberSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "FarMember",
            x: 900,
            y: 584,
            minHitPoints: 2_100,
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "7100",
            });
        var deadMemberSession = CreateSession(
            uid: TargetPlayerUid + 2,
            name: "DeadMember",
            x: 836,
            y: 584,
            minHitPoints: 1,
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "7200",
                ["StatusFlags"] = BuildStatusFlags(FineRainDeadStatusFlag),
            });
        var laterMemberSession = CreateSession(
            uid: TargetPlayerUid + 3,
            name: "LaterMember",
            x: 837,
            y: 584,
            minHitPoints: 2_300,
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "7300",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(memberConnection, memberSession);
        // Membership must pass the real visible/living join path. Apply the
        // original far/dead cast conditions only AFTER all five valid joins.
        sessions.Set(farMemberConnection, farMemberSession with
        {
            Character = farMemberSession.Character with { X = 834 },
        });
        var deadJoinAttributes = new Dictionary<string, string>(deadMemberSession.Character.Attributes);
        deadJoinAttributes.Remove("StatusFlags");
        sessions.Set(deadMemberConnection, deadMemberSession with
        {
            Character = deadMemberSession.Character with { Attributes = deadJoinAttributes },
        });
        sessions.Set(laterMemberConnection, laterMemberSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgTeamPacket(MsgTeamType.Create, PlayerUid).ToFrameBytes()));
        foreach (var joining in new[]
                 {
                     (Connection: memberConnection, Uid: TargetPlayerUid),
                     (Connection: farMemberConnection, Uid: TargetPlayerUid + 1),
                     (Connection: deadMemberConnection, Uid: TargetPlayerUid + 2),
                     (Connection: laterMemberConnection, Uid: TargetPlayerUid + 3),
                 })
        {
            Assert.True(sessions.TryGet(joining.Connection, out var joiningSession));
            Assert.NotNull(joiningSession);
            await PacketHandlers.HandleAsync(context with { Connection = joining.Connection, Session = joiningSession },
                ReadFrame(new MsgTeamPacket(MsgTeamType.AcceptInvitation, PlayerUid).ToFrameBytes()));
            Assert.True(teamStates.IsTeamMember(casterConnection, joining.Uid));
        }
        Assert.Equal(5U, teamStates.GetMemberCount(casterConnection));
        sessions.Set(farMemberConnection, farMemberSession);
        sessions.Set(deadMemberConnection, deadMemberSession);
        foreach (var capture in new[] { casterSent, memberSent, farMemberSent, deadMemberSent, laterMemberSent })
            capture.Clear(); // No setup membership frames may satisfy cast-time assertions.
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: 848,
                Y: 593,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: FineRainAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        AssertFineRainApplied(updatedCasterSession, fineRainPower, beforeCast, afterCast, duration);
        Assert.Equal(8_477, updatedCasterSession.Character.MinHitPoints);

        Assert.True(sessions.TryGet(memberConnection, out var updatedMemberSession));
        Assert.NotNull(updatedMemberSession);
        AssertFineRainApplied(updatedMemberSession, fineRainPower, beforeCast, afterCast, duration);
        Assert.Equal(7_477, updatedMemberSession.Character.MinHitPoints);

        Assert.True(sessions.TryGet(farMemberConnection, out var updatedFarMemberSession));
        Assert.NotNull(updatedFarMemberSession);
        AssertFineRainNotApplied(updatedFarMemberSession, 2_100);

        Assert.True(sessions.TryGet(deadMemberConnection, out var updatedDeadMemberSession));
        Assert.NotNull(updatedDeadMemberSession);
        AssertFineRainNotApplied(updatedDeadMemberSession, 1);
        Assert.True(HasLegacyStatusFlag(updatedDeadMemberSession.Character.Attributes, FineRainDeadStatusFlag));

        Assert.True(sessions.TryGet(laterMemberConnection, out var updatedLaterMemberSession));
        Assert.NotNull(updatedLaterMemberSession);
        AssertFineRainNotApplied(updatedLaterMemberSession, 2_300);

        var casterSnapshot = casterSent.ToArray();
        var interactionIndex = SingleFrameIndex(
            casterSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == FineRainAttachStatusSpellId);
        var animationIndex = SingleFrameIndex(
            casterSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == FineRainAttachStatusSpellId);
        Assert.True(interactionIndex < animationIndex);

        AssertFineRainStatusAndCountdowns(casterSent, PlayerUid, fineRainPower, duration);
        AssertFineRainStatusAndCountdowns(memberSent, TargetPlayerUid, fineRainPower, duration);
        AssertFineRainHitpoints(casterSent, PlayerUid, 8_477);
        AssertFineRainHitpoints(memberSent, TargetPlayerUid, 7_477);
        Assert.DoesNotContain(
            ParseMsgUpdates(farMemberSent),
            update => update.Uid == TargetPlayerUid + 1 &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.FineRain));
        Assert.DoesNotContain(
            ParseMsgUpdates(deadMemberSent),
            update => update.Uid == TargetPlayerUid + 2 &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.FineRain));
        Assert.DoesNotContain(
            ParseMsgUpdates(laterMemberSent),
            update => update.Uid == TargetPlayerUid + 3 &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.FineRain));

        Assert.Contains(
            ParseShowEquipmentRefreshes(casterSent),
            packet => packet.Action == LegacyShowEquipmentPacket.ShowAction);
        Assert.Contains(
            ParseShowEquipmentRefreshes(memberSent),
            packet => packet.Action == LegacyShowEquipmentPacket.ShowAction);
        Assert.DoesNotContain(
            ParseShowEquipmentRefreshes(farMemberSent),
            packet => packet.Action == LegacyShowEquipmentPacket.ShowAction);

        // Three real setters per affected actor (AddRune, clamp, fill), each
        // broadcasts one captured member to ALL five teammates, even far/dead.
        // The explicit query pair has no team side effect and there is no late batch.
        foreach (var capture in new[] { casterSent, memberSent, farMemberSent, deadMemberSent, laterMemberSent })
        {
            var teamInfos = ParseFineRainTeamMemberInfos(capture);
            Assert.Equal(6, teamInfos.Count);
            Assert.All(teamInfos, packet =>
            {
                Assert.Equal(LegacyTeamMemberInfoAction.AddMember, packet.Action);
                Assert.Equal(1, packet.Count);
                Assert.Equal(1, packet.Unknown);
                Assert.Single(packet.Entries);
            });
            foreach (var expected in new[] { (Uid: PlayerUid, Maximum: 8_477U), (Uid: TargetPlayerUid, Maximum: 7_477U) })
            {
                var entries = teamInfos.Select(packet => packet.Entries[0]).Where(entry => entry.Uid == expected.Uid).ToArray();
                Assert.Equal(3, entries.Length);
                Assert.Equal(expected.Maximum, entries[^1].MaxHitpoints);
                Assert.Equal(expected.Maximum, entries[^1].MinHitpoints);
            }
        }
        AssertFineRainSourceSetterOrder(casterSent, PlayerUid,
            [(8_000U, 3_000U), (8_477U, 3_000U), (8_477U, 3_000U), (8_477U, 8_477U)]);
        AssertFineRainSourceSetterOrder(memberSent, TargetPlayerUid,
            [(7_000U, 2_000U), (7_477U, 2_000U), (7_477U, 2_000U), (7_477U, 7_477U)]);

        static void AssertFineRainSourceSetterOrder(IEnumerable<byte[]> frames, uint uid,
            (uint Maximum, uint Hitpoints)[] phases)
        {
            var timeline = frames.Where(bytes =>
                (LegacyTeamMemberInfoPacket.TryParse(ReadFrame(bytes), out var team) &&
                    team.Count == 1 && team.Entries[0].Uid == uid) ||
                (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var hp) && hp.Uid == uid &&
                    hp.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)))
                .ToArray();
            Assert.Equal(11, timeline.Length); // 3 self-recipient team packets +4 exact two-frame HP pairs.
            var offset = 0;
            for (var index = 0; index < phases.Length; index++)
            {
                var phase = phases[index];
                if (index != 2)
                {
                    Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(timeline[offset++]), out var team));
                    var entry = Assert.Single(team.Entries);
                    Assert.Equal(uid, entry.Uid);
                    Assert.Equal(phase.Maximum, entry.MaxHitpoints);
                    Assert.Equal(phase.Hitpoints, entry.MinHitpoints);
                }
                Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(timeline[offset++]), out var maximum));
                Assert.Equal(uid, maximum.Uid);
                var maximumEntry = Assert.Single(maximum.Updates);
                Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximumEntry.Type);
                Assert.Equal((ulong)phase.Maximum, maximumEntry.Value);
                Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(timeline[offset++]), out var current));
                Assert.Equal(uid, current.Uid);
                Assert.Collection(current.Updates,
                    max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal((ulong)phase.Maximum, max.Value); },
                    hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal((ulong)phase.Hitpoints, hp.Value); });
            }
            Assert.Equal(timeline.Length, offset);
        }

        var spell = Assert.Single(ParseSpellAnimations(casterSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(848, spell.X);
        Assert.Equal(593, spell.Y);
        Assert.Equal(FineRainAttachStatusSpellId, spell.SpellId);
        Assert.Collection(
            spell.Targets.OrderBy(target => target.Uid),
            target =>
            {
                Assert.Equal(PlayerUid, target.Uid);
                Assert.Equal(0U, target.Damage);
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            },
            target =>
            {
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.Equal(0U, target.Damage);
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        Assert.DoesNotContain(
            casterSnapshot,
            frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy FineRain AttachStatus magic", output);
        Assert.Contains("statusFlags=212/213", output);
        Assert.Contains("fineRainPower=477", output);
        Assert.Contains("teamMembersApplied=1", output);
        Assert.Contains("teamMembersOutOfRange=1", output);
        Assert.Contains("stoppedOnDeadMember=True", output);
        Assert.Contains("source=AttachStatus.cs case 14500", output);
        Assert.DoesNotContain("appliedExperience=500", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresFineRain14500FlagsAndPowerAttributes()
    {
        using var log = new FineRainExpiryLog();
        using var cancel = new CancellationTokenSource();
        var sent = new System.Collections.Concurrent.ConcurrentQueue<byte[]>();
        var connection = new WolfConquer.Core.Networking.ConquerTcpConnection(null,
            new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 9958),
            sendAsync: (memory, _) =>
            {
                sent.Enqueue(memory.ToArray());
                return ValueTask.CompletedTask;
            });
        var now = DateTimeOffset.UtcNow;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(10))),
            log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var session = CreateSession(
            spellIds: [FineRainAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(FineRain1StatusFlag, FineRain2StatusFlag),
                ["FineRainPower"] = "477",
                ["defFineRainPower"] = "477",
                ["LegacyFineRain1ExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyFineRain2ExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyFineRain1RemoveOnDead"] = "true",
                ["LegacyFineRain2RemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            // Legacy synthetic expired state; new public-acquisition facts cover literal HP phases.
            FineRainExpiryStartGlobal(handler, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, FineRain1StatusFlag) &&
                        !HasLegacyStatusFlag(current.Character.Attributes, FineRain2StatusFlag) &&
                        current.Character.Attributes.TryGetValue("FineRainPower", out var power) && power == "477" &&
                        current.Character.Attributes.TryGetValue("defFineRainPower", out var defaultPower) && defaultPower == "477" &&
                        !current.Character.Attributes.ContainsKey("LegacyFineRain1ExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyFineRain2ExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyFineRain1RemoveOnDead") &&
                        !current.Character.Attributes.ContainsKey("LegacyFineRain2RemoveOnDead") &&
                        sent.ToArray().Count(bytes => LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(bytes), out _)) == 2 &&
                        log.ToString().Contains("WorldServer expired legacy FineRain flags", StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(1));

            // State removal alone precedes awaited query publication. Drain the
            // actual global owner before reading the strict frame sequence.
            await cancel.CancelAsync();
            await WaitUntilAsync(() => FineRainExpiryGlobalStopped(handler), TimeSpan.FromSeconds(1));
            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, FineRain1StatusFlag));
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, FineRain2StatusFlag));
            Assert.Equal("477", expiredSession.Character.Attributes["FineRainPower"]);
            Assert.Equal("477", expiredSession.Character.Attributes["defFineRainPower"]);
            var frames = sent.ToArray();
            var vectors = ParseMsgUpdates(frames).Where(update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag)).ToArray();
            Assert.Equal(2, vectors.Length); // One RemoveFlag publication per expired registration.
            var first = Assert.Single(vectors[0].Updates).Values!;
            Assert.NotEqual(IsLegacyStatusFlagSet(first, FineRain1StatusFlag),
                IsLegacyStatusFlagSet(first, FineRain2StatusFlag));
            var last = Assert.Single(vectors[1].Updates).Values!;
            Assert.False(IsLegacyStatusFlagSet(last, FineRain1StatusFlag));
            Assert.False(IsLegacyStatusFlagSet(last, FineRain2StatusFlag));
            Assert.Equal(2, frames.Count(bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _)));
            Assert.Equal(2, frames.Count(bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _)));
            Assert.Equal(2, frames.Count(bytes => LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(bytes), out _)));
            Assert.Contains("WorldServer expired legacy FineRain flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
            await WaitUntilAsync(() => FineRainExpiryGlobalStopped(handler), TimeSpan.FromSeconds(1));
        }
    }

    private static void AssertFineRainApplied(
        WorldSession session,
        uint fineRainPower,
        DateTimeOffset beforeCast,
        DateTimeOffset afterCast,
        uint duration)
    {
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, FineRain1StatusFlag));
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, FineRain2StatusFlag));
        Assert.Equal(fineRainPower.ToString(), session.Character.Attributes["FineRainPower"]);
        Assert.Equal(fineRainPower.ToString(), session.Character.Attributes["defFineRainPower"]);
        Assert.Equal("true", session.Character.Attributes["LegacyFineRain1RemoveOnDead"]);
        Assert.Equal("true", session.Character.Attributes["LegacyFineRain2RemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            session.Character.Attributes,
            "LegacyFineRain1ExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)duration));
        AssertLegacyXpSkillExpiresAround(
            session.Character.Attributes,
            "LegacyFineRain2ExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)duration));
    }

    private static void AssertFineRainNotApplied(WorldSession session, long expectedHitpoints)
    {
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, FineRain1StatusFlag));
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, FineRain2StatusFlag));
        Assert.False(session.Character.Attributes.ContainsKey("FineRainPower"));
        Assert.False(session.Character.Attributes.ContainsKey("defFineRainPower"));
        Assert.Equal(expectedHitpoints, session.Character.MinHitPoints);
    }

    private static void AssertFineRainStatusAndCountdowns(
        IEnumerable<byte[]> frames,
        uint uid,
        uint fineRainPower,
        uint duration)
    {
        var updates = ParseMsgUpdates(frames);
        Assert.Contains(updates, update => update.Uid == uid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, FineRain1StatusFlag) &&
                IsLegacyStatusFlagSet(entry.Values, FineRain2StatusFlag)));

        foreach (var flag in new[] { FineRain1StatusFlag, FineRain2StatusFlag })
        {
            Assert.Contains(updates.SelectMany(update => update.Updates), entry =>
                entry.Type == LegacyMsgUpdateDataType.FineRain &&
                entry.Flag == (uint)flag &&
                entry.Time == duration &&
                entry.Damage == fineRainPower &&
                entry.SpellLevel == fineRainPower);
        }
    }

    private static void AssertFineRainHitpoints(IEnumerable<byte[]> frames, uint uid, uint hitpoints)
    {
        Assert.Contains(ParseMsgUpdates(frames), update => update.Uid == uid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                entry.Value == hitpoints));
    }

    private static IReadOnlyList<LegacyTeamMemberInfoPacket> ParseFineRainTeamMemberInfos(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyTeamMemberInfoPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyTeamMemberInfoPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
