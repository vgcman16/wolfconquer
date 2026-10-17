namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> TryApplyLegacyStarFlowFlashingNameAsync(
        WorldPacketHandlerContext context, WorldSession session, WorldSession target,
        CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session)) return null;
        // Native AddFlag rejects duplicates: neither known nor historical0 receives a new timer.
        if (HasLegacyStatusFlag(session.Character, LegacyFlashingNameStatusFlag) ||
            HasLegacyPlayerMagicInterserverAttribute(session)) return session;
        var pkMode = ReadAttributeUInt64(session.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        if (pkMode == LegacyPkModeCapture || pkMode == LegacyPkModePeace ||
            !CanApplyLegacyPlayerMagicCanAttackFlashingName(target.Character)) return session;

        var before = session.Character;
        var added = WithLegacyStatusFlag(before, LegacyFlashingNameStatusFlag);
        session = session with { Character = LegacyStatusFlagDeathRegistration.CaptureAddFlag(
            before, added, LegacyFlashingNameStatusFlag, removeOnDead: true) };
        session = LegacyCanAttackFlashingRegistration.Create(
            uid, context.TimeProvider ?? TimeProvider.System).Install(session);
        context.Sessions?.Set(context.Connection, session);
        // One captured vector; owner first, fresh visible peer snapshot after that await.
        // Original owner/peer UID fences are adapter safety, not invented same-UID generations.
        return await SendLegacyArrowBladesVectorAsync(
            context with { Session = session }, session, uid, cancellationToken).ConfigureAwait(false);
    }
}
