namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyEquipmentQueryPublicationResult> ExecuteLegacyFineRainAcquisitionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        LegacyEquipmentQueryPublicationScope scope,
        CancellationToken cancellationToken)
    {
        var current = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!current.Completed) return current;
        var provider = context.TimeProvider ?? TimeProvider.System;
        var uid = CheckedUInt32(scope.OwnerUid, nameof(scope.OwnerUid));
        foreach (var flag in new[] { LegacyFineRain1Flag, LegacyFineRain2Flag })
        {
            session = current.Session!;
            // Source captures this packet's power before its owner/view sends.
            // The NEXT213 call rereads CURRENT power after the whole212 publication.
            var power = ReadAttributeUInt32(session.Character.Attributes, RuntimeLegacyFineRainPowerAttribute);
            var frame = new LegacyMsgUpdatePacket(uid,
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.FineRain, Value: 0,
                    Flag: checked((uint)flag), Time: spell.Duration, Damage: power, SpellLevel: power)])
                .ToFrameBytes();
            current = await SendLegacyFineRainAcquisitionFrameAsync(
                    context, session, scope, frame, cancellationToken)
                .ConfigureAwait(false);
            if (!current.Completed) return current;
        }

        foreach (var flag in new[] { LegacyFineRain1Flag, LegacyFineRain2Flag })
        {
            // A sameUID callback/recast cannot cancel the next entered Add.
            // It also cannot make this tail reinstall the already-completed212.
            session = ApplyLegacyFineRainRegistration(current.Session!, spell, flag, provider);
            context.Sessions?.Set(context.Connection, session);
            var frame = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
                    uid, LegacyPlayerDeathState.CreateStatusFlagVector(
                        session.Character.Attributes, LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();
            current = await SendLegacyFineRainAcquisitionFrameAsync(
                    context, session, scope, frame, cancellationToken)
                .ConfigureAwait(false);
            if (!current.Completed) return current;
        }
        return current;
    }
}
