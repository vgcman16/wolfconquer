using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> CompleteLegacyPhysicalMonsterDeathAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyEquipmentQueryPublicationScope scope,
        LegacyMonsterInstance monster, LegacyNormalPlayerDeathCause cause, CancellationToken cancellationToken)
    {
        var current = await ClearLegacyPhysicalMonsterThundercloudsAsync(context, session, scope, cancellationToken)
            .ConfigureAwait(false);
        if (!current.Completed) return null;
        session = current.Session!;
        if (TryPrepareLegacyPlayerTransformFinish(session, context.WorldData,
                context.LegacyClock ?? LegacySignedMillisecondClock.System, out var prepared))
        {
            context.Sessions?.Set(context.Connection, prepared.Session);
            var finish = await PublishLegacyPlayerTransformFinishAsync(
                    context with { Session = prepared.Session }, prepared, cancellationToken)
                .ConfigureAwait(false);
            if (finish.Session is null) return null;
            session = finish.Session;
        }

        current = await ExecuteLegacyNormalPlayerDeathPhasesAsync(
                context with { Session = session }, session, scope, cause, cancellationToken)
            .ConfigureAwait(false);
        if (!current.Completed) return null;
        var packet = new MsgAttackPacket(new MsgAttackQuery(
            Uid: monster.Uid, OpponentUid: CheckedUInt32(scope.OwnerUid, nameof(scope.OwnerUid)),
            X: cause.X, Y: cause.Y, AttackType: MsgAttackType.Death, Damage: 0,
            KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0));
        return await SendLegacyPhysicalMonsterInteractionAsync(
                context with { Session = current.Session }, current.Session!, scope.OwnerUid,
                monster, packet, aroundMonster: false, cancellationToken)
            .ConfigureAwait(false);
    }
}
