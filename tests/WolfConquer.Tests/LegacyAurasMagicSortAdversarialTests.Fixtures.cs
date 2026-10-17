using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const LegacyMagicSort AurasMagicSort = (LegacyMagicSort)51;

    private const ushort AuraTyrantSpellId = 10_395;
    private const ushort AuraFeandSpellId = 10_410;
    private const ushort AuraMetalSpellId = 10_420;
    private const ushort AuraWoodSpellId = 10_421;
    private const ushort AuraWaterSpellId = 10_422;
    private const ushort AuraFireSpellId = 10_423;
    private const ushort AuraEarthSpellId = 10_424;
    private const ushort AuraMagicDefenderSpellId = 11_200;

    private const int AuraRideFlag = 0x32;
    private const int AuraTyrantFlag = 0x62;
    private const int AuraFeandFlag = 0x64;
    private const int AuraMetalFlag = 0x66;
    private const int AuraWoodFlag = 0x68;
    private const int AuraWaterFlag = 0x6A;
    private const int AuraFireFlag = 0x6C;
    private const int AuraEarthFlag = 0x6E;
    private const int AuraMagicDefenderFlag = 0x80;
    private const int AttachStatusAzureShieldFlag = 0x5D;
    private const int AuraDeadFlag = 0x05;
    private const int AuraUnrelatedDefensiveStanceFlag = 0x7E;

    private const byte AuraLearnedLevel = 2;
    private const uint AuraDamage = 7;
    private const uint AuraGDamage = 321;
    private const int AuraProgressDamage = 200;
    private const int AuraPrimaryDurationSeconds = 2_592_000;
    private const int AuraTeamDurationSeconds = 30;
    private const int AuraMagicDefenderDurationSeconds = 90;
    private const uint AuraUnknownTargetUid = 99_999_999;

    private const string RuntimeAuraStatusFlagAttribute = "LegacyAuraStatusFlag";
    private const string RuntimeAuraSpellIdAttribute = "LegacyAuraSpellId";
    private const string RuntimeAuraLevelAttribute = "LegacyAuraLevel";
    private const string RuntimeAuraDamageAttribute = "LegacyAuraDamage";
    private const string RuntimeAuraGDamageAttribute = "LegacyAuraGDamage";
    private const string RuntimeAuraExpiresAtUtcAttribute = "LegacyAuraExpiresAtUtc";
    private const string RuntimeAuraRemoveOnDeadAttribute = "LegacyAuraRemoveOnDead";
    private const string RuntimeAuraMagicDefenderExpiresAtUtcAttribute = "LegacyAuraMagicDefenderExpiresAtUtc";
    private const string RuntimeAuraMagicDefenderRemoveOnDeadAttribute = "LegacyAuraMagicDefenderRemoveOnDead";
    private const string RuntimeAuraRideExpiresAtUtcAttribute = "LegacyRideExpiresAtUtc";
    private const string RuntimeAuraRideRemoveOnDeadAttribute = "LegacyRideRemoveOnDead";
    private const string RuntimeAuraRideStampSecondsAttribute = "LegacyRideStampSeconds";

    private static readonly int[] AuraStatusFlags =
    [
        AuraTyrantFlag,
        AuraFeandFlag,
        AuraMetalFlag,
        AuraWoodFlag,
        AuraWaterFlag,
        AuraFireFlag,
        AuraEarthFlag,
    ];

    private static readonly int[] AuraStatusFieldIndexes =
    [
        LegacyPlayerStatusPacket.CriticalStrikeIndex,
        LegacyPlayerStatusPacket.ImmunityIndex,
        LegacyPlayerStatusPacket.MetalResistanceIndex,
        LegacyPlayerStatusPacket.WoodResistanceIndex,
        LegacyPlayerStatusPacket.WaterResistanceIndex,
        LegacyPlayerStatusPacket.FireResistanceIndex,
        LegacyPlayerStatusPacket.EarthResistanceIndex,
    ];

    [Fact]
    public async Task RuntimeLoopExpiresOrdinaryAuraAndAura11200WithoutInventingIconAnimationOrAzureReset()
    {
        var now = DateTimeOffset.UtcNow;
        var tyrant = GetAuraDescriptor(AuraTyrantSpellId);
        var attributes = CreateActiveAuraAttributes(tyrant, now.AddSeconds(-1));
        attributes["StatusFlags"] = BuildStatusFlags(
            tyrant.StatusFlag,
            AuraMagicDefenderFlag,
            AuraUnrelatedDefensiveStanceFlag);
        attributes[RuntimeAuraMagicDefenderExpiresAtUtcAttribute] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture);
        attributes[RuntimeAuraMagicDefenderRemoveOnDeadAttribute] = bool.TrueString;

        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var worldData = CreateAuraCatalog([AuraTyrantSpellId, AuraMagicDefenderSpellId]);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: worldData,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(1))),
            log: log);
        var session = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreateAuraPlayerSpell(AuraTyrantSpellId),
                CreateAuraPlayerSpell(AuraMagicDefenderSpellId),
            ],
            attributes: attributes);
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);
            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, tyrant.StatusFlag) &&
                        !HasLegacyStatusFlag(current.Character.Attributes, AuraMagicDefenderFlag) &&
                        !HasAuraMetadata(current.Character.Attributes) &&
                        !HasAuraMagicDefenderMetadata(current.Character.Attributes);
                },
                TimeSpan.FromSeconds(1));

            var expired = AssertHandlerSession(handler, connection);
            Assert.True(HasLegacyStatusFlag(expired.Character.Attributes, AuraUnrelatedDefensiveStanceFlag));
            AssertAuraStatusBonus(session, expired, worldData, expected: null, removed: tyrant);
            Assert.Contains(
                ParseMsgUpdates(sent),
                packet => packet.Uid == PlayerUid &&
                    packet.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        !IsLegacyStatusFlagSet(entry.Values, tyrant.StatusFlag) &&
                        !IsLegacyStatusFlagSet(entry.Values, AuraMagicDefenderFlag) &&
                        IsLegacyStatusFlagSet(entry.Values, AuraUnrelatedDefensiveStanceFlag)));
            Assert.Empty(ParseAuraFlagIcons(sent));
            Assert.Empty(ParseSpellAnimations(sent));
            Assert.Empty(ParseLegacySpellPackets(sent));
            Assert.DoesNotContain(
                ParseMsgUpdates(sent).SelectMany(packet => packet.Updates),
                entry => entry.Type == LegacyMsgUpdateDataType.AzureShield);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public void PlayerDeathClearsAuraStatusStatMetadataAndAura11200LifetimeMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var fire = GetAuraDescriptor(AuraFireSpellId);
        var attributes = CreateActiveAuraAttributes(fire, now.AddMinutes(1));
        attributes["StatusFlags"] = BuildStatusFlags(fire.StatusFlag, AuraMagicDefenderFlag);
        attributes[RuntimeAuraMagicDefenderExpiresAtUtcAttribute] = now.AddMinutes(1).ToString("O", CultureInfo.InvariantCulture);
        attributes[RuntimeAuraMagicDefenderRemoveOnDeadAttribute] = bool.TrueString;
        var session = CreateSession(
            spellEntries:
            [
                CreateAuraPlayerSpell(AuraFireSpellId),
                CreateAuraPlayerSpell(AuraMagicDefenderSpellId),
            ],
            attributes: attributes);
        var worldData = CreateAuraCatalog([AuraFireSpellId, AuraMagicDefenderSpellId]);

        var death = LegacyPlayerDeathState.ApplyDeath(
            session.Character,
            now,
            checked((ushort)session.Character.X),
            checked((ushort)session.Character.Y));
        var deadSession = session with { Character = death.Character };

        Assert.False(HasLegacyStatusFlag(deadSession.Character.Attributes, fire.StatusFlag));
        Assert.False(HasLegacyStatusFlag(deadSession.Character.Attributes, AuraMagicDefenderFlag));
        Assert.False(HasAuraMetadata(deadSession.Character.Attributes));
        Assert.False(HasAuraMagicDefenderMetadata(deadSession.Character.Attributes));
        AssertAuraStatusBonus(session, deadSession, worldData, expected: null, removed: fire);
    }

    private static async Task<AuraCastResult> RunAuraCastAsync(
        ushort spellId,
        uint opponentUid,
        IReadOnlyList<ushort>? learnedSpellIds = null,
        IReadOnlyDictionary<string, string>? casterAttributes = null,
        IReadOnlyList<AuraPeer>? peers = null,
        bool createTeam = false,
        long minMana = 500,
        ulong casterStamina = 100,
        ushort useMana = 10,
        byte useStamina = 5,
        int coldTime = 0,
        IReadOnlyDictionary<ushort, DateTimeOffset>? magicCooldowns = null)
    {
        learnedSpellIds ??= [spellId];
        peers ??= [];
        var worldData = CreateAuraCatalog(
            learnedSpellIds,
            useMana,
            useStamina,
            coldTime);
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var casterSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var log = new StringWriter(CultureInfo.InvariantCulture);
        var initialCaster = CreateSession(
            minMana: minMana,
            stamina: casterStamina,
            spellEntries: learnedSpellIds.Select(CreateAuraPlayerSpell).ToArray(),
            attributes: casterAttributes) with
        {
            MagicCooldowns = magicCooldowns,
        };
        sessions.Set(casterConnection, initialCaster);

        var peerRuntime = new Dictionary<uint, AuraPeerRuntime>();
        foreach (var peer in peers)
        {
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);
            var initialPeer = CreateSession(
                uid: peer.Uid,
                name: peer.Name,
                x: peer.X,
                y: peer.Y,
                mapId: peer.MapId,
                spellIds: [],
                attributes: peer.Attributes);
            sessions.Set(connection, initialPeer);
            peer.InitialSession = initialPeer;
            peerRuntime.Add(peer.Uid, new AuraPeerRuntime(connection, sent));
        }

        if (createTeam)
        {
            teamStates.CreateTeam(casterConnection, PlayerUid);
            foreach (var peer in peers.Where(peer => peer.JoinTeam))
            {
                Assert.True(teamStates.TryAddMember(
                    casterConnection,
                    peerRuntime[peer.Uid].Connection,
                    peer.Uid));
            }
        }

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            initialCaster,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates);
        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(CreateAuraAttackPacket(spellId, opponentUid).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        return new AuraCastResult(
            worldData,
            sessions,
            teamStates,
            casterConnection,
            casterSent,
            peerRuntime,
            context,
            initialCaster,
            beforeCast,
            afterCast,
            log);
    }

    private static async Task<AuraRepeatResult> RepeatAuraCastAsync(
        AuraCastResult result,
        ushort spellId,
        uint opponentUid)
    {
        var beforeSession = GetAuraCaster(result);
        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            result.Context with { Session = beforeSession },
            ReadFrame(CreateAuraAttackPacket(spellId, opponentUid).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;
        return new AuraRepeatResult(beforeSession, GetAuraCaster(result), beforeCast, afterCast);
    }

    private static MsgAttackPacket CreateAuraAttackPacket(ushort spellId, uint opponentUid)
    {
        return new MsgAttackPacket(new MsgAttackQuery(
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
            SpellId: spellId));
    }

    private static WorldDataCatalog CreateAuraCatalog(
        IEnumerable<ushort> spellIds,
        ushort useMana = 10,
        byte useStamina = 5,
        int coldTime = 0)
    {
        var rows = spellIds
            .Distinct()
            .SelectMany(spellId => Enumerable.Range(0, 4).Select(level =>
                CreateMagicRow(
                    spellId,
                    sort: (byte)AurasMagicSort,
                    level: checked((byte)level),
                    useMana: useMana,
                    damage: AuraDamage,
                    experience: 10_000,
                    useStamina: useStamina,
                    coolDown: 0,
                    coldTime: coldTime,
                    duration: spellId == AuraMagicDefenderSpellId
                        ? checked((uint)AuraMagicDefenderDurationSeconds)
                        : 0U,
                    gDamage: AuraGDamage)))
            .ToArray();
        return CreateMagicCatalog(CreateMonsterFamily(), [], rows);
    }

    private static PlayerSpellSnapshotEntry CreateAuraPlayerSpell(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: AuraLearnedLevel,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static AuraDescriptor GetAuraDescriptor(ushort spellId)
    {
        return spellId switch
        {
            AuraTyrantSpellId => new AuraDescriptor(
                spellId,
                AuraTyrantFlag,
                LegacyFlagIcon.TyrantAura,
                LegacyPlayerStatusPacket.CriticalStrikeIndex,
                AuraDamage * 100),
            AuraFeandSpellId => new AuraDescriptor(
                spellId,
                AuraFeandFlag,
                LegacyFlagIcon.FeandAura,
                LegacyPlayerStatusPacket.ImmunityIndex,
                AuraDamage * 100),
            AuraMetalSpellId => new AuraDescriptor(
                spellId,
                AuraMetalFlag,
                LegacyFlagIcon.MetalAura,
                LegacyPlayerStatusPacket.MetalResistanceIndex,
                AuraDamage),
            AuraWoodSpellId => new AuraDescriptor(
                spellId,
                AuraWoodFlag,
                LegacyFlagIcon.WoodAura,
                LegacyPlayerStatusPacket.WoodResistanceIndex,
                AuraDamage),
            AuraWaterSpellId => new AuraDescriptor(
                spellId,
                AuraWaterFlag,
                LegacyFlagIcon.WaterAura,
                LegacyPlayerStatusPacket.WaterResistanceIndex,
                AuraDamage),
            AuraFireSpellId => new AuraDescriptor(
                spellId,
                AuraFireFlag,
                LegacyFlagIcon.FireAura,
                LegacyPlayerStatusPacket.FireResistanceIndex,
                AuraDamage),
            AuraEarthSpellId => new AuraDescriptor(
                spellId,
                AuraEarthFlag,
                LegacyFlagIcon.EarthAura,
                LegacyPlayerStatusPacket.EarthResistanceIndex,
                AuraDamage),
            _ => throw new ArgumentOutOfRangeException(nameof(spellId), spellId, "Not an ordinary Auras spell."),
        };
    }

    private static Dictionary<string, string> CreateActiveAuraAttributes(
        AuraDescriptor aura,
        DateTimeOffset expiresAt)
    {
        return new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(aura.StatusFlag),
            [RuntimeAuraStatusFlagAttribute] = aura.StatusFlag.ToString(CultureInfo.InvariantCulture),
            [RuntimeAuraSpellIdAttribute] = aura.SpellId.ToString(CultureInfo.InvariantCulture),
            [RuntimeAuraLevelAttribute] = AuraLearnedLevel.ToString(CultureInfo.InvariantCulture),
            [RuntimeAuraDamageAttribute] = AuraDamage.ToString(CultureInfo.InvariantCulture),
            [RuntimeAuraGDamageAttribute] = AuraGDamage.ToString(CultureInfo.InvariantCulture),
            [RuntimeAuraExpiresAtUtcAttribute] = expiresAt.ToString("O", CultureInfo.InvariantCulture),
            [RuntimeAuraRemoveOnDeadAttribute] = bool.TrueString,
        };
    }

    private static WorldSession GetAuraCaster(AuraCastResult result)
    {
        Assert.True(result.Sessions.TryGet(result.CasterConnection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static WorldSession GetAuraPeer(AuraCastResult result, uint uid)
    {
        var peer = result.Peers[uid];
        Assert.True(result.Sessions.TryGet(peer.Connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static void ClearAuraSent(AuraCastResult result)
    {
        result.CasterSent.Clear();
        foreach (var peer in result.Peers.Values)
        {
            peer.Sent.Clear();
        }
    }

    private static void AssertActiveAura(
        WorldSession session,
        AuraDescriptor aura,
        DateTimeOffset beforeApply,
        DateTimeOffset afterApply,
        int durationSeconds)
    {
        var attributes = session.Character.Attributes;
        Assert.True(HasLegacyStatusFlag(attributes, aura.StatusFlag));
        foreach (var other in AuraStatusFlags.Where(flag => flag != aura.StatusFlag))
        {
            Assert.False(HasLegacyStatusFlag(attributes, other));
        }

        Assert.Equal(aura.StatusFlag.ToString(CultureInfo.InvariantCulture), attributes[RuntimeAuraStatusFlagAttribute]);
        Assert.Equal(aura.SpellId.ToString(CultureInfo.InvariantCulture), attributes[RuntimeAuraSpellIdAttribute]);
        Assert.Equal(AuraLearnedLevel.ToString(CultureInfo.InvariantCulture), attributes[RuntimeAuraLevelAttribute]);
        Assert.Equal(AuraDamage.ToString(CultureInfo.InvariantCulture), attributes[RuntimeAuraDamageAttribute]);
        Assert.Equal(AuraGDamage.ToString(CultureInfo.InvariantCulture), attributes[RuntimeAuraGDamageAttribute]);
        Assert.True(
            bool.TryParse(attributes[RuntimeAuraRemoveOnDeadAttribute], out var removeOnDead) &&
            removeOnDead);
        AssertLegacyXpSkillExpiresAround(
            attributes,
            RuntimeAuraExpiresAtUtcAttribute,
            beforeApply,
            afterApply,
            durationSeconds);
    }

    private static void AssertNoActiveAura(WorldSession session)
    {
        foreach (var flag in AuraStatusFlags)
        {
            Assert.False(HasLegacyStatusFlag(session.Character.Attributes, flag));
        }

        Assert.False(HasAuraMetadata(session.Character.Attributes));
    }

    private static bool HasAuraMetadata(IReadOnlyDictionary<string, string> attributes)
    {
        return new[]
        {
            RuntimeAuraStatusFlagAttribute,
            RuntimeAuraSpellIdAttribute,
            RuntimeAuraLevelAttribute,
            RuntimeAuraDamageAttribute,
            RuntimeAuraGDamageAttribute,
            RuntimeAuraExpiresAtUtcAttribute,
            RuntimeAuraRemoveOnDeadAttribute,
        }.Any(attributes.ContainsKey);
    }

    private static void AssertAuraMagicDefenderMetadata(
        WorldSession session,
        DateTimeOffset beforeApply,
        DateTimeOffset afterApply,
        int durationSeconds)
    {
        Assert.True(
            bool.TryParse(
                session.Character.Attributes[RuntimeAuraMagicDefenderRemoveOnDeadAttribute],
                out var removeOnDead) &&
            removeOnDead);
        AssertLegacyXpSkillExpiresAround(
            session.Character.Attributes,
            RuntimeAuraMagicDefenderExpiresAtUtcAttribute,
            beforeApply,
            afterApply,
            durationSeconds);
    }

    private static void AssertNoAuraMagicDefenderMetadata(WorldSession session)
    {
        Assert.False(HasAuraMagicDefenderMetadata(session.Character.Attributes));
    }

    private static bool HasAuraMagicDefenderMetadata(IReadOnlyDictionary<string, string> attributes)
    {
        return attributes.ContainsKey(RuntimeAuraMagicDefenderExpiresAtUtcAttribute) ||
            attributes.ContainsKey(RuntimeAuraMagicDefenderRemoveOnDeadAttribute);
    }

    private static void AssertNoAttachStatus30000Metadata(WorldSession session)
    {
        foreach (var key in new[]
        {
            "LegacyMagicDefenderExpiresAtUtc",
            "LegacyMagicDefenderRemoveOnDead",
            "AzureShieldLevel",
            "AzureShieldDefence",
        })
        {
            Assert.False(session.Character.Attributes.ContainsKey(key));
        }
    }

    private static void AssertAuraStatusBonus(
        WorldSession baseline,
        WorldSession actual,
        WorldDataCatalog worldData,
        AuraDescriptor? expected,
        AuraDescriptor? removed = null)
    {
        var baselineFields = CalculateAuraStatusFields(baseline, worldData);
        var actualFields = CalculateAuraStatusFields(actual, worldData);
        foreach (var index in AuraStatusFieldIndexes)
        {
            var expectedValue = baselineFields[index];
            if (removed is { } removedAura && index == removedAura.StatusFieldIndex)
            {
                expectedValue = checked(expectedValue - removedAura.StatusBonus);
            }

            if (expected is { } activeAura && index == activeAura.StatusFieldIndex)
            {
                expectedValue = checked(expectedValue + activeAura.StatusBonus);
            }

            Assert.Equal(expectedValue, actualFields[index]);
        }
    }

    private static uint[] CalculateAuraStatusFields(WorldSession session, WorldDataCatalog worldData)
    {
        return LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            maxHitpoints: checked((uint)Math.Max(1, session.Character.MinHitPoints)),
            prestigeLevel: 0,
            playerSpells: session.PlayerSpells);
    }

    private static void AssertAuraFlagIcon(
        LegacyFlagIconPacket icon,
        uint uid,
        LegacyFlagIcon expectedIcon)
    {
        Assert.Equal(LegacyFlagIconPacket.ResponseType, icon.Type);
        Assert.Equal(uid, icon.Uid);
        Assert.Equal(expectedIcon, icon.Icon);
        Assert.Equal(AuraLearnedLevel, icon.Level);
        Assert.Equal(LegacyFlagIconPacket.ResponseMode, icon.Mode);
        Assert.Equal(AuraGDamage, icon.Damage);
    }

    private static void AssertOrdinaryAuraAnimation(
        LegacySpellAnimationPacket animation,
        ushort spellId,
        IReadOnlyList<uint> expectedTargets)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(840, animation.X);
        Assert.Equal(590, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal(AuraLearnedLevel, animation.SpellLevel);
        Assert.Equal(expectedTargets, animation.Targets.Select(target => target.Uid).ToArray());
        Assert.All(animation.Targets, target =>
        {
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });
    }

    private static void AssertAuraMagicDefenderAnimation(LegacySpellAnimationPacket animation)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(840, animation.X);
        Assert.Equal(590, animation.Y);
        Assert.Equal(AuraMagicDefenderSpellId, animation.SpellId);
        Assert.Equal(AuraLearnedLevel, animation.SpellLevel);
        Assert.Empty(animation.Targets);
    }

    private static IReadOnlyList<LegacyFlagIconPacket> ParseAuraFlagIcons(IEnumerable<byte[]> frames)
    {
        var icons = new List<LegacyFlagIconPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyFlagIconPacket.TryParse(ReadFrame(bytes), out var icon))
            {
                icons.Add(icon);
            }
        }

        return icons;
    }

    private static void AssertAuraStatusVector(
        IEnumerable<byte[]> frames,
        uint uid,
        int statusFlag,
        bool expectedSet)
    {
        AuraStatusFrameIndex(
            frames,
            uid,
            values => IsLegacyStatusFlagSet(values, statusFlag) == expectedSet);
    }

    private static int AuraStatusFrameIndex(
        IEnumerable<byte[]> frames,
        uint uid,
        Func<IReadOnlyList<ulong>, bool> predicate,
        int startIndex = 0)
    {
        return AuraFrameIndex(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    predicate(entry.Values)),
            startIndex);
    }

    private static int AuraFlagIconFrameIndex(IEnumerable<byte[]> frames, LegacyFlagIcon icon)
    {
        return AuraFrameIndex(
            frames,
            frame => LegacyFlagIconPacket.TryParse(frame, out var packet) && packet.Icon == icon);
    }

    private static int AuraAnimationFrameIndex(IEnumerable<byte[]> frames, ushort spellId)
    {
        return AuraFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) && packet.SpellId == spellId);
    }

    private static int AuraProgressFrameIndex(IEnumerable<byte[]> frames, ushort spellId)
    {
        return AuraFrameIndex(
            frames,
            frame => LegacySpellPacket.TryParse(frame, out var packet) && packet.Id == spellId);
    }

    private static int AuraAzureShieldFrameIndex(
        IEnumerable<byte[]> frames,
        uint expectedTime,
        uint expectedSpellLevel)
    {
        return AuraFrameIndex(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var packet) &&
                packet.Uid == PlayerUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.AzureShield &&
                    entry.Value == 0 &&
                    entry.Flag == AuraMagicDefenderFlag &&
                    entry.Time == expectedTime &&
                    entry.Damage == 0 &&
                    entry.SpellLevel == expectedSpellLevel));
    }

    private static int AuraFrameIndex(
        IEnumerable<byte[]> frames,
        Func<PacketFrame, bool> predicate,
        int startIndex = 0)
    {
        var snapshot = SnapshotFrames(frames);
        for (var index = Math.Max(0, startIndex); index < snapshot.Length; index++)
        {
            if (predicate(ReadFrame(snapshot[index])))
            {
                return index;
            }
        }

        Assert.Fail("Expected Auras runtime frame was not emitted.");
        return -1;
    }

    private static void AssertAuraSessionUnchanged(WorldSession expected, WorldSession actual)
    {
        Assert.Equal(
            expected.Character with { Attributes = actual.Character.Attributes },
            actual.Character);
        Assert.Equal(
            expected.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            actual.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(expected.PlayerSpells.Spells, actual.PlayerSpells.Spells);
        Assert.Equal(
            (expected.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>())
                .OrderBy(pair => pair.Key),
            (actual.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>())
                .OrderBy(pair => pair.Key));
    }

    private sealed record AuraDescriptor(
        ushort SpellId,
        int StatusFlag,
        LegacyFlagIcon Icon,
        int StatusFieldIndex,
        uint StatusBonus);

    private sealed class AuraPeer(
        uint uid,
        string name,
        long X,
        long Y,
        long MapId,
        bool JoinTeam,
        IReadOnlyDictionary<string, string>? Attributes)
    {
        public uint Uid { get; } = uid;

        public string Name { get; } = name;

        public long X { get; } = X;

        public long Y { get; } = Y;

        public long MapId { get; } = MapId;

        public bool JoinTeam { get; } = JoinTeam;

        public IReadOnlyDictionary<string, string>? Attributes { get; } = Attributes;

        public WorldSession? InitialSession { get; set; }
    }

    private sealed record AuraPeerRuntime(
        ConquerTcpConnection Connection,
        List<byte[]> Sent);

    private sealed record AuraCastResult(
        WorldDataCatalog WorldData,
        WorldSessionStore Sessions,
        TeamSessionStateStore TeamStates,
        ConquerTcpConnection CasterConnection,
        List<byte[]> CasterSent,
        IReadOnlyDictionary<uint, AuraPeerRuntime> Peers,
        WorldPacketHandlerContext Context,
        WorldSession InitialCaster,
        DateTimeOffset BeforeCast,
        DateTimeOffset AfterCast,
        StringWriter Log);

    private sealed record AuraRepeatResult(
        WorldSession BeforeSession,
        WorldSession AfterSession,
        DateTimeOffset BeforeCast,
        DateTimeOffset AfterCast);
}
