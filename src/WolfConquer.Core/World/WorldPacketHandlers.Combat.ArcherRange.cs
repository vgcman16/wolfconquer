using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyArcherDirectTargetRange = 18;

    private static async ValueTask<bool> TryRejectLegacyArcherDirectTargetOutOfRangeAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.Archer ||
            context.Session is not { } session ||
            !TryResolveLegacyArcherDirectTargetDistance(
                context,
                session,
                packet.Query.OpponentUid,
                out var targetDiagnostic,
                out var sourceBranch,
                out var distance) ||
            distance <= LegacyArcherDirectTargetRange)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await context.Log.WriteLineAsync(
                $"WorldServer rejected legacy Archer {sourceBranch} target " +
                $"{FormatSession(context)} {targetDiagnostic} distance={distance} " +
                $"range={LegacyArcherDirectTargetRange} autoAttack=false; " +
                $"source=MsgAttackPacket.cs AttackID.Archer {sourceBranch} branch Base.GetDistance <= 18 gate.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryResolveLegacyArcherDirectTargetDistance(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint opponentUid,
        out string targetDiagnostic,
        out string sourceBranch,
        out long distance)
    {
        var character = session.Character;
        if (TryGetSessionByUid(context, opponentUid, out var activeSession) &&
            !ReferenceEquals(activeSession.Connection, context.Connection) &&
            activeSession.Session.Character.MapId == character.MapId)
        {
            var target = activeSession.Session.Character;
            targetDiagnostic = $"targetUid={target.Uid}";
            sourceBranch = "player";
            distance = LegacyGetDistance(character.X, character.Y, target.X, target.Y);
            return true;
        }

        if (TryFindMonsterInstanceByUid(context, character, opponentUid, out var monster, out _))
        {
            if (context.MonsterStates is { } monsterStates &&
                monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster))
            {
                monster = visibleMonster;
            }

            targetDiagnostic = $"monsterUid={monster.Uid}";
            sourceBranch = "monster";
            distance = LegacyGetDistance(character.X, character.Y, monster.X, monster.Y);
            return true;
        }

        if (TryFindSobNpcByUidForLegacyArcherRangeGate(context, character, opponentUid, out var sobNpc))
        {
            targetDiagnostic = $"sobNpcUid={sobNpc.Id}";
            sourceBranch = "SOB";
            distance = LegacyGetDistance(character.X, character.Y, sobNpc.X, sobNpc.Y);
            return true;
        }

        targetDiagnostic = string.Empty;
        sourceBranch = string.Empty;
        distance = 0;
        return false;
    }

    private static bool TryFindSobNpcByUidForLegacyArcherRangeGate(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out SobNpc sobNpc)
    {
        sobNpc = default!;
        if (uid > int.MaxValue ||
            ReadCharacterDynamicId(character) != 0 ||
            context.WorldData is not { } worldData ||
            !worldData.SobNpcsById.TryGetValue((int)uid, out var sobNpcs))
        {
            return false;
        }

        foreach (var candidate in sobNpcs)
        {
            if (candidate.MapId == character.MapId)
            {
                sobNpc = candidate;
                return true;
            }
        }

        return false;
    }
}
