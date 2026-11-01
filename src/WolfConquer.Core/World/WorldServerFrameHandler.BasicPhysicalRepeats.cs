using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal sealed class BasicPhysicalRepeatTicket { }
    private sealed record BasicPhysicalRepeatTicketData(BasicPhysicalSlot Slot, BasicPhysicalRepeat Repeat, long Generation);
    private readonly ConditionalWeakTable<BasicPhysicalRepeatTicket, BasicPhysicalRepeatTicketData> basicPhysicalRepeatTickets = new();

    internal bool IsActualBasicPhysicalServerRequest(WorldPacketHandlerContext context, MsgAttackPacket packet)
    {
        if (context.BasicPhysicalRequest is not { } request || !basicPhysicalRequests.TryGetValue(request, out var issued) ||
            issued.Repeat is null || issued.Query != packet.Query) return false;
        lock (_tradeStates.SyncRoot)
            return !issued.Begun && IsStaminaReadinessCurrent(issued.Slot.Readiness) &&
                ReferenceEquals(issued.Slot.Readiness.Connection, context.Connection) &&
                ReferenceEquals(issued.Slot.Repeat, issued.Repeat) && issued.Slot.Generation == issued.Generation;
    }

    internal bool IsBasicPhysicalRepeatTicketCurrent(WorldSessionStore store, BasicPhysicalRepeatTicket ticket,
        LegacyOrdinarySpellBinding binding) => ReferenceEquals(store, _sessions) && Monitor.IsEntered(_tradeStates.SyncRoot) &&
        basicPhysicalRepeatTickets.TryGetValue(ticket, out var actual) && ReferenceEquals(actual.Slot.Readiness.Binding, binding) &&
        ReferenceEquals(actual.Slot.Repeat, actual.Repeat) && actual.Slot.Generation == actual.Generation &&
        IsStaminaReadinessCurrent(actual.Slot.Readiness);

    internal bool TryIssueDueBasicPhysicalRequest(WorldSessionStore store, WorldSession current,
        out BasicPhysicalRequest? request, out MsgAttackPacket? packet, out BasicPhysicalRepeatTicket? ticket)
    {
        request = null; packet = null; ticket = null;
        var binding = current.NativeGameClient?.OrdinarySpellBinding;
        if (binding is null || !ReferenceEquals(store, _sessions) || !Monitor.IsEntered(_tradeStates.SyncRoot) ||
            !basicPhysicalSlots.TryGetValue(binding.Connection, out var slot) || !ReferenceEquals(slot.Readiness.Binding, binding) ||
            slot.Repeat is not { } repeat || slot.Attempt is not null || slot.Generation != repeat.Generation ||
            !IsStaminaReadinessCurrent(slot.Readiness)) return false;
        ticket = new BasicPhysicalRepeatTicket();
        basicPhysicalRepeatTickets.Add(ticket, new(slot, repeat, slot.Generation));
        if (!ReferenceEquals(slot.Native, repeat.Native) || !repeat.Native.IsCurrentPlain(binding) || !repeat.Native.OnAutoAttack ||
            repeat.Native.Request is not { AttackType: MsgAttackType.Physical } source ||
            !LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(binding, out var speed) || speed is null ||
            current.Character.Agility is < 0 or > ushort.MaxValue) return false;
        if (repeat.Monster is { } target &&
            (!target.TryReadCurrent(out var monster, out _, out _) || source.OpponentUid != monster.Uid ||
             !CanSeeMonster(current.Character, monster))) return false;
        if (repeat.Dragonhowl is { } proc && !IsDragonhowlRepeatTargetCurrent(proc, current, source.OpponentUid)) return false;
        var now = slot.Readiness.ReadLocalNow();
        var delay = CalculateBasicPhysicalOrdinaryDelay(speed, checked((ushort)current.Character.Agility));
        if (now < slot.AttackStamp || now < slot.AttackStamp.AddMilliseconds(delay))
        { ticket = null; return false; } // Not due: this entry retains its exact authority.
        if (!speed.IsCurrentPlain(binding)) return false;
        request = new BasicPhysicalRequest(); packet = new MsgAttackPacket(source);
        basicPhysicalRequests.Add(request, new(slot, slot.Generation, source, repeat));
        return true;
    }

    internal WorldPacketHandlerContext PrepareBasicPhysicalRuntimeContext(WorldSessionStore store,
        WorldActiveSession active)
    {
        if (!ReferenceEquals(store, _sessions) || !basicPhysicalSlots.TryGetValue(active.Connection, out var slot) ||
            !IsStaminaReadinessCurrent(slot.Readiness)) throw new InvalidOperationException("basicPhysicalRuntimeOwnerUnavailable");
        var context = CreateMonsterRuntimeContext(active);
        if (!TryPrepareActionContext(store, context, active.Session, out var actual))
            throw new InvalidOperationException("basicPhysicalRuntimeContextUnavailable");
        return actual;
    }
}
