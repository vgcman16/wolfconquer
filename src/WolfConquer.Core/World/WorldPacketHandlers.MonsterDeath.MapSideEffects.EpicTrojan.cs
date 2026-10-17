using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyEpicTrojanMonsterDeathMapSideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        var updatedSession = session;
        var updatedContext = context;

        if (monster.MapId == 3_833)
        {
            updatedSession = await TryApplyLegacyMap3833EpicTrojanFlameDevastatorAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        if (monster.MapId == 3_831)
        {
            updatedSession = await TryApplyLegacyMap3831EpicTrojanGeneralPakHandoffAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        if (monster.MapId == 3_834)
        {
            updatedSession = await TryApplyLegacyMap3834EpicTrojanGhostReaverAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        return updatedSession;
    }
}
