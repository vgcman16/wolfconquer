using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyPlayerMagicCanAttackFlashingNameAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        IReadOnlyList<LegacyPlayerMagicTarget> targets,
        CancellationToken cancellationToken)
    {
        if (HasLegacyStatusFlag(session.Character, LegacyFlashingNameStatusFlag) ||
            HasLegacyPlayerMagicInterserverAttribute(session))
        {
            return session;
        }

        var pkMode = ReadAttributeUInt64(session.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        if (pkMode == LegacyPkModeCapture || pkMode == LegacyPkModePeace)
        {
            return session;
        }

        foreach (var target in targets)
        {
            var targetCharacter = target.ActiveSession.Session.Character;
            if (!CanApplyLegacyPlayerMagicCanAttackFlashingName(targetCharacter))
            {
                continue;
            }

            var updatedSession = session with
            {
                Character = WithLegacyStatusFlag(session.Character, LegacyFlashingNameStatusFlag),
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };
            await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy player-target magic flashing-name " +
                    $"{FormatSession(updatedContext)} targetUid={targetCharacter.Uid} " +
                    $"targetMap={targetCharacter.MapId} targetDynamicId={ReadCharacterDynamicId(targetCharacter)} " +
                    $"statusFlag={LegacyFlashingNameStatusFlag}; " +
                    "source=CanAttackPlayer.Verified flashingname AddFlag(FlashingName, 30, RemoveOnDead: true).")
                .ConfigureAwait(false);
            return updatedSession;
        }

        return session;
    }

    private static bool CanApplyLegacyPlayerMagicCanAttackFlashingName(CharacterSnapshot target)
    {
        if (HasLegacyStatusFlag(target, LegacyFlashingNameStatusFlag) ||
            HasLegacyStatusFlag(target, LegacyRedNameStatusFlag) ||
            HasLegacyStatusFlag(target, LegacyBlackNameStatusFlag) ||
            LegacyFreePkMapIds.Contains(target.MapId) ||
            ReadCharacterDynamicId(target) != 0)
        {
            return false;
        }

        return target.MapId != 1_015 || DateTime.Now.Minute is < 28 or > 35;
    }

    private static bool HasLegacyPlayerMagicInterserverAttribute(WorldSession session)
    {
        return ReadAttributeBool(
            session.Character.Attributes,
            "OnInterServer",
            "OnInterserver",
            "InterServer",
            "Interserver",
            "InterServerActive",
            "RuntimeOnInterServer",
            "RuntimeInterServer");
    }
}
