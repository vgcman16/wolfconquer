namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask SendDueLegacyArrowAndFlashingFlagExpirationsAsync(CancellationToken cancellationToken)
    {
        var provider = _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System;
        foreach (var candidate in _sessions.Snapshot())
        {
            var uid = candidate.Session.Character.Uid;
            if (!TryRefreshLegacyLightningShieldLifetimeSubject(candidate.Connection, uid, out var current)) continue;
            // Native captures one per-client now and GetFlags array before any entered expiry send.
            var localNow = LegacyArrowBladesFlagRegistration.LocalTime(provider, provider.GetUtcNow());
            var entries = new List<(int Flag, DateTime ExpiresAtLocal)>();
            // Retain the existing adapter capture sequence; native registry enumeration/history remains OPEN.
            foreach (var flag in new[] { LegacyArrowBladesFlagRegistration.FireFlag,
                LegacyArrowBladesFlagRegistration.WaterFlag, LegacyArrowBladesFlagRegistration.StarFlowFlag,
                LegacyArrowBladesFlagRegistration.StarFlowTriggerFlag })
                if (LegacyArrowBladesFlagRegistration.TryRead(current.Character, flag, out var entry))
                    entries.Add((flag, entry.ExpiresAtLocal));
            if (LegacyCanAttackFlashingRegistration.TryRead(current, out var flashing))
                entries.Add((LegacyCanAttackFlashingRegistration.StatusFlag, flashing.ExpiresAtLocal));

            foreach (var entry in entries)
            {
                if (localNow < entry.ExpiresAtLocal) continue;
                if (!TryRefreshLegacyLightningShieldLifetimeSubject(candidate.Connection, uid, out current)) break;
                // Captured due entry removes the CURRENT integer key; no rearm/identity-token cancellation.
                var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase);
                var removed = RemoveLegacyStatusFlag(attributes, entry.Flag);
                if (!removed && entry.Flag == LegacyCanAttackFlashingRegistration.StatusFlag) continue;
                attributes.Remove(LegacyStatusFlagDeathRegistration.Key(entry.Flag));
                current = current with { Character = current.Character with { Attributes = attributes } };
                _sessions.Set(candidate.Connection, current);
                if (!removed) continue;
                if (entry.Flag == LegacyCanAttackFlashingRegistration.StatusFlag)
                    await SendLegacyCanAttackFlashingExpiryVectorAsync(
                        candidate.Connection, uid, current, cancellationToken).ConfigureAwait(false);
                else
                    await SendLegacyArrowBladesExpiryVectorAsync(
                        candidate.Connection, uid, current, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
