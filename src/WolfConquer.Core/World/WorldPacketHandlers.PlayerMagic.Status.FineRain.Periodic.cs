namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static async ValueTask<WorldSession?> ExecuteLegacyFineRain212InvocationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyFineRain212Invocation captured,
        DateTimeOffset capturedNowUtc,
        ILegacySignedMillisecondClock environmentClock,
        CancellationToken cancellationToken)
    {
        var scope = new LegacyEquipmentQueryPublicationScope(captured.OwnerUid);
        var fresh = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!fresh.Completed) return null;
        session = fresh.Session!;
        var provider = context.TimeProvider ?? TimeProvider.System;
        var capturedNow = LegacyFineRain212Invocation.LocalTime(provider, capturedNowUtc);
        if (capturedNow < captured.TimerInvoker.AddSeconds(1)) return session;

        // CheckInvoke advances BEFORE the independent current-power predicate.
        var invoked = captured with
        {
            TimerInvoker = LegacyFineRain212Invocation.LocalTime(provider, provider.GetUtcNow()),
        };
        if (LegacyFineRain212Invocation.TryRead(session.Character, out var active) &&
            active.Identity == captured.Identity)
        {
            session = session with { Character = invoked.Install(session.Character) };
            context.Sessions?.Set(context.Connection, session);
        }

        var power = ReadAttributeUInt32(session.Character.Attributes, RuntimeLegacyFineRainPowerAttribute);
        if (power == 0) return session;
        var defaultPower = ReadAttributeUInt32(session.Character.Attributes, RuntimeLegacyFineRainDefaultPowerAttribute);
        // uint/int promotes to long; preserve signed-int cast in the next predicate.
        var decrement = unchecked((uint)(defaultPower / (long)captured.Seconds));
        if ((long)power - unchecked((int)decrement) > 0)
            power = unchecked(power - decrement);
        else
        {
            power = 0;
            decrement = 0;
        }

        session = session with
        {
            Character = WithCharacterAttribute(session.Character, RuntimeLegacyFineRainPowerAttribute, power),
        };
        context.Sessions?.Set(context.Connection, session);
        var time = LegacyFineRainCountdownClock.Read(
            context.LegacyClock ?? LegacySignedMillisecondClock.System,
            environmentClock, provider, captured.Timer, captured.Seconds);
        var frame = new LegacyMsgUpdatePacket(CheckedUInt32(captured.OwnerUid, nameof(captured.OwnerUid)),
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.FineRain, Value: 0,
                Flag: 212, Time: time, Damage: power, SpellLevel: power)]).ToFrameBytes();
        // Periodic source client.Send is SELF ONLY, unlike initial SendUpdate(scren:true).
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        fresh = RefreshLegacyEquipmentQueryPublication(context, session, scope);
        if (!fresh.Completed) return null;
        session = fresh.Session!;

        var candidateMaximum = unchecked(GetLegacyEquipmentRuntimeMaxHitpoints(session, context.WorldData) - decrement);
        var maximum = unchecked((int)candidateMaximum) > 0 ? candidateMaximum : 1U;
        session = session with
        {
            EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(captured.OwnerUid, maximum),
        };
        context.Sessions?.Set(context.Connection, session);
        if (session.Character.MinHitPoints <= (long)maximum) return session;

        var assignment = await AssignLegacyPlayerHitpointsAsync(
                context with { Session = session }, session, scope, unchecked((int)maximum), cancellationToken)
            .ConfigureAwait(false);
        return assignment.Completed ? assignment.Session : null;
    }
}
