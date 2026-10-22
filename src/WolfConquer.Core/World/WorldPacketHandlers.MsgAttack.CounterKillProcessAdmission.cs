namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // CounterKill-only native Process gates AFTER the existing public prefix, BEFORE book/CanUse.
    // Concrete Arena/TeamArena watcher state only; Elite/TeamElite watcher holders remain absent.
    private static async ValueTask<bool> TryRejectLegacyCounterKillProcessAdmissionAsync(
        WorldPacketHandlerContext context, WorldSession session,
        LegacyCounterKillCanUseOwner owner, CancellationToken cancellationToken)
    {
        if (!IsLegacyCounterKillCanUseOwnerCurrent(context, owner) ||
            !TryRefreshLegacyPlayerMagicResourceSession(
                context, owner.Uid, ref session, owner.NativeClientIdentity))
            return true;

        var character = session.Character;
        if (character.MapId == 2_068 ||
            (IsLegacyCounterKillProcessWatching(context) && character.MapId == 700 &&
             !IsLegacyCounterKillProcessInQualifier(context)))
        {
            await context.Connection.SendAsync(LegacyMsgMessagePacket.SystemTopLeft(
                    "Spells are not allowed on this area.").ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            return true; // Terminal owner-only denial; never resume on a post-send replacement.
        }

        if (HasLegacyStatusFlag(character, LegacyNeptuneCurseFlag))
            return true;

        // Native CheckItems has empty result branches: consume the current physical Player draw.
        // No draw on 1039; never synthesize a Player/stream for unbound compatibility sessions.
        if (character.MapId != 1_039 && !TryAdvanceLegacyCounterKillProcessRate4(context, owner))
            return true;
        if (HasLegacyStatusFlag(character, LegacyFreezeFlag))
            return true;
        return false;
    }

    private static bool IsLegacyCounterKillProcessWatching(WorldPacketHandlerContext context)
    {
        return context.ArenaStates?.IsWatcher(context.Connection) == true ||
            context.TeamArenaStates?.IsWatcher(context.Connection) == true;
        // No Character-attribute alias: native Elite/TeamElite watcher holders are a separate gap.
    }

    private static bool IsLegacyCounterKillProcessInQualifier(WorldPacketHandlerContext context)
    {
        return context.ArenaStates?.IsQualifierFighter(context.Connection) == true ||
            IsLegacyTeamArenaQualifierMember(context, context.Connection) ||
            context.ElitePkStates?.IsQualifierFighter(context.Connection) == true ||
            IsLegacyTeamPkQualifierMember(context, context.Connection);
        // Existing connection-only team helpers include nonleader members of active team matches.
    }
}
