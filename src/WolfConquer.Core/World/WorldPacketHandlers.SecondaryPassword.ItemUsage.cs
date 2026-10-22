namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Native MsgItemUsuagePacket SellItem and DropItem silently exit before
    // item/Anima work when the physical Player is not checked. Anima is separate.
    private static bool TryAdmitLegacySecondaryPasswordItemUsage(WorldPacketHandlerContext context,
        WorldSession suppliedSession, out WorldSession currentSession, out string reason)
    {
        currentSession = suppliedSession;
        reason = string.Empty;
        if (context.Sessions is { } sessions)
        {
            if (!sessions.TryGet(context.Connection, out var stored) || stored is null)
            { reason = "currentSessionMissing"; return false; }
            currentSession = stored;
        }
        if (LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, currentSession)) return true;
        reason = "secondaryPasswordUnverifiedOrOwnerInvalid";
        return false;
    }
}
