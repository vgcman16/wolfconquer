namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyPhysicalMonsterReceiveResult(
        WorldSession? Session, uint PreviousHitpoints, uint DamageApplied, bool Killed, bool Entered);

    private static async ValueTask<LegacyPhysicalMonsterReceiveResult> ApplyLegacyPhysicalMonsterHitAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyMonsterInstance monster,
        uint damage, CancellationToken cancellationToken)
    {
        // Native CheckForOponnentDead holds Player BEFORE RemovePick can publish.
        var nativePlayerReceiver = session.NativePlayer;
        var scope = new LegacyEquipmentQueryPublicationScope(session.Character.Uid);
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return default;
        session = current.Session!;
        // CheckForOponnentDead tests Alive ONCE, AFTER the incoming interaction.
        if (session.Character.MinHitPoints <= 0) return new(session, 0, 0, false, false);

        session = await TryApplyLegacyMonsterHitActivePickRemovalAsync(
                context with { Session = session }, session, damage, cancellationToken)
            .ConfigureAwait(false);
        current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return default;
        session = current.Session!;
        var previous = session.Character.MinHitPoints <= 0 ? 0U
            : CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
        if (damage >= session.Character.MinHitPoints)
        {
            // Chosen branch and source XY are captured before target Finish/Dead awaits.
            var cause = new LegacyNormalPlayerDeathCause(
                CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                CheckedUInt16(session.Character.Y, nameof(session.Character.Y)), monster.Uid, monster.Name);
            var killed = await CompleteLegacyPhysicalMonsterDeathAsync(
                    context with { Session = session }, session, scope, nativePlayerReceiver, monster, cause, cancellationToken)
                .ConfigureAwait(false);
            return killed is null ? default : new(killed, previous, previous, true, true);
        }

        var sitting = await InterruptLegacyPhysicalMonsterSitAsync(
                context with { Session = session }, session, scope, damage, cancellationToken)
            .ConfigureAwait(false);
        if (!sitting.Completed) return default;
        session = sitting.Session!;
        // No new Alive/lethal decision after the entered Sit publication.
        // Source signed-int -= uses CURRENT HP, including a sameUID callback change.
        var value = unchecked((int)session.Character.MinHitPoints - (int)damage);
        var assigned = await AssignLegacyPlayerHitpointsAsync(
                context with { Session = session }, session, scope, value, cancellationToken)
            .ConfigureAwait(false);
        return assigned.Completed ? new(assigned.Session, previous, damage, false, true) : default;
    }
}
