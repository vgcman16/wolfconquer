namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryHandleMailboxRecoveryPin(WorldPacketHandlerContext context,
        WorldSessionStore sessions, WorldSession current, LegacySecondaryPasswordPacket packet,
        LegacySecondaryPasswordState state, bool wasChecked, LegacySecondaryPasswordTransition transition,
        out LegacySecondaryPasswordTransition? response, out string reason)
    {
        response = null;
        reason = string.Empty;
        var producer = context.MailboxPackages ?? sessions.MailboxPackages;
        if (producer is null) return false;
        if (!producer.HasPendingAccountWork(current.AccountId, out var pending, out reason)) return true;
        if (!pending) return false;
        reason = "secondaryPasswordRecoveryVerificationOnly";
        if (packet.Action != 1 || transition.State != state) return true;
        var failure = "secondaryPasswordRecoveryOwnerChanged";
        var published = false;
        // The caller holds the actual world-exclusive lease and PIN gate. Use
        // the Store's own connection gate without invoking a saved-state CAS.
        sessions.WithCurrentNativeClientIdentity(context.Connection, current.Character.Uid, _ =>
        {
            if (!sessions.TryGet(context.Connection, out var latestSession) || !ReferenceEquals(latestSession, current) ||
                !LegacySecondaryPasswordRuntime.TryGet(context.Connection, current, out var latest, out var latestChecked) ||
                latest != state || latestChecked != wasChecked ||
                !LegacySecondaryPasswordPersistence.TryValidateBaseline(current, out failure) ||
                !sessions.TryGet(context.Connection, out latestSession) || !ReferenceEquals(latestSession, current) ||
                !LegacySecondaryPasswordRuntime.TryGet(context.Connection, current, out latest, out latestChecked) ||
                latest != state || latestChecked != wasChecked) return;
            published = LegacySecondaryPasswordRuntime.TryPublish(context.Connection, current, current,
                state, transition.IsChecked);
        });
        if (published)
        {
            response = transition;
            reason = "handledRecoveryVerification";
        }
        else reason = failure;
        return true;
    }
}
