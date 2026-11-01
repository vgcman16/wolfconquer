using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Empty tokens confer nothing without this real Frame's private issued
    // state. Neither a packet caller nor a test can register an accepted hit.
    internal sealed class BasicPhysicalRequest { }
    internal sealed class BasicPhysicalAttempt { }

    private sealed class BasicPhysicalSlot(LegacyOwnedStaminaRuntime.Readiness readiness,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot native)
    {
        internal readonly LegacyOwnedStaminaRuntime.Readiness Readiness = readiness;
        internal long Generation;
        internal DateTime AttackStamp = native.AttackStamp;
        internal LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot Native = native;
        internal BasicPhysicalAttempt? Attempt;
        internal WorldSessionStore.BasicPhysicalHitReceipt? Committed;
        internal BasicPhysicalRepeat? Repeat;
    }
    private sealed record BasicPhysicalRequestState(BasicPhysicalSlot Slot, long Generation, MsgAttackQuery Query,
        BasicPhysicalRepeat? Repeat = null)
    {
        internal bool Begun;
    }
    private sealed record BasicPhysicalAttemptState(BasicPhysicalSlot Slot, long Generation, MsgAttackQuery Query,
        LegacyOrdinarySpellBinding Attacker, LegacyOrdinarySpellBinding Target, DateTime Stamp)
    {
        internal long CommittedGeneration = -1;
    }
    private readonly ConditionalWeakTable<ConquerTcpConnection, BasicPhysicalSlot> basicPhysicalSlots = new();
    private readonly ConditionalWeakTable<BasicPhysicalRequest, BasicPhysicalRequestState> basicPhysicalRequests = new();
    private readonly ConditionalWeakTable<BasicPhysicalAttempt, BasicPhysicalAttemptState> basicPhysicalAttempts = new();

    private void RegisterBasicPhysicalReadiness(LegacyOwnedStaminaRuntime.Readiness readiness)
    {
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || !IsStaminaReadinessCurrent(readiness))
            throw new InvalidOperationException("basicPhysicalReadinessOwnerInvalid");
        if (basicPhysicalSlots.TryGetValue(readiness.Connection, out var previous))
        {
            if (!ReferenceEquals(previous.Readiness, readiness)) throw new InvalidOperationException("basicPhysicalReadinessChanged");
            return;
        }
        if (!_sessions.TryCaptureBasicPhysicalReadiness(readiness, out var native) || native is null ||
            native.OnAutoAttack || native.Request is not null || native.AttackStamp != DateTime.MinValue)
            throw new InvalidOperationException("basicPhysicalColdNativeOwnerInvalid");
        basicPhysicalSlots.Add(readiness.Connection, new(readiness, native));
    }

    private BasicPhysicalRequest? CaptureBasicPhysicalRequest(WorldPacketHandlerContext context, PacketFrame frame)
    {
        if (!MsgAttackPacket.TryParse(frame, out var packet) || packet.Query.AttackType != MsgAttackType.Physical ||
            packet.Query.Damage > 0 || context.Session is not { } expected ||
            !TryPrepareActionContext(_sessions, context, expected, out _) ||
            !basicPhysicalSlots.TryGetValue(context.Connection, out var slot)) return null;
        lock (_tradeStates.SyncRoot)
        {
            if (!IsStaminaReadinessCurrent(slot.Readiness) || packet.Query.Uid != slot.Readiness.Binding.Uid ||
                !_sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected)) return null;
            var token = new BasicPhysicalRequest();
            basicPhysicalRequests.Add(token, new(slot, slot.Generation, packet.Query));
            return token;
        }
    }

    internal bool TryBeginBasicPhysical(WorldPacketHandlerContext context, MsgAttackPacket packet,
        WorldActiveSession target, out BasicPhysicalAttempt? attempt)
    {
        attempt = null;
        if (context.BasicPhysicalRequest is not { } request ||
            !basicPhysicalRequests.TryGetValue(request, out var issued) || packet.Query != issued.Query ||
            context.Session is not { } expected || !TryPrepareActionContext(_sessions, context, expected, out var actual)) return false;
        lock (_tradeStates.SyncRoot)
        {
            var slot = issued.Slot;
            var attacker = slot.Readiness.Binding;
            if (!IsStaminaReadinessCurrent(slot.Readiness) || !IsActionLifecycleCurrent(_sessions, actual) ||
                issued.Begun || slot.Generation != issued.Generation || slot.Attempt is not null ||
                issued.Repeat is not null && !ReferenceEquals(slot.Repeat, issued.Repeat) ||
                !ReferenceEquals(attacker.Connection, context.Connection) ||
                !ReferenceEquals(expected.NativeGameClient, attacker.Client) ||
                target.Session.NativeGameClient?.OrdinarySpellBinding is not { IsRetired: false } victim ||
                ReferenceEquals(attacker, victim) || !ReferenceEquals(victim.Store, _sessions) ||
                !ReferenceEquals(victim.Recovery, _tradeRecovery) || !ReferenceEquals(victim.ItemCatalog, _worldData) ||
                target.Session.Character.Uid != issued.Query.OpponentUid || !ReferenceEquals(victim.Connection, target.Connection) ||
                !_sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
                !_sessions.TryGet(target.Connection, out var currentTarget) || !ReferenceEquals(currentTarget, target.Session) ||
                !_sessions.IsBoundSpellOwnerCurrent(attacker) || !_sessions.IsBoundSpellOwnerCurrent(victim) ||
                !LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(attacker, out var speed) || speed is null) return false;
            using var nativeRead = BasicPhysicalReadScope.Enter(attacker);
            if (!slot.Native.IsCurrentPlain(attacker) || slot.Native.AttackStamp != slot.AttackStamp) return false;
            var attributes = new LegacyNativeAttributeMapProof(expected.Character.Attributes);
            if (expected.Character.Agility is < 0 or > ushort.MaxValue) return false;
            // Applicable special/status branches are joined separately. Do
            // not reinterpret them as the ordinary repeat algorithm.
            if (!TryReadBasicPhysicalOrdinaryFlags(attacker, expected, out var flagProof)) return false;
            if (!LegacyBasicPhysicalRuneSelection.TryCapture(attacker, expected, out var runeProof) ||
                runeProof is null || runeProof.HasDeadlySight) return false;
            var stamp = slot.Readiness.ReadLocalNow();
            int delay = CalculateBasicPhysicalOrdinaryDelay(speed, checked((ushort)expected.Character.Agility));
            if (stamp < slot.AttackStamp || stamp < slot.AttackStamp.AddMilliseconds(delay)) return false;
            long generation = slot.Generation;
            _ = checked(generation + 1); // Representability before any accepted state change.
            var token = new BasicPhysicalAttempt();
            var state = new BasicPhysicalAttemptState(slot, generation, issued.Query, attacker, victim, stamp);
            if (!attributes.IsCurrentPlain() || !speed.IsCurrentPlain(attacker) || !flagProof.IsCurrentPlain() || !runeProof.IsCurrentPlain() ||
                !IsStaminaReadinessCurrent(slot.Readiness) || slot.Generation != issued.Generation ||
                !_sessions.TryGet(context.Connection, out current) || !ReferenceEquals(current, expected) ||
                !_sessions.TryGet(target.Connection, out currentTarget) || !ReferenceEquals(currentTarget, target.Session)) return false;
            basicPhysicalAttempts.Add(token, state);
            issued.Begun = true;
            slot.Attempt = token;
            attempt = token;
            return true;
        }
    }

    private static int CalculateBasicPhysicalOrdinaryDelay(LegacyNativeMagicQueryClient.AttackSpeedCache speed, ushort agility)
    {
        int delay = Math.Max(300, 800 - 100);
        delay = Math.Max(300, delay - speed.Right);
        delay = Math.Max(300, delay - speed.Left);
        delay = Math.Max(300, delay - speed.Ring);
        return Math.Max(300, delay - agility / 2);
    }
}
