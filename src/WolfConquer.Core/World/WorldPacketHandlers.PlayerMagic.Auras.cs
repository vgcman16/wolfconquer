using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyAuraStatusFlagAttribute = "LegacyAuraStatusFlag";
    internal const string RuntimeLegacyAuraSpellIdAttribute = "LegacyAuraSpellId";
    internal const string RuntimeLegacyAuraSpellLevelAttribute = "LegacyAuraLevel";
    internal const string RuntimeLegacyAuraDamageAttribute = "LegacyAuraDamage";
    internal const string RuntimeLegacyAuraGDamageAttribute = "LegacyAuraGDamage";
    internal const string RuntimeLegacyAuraExpiresAtUtcAttribute = "LegacyAuraExpiresAtUtc";
    internal const string RuntimeLegacyAuraRemoveOnDeadAttribute = "LegacyAuraRemoveOnDead";
    internal const string RuntimeLegacyAurasMagicDefenderExpiresAtUtcAttribute =
        "LegacyAuraMagicDefenderExpiresAtUtc";
    internal const string RuntimeLegacyAurasMagicDefenderRemoveOnDeadAttribute =
        "LegacyAuraMagicDefenderRemoveOnDead";

    internal const int LegacyTyrantAuraStatusFlag = 0x62;
    internal const int LegacyFendAuraStatusFlag = 0x64;
    internal const int LegacyMetalAuraStatusFlag = 0x66;
    internal const int LegacyWoodAuraStatusFlag = 0x68;
    internal const int LegacyWaterAuraStatusFlag = 0x6A;
    internal const int LegacyAurasFireStatusFlag = 0x6C;
    internal const int LegacyEarthAuraStatusFlag = 0x6E;

    private const uint LegacyPersistentAuraDurationSeconds = 2_592_000;
    private const uint LegacyTeamAuraDurationSeconds = 30;
    private const long LegacyAuraTeamDistance = 18;
    private const ulong LegacyAuraSpellProgress = 200;

    private static async ValueTask<bool> HandleLegacyPlayerAurasMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.Auras)
        {
            return false;
        }

        // Auras.Execute calls CanUseSpell.Verified before its spell-ID branches.
        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var isMagicDefender = spell.Id == 11_200;
        var hasOrdinaryAuraProfile = TryGetLegacyAuraProfile(spell.Id, out var profile);
        if (!isMagicDefender && !hasOrdinaryAuraProfile)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped unsupported legacy Auras magic " +
                    $"{FormatSession(context)} spellId={spell.Id}; exact reviewed IDs only.")
                .ConfigureAwait(false);
            return true;
        }

        if (isMagicDefender)
        {
            return await HandleLegacyAurasMagicDefenderAsync(
                    context,
                    resourceSession,
                    query,
                    playerSpell,
                    spell,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await HandleLegacyOrdinaryAuraAsync(
                context,
                resourceSession,
                query,
                playerSpell,
                spell,
                profile,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> HandleLegacyOrdinaryAuraAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        LegacyAuraProfile profile,
        CancellationToken cancellationToken)
    {
        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var casterSession = GetCurrentLegacyAuraSubjectSession(context, context.Connection, resourceSession);
        var primaryAdded = false;
        var primaryTargetValid = false;

        if (query.OpponentUid == casterUid)
        {
            primaryTargetValid = true;
            var primaryResult = await ApplyLegacyAuraToSubjectAsync(
                    context,
                    context.Connection,
                    casterSession,
                    spell,
                    profile,
                    LegacyPersistentAuraDurationSeconds,
                    toggleSameAura: true,
                    cancellationToken)
                .ConfigureAwait(false);
            casterSession = primaryResult.Session;
            primaryAdded = primaryResult.Added;
        }
        else if (TryGetVisibleSessionByUid(context, casterSession, query.OpponentUid, out var primaryTarget))
        {
            primaryTargetValid = true;
            var primaryResult = await ApplyLegacyAuraToSubjectAsync(
                    context,
                    primaryTarget.Connection,
                    primaryTarget.Session,
                    spell,
                    profile,
                    LegacyPersistentAuraDurationSeconds,
                    toggleSameAura: true,
                    cancellationToken)
                .ConfigureAwait(false);
            primaryAdded = primaryResult.Added;
        }

        casterSession = GetCurrentLegacyAuraSubjectSession(context, context.Connection, casterSession);
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var teamMembersApplied = 0;
        var teamMembersWithSameAura = 0;

        foreach (var teammate in SnapshotLegacyAuraTeamMembers(context, casterSession))
        {
            var currentTeammate = GetCurrentLegacyAuraSubjectSession(
                context,
                teammate.Connection,
                teammate.Session);
            if (TryGetLegacyActiveAuraStatusFlag(currentTeammate.Character, out var activeAuraFlag) &&
                activeAuraFlag == profile.StatusFlag)
            {
                teamMembersWithSameAura++;
                continue;
            }

            var teamResult = await ApplyLegacyAuraToSubjectAsync(
                    context,
                    teammate.Connection,
                    currentTeammate,
                    spell,
                    profile,
                    LegacyTeamAuraDurationSeconds,
                    toggleSameAura: false,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!teamResult.Added)
            {
                continue;
            }

            animationTargets.Add(new LegacySpellAnimationTarget(
                CheckedUInt32(teamResult.Session.Character.Uid, nameof(teamResult.Session.Character.Uid)),
                0,
                Hit: true,
                MsgAttackEffect.None));
            teamMembersApplied++;
        }

        casterSession = GetCurrentLegacyAuraSubjectSession(context, context.Connection, casterSession);
        var casterContext = context with { Session = casterSession };
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(casterContext, casterSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var progressSession = casterSession;
        if (primaryAdded)
        {
            progressSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                    casterContext,
                    casterSession,
                    playerSpell,
                    spell,
                    LegacyAuraSpellProgress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled matching legacy Auras magic " +
                $"{FormatSession(context with { Session = progressSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} statusFlag={profile.StatusFlag} " +
                $"primaryTargetUid={query.OpponentUid} primaryTargetValid={primaryTargetValid} " +
                $"primaryAdded={primaryAdded} teamMembersApplied={teamMembersApplied} " +
                $"teamMembersWithSameAura={teamMembersWithSameAura}; " +
                "source=Auras.cs plus Player.AddAura.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> HandleLegacyAurasMagicDefenderAsync(
        WorldPacketHandlerContext context,
        WorldSession resourceSession,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        var currentSession = GetCurrentLegacyAuraSubjectSession(context, context.Connection, resourceSession);
        var removedRide = HasLegacyStatusFlag(currentSession.Character, LegacyRideFlag);
        if (removedRide)
        {
            currentSession = currentSession with
            {
                Character = ClearLegacyMountRideStatus(currentSession.Character),
            };
            context.Sessions?.Set(context.Connection, currentSession);
            var rideRemovedContext = context with { Session = currentSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    rideRemovedContext,
                    currentSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var hadMagicDefender = HasLegacyStatusFlag(currentSession.Character, LegacyMagicDefenderFlag);
        var now = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
        var updatedCharacter = hadMagicDefender
            ? ClearLegacyAurasMagicDefenderStatus(currentSession.Character)
            : ApplyLegacyAurasMagicDefenderStatus(currentSession.Character, spell.Duration, now);
        currentSession = currentSession with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, currentSession);
        var updatedContext = context with { Session = currentSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, currentSession, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyAurasMagicDefenderUpdateToVisibleAsync(
                updatedContext,
                currentSession,
                hadMagicDefender ? 0u : spell.Duration,
                playerSpell.Level,
                cancellationToken)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(currentSession.Character.Uid, nameof(currentSession.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            Targets: []);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, currentSession, animation, cancellationToken)
            .ConfigureAwait(false);

        var progressSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                currentSession,
                playerSpell,
                spell,
                LegacyAuraSpellProgress,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled matching legacy Auras MagicDefender magic " +
                $"{FormatSession(context with { Session = progressSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacyMagicDefenderFlag} " +
                $"toggledOn={!hadMagicDefender} duration={spell.Duration} removedRide={removedRide}; " +
                "source=Auras.cs spell 11200.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<LegacyAuraMutationResult> ApplyLegacyAuraToSubjectAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession fallbackSession,
        LegacyMagicRuntimeSpell spell,
        LegacyAuraProfile profile,
        uint durationSeconds,
        bool toggleSameAura,
        CancellationToken cancellationToken)
    {
        var currentSession = GetCurrentLegacyAuraSubjectSession(context, subjectConnection, fallbackSession);
        if (TryGetLegacyActiveAuraStatusFlag(currentSession.Character, out var activeAuraFlag))
        {
            if (activeAuraFlag == profile.StatusFlag && !toggleSameAura)
            {
                return new LegacyAuraMutationResult(currentSession, Added: false);
            }

            var removedSession = currentSession with
            {
                Character = ClearLegacyAuraStatus(currentSession.Character, activeAuraFlag),
            };
            context.Sessions?.Set(subjectConnection, removedSession);
            var removedContext = context with
            {
                Connection = subjectConnection,
                Session = removedSession,
            };
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    removedContext,
                    subjectConnection,
                    removedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            currentSession = removedSession;

            if (activeAuraFlag == profile.StatusFlag)
            {
                return new LegacyAuraMutationResult(currentSession, Added: false);
            }
        }

        var now = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
        var addedSession = currentSession with
        {
            Character = ApplyLegacyAuraStatus(
                currentSession.Character,
                spell,
                profile.StatusFlag,
                durationSeconds,
                now),
        };
        context.Sessions?.Set(subjectConnection, addedSession);
        var addedContext = context with
        {
            Connection = subjectConnection,
            Session = addedSession,
        };
        await SendPlayerStatusFlagVectorToVisibleAsync(
                addedContext,
                subjectConnection,
                addedSession,
                cancellationToken)
            .ConfigureAwait(false);

        var uid = CheckedUInt32(addedSession.Character.Uid, nameof(addedSession.Character.Uid));
        await subjectConnection.SendAsync(
                LegacyFlagIconPacket
                    .CreateResponse(uid, profile.Icon, spell.Level, spell.GDamage)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        return new LegacyAuraMutationResult(addedSession, Added: true);
    }

    private static CharacterSnapshot ApplyLegacyAuraStatus(
        CharacterSnapshot character,
        LegacyMagicRuntimeSpell spell,
        int statusFlag,
        uint durationSeconds,
        DateTimeOffset now)
    {
        var updatedCharacter = WithLegacyStatusFlag(character, statusFlag);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAuraStatusFlagAttribute,
            checked((uint)statusFlag));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAuraSpellIdAttribute,
            spell.Id);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAuraSpellLevelAttribute,
            spell.Level);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAuraDamageAttribute,
            spell.Damage);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAuraGDamageAttribute,
            spell.GDamage);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAuraExpiresAtUtcAttribute,
            now.ToUniversalTime().AddSeconds(durationSeconds).ToString("O", CultureInfo.InvariantCulture));
        return WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAuraRemoveOnDeadAttribute,
            "true");
    }

    private static CharacterSnapshot ClearLegacyAuraStatus(
        CharacterSnapshot character,
        int statusFlag)
    {
        var updatedCharacter = WithoutLegacyStatusFlag(character, statusFlag);
        foreach (var attribute in LegacyAuraMetadataAttributes)
        {
            updatedCharacter = WithoutCharacterAttribute(updatedCharacter, attribute);
        }

        return updatedCharacter;
    }

    private static CharacterSnapshot ApplyLegacyAurasMagicDefenderStatus(
        CharacterSnapshot character,
        uint durationSeconds,
        DateTimeOffset now)
    {
        var updatedCharacter = WithLegacyStatusFlag(character, LegacyMagicDefenderFlag);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAurasMagicDefenderExpiresAtUtcAttribute,
            now.ToUniversalTime().AddSeconds(durationSeconds).ToString("O", CultureInfo.InvariantCulture));
        return WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyAurasMagicDefenderRemoveOnDeadAttribute,
            "true");
    }

    private static CharacterSnapshot ClearLegacyAurasMagicDefenderStatus(CharacterSnapshot character)
    {
        return WithoutCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutLegacyStatusFlag(character, LegacyMagicDefenderFlag),
                RuntimeLegacyAurasMagicDefenderExpiresAtUtcAttribute),
            RuntimeLegacyAurasMagicDefenderRemoveOnDeadAttribute);
    }

    private static async ValueTask SendLegacyAurasMagicDefenderUpdateToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint duration,
        uint playerSpellLevel,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AzureShield,
                    Value: 0,
                    Flag: LegacyMagicDefenderFlag,
                    Time: duration,
                    Damage: 0,
                    SpellLevel: playerSpellLevel),
            ]);
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                context.Connection,
                session,
                packet.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<WorldActiveSession> SnapshotLegacyAuraTeamMembers(
        WorldPacketHandlerContext context,
        WorldSession casterSession)
    {
        if (context.TeamStates is not { } teamStates ||
            context.Sessions is not { } sessions ||
            !teamStates.HasTeam(context.Connection))
        {
            return [];
        }

        var targets = new List<WorldActiveSession>();
        foreach (var connection in teamStates.GetConnections(context.Connection))
        {
            if (ReferenceEquals(connection, context.Connection) ||
                !sessions.TryGet(connection, out var memberSession) ||
                memberSession is null ||
                memberSession.Character.Uid == casterSession.Character.Uid ||
                !IsLegacyAuraTeamMemberInRange(casterSession.Character, memberSession.Character))
            {
                continue;
            }

            targets.Add(new WorldActiveSession(connection, memberSession));
        }

        return targets;
    }

    internal static bool IsLegacyAuraTeamMemberInRange(
        CharacterSnapshot caster,
        CharacterSnapshot member)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(member);

        // Matching Auras.cs checks only Team.GetMembers and strict Core.GetDistance < 18.
        return LegacyGetDistance(caster.X, caster.Y, member.X, member.Y) < LegacyAuraTeamDistance;
    }

    internal static bool TryGetLegacyActiveAuraStatusFlag(
        CharacterSnapshot character,
        out int statusFlag)
    {
        var storedFlag = ReadAttributeUInt64(character.Attributes, RuntimeLegacyAuraStatusFlagAttribute);
        if (storedFlag <= int.MaxValue &&
            IsLegacyAuraStatusFlag((int)storedFlag) &&
            HasLegacyStatusFlag(character, (int)storedFlag))
        {
            statusFlag = (int)storedFlag;
            return true;
        }

        foreach (var candidate in LegacyAuraStatusFlags)
        {
            if (HasLegacyStatusFlag(character, candidate))
            {
                statusFlag = candidate;
                return true;
            }
        }

        statusFlag = 0;
        return false;
    }

    internal static bool TryGetLegacyAuraStatEffect(
        CharacterSnapshot character,
        out int statusFlag,
        out uint damage)
    {
        damage = 0;
        if (!TryGetLegacyActiveAuraStatusFlag(character, out statusFlag))
        {
            return false;
        }

        damage = ReadAttributeUInt32(character.Attributes, RuntimeLegacyAuraDamageAttribute);
        return true;
    }

    internal static void RemoveLegacyAuraDeathMetadata(IDictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        foreach (var attribute in LegacyAuraMetadataAttributes)
        {
            attributes.Remove(attribute);
        }

        RemoveLegacyAurasMagicDefenderMetadata(attributes);
    }

    internal static void RemoveLegacyAurasMagicDefenderMetadata(IDictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        attributes.Remove(RuntimeLegacyAurasMagicDefenderExpiresAtUtcAttribute);
        attributes.Remove(RuntimeLegacyAurasMagicDefenderRemoveOnDeadAttribute);
    }

    private static WorldSession GetCurrentLegacyAuraSubjectSession(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession fallbackSession)
    {
        return context.Sessions is { } sessions &&
            sessions.TryGet(subjectConnection, out var currentSession) &&
            currentSession is not null
                ? currentSession
                : fallbackSession;
    }

    private static bool TryGetLegacyAuraProfile(ushort spellId, out LegacyAuraProfile profile)
    {
        profile = spellId switch
        {
            10_395 => new LegacyAuraProfile(LegacyTyrantAuraStatusFlag, LegacyFlagIcon.TyrantAura),
            10_410 => new LegacyAuraProfile(LegacyFendAuraStatusFlag, LegacyFlagIcon.FeandAura),
            10_420 => new LegacyAuraProfile(LegacyMetalAuraStatusFlag, LegacyFlagIcon.MetalAura),
            10_421 => new LegacyAuraProfile(LegacyWoodAuraStatusFlag, LegacyFlagIcon.WoodAura),
            10_422 => new LegacyAuraProfile(LegacyWaterAuraStatusFlag, LegacyFlagIcon.WaterAura),
            10_423 => new LegacyAuraProfile(LegacyAurasFireStatusFlag, LegacyFlagIcon.FireAura),
            10_424 => new LegacyAuraProfile(LegacyEarthAuraStatusFlag, LegacyFlagIcon.EarthAura),
            _ => default,
        };
        return profile.StatusFlag != 0;
    }

    private static bool IsLegacyAuraStatusFlag(int statusFlag)
    {
        return statusFlag is
            LegacyTyrantAuraStatusFlag or
            LegacyFendAuraStatusFlag or
            LegacyMetalAuraStatusFlag or
            LegacyWoodAuraStatusFlag or
            LegacyWaterAuraStatusFlag or
            LegacyAurasFireStatusFlag or
            LegacyEarthAuraStatusFlag;
    }

    private static readonly int[] LegacyAuraStatusFlags =
    [
        LegacyTyrantAuraStatusFlag,
        LegacyFendAuraStatusFlag,
        LegacyMetalAuraStatusFlag,
        LegacyWoodAuraStatusFlag,
        LegacyWaterAuraStatusFlag,
        LegacyAurasFireStatusFlag,
        LegacyEarthAuraStatusFlag,
    ];

    private static readonly string[] LegacyAuraMetadataAttributes =
    [
        RuntimeLegacyAuraStatusFlagAttribute,
        RuntimeLegacyAuraSpellIdAttribute,
        RuntimeLegacyAuraSpellLevelAttribute,
        RuntimeLegacyAuraDamageAttribute,
        RuntimeLegacyAuraGDamageAttribute,
        RuntimeLegacyAuraExpiresAtUtcAttribute,
        RuntimeLegacyAuraRemoveOnDeadAttribute,
    ];

    private readonly record struct LegacyAuraProfile(int StatusFlag, LegacyFlagIcon Icon);

    private readonly record struct LegacyAuraMutationResult(WorldSession Session, bool Added);
}
