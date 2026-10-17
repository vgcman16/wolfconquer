using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyRideExpiresAtUtcAttribute = "LegacyRideExpiresAtUtc";
    internal const string RuntimeLegacyRideRemoveOnDeadAttribute = "LegacyRideRemoveOnDead";
    internal const string RuntimeLegacyRideStampSecondsAttribute = "LegacyRideStampSeconds";

    private const ushort LegacyRidingMountSkillId = 7_001;
    private const ushort LegacySpookMountSkillId = 7_002;
    private const ushort LegacyWarCryMountSkillId = 7_003;
    private const int LegacyRidingMountSkillSort = 32;
    private const int LegacySpookMountSkillSort = 30;
    private const int LegacyWarCryMountSkillSort = 31;
    private const int LegacyRideDurationSeconds = 2_592_000;
    private const int LegacyRideStampSeconds = 1;
    private const ushort LegacySteedEquipmentPosition = 12;

    private static readonly LegacyMountSkillProfile LegacyRidingMountSkillProfile = new(
        LegacyRidingMountSkillId,
        LegacyRidingMountSkillSort,
        LegacyMountSkillKind.Riding,
        RowId: 10_301,
        SourceFile: "Riding.cs");

    private static readonly LegacyMountSkillProfile LegacySpookMountSkillProfile = new(
        LegacySpookMountSkillId,
        LegacySpookMountSkillSort,
        LegacyMountSkillKind.Spook,
        RowId: 10_302,
        SourceFile: "Spook.cs");

    private static readonly LegacyMountSkillProfile LegacyWarCryMountSkillProfile = new(
        LegacyWarCryMountSkillId,
        LegacyWarCryMountSkillSort,
        LegacyMountSkillKind.WarCry,
        RowId: 10_303,
        SourceFile: "WarCry.cs");

    private static async ValueTask<bool> HandleLegacyPlayerMountControlMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var profile = GetLegacyMountSkillProfile(spell);
        if (profile is null)
        {
            return false;
        }

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

        var resourceContext = context with { Session = resourceSession };
        switch (profile.Kind)
        {
            case LegacyMountSkillKind.Riding:
                await ExecuteLegacyRidingMountSkillAsync(
                        resourceContext,
                        query,
                        playerSpell,
                        spell,
                        resourceSession,
                        profile,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case LegacyMountSkillKind.Spook:
                await ExecuteLegacySpookMountSkillAsync(
                        resourceContext,
                        query,
                        playerSpell,
                        spell,
                        resourceSession,
                        profile,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
            case LegacyMountSkillKind.WarCry:
                await ExecuteLegacyWarCryMountSkillAsync(
                        resourceContext,
                        query,
                        playerSpell,
                        spell,
                        resourceSession,
                        profile,
                        cancellationToken)
                    .ConfigureAwait(false);
                break;
        }

        return true;
    }

    private static async ValueTask ExecuteLegacyRidingMountSkillAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        LegacyMountSkillProfile profile,
        CancellationToken cancellationToken)
    {
        if (IsLegacyRidingStateBlocked(session))
        {
            await SendLegacyMountTopLeftMessageAsync(
                    context,
                    "You can`t use this skill right now !",
                    cancellationToken)
                .ConfigureAwait(false);
            await WriteLegacyMountBlockedLogAsync(context, spell, profile, "state-or-steed-race")
                .ConfigureAwait(false);
            return;
        }

        if (IsLegacyRidingMapOrTournamentBlocked(session))
        {
            await SendLegacyMountTopLeftMessageAsync(
                    context,
                    "You can't use this skill on this map.",
                    cancellationToken)
                .ConfigureAwait(false);
            await WriteLegacyMountBlockedLogAsync(context, spell, profile, "map-or-current-tournament")
                .ConfigureAwait(false);
            return;
        }

        var hadRide = HasLegacyStatusFlag(session.Character, LegacyRideFlag);
        var updatedCharacter = hadRide
            ? ClearLegacyMountRideStatus(session.Character)
            : ApplyLegacyMountRideStatus(session.Character, DateTimeOffset.UtcNow);
        uint? restoredVigor = null;
        if (!hadRide &&
            TryReadUInt32Attribute(
                session.Character.Attributes,
                out var maximumVigor,
                "LegacyMaxVigor",
                "MaxVigor"))
        {
            restoredVigor = maximumVigor;
            updatedCharacter = WithCharacterAttribute(updatedCharacter, "Vigor", restoredVigor.Value);
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        if (restoredVigor is { } vigor)
        {
            await context.Connection.SendAsync(
                    new LegacyServerInfoPacket(vigor).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var casterUid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Riding mount magic " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} sort={(int)spell.Sort} rowId={profile.RowId} " +
                $"spellLevel={playerSpell.Level} statusFlag={LegacyRideFlag} toggledOn={!hadRide} " +
                $"durationSeconds={LegacyRideDurationSeconds} removeOnDead=True stampSeconds={LegacyRideStampSeconds} " +
                $"restoredVigor={(restoredVigor?.ToString(CultureInfo.InvariantCulture) ?? "unchanged")}; " +
                $"source={profile.SourceFile}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ExecuteLegacySpookMountSkillAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        LegacyMountSkillProfile profile,
        CancellationToken cancellationToken)
    {
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var visibleTarget = false;
        var mountedTarget = false;
        var dismountedTarget = false;
        if (TryGetVisibleSessionByUid(context, session, query.OpponentUid, out var target))
        {
            visibleTarget = true;
            mountedTarget = HasLegacyStatusFlag(target.Session.Character, LegacyRideFlag);
            if (!mountedTarget)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer handled legacy Spook mount magic " +
                        $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} rowId={profile.RowId} " +
                        $"spellLevel={playerSpell.Level} targetUid={query.OpponentUid} visibleTarget=True " +
                        $"mountedTarget=False returnedBeforeAnimation=True progressDamage=0; source={profile.SourceFile}.")
                    .ConfigureAwait(false);
                return;
            }

            if (CanLegacySpookDismount(session, target.Session))
            {
                await DismountLegacyMountSkillTargetAsync(
                        context,
                        session,
                        target,
                        query,
                        LegacySpookMountSkillId,
                        cancellationToken)
                    .ConfigureAwait(false);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.Session.Character.Uid, nameof(target.Session.Character.Uid)),
                    0,
                    Hit: true,
                    MsgAttackEffect.None));
                dismountedTarget = true;
            }
        }

        var casterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Spook mount magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} rowId={profile.RowId} " +
                $"spellLevel={playerSpell.Level} targetUid={query.OpponentUid} visibleTarget={visibleTarget} " +
                $"mountedTarget={mountedTarget} dismountedTarget={dismountedTarget} " +
                $"spellRange={spell.Range} sourceUsesVisibleTargetWithoutSpellRangeGate=True progressDamage=0; " +
                $"source={profile.SourceFile}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ExecuteLegacyWarCryMountSkillAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        LegacyMountSkillProfile profile,
        CancellationToken cancellationToken)
    {
        var animationTargets = new List<LegacySpellAnimationTarget>();
        var mountedVisibleTargets = 0;
        if (context.Sessions is { } sessions)
        {
            foreach (var target in sessions.Snapshot())
            {
                if (ReferenceEquals(target.Connection, context.Connection) ||
                    target.Session.Character.Uid == session.Character.Uid ||
                    !CanSeePlayer(session.Character, target.Session.Character) ||
                    !HasLegacyStatusFlag(target.Session.Character, LegacyRideFlag))
                {
                    continue;
                }

                mountedVisibleTargets++;
                if (!CanLegacyWarCryDismount(session, target.Session))
                {
                    continue;
                }

                await DismountLegacyMountSkillTargetAsync(
                        context,
                        session,
                        target,
                        query,
                        LegacyWarCryMountSkillId,
                        cancellationToken)
                    .ConfigureAwait(false);
                animationTargets.Add(new LegacySpellAnimationTarget(
                    CheckedUInt32(target.Session.Character.Uid, nameof(target.Session.Character.Uid)),
                    0,
                    Hit: true,
                    MsgAttackEffect.None));
            }
        }

        var casterUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy WarCry mount magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} rowId={profile.RowId} " +
                $"spellLevel={playerSpell.Level} mountedVisibleTargets={mountedVisibleTargets} " +
                $"dismountedTargets={animationTargets.Count} spellRange={spell.Range} " +
                $"constructedSectorUnused=True progressDamage=0; source={profile.SourceFile}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask DismountLegacyMountSkillTargetAsync(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        WorldActiveSession target,
        MsgAttackQuery sourceQuery,
        ushort spellId,
        CancellationToken cancellationToken)
    {
        var targetCharacter = WithCharacterAttribute(
            ClearLegacyMountRideStatus(target.Session.Character),
            "Stamina",
            0UL);
        var updatedTargetSession = target.Session with { Character = targetCharacter };
        context.Sessions?.Set(target.Connection, updatedTargetSession);
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                target.Connection,
                updatedTargetSession,
                cancellationToken)
            .ConfigureAwait(false);

        var interaction = new MsgAttackPacket(sourceQuery with
        {
            OpponentUid = CheckedUInt32(targetCharacter.Uid, nameof(targetCharacter.Uid)),
            X = checked((ushort)targetCharacter.X),
            Y = checked((ushort)targetCharacter.Y),
            SpellId = spellId,
        });
        await SendFrameToVisiblePlayersAsync(
                context,
                casterSession,
                interaction.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool CanLegacySpookDismount(WorldSession caster, WorldSession target)
    {
        var casterSteed = FindLegacyMountSteed(caster);
        var targetSteed = FindLegacyMountSteed(target);
        var casterPlus = GetLegacyMountSteedPlus(casterSteed);
        var targetPlus = GetLegacyMountSteedPlus(targetSteed);
        var succeeds = casterPlus > targetPlus;
        if (casterPlus == targetPlus)
        {
            succeeds = (casterSteed?.PlusProgress ?? 0) > (targetSteed?.PlusProgress ?? 0);
        }

        if (!succeeds)
        {
            if (casterSteed is null || targetSteed is null)
            {
                return false;
            }

            succeeds = casterSteed.PerfectionLevel > targetSteed.PerfectionLevel;
            if (!succeeds)
            {
                succeeds = casterSteed.PerfectionProgress > targetSteed.PerfectionProgress;
            }
        }

        return succeeds;
    }

    private static bool CanLegacyWarCryDismount(WorldSession caster, WorldSession target)
    {
        var casterSteed = FindLegacyMountSteed(caster);
        var targetSteed = FindLegacyMountSteed(target);
        var casterPlus = GetLegacyMountSteedPlus(casterSteed);
        var targetPlus = GetLegacyMountSteedPlus(targetSteed);
        var succeeds = casterPlus > targetPlus;
        if (casterPlus == targetPlus)
        {
            succeeds = (casterSteed?.PlusProgress ?? 0) > (targetSteed?.PlusProgress ?? 0);
        }

        return succeeds;
    }

    private static PlayerItemSnapshotItem? FindLegacyMountSteed(WorldSession session)
    {
        return session.PlayerItems.EquipmentItems.FirstOrDefault(
            item => item.Position % 50 == LegacySteedEquipmentPosition);
    }

    private static byte GetLegacyMountSteedPlus(PlayerItemSnapshotItem? steed)
    {
        return steed?.Plus ?? 0;
    }

    private static CharacterSnapshot ApplyLegacyMountRideStatus(
        CharacterSnapshot character,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithLegacyStatusFlag(character, LegacyRideFlag);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyRideExpiresAtUtcAttribute,
            nowUtc
                .ToUniversalTime()
                .AddSeconds(LegacyRideDurationSeconds)
                .ToString("O", CultureInfo.InvariantCulture));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyRideRemoveOnDeadAttribute,
            "true");
        return WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyRideStampSecondsAttribute,
            LegacyRideStampSeconds);
    }

    private static CharacterSnapshot ClearLegacyMountRideStatus(CharacterSnapshot character)
    {
        return WithoutCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutCharacterAttribute(
                    WithoutLegacyStatusFlag(character, LegacyRideFlag),
                    RuntimeLegacyRideExpiresAtUtcAttribute),
                RuntimeLegacyRideRemoveOnDeadAttribute),
            RuntimeLegacyRideStampSecondsAttribute);
    }

    private static bool IsLegacyRidingStateBlocked(WorldSession session)
    {
        return HasLegacyStatusFlag(session.Character, LegacyFlyFlag) ||
            ReadAttributeUInt64(session.Character.Attributes, "TransformationID", "TransformationId") != 0 ||
            ReadAttributeBool(
                session.Character.Attributes,
                "LegacySteedRaceInSteedRace",
                "SteedRaceInSteedRace");
    }

    private static bool IsLegacyRidingMapOrTournamentBlocked(WorldSession session)
    {
        return session.Character.MapId is
                3_825 or 6_011 or 5_342 or 9_988 or 6_891 or 5_599 or 8_521 or
                1_005 or 700 or 1_858 or 10_230 or 1_860 or 1_511 or 1_004 ||
            ReadAttributeBool(
                session.Character.Attributes,
                "LegacyTournamentInTournament",
                "TournamentInTournament",
                "InTournament");
    }

    private static ValueTask SendLegacyMountTopLeftMessageAsync(
        WorldPacketHandlerContext context,
        string message,
        CancellationToken cancellationToken)
    {
        // The local 2005 helper maps the decompiled TopLeftSystem chat mode.
        return context.Connection.SendAsync(
            LegacyMsgMessagePacket.SystemRuneRank(message).ToFrameBytes(),
            cancellationToken);
    }

    private static async ValueTask WriteLegacyMountBlockedLogAsync(
        WorldPacketHandlerContext context,
        LegacyMagicRuntimeSpell spell,
        LegacyMountSkillProfile profile,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer blocked legacy Riding mount magic " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} rowId={profile.RowId} " +
                $"reason={reason}; resourcesSpentBeforeGate=True source={profile.SourceFile}.")
            .ConfigureAwait(false);
    }

    private static LegacyMountSkillProfile? GetLegacyMountSkillProfile(LegacyMagicRuntimeSpell spell)
    {
        return (spell.Id, (int)spell.Sort) switch
        {
            (LegacyRidingMountSkillId, LegacyRidingMountSkillSort) => LegacyRidingMountSkillProfile,
            (LegacySpookMountSkillId, LegacySpookMountSkillSort) => LegacySpookMountSkillProfile,
            (LegacyWarCryMountSkillId, LegacyWarCryMountSkillSort) => LegacyWarCryMountSkillProfile,
            _ => null,
        };
    }

    private enum LegacyMountSkillKind
    {
        Riding,
        Spook,
        WarCry,
    }

    private sealed record LegacyMountSkillProfile(
        ushort SpellId,
        int Sort,
        LegacyMountSkillKind Kind,
        int RowId,
        string SourceFile);
}
