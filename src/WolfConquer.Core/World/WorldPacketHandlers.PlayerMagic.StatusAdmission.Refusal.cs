namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask RejectLegacyFlyAdmissionAsync(
        WorldPacketHandlerContext context, LegacyRootAdmissionOwner owner,
        LegacyMagicRuntimeSpell spell, CancellationToken cancellationToken)
    {
        // Failure output belongs to the retained real issuance/record, not a
        // caller snapshot or a UID-discovered replacement. No costs committed.
        if (!IsLegacyRootCommittedRecordCurrent(owner, owner.Expected)) return;
        cancellationToken.ThrowIfCancellationRequested();
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(LegacyFlyBlockedMessage).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer blocked legacy Fly AttachStatus magic " +
                $"{FormatSession(context with { Session = owner.Expected })} spellId={spell.Id} " +
                "beforeResourceAdmission=True; source=AttachStatus.cs case 8003 transform/Ride/PathOfShadow gate.")
            .ConfigureAwait(false);
    }
}
