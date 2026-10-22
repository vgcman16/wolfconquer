namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private WorldSession PrepareLegacySecondaryPasswordSession(
        Networking.ConquerTcpConnection connection, WorldSession session)
    {
        LegacySecondaryPasswordRuntime.Invalidate(connection, session);
        if (!LegacySecondaryPasswordState.TryRead(session.Character.Attributes, out var stored, out var reason))
        {
            _log.WriteLine($"WorldServer secondary password initialization retained fail-closed state: {reason}.");
            return session;
        }
        var state = stored.LoadAt((_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime, out var expired);
        if (expired)
        {
            if (!LegacySecondaryPasswordPersistence.TryPersist(session, state, out reason))
            {
                if (reason.StartsWith("secondaryPasswordRecoveryRequired", StringComparison.Ordinal)) _tradeRecovery.Block(reason);
                _log.WriteLine($"WorldServer secondary password expiry retained fail-closed state: {reason}.");
                return session;
            }
            session = LegacySecondaryPasswordPersistence.WithState(session, state);
        }
        else if ((connection.NativeGameClient is not null || session.NativeGameClient is not null) &&
            !LegacySecondaryPasswordPersistence.TryValidateBaseline(session, out reason))
        {
            // The permissive INI loader collapses duplicate keys before constructing Attributes.
            // A real Player must not gain the zero-PIN checked flag from that lossy projection.
            // Keep world login available, but retain the invalidated PIN/Trade owner until the
            // physical UID and exact SecurityPass baseline can be proven on a fresh initialization.
            _log.WriteLine($"WorldServer secondary password startup baseline retained fail-closed state: {reason}.");
            return session;
        }
        // Unknown/manual sessions do not fabricate a physical client or Player.
        // Their zero-PIN compatibility path remains unprotected, as before.
        LegacySecondaryPasswordRuntime.TryInitialize(connection, session, state);
        return session;
    }
}
