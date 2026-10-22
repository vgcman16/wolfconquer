namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Native MsgFloorItem/MsgItemPacket.cs:116..122 checks InTrade and
    // OnMyOwnServer before parsing/lookup. Pickup has no secondary-PIN gate.
    private static bool TryAdmitLegacyFloorPickup(WorldPacketHandlerContext context,
        out WorldSession currentSession, out string reason)
    {
        currentSession = null!;
        reason = "currentSessionMissing";
        if (context.TradeRecovery?.IsBlocked == true)
        { reason = "recoveryRequired"; return false; }
        if (context.Trades?.HasConnectionEnded(context.Connection) == true)
        { reason = "connectionEnded"; return false; }
        if (context.Sessions is { } sessions)
        {
            if (!sessions.TryGet(context.Connection, out var stored) || stored is null) return false;
            currentSession = stored;
        }
        else if (context.Session is { } supplied) currentSession = supplied;
        else return false;

        if (currentSession.LegacyTradeActive)
        { reason = "tradeActive"; return false; }
        if (context.Trades is { } trades)
        {
            // Only the live dictionary read holds its existing gate. Do not call
            // the session store or another owner runtime while holding this lock.
            lock (trades.SyncRoot)
            {
                // Native GameClient.InTrade (430..439) reads WindowOpen;
                // a request-only pair is not yet an active trade window.
                if (trades.TryGet(context.Connection, out var party) && party.WindowOpen)
                { reason = "tradeActive"; return false; }
            }
        }
        if (!LegacyTradeEscrowClaims.TryRead(currentSession.Character.Attributes, out var claim, out var claimReason))
        { reason = "tradeEscrowInvalid:" + claimReason; return false; }
        if (claim is not null)
        { reason = "tradeEscrowUnresolved"; return false; }

        // Only wholly unbound legacy fixtures retain the no-identity fallback.
        // Any physical owner requires the privately issued current binding;
        // public ServerID or saved attributes cannot manufacture own-server state.
        if ((context.Connection.NativeGameClient is not null || currentSession.NativeGameClient is not null) &&
            !LegacyServerIdentityRuntime.IsOnOwnServer(context.Connection, currentSession, context.LocalServerIdentity))
        { reason = "ownServerIdentityUnverifiedOrOwnerInvalid"; return false; }
        reason = string.Empty;
        return true;
    }
}
