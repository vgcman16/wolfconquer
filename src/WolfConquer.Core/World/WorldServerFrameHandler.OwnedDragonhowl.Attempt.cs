using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal sealed class OwnedDragonhowlAttempt { }
    private sealed class DragonhowlAttemptState
    {
        internal readonly OwnedDragonhowlRequest Request;
        internal readonly DragonhowlRequestState Source;
        internal readonly BasicPhysicalAttempt Reservation;
        internal readonly BasicPhysicalAttemptState? PlayerAttempt;
        internal readonly long Generation;
        internal readonly DateTime Stamp;
        private long committedGeneration = -1;
        internal long CommittedGeneration => PlayerAttempt?.CommittedGeneration ?? committedGeneration;

        internal DragonhowlAttemptState(OwnedDragonhowlRequest request, DragonhowlRequestState source,
            BasicPhysicalAttempt actual, BasicPhysicalAttemptState player)
        { Request = request; Source = source; Reservation = actual; PlayerAttempt = player; Generation = player.Generation; Stamp = player.Stamp; }
        internal DragonhowlAttemptState(OwnedDragonhowlRequest request, DragonhowlRequestState source,
            BasicPhysicalAttempt reservation, long generation, DateTime stamp)
        { Request = request; Source = source; Reservation = reservation; Generation = generation; Stamp = stamp; }

        internal void RecordCommittedGeneration(long value)
        {
            if (PlayerAttempt is not null) PlayerAttempt.CommittedGeneration = value;
            else committedGeneration = value;
        }
    }
    private readonly ConditionalWeakTable<OwnedDragonhowlAttempt, DragonhowlAttemptState> dragonhowlAttempts = new();

    internal bool TryBeginDragonhowlAttempt(OwnedDragonhowlRequest request, out OwnedDragonhowlAttempt? attempt)
    {
        attempt = null;
        if (!dragonhowlRequests.TryGetValue(request, out var source) || !source.Evaluated || source.Selection is null ||
            !DragonhowlBeforeCurrent(source) || !dragonhowlPrimaries.TryGetValue(source.Primary, out var primary)) return false;
        var slot = source.Wire.Slot;
        DragonhowlAttemptState state;
        if (primary.Player is { } player)
        {
            if (!TryBeginBasicPhysical(source.Context, new MsgAttackPacket(source.Wire.Query), player, out var actual) || actual is null ||
                !basicPhysicalAttempts.TryGetValue(actual, out var issued)) return false;
            state = new(request, source, actual, issued);
        }
        else
        {
            var actor = source.Actor;
            var expected = source.Context.Session!;
            if (!LegacyNativeMagicQueryClient.TryReadAttackSpeedCache(actor, out var speed) || speed is null ||
                expected.Character.Agility is < 0 or > ushort.MaxValue ||
                !TryReadBasicPhysicalOrdinaryFlags(actor, expected, out var flags) ||
                !LegacyBasicPhysicalRuneSelection.TryCapture(actor, expected, out var runes) || runes is null || runes.HasDeadlySight)
                return false;
            var attributes = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.Attributes);
            var stamp = slot.Readiness.ReadLocalNow();
            var delay = CalculateBasicPhysicalOrdinaryDelay(speed, checked((ushort)expected.Character.Agility));
            if (stamp < slot.AttackStamp || stamp < slot.AttackStamp.AddMilliseconds(delay) ||
                !attributes.IsCurrentPlain() || !speed.IsCurrentPlain(actor) || !flags.IsCurrentPlain() || !runes.IsCurrentPlain() ||
                !DragonhowlBeforeCurrent(source)) return false;
            _ = checked(slot.Generation + 1);
            // This object is strictly the existing slot's exclusion identity,
            // exactly as for MonsterPhysicalAttempt. It is NEVER entered in
            // basicPhysicalAttempts and cannot authorize a basic hit/death.
            state = new(request, source, new BasicPhysicalAttempt(), slot.Generation, stamp);
        }
        var token = new OwnedDragonhowlAttempt();
        try { dragonhowlAttempts.Add(token, state); }
        catch
        {
            if (state.PlayerAttempt is not null) EndBasicPhysicalAttempt(_sessions, state.Reservation);
            throw;
        }
        if (state.PlayerAttempt is null)
        {
            source.Wire.Begun = true;
            slot.Attempt = state.Reservation;
        }
        attempt = token; return true;
    }

    private bool DragonhowlAttemptCurrent(OwnedDragonhowlAttempt attempt, OwnedDragonhowlRequest request,
        out DragonhowlAttemptState state)
    {
        state = null!;
        if (!Monitor.IsEntered(_tradeStates.SyncRoot) || !dragonhowlAttempts.TryGetValue(attempt, out var actual) ||
            !ReferenceEquals(actual.Request, request) || !dragonhowlRequests.TryGetValue(request, out var source) ||
            !ReferenceEquals(actual.Source, source) || !source.Wire.Begun ||
            !ReferenceEquals(source.Wire.Slot.Attempt, actual.Reservation) ||
            source.Wire.Slot.Generation != (actual.CommittedGeneration < 0 ? actual.Generation : actual.CommittedGeneration) ||
            !IsStaminaReadinessCurrent(source.Wire.Slot.Readiness)) return false;
        if (actual.PlayerAttempt is { } player &&
            (!basicPhysicalAttempts.TryGetValue(actual.Reservation, out var registered) || !ReferenceEquals(player, registered))) return false;
        state = actual; return true;
    }

    internal bool IsDragonhowlBasicAttempt(OwnedDragonhowlAttempt actual, BasicPhysicalAttempt basic) =>
        dragonhowlAttempts.TryGetValue(actual, out var state) && state.PlayerAttempt is not null &&
        ReferenceEquals(state.Reservation, basic) && DragonhowlAttemptCurrent(actual, state.Request, out _);

    internal void EndDragonhowlAttempt(OwnedDragonhowlAttempt attempt)
    {
        lock (_tradeStates.SyncRoot)
            if (dragonhowlAttempts.TryGetValue(attempt, out var state) &&
                DragonhowlAttemptCurrent(attempt, state.Request, out _)) state.Source.Wire.Slot.Attempt = null;
    }
}
