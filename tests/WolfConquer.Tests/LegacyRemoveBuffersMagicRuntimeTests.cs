using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort RemoveBuffersSerenitySpellId = 10_400;
    private const ushort RemoveBuffersTranquilitySpellId = 10_425;
    private const ushort RemoveBuffersCompassionSpellId = 10_430;
    private const ushort RemoveBuffersGraceOfHeavenSpellId = 12_560;
    private const int RemoveBuffersSoulShackleFlag = 0x6F;
    private const int RemoveBuffersPoisonedFlag = 1;
    private const int RemoveBuffersPoisonStarFlag = 0x77;
    private const int RemoveBuffersChillingSnowFlag = 0xC5;
    private const int RemoveBuffersUnrelatedFlag = 0xA0;

    [Fact]
    public void LegacyMagicRuntimeResolvesExactRemoveBuffersRowsCadenceAndCapabilities()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateRemoveBuffersRows());
        var expected = new[]
        {
            (RemoveBuffersSerenitySpellId, Sort: (byte)46, Name: "Serenity", Mana: (ushort)50, Stamina: (byte)50,
                ColdTime: 3_000, Duration: 10U, MaxTargets: (byte)0, Range: (byte)0, NeedLevel: (ushort)40,
                Damage: 0U, DamageOnMonster: 0U, SortName: "RemoveBuffers(46)"),
            (RemoveBuffersTranquilitySpellId, Sort: (byte)47, Name: "Tranquility", Mana: (ushort)100, Stamina: (byte)25,
                ColdTime: 1_500, Duration: 2U, MaxTargets: (byte)12, Range: (byte)20, NeedLevel: (ushort)70,
                Damage: 100U, DamageOnMonster: 10U, SortName: "Tranquility(47)"),
            (RemoveBuffersCompassionSpellId, Sort: (byte)50, Name: "Compassion", Mana: (ushort)250, Stamina: (byte)75,
                ColdTime: 3_000, Duration: 0U, MaxTargets: (byte)20, Range: (byte)20, NeedLevel: (ushort)100,
                Damage: 0U, DamageOnMonster: 0U, SortName: "Compasion(50)"),
        };
        var cadenceMethod = typeof(WorldPacketHandlers).GetMethod(
            "TryGetLegacyPlayerMagicAttackStampCadence",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(cadenceMethod);

        foreach (var row in expected)
        {
            Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, row.Item1, 0, out var spell));
            Assert.Equal((LegacyMagicSort)row.Sort, spell.Sort);
            Assert.Equal(row.Name, spell.Name);
            Assert.Equal(row.Mana, spell.UseMana);
            Assert.Equal(row.Stamina, spell.UseStamina);
            Assert.Equal(0, spell.CoolDown);
            Assert.Equal(row.ColdTime, spell.ColdTime);
            Assert.Equal(row.Duration, spell.Duration);
            Assert.Equal(row.MaxTargets, spell.MaxTargets);
            Assert.Equal(row.Range, spell.Range);
            Assert.Equal(row.NeedLevel, spell.NeedLevel);
            Assert.Equal(row.Damage, spell.RawDamage);
            Assert.Equal(row.DamageOnMonster, spell.DamageOnMonster);

            object?[] cadenceArguments = [spell, null];
            Assert.True(Assert.IsType<bool>(cadenceMethod.Invoke(null, cadenceArguments)));
            Assert.Equal(TimeSpan.FromMilliseconds(500), Assert.IsType<TimeSpan>(cadenceArguments[1]));

            var capability = LegacyMagicSortCapabilities.Classify(spell);
            Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, capability.Capability);
            Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, capability.Status);
            Assert.True(capability.Supported);
            Assert.Equal(row.SortName, capability.SortName);
            Assert.Equal("remove-buffers-runtime", capability.NextSeam);

            var adjacent = LegacyMagicSortCapabilities.Classify(spell.Sort, checked((ushort)(spell.Id + 1)));
            Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, adjacent.Capability);
            Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, adjacent.Status);
            Assert.False(adjacent.Supported);
        }
    }

    [Fact]
    public async Task HandlerSerenityArmsFiveHundredMillisecondAttackStampBeforeResourceValidation()
    {
        var result = await RunRemoveBuffersCastAsync(
            RemoveBuffersSerenitySpellId,
            opponentUid: 0,
            casterStamina: 49,
            repeatImmediately: true);
        var caster = GetRemoveBuffersCaster(result);

        Assert.Equal(500, caster.Character.MinMana);
        Assert.Equal("49", caster.Character.Attributes["Stamina"]);
        Assert.True(caster.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.Null(caster.MagicCooldowns);
        Assert.Empty(ParseSpellAnimations(result.CasterSent));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        Assert.Empty(ParseMagicColdTimePackets(result.CasterSent));
        Assert.Equal(1, CountText(result.Log, "requiredStamina=50"));
        Assert.Equal(1, CountText(result.Log, "attackStampRemainingMs="));
    }

    [Theory]
    [InlineData("self")]
    [InlineData("visible")]
    [InlineData("unknown")]
    public async Task HandlerSerenityCleansExactTargetOrSendsEmptyAnimationThenProgress(string targetMode)
    {
        var isSelf = targetMode == "self";
        var isVisible = targetMode == "visible";
        var peer = new RemoveBuffersPeer(
            TargetPlayerUid,
            "VisibleTarget",
            831,
            584,
            1_015,
            JoinTeam: false,
            CreateRemoveBuffersStatusAttributes());
        var result = await RunRemoveBuffersCastAsync(
            RemoveBuffersSerenitySpellId,
            isSelf ? PlayerUid : isVisible ? TargetPlayerUid : 99_999_999,
            casterAttributes: isSelf ? CreateRemoveBuffersStatusAttributes() : null,
            peers: isVisible ? [peer] : []);
        var affected = isSelf
            ? GetRemoveBuffersCaster(result)
            : isVisible
                ? GetRemoveBuffersPeer(result, TargetPlayerUid)
                : null;

        if (affected is not null)
        {
            AssertRemoveBuffersCleaned(affected, removeSoulShackle: true, removeChillingSnow: false);
            AssertRemoveBuffersStatusPublished(
                isSelf ? result.CasterSent : result.Peers[TargetPlayerUid].Sent,
                checked((uint)affected.Character.Uid),
                soulShacklePresent: false,
                chillingSnowPresent: true);
        }

        AssertRemoveBuffersResources(result, RemoveBuffersSerenitySpellId, expectedMana: 450, expectedStamina: 50, coldTime: 3_000);
        AssertRemoveBuffersAnimation(
            Assert.Single(ParseSpellAnimations(result.CasterSent)),
            RemoveBuffersSerenitySpellId,
            affected is null ? [] : [checked((uint)affected.Character.Uid)]);
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        Assert.Contains("progressDamage=1000", result.Log);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandlerTranquilityKeepsSelfChillingSnowButRemovesExternalChillingSnow(bool selfCast)
    {
        var peer = new RemoveBuffersPeer(
            TargetPlayerUid,
            "VisibleTarget",
            831,
            584,
            1_015,
            JoinTeam: false,
            CreateRemoveBuffersStatusAttributes());
        var result = await RunRemoveBuffersCastAsync(
            RemoveBuffersTranquilitySpellId,
            selfCast ? PlayerUid : TargetPlayerUid,
            casterAttributes: selfCast ? CreateRemoveBuffersStatusAttributes() : null,
            peers: selfCast ? [] : [peer]);
        var affected = selfCast
            ? GetRemoveBuffersCaster(result)
            : GetRemoveBuffersPeer(result, TargetPlayerUid);
        var affectedSent = selfCast ? result.CasterSent : result.Peers[TargetPlayerUid].Sent;

        AssertRemoveBuffersCleaned(
            affected,
            removeSoulShackle: true,
            removeChillingSnow: !selfCast);
        AssertRemoveBuffersStatusPublished(
            affectedSent,
            checked((uint)affected.Character.Uid),
            soulShacklePresent: false,
            chillingSnowPresent: selfCast);
        AssertRemoveBuffersResources(result, RemoveBuffersTranquilitySpellId, expectedMana: 400, expectedStamina: 75, coldTime: 1_500);
        AssertRemoveBuffersAnimation(
            Assert.Single(ParseSpellAnimations(result.CasterSent)),
            RemoveBuffersTranquilitySpellId,
            [checked((uint)affected.Character.Uid)]);
        Assert.Contains("baseProgressDamage=1000", result.Log);
    }

    [Fact]
    public async Task HandlerTranquilityGraceIncludesCasterAndCrossMapMemberButExcludesDistanceEighteen()
    {
        var peers = new[]
        {
            new RemoveBuffersPeer(TargetPlayerUid, "Near", 831, 584, 1_015, true, CreateRemoveBuffersStatusAttributes()),
            new RemoveBuffersPeer(TargetPlayerUid + 1, "Boundary", 848, 584, 1_015, true, CreateRemoveBuffersStatusAttributes()),
            new RemoveBuffersPeer(TargetPlayerUid + 2, "CrossMap", 830, 584, 1_002, true, CreateRemoveBuffersStatusAttributes()),
        };
        var result = await RunRemoveBuffersCastAsync(
            RemoveBuffersTranquilitySpellId,
            opponentUid: 99_999_999,
            casterAttributes: CreateRemoveBuffersStatusAttributes(),
            peers: peers,
            createTeam: true,
            learnGraceOfHeaven: true);

        foreach (var uid in new[] { PlayerUid, TargetPlayerUid, TargetPlayerUid + 2 })
        {
            var session = uid == PlayerUid ? GetRemoveBuffersCaster(result) : GetRemoveBuffersPeer(result, uid);
            var sent = uid == PlayerUid ? result.CasterSent : result.Peers[uid].Sent;
            AssertRemoveBuffersCleaned(session, removeSoulShackle: true, removeChillingSnow: false);
            AssertRemoveBuffersStatusPublished(sent, uid, soulShacklePresent: false, chillingSnowPresent: true);
        }

        AssertRemoveBuffersUntouched(GetRemoveBuffersPeer(result, TargetPlayerUid + 1));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Peers[TargetPlayerUid + 1].Sent),
            update => update.Uid == TargetPlayerUid + 1 &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));

        var animations = ParseSpellAnimations(result.CasterSent);
        Assert.Equal(2, animations.Count);
        AssertRemoveBuffersAnimation(animations[0], RemoveBuffersTranquilitySpellId, []);
        AssertRemoveBuffersAnimation(
            animations[1],
            RemoveBuffersGraceOfHeavenSpellId,
            [PlayerUid, TargetPlayerUid, TargetPlayerUid + 2]);
        var graceProgress = Assert.Single(ParseLegacySpellPackets(result.CasterSent));
        Assert.Equal(RemoveBuffersGraceOfHeavenSpellId, graceProgress.Id);
        Assert.Equal(1_000, graceProgress.Experience);

        var baseAnimationIndex = RemoveBuffersFrameIndex(result.CasterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == RemoveBuffersTranquilitySpellId);
        var casterStatusIndex = RemoveBuffersFrameIndex(result.CasterSent, frame =>
            IsRemoveBuffersStatusVector(frame, PlayerUid, soulShacklePresent: false, chillingSnowPresent: true));
        var graceAnimationIndex = RemoveBuffersFrameIndex(result.CasterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == RemoveBuffersGraceOfHeavenSpellId);
        var graceProgressIndex = RemoveBuffersFrameIndex(result.CasterSent, frame =>
            LegacySpellPacket.TryParse(frame, out var packet) && packet.Id == RemoveBuffersGraceOfHeavenSpellId);
        Assert.True(baseAnimationIndex < casterStatusIndex);
        Assert.True(casterStatusIndex < graceAnimationIndex);
        Assert.True(graceAnimationIndex < graceProgressIndex);
        Assert.Contains("graceFollowUp=True", result.Log);
        Assert.Contains("baseProgressDamage=1000", result.Log);
        Assert.Contains("graceProgressDamage=1000", result.Log);
    }

    [Fact]
    public async Task HandlerCompassionIncludesCasterAndCrossMapMemberAndNoTeamStillAnimatesBeforeProgress()
    {
        var peers = new[]
        {
            new RemoveBuffersPeer(TargetPlayerUid, "Near", 831, 584, 1_015, true, CreateRemoveBuffersStatusAttributes()),
            new RemoveBuffersPeer(TargetPlayerUid + 1, "Boundary", 848, 584, 1_015, true, CreateRemoveBuffersStatusAttributes()),
            new RemoveBuffersPeer(TargetPlayerUid + 2, "CrossMap", 830, 584, 1_002, true, CreateRemoveBuffersStatusAttributes()),
        };
        var result = await RunRemoveBuffersCastAsync(
            RemoveBuffersCompassionSpellId,
            opponentUid: 0,
            casterAttributes: CreateRemoveBuffersStatusAttributes(),
            peers: peers,
            createTeam: true);

        foreach (var uid in new[] { PlayerUid, TargetPlayerUid, TargetPlayerUid + 2 })
        {
            var session = uid == PlayerUid ? GetRemoveBuffersCaster(result) : GetRemoveBuffersPeer(result, uid);
            var sent = uid == PlayerUid ? result.CasterSent : result.Peers[uid].Sent;
            AssertRemoveBuffersCleaned(session, removeSoulShackle: false, removeChillingSnow: false);
            AssertRemoveBuffersStatusPublished(sent, uid, soulShacklePresent: true, chillingSnowPresent: true);
        }

        AssertRemoveBuffersUntouched(GetRemoveBuffersPeer(result, TargetPlayerUid + 1));
        var statusIndex = RemoveBuffersFrameIndex(result.CasterSent, frame =>
            IsRemoveBuffersStatusVector(frame, PlayerUid, soulShacklePresent: true, chillingSnowPresent: true));
        var animationIndex = RemoveBuffersFrameIndex(result.CasterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == RemoveBuffersCompassionSpellId);
        Assert.True(statusIndex < animationIndex);
        AssertRemoveBuffersAnimation(
            Assert.Single(ParseSpellAnimations(result.CasterSent)),
            RemoveBuffersCompassionSpellId,
            [PlayerUid, TargetPlayerUid, TargetPlayerUid + 2]);
        AssertRemoveBuffersResources(result, RemoveBuffersCompassionSpellId, expectedMana: 250, expectedStamina: 25, coldTime: 3_000);
        Assert.Contains("progressDamage=1000", result.Log);

        var noTeam = await RunRemoveBuffersCastAsync(RemoveBuffersCompassionSpellId, opponentUid: 0);
        AssertRemoveBuffersAnimation(
            Assert.Single(ParseSpellAnimations(noTeam.CasterSent)),
            RemoveBuffersCompassionSpellId,
            []);
        Assert.Empty(ParseLegacySpellPackets(noTeam.CasterSent));
        Assert.Contains("progressDamage=1000", noTeam.Log);
    }

    private static async Task<RemoveBuffersCastResult> RunRemoveBuffersCastAsync(
        ushort spellId,
        uint opponentUid,
        IReadOnlyDictionary<string, string>? casterAttributes = null,
        IReadOnlyList<RemoveBuffersPeer>? peers = null,
        bool createTeam = false,
        bool learnGraceOfHeaven = false,
        ulong casterStamina = 100,
        bool repeatImmediately = false)
    {
        peers ??= [];
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [], CreateRemoveBuffersRows());
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            minMana: 500,
            stamina: casterStamina,
            spellIds: learnGraceOfHeaven ? [spellId, RemoveBuffersGraceOfHeavenSpellId] : [spellId],
            attributes: casterAttributes);
        sessions.Set(casterConnection, casterSession);
        var peerRuntime = new Dictionary<uint, RemoveBuffersPeerRuntime>();
        foreach (var peer in peers)
        {
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);
            sessions.Set(
                connection,
                CreateSession(
                    uid: peer.Uid,
                    name: peer.Name,
                    x: peer.X,
                    y: peer.Y,
                    mapId: peer.MapId,
                    attributes: peer.Attributes));
            peerRuntime.Add(peer.Uid, new RemoveBuffersPeerRuntime(connection, sent));
        }

        if (createTeam)
        {
            teamStates.CreateTeam(casterConnection, PlayerUid);
            foreach (var peer in peers.Where(peer => peer.JoinTeam))
            {
                Assert.True(teamStates.TryAddMember(casterConnection, peerRuntime[peer.Uid].Connection, peer.Uid));
            }
        }

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates);
        var frame = ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
            X: 840,
            Y: 590,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId)).ToFrameBytes());
        await PacketHandlers.HandleAsync(context, frame);
        if (repeatImmediately)
        {
            Assert.True(sessions.TryGet(casterConnection, out var current));
            await PacketHandlers.HandleAsync(context with { Session = current }, frame);
        }

        return new RemoveBuffersCastResult(sessions, casterConnection, casterSent, peerRuntime, log.ToString());
    }

    private static MagicType[] CreateRemoveBuffersRows()
    {
        return
        [
            ParseRemoveBuffersRow("10400@@10400@@46@@Serenity@@0@@0@@0@@4@@0@@50@@0@@0@@100@@10@@0@@0@@0@@0@@0@@0@@40@@0@@0@@0@@0@@0@@0@@0@@0@@50@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@3000@@0@@0@@0@@0@@0@@"),
            ParseRemoveBuffersRow("10425@@10425@@47@@Tranquility@@0@@0@@0@@0@@0@@100@@100@@0@@100@@2@@12@@20@@0@@0@@0@@0@@70@@0@@0@@0@@0@@0@@0@@0@@0@@25@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@10@@0@@0@@1500@@0@@0@@0@@0@@0@@"),
            ParseRemoveBuffersRow("10430@@10430@@50@@Compassion@@0@@0@@1@@2@@0@@250@@0@@0@@100@@0@@20@@20@@0@@0@@0@@0@@100@@0@@0@@0@@0@@0@@0@@0@@0@@75@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@3000@@0@@0@@0@@0@@0@@"),
            ParseRemoveBuffersRow("12560@@12560@@0@@GraceofHeaven@@0@@0@@0@@8@@0@@0@@10@@0@@0@@0@@0@@0@@0@@0@@5000@@1666@@50@@0@@61622622@@0@@0@@0@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@3000@@0@@0@@0@@0@@"),
            ParseRemoveBuffersRow("12561@@12560@@0@@GraceofHeaven@@0@@0@@0@@8@@1@@0@@15@@0@@0@@0@@0@@0@@0@@0@@10000@@3333@@60@@0@@61622622@@0@@0@@0@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@5000@@0@@0@@0@@0@@"),
        ];
    }

    private static MagicType ParseRemoveBuffersRow(string raw)
    {
        var fields = raw.Split("@@", StringSplitOptions.None);
        return new MagicType(
            int.Parse(fields[0], CultureInfo.InvariantCulture),
            int.Parse(fields[1], CultureInfo.InvariantCulture),
            fields[3],
            fields);
    }

    private static Dictionary<string, string> CreateRemoveBuffersStatusAttributes()
    {
        return new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(
                RemoveBuffersSoulShackleFlag,
                RemoveBuffersPoisonedFlag,
                RemoveBuffersPoisonStarFlag,
                RemoveBuffersChillingSnowFlag,
                RemoveBuffersUnrelatedFlag),
            ["LegacySoulShackleExpiresAtUtc"] = "2026-07-18T20:00:00.0000000+00:00",
            ["LegacySoulShackleRemoveOnDead"] = "true",
            ["LegacyPoisonStarExpiresAtUtc"] = "2026-07-18T20:00:00.0000000+00:00",
            ["LegacyPoisonStarRemoveOnDead"] = "true",
            ["PoisonLevel"] = "9",
            ["LegacyPoisonedExpiresAtUtc"] = "2026-07-18T20:00:00.0000000+00:00",
            ["LegacyPoisonExpiresAtUtc"] = "2026-07-18T20:00:00.0000000+00:00",
            ["LegacyToxicPoisonExpiresAtUtc"] = "2026-07-18T20:00:00.0000000+00:00",
            ["LegacyToxicFogPoisonExpiresAtUtc"] = "2026-07-18T20:00:00.0000000+00:00",
            ["RemoveBuffersUnrelatedMetadata"] = "keep",
        };
    }

    private static void AssertRemoveBuffersCleaned(
        WorldSession session,
        bool removeSoulShackle,
        bool removeChillingSnow)
    {
        Assert.Equal(!removeSoulShackle, HasLegacyStatusFlag(session.Character.Attributes, RemoveBuffersSoulShackleFlag));
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, RemoveBuffersPoisonedFlag));
        Assert.False(HasLegacyStatusFlag(session.Character.Attributes, RemoveBuffersPoisonStarFlag));
        Assert.Equal(!removeChillingSnow, HasLegacyStatusFlag(session.Character.Attributes, RemoveBuffersChillingSnowFlag));
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, RemoveBuffersUnrelatedFlag));
        Assert.Equal("keep", session.Character.Attributes["RemoveBuffersUnrelatedMetadata"]);

        foreach (var key in new[]
        {
            "PoisonLevel",
            "LegacyPoisonedExpiresAtUtc",
            "LegacyPoisonExpiresAtUtc",
            "LegacyToxicPoisonExpiresAtUtc",
            "LegacyToxicFogPoisonExpiresAtUtc",
            "LegacyPoisonStarExpiresAtUtc",
            "LegacyPoisonStarRemoveOnDead",
        })
        {
            Assert.False(session.Character.Attributes.ContainsKey(key));
        }

        foreach (var key in new[] { "LegacySoulShackleExpiresAtUtc", "LegacySoulShackleRemoveOnDead" })
        {
            Assert.Equal(!removeSoulShackle, session.Character.Attributes.ContainsKey(key));
        }
    }

    private static void AssertRemoveBuffersUntouched(WorldSession session)
    {
        foreach (var flag in new[]
        {
            RemoveBuffersSoulShackleFlag,
            RemoveBuffersPoisonedFlag,
            RemoveBuffersPoisonStarFlag,
            RemoveBuffersChillingSnowFlag,
            RemoveBuffersUnrelatedFlag,
        })
        {
            Assert.True(HasLegacyStatusFlag(session.Character.Attributes, flag));
        }

        foreach (var pair in CreateRemoveBuffersStatusAttributes())
        {
            Assert.Equal(pair.Value, session.Character.Attributes[pair.Key]);
        }
    }

    private static void AssertRemoveBuffersStatusPublished(
        IEnumerable<byte[]> sent,
        uint uid,
        bool soulShacklePresent,
        bool chillingSnowPresent)
    {
        Assert.Contains(
            sent,
            bytes => IsRemoveBuffersStatusVector(
                ReadFrame(bytes),
                uid,
                soulShacklePresent,
                chillingSnowPresent));
    }

    private static bool IsRemoveBuffersStatusVector(
        PacketFrame frame,
        uint uid,
        bool soulShacklePresent,
        bool chillingSnowPresent)
    {
        return LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == uid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, RemoveBuffersSoulShackleFlag) == soulShacklePresent &&
                !IsLegacyStatusFlagSet(entry.Values, RemoveBuffersPoisonedFlag) &&
                !IsLegacyStatusFlagSet(entry.Values, RemoveBuffersPoisonStarFlag) &&
                IsLegacyStatusFlagSet(entry.Values, RemoveBuffersChillingSnowFlag) == chillingSnowPresent &&
                IsLegacyStatusFlagSet(entry.Values, RemoveBuffersUnrelatedFlag));
    }

    private static void AssertRemoveBuffersResources(
        RemoveBuffersCastResult result,
        ushort spellId,
        long expectedMana,
        ulong expectedStamina,
        int coldTime)
    {
        var caster = GetRemoveBuffersCaster(result);
        Assert.Equal(expectedMana, caster.Character.MinMana);
        Assert.Equal(expectedStamina.ToString(CultureInfo.InvariantCulture), caster.Character.Attributes["Stamina"]);
        Assert.True(caster.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));
        Assert.True(caster.MagicCooldowns?.ContainsKey(spellId) == true);
        Assert.Equal(
            [new LegacyMagicColdTimeSpell(spellId, coldTime)],
            Assert.Single(ParseMagicColdTimePackets(result.CasterSent)).Spells);
    }

    private static void AssertRemoveBuffersAnimation(
        LegacySpellAnimationPacket animation,
        ushort spellId,
        IReadOnlyList<uint> expectedTargets)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(840, animation.X);
        Assert.Equal(590, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal(expectedTargets.OrderBy(uid => uid), animation.Targets.Select(target => target.Uid).OrderBy(uid => uid));
        Assert.All(animation.Targets, target =>
        {
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });
    }

    private static WorldSession GetRemoveBuffersCaster(RemoveBuffersCastResult result)
    {
        Assert.True(result.Sessions.TryGet(result.CasterConnection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static WorldSession GetRemoveBuffersPeer(RemoveBuffersCastResult result, uint uid)
    {
        Assert.True(result.Sessions.TryGet(result.Peers[uid].Connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static int RemoveBuffersFrameIndex(IReadOnlyList<byte[]> frames, Func<PacketFrame, bool> predicate)
    {
        return Assert.Single(Enumerable.Range(0, frames.Count), index => predicate(ReadFrame(frames[index])));
    }

    private static int CountText(string text, string value)
    {
        return text.Split(value, StringSplitOptions.None).Length - 1;
    }

    private sealed record RemoveBuffersPeer(
        uint Uid,
        string Name,
        long X,
        long Y,
        long MapId,
        bool JoinTeam,
        IReadOnlyDictionary<string, string> Attributes);

    private sealed record RemoveBuffersPeerRuntime(ConquerTcpConnection Connection, List<byte[]> Sent);

    private sealed record RemoveBuffersCastResult(
        WorldSessionStore Sessions,
        ConquerTcpConnection CasterConnection,
        List<byte[]> CasterSent,
        IReadOnlyDictionary<uint, RemoveBuffersPeerRuntime> Peers,
        string Log);
}
