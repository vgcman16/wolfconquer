using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool IsBasicPhysicalAttemptPacketCurrent(WorldPacketHandlerContext context, BasicPhysicalAttempt attempt,
        MsgAttackPacket packet) => ReferenceEquals(context.BasicPhysicalAttempt, attempt) &&
        basicPhysicalAttempts.TryGetValue(attempt, out var issued) && ReferenceEquals(issued.Slot.Attempt, attempt) &&
        IsBasicPhysicalAttemptGenerationCurrent(issued) && packet.Query == issued.Query;

    internal bool TryGetBasicPhysicalAttemptRoles(WorldPacketHandlerContext context, BasicPhysicalAttempt attempt,
        out LegacyOrdinarySpellBinding attacker, out LegacyOrdinarySpellBinding target)
    {
        attacker = target = null!;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || !ReferenceEquals(context.Sessions, _sessions) ||
            !ReferenceEquals(context.Trades, _tradeStates) || !ReferenceEquals(context.TradeRecovery, _tradeRecovery) ||
            !ReferenceEquals(context.TradeOperations, _worldOperations) || !ReferenceEquals(context.WorldData, _worldData) ||
            context.ProtocolProfile != _protocolProfile || !ReferenceEquals(context.BasicPhysicalAttempt, attempt) ||
            !basicPhysicalAttempts.TryGetValue(attempt, out var issued) ||
            !ReferenceEquals(issued.Slot.Attempt, attempt) || !IsBasicPhysicalAttemptGenerationCurrent(issued) ||
            !IsStaminaReadinessCurrent(issued.Slot.Readiness) ||
            !ReferenceEquals(context.Connection, issued.Attacker.Connection) && !ReferenceEquals(context.Connection, issued.Target.Connection) ||
            _tradeStates.HasConnectionEnded(issued.Target.Connection) ||
            !_sessions.IsBoundSpellOwnerCurrent(issued.Attacker) || !_sessions.IsBoundSpellOwnerCurrent(issued.Target)) return false;
        attacker = issued.Attacker; target = issued.Target;
        return true;
    }

    private static bool IsBasicPhysicalAttemptGenerationCurrent(BasicPhysicalAttemptState issued) =>
        issued.CommittedGeneration < 0 ? issued.Slot.Generation == issued.Generation :
            issued.Slot.Generation == issued.CommittedGeneration;

    internal bool IsBasicPhysicalPublicationGenerationCurrent(WorldPacketHandlerContext context,
        WorldPacketHandlers.BasicPhysicalReceiveDecision decision,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot native) =>
        Monitor.IsEntered(_tradeStates.SyncRoot) && context.BasicPhysicalAttempt is { } attempt &&
        WorldPacketHandlers.TryReadBasicPhysicalReceive(decision, out var actualAttempt, out _, out _, out _, out _) &&
        ReferenceEquals(actualAttempt, attempt) && basicPhysicalAttempts.TryGetValue(attempt, out var issued) &&
        ReferenceEquals(issued.Slot.Attempt, attempt) && issued.CommittedGeneration >= 0 &&
        issued.Slot.Generation == issued.CommittedGeneration && ReferenceEquals(issued.Slot.Native, native) &&
        IsStaminaReadinessCurrent(issued.Slot.Readiness);

    internal void EndBasicPhysicalAttempt(WorldSessionStore store, BasicPhysicalAttempt attempt)
    {
        if (!ReferenceEquals(store, _sessions)) return;
        lock (_tradeStates.SyncRoot)
            if (basicPhysicalAttempts.TryGetValue(attempt, out var issued) && ReferenceEquals(issued.Slot.Attempt, attempt))
                issued.Slot.Attempt = null;
    }
}
