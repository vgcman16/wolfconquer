namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Scan actual holders without materializing every active session for one query.
    // The selected holder is revalidated under its real connection/actor gates below.
    internal WorldActiveSession? FindUniqueRuntimeInspectionSession(uint accountId, uint uid)
    {
        WorldActiveSession? selected = null;
        foreach (var pair in _sessions)
        {
            var current = pair.Value.Read();
            if (current is null || current.AccountId != accountId || current.Character is not { } character || character.Uid != uid) continue;
            if (selected is not null) return null;
            selected = new(pair.Key, current);
        }
        return selected;
    }

    // Typed READ: no writer/acceptance callback, mutation lease, save or publication.
    // Uses the same package -> connection -> actor order as current typed owners.
    internal bool TryReadRuntimeInspection(LegacyOrdinarySpellBinding binding, WorldSession expected,
        Guid domain, out LegacyRuntimeInspectionSnapshot? snapshot)
    {
        snapshot = null;
        if (IsCurrentThreadInsideAnyConnectionGate || LegacyRuneRankingState.IsCurrentThreadInsideLease ||
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
            Monitor.IsEntered(binding.Storage.MutationGate) || Monitor.IsEntered(binding.Proficiencies.MutationGate)) return false;
        using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(binding.Connection));
        if (!_connectionGates.TryGetValue(binding.Connection, out var gate)) return false;
        lock (gate)
        {
            using var entered = EnterNativeConnectionGate();
            if (!_sessions.TryGetValue(binding.Connection, out var holder) || !BindingIsCurrentPlain(binding, holder, expected)) return false;
            using var actors = new SpellActorScope(binding);
            if (!BindingIsCurrentPlain(binding, holder, expected) ||
                !LegacySecondaryPasswordRuntime.TryGet(binding.Connection, expected, out _, out _)) return false;
            var managed = LegacyRuntimeInspectionProjection.CaptureManaged(expected);
            var character = LegacyRuntimeInspectionProjection.Character(expected);
            var native = LegacyRuntimeInspectionProjection.CaptureNative(binding);
            var nativeAgain = LegacyRuntimeInspectionProjection.CaptureNative(binding);
            if (!LegacyRuntimeInspectionProjection.SameNative(native, nativeAgain) ||
                !LegacyRuntimeInspectionProjection.SameManaged(managed, LegacyRuntimeInspectionProjection.CaptureManaged(expected)) ||
                character != LegacyRuntimeInspectionProjection.Character(expected) ||
                !BindingIsCurrentPlain(binding, holder, expected) ||
                !LegacySecondaryPasswordRuntime.TryGet(binding.Connection, expected, out _, out _)) return false;
            var rows = native.UnavailableReason is null ? native.Rows.Select(row => row.Value).ToArray() : null;
            var managedRows = managed.Rows.Select(row => row.Value).ToArray();
            snapshot = new(domain, checked((uint)expected.AccountId), checked((uint)expected.Character.Uid), DateTimeOffset.UtcNow,
                "VisibleDx8D13", character, native.Vitals, managedRows, rows,
                rows is null ? null : character.Hitpoints == native.Vitals.Hitpoints && character.MinMana == native.Vitals.Mana &&
                    managedRows.SequenceEqual(rows), native.UnavailableReason);
            return true;
        }
    }
}
