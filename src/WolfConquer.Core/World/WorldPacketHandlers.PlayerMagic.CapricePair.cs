namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyCapricePairCapture(WorldSessionStore Store,
        WorldSessionStore.ProcParticipant Attacker, WorldSessionStore.ProcParticipant Target);

    private static LegacyCapricePairCapture? CaptureLegacyCapricePair(WorldPacketHandlerContext context,
        WorldSession? attacker, WorldActiveSession target)
    {
        if (attacker is null || !TryGetEquippedLegacyCapriceLevel(attacker.PlayerItems, out _) ||
            context.Sessions is not { } store ||
            !LegacyNativeClientPublication.TryCaptureIdentity(store, context.Connection, attacker.Character.Uid, out var attackerIdentity) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(store, target.Connection, target.Session.Character.Uid, out var targetIdentity) ||
            !LegacyNativeClientPublication.TryCurrent(store, context.Connection, attacker.Character.Uid, attackerIdentity, out var currentAttacker) ||
            !ReferenceEquals(currentAttacker, attacker) ||
            !LegacyNativeClientPublication.TryCurrent(store, target.Connection, target.Session.Character.Uid, targetIdentity, out var currentTarget) ||
            !ReferenceEquals(currentTarget, target.Session)) return null;
        return new(store, new(context.Connection, attacker, attackerIdentity),
            new(target.Connection, target.Session, targetIdentity));
    }

    private static WorldSessionStore.CapricePairReceipt CommitLegacyCapricePair(LegacyCapricePairCapture? capture,
        LegacyPlayerReceiveDamageModifierResult modifier)
    {
        if (capture is null) throw new LegacyCapricePairRefusalException("capricePairCapturedOwnerUnavailable");
        var attacker = capture.Attacker.Expected with { Character = modifier.Attacker };
        var target = capture.Target.Expected with { Character = modifier.Target };
        if (!LegacyCapricePairIntent.TryCreate(capture.Attacker.Expected, capture.Target.Expected, attacker, target, out var intent))
            throw new LegacyCapricePairRefusalException("capricePairPreparedTransferInvalid");
        if (!capture.Store.TryCommitCapricePair(capture.Attacker, attacker, capture.Target, target, intent!, out var receipt, out var reason))
            throw new LegacyCapricePairRefusalException(reason);
        return receipt!;
    }
}
