using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyBattleFieldMonsterDeathMapSideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldPacketHandlerContext updatedContext,
        WorldSession updatedSession,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        ulong battlePoints,
        CancellationToken cancellationToken)
    {
        var tournamentAlive = ReadAttributeBool(
            updatedSession.Character.Attributes,
            RuntimeLegacyBattleFieldTournamentAliveAttribute);
        if (tournamentAlive)
        {
            var current = ReadAttributeUInt64(updatedSession.Character.Attributes, "BattleFieldPoints");
            var updatedCharacter = WithCharacterAttribute(
                updatedSession.Character,
                "BattleFieldPoints",
                checked(current + battlePoints));
            updatedSession = updatedSession with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(
                        FormattableString.Invariant($"You received {battlePoints} BattlePoints."))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy monster death battlefield side effect " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                $"battlePoints={battlePoints} pointsApplied={tournamentAlive} branch=\"MonsterRole.cs Map == {monster.MapId}\".")
            .ConfigureAwait(false);

        return updatedSession;
    }
}
