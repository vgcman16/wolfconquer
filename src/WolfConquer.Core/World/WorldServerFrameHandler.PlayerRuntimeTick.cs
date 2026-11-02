using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private bool TryCommitPlayerRuntimeTick(ConquerTcpConnection connection, long uid, object workerIdentity,
        LegacyRuntimeUpdateTicker ticker, DateTimeOffset now, bool ownedStamina,
        out LegacyRuntimeUpdateTicker nextTicker, out IReadOnlyList<LegacyRuntimeUpdate> updates,
        out PlayerTickXpPermit? wirePermit)
    {
        nextTicker = ticker;
        updates = []; wirePermit = null;
        lock (RequireActionLifecycleGate(_sessions))
        {
            if (_tradeRecovery.IsBlocked || _tradeStates.HasConnectionEnded(connection) ||
                _ownedAdmissionRuntime is { IsStopping: true } ||
                !_creationLifetimes.TryGetValue(connection, out var lifetime) || !lifetime.IsCurrent(connection) ||
                !LegacyNativeClientPublication.TryCurrent(_sessions, connection, uid, workerIdentity, out var current)) return false;
            if (current.NativeGameClient?.OrdinarySpellBinding is { } binding)
            {
                if (!ReferenceEquals(binding.NativeIdentity, workerIdentity) || !_sessions.IsBoundSpellOwnerCurrent(binding)) return false;
                if (_ownedAdmissionRuntime is not null)
                {
                    var context = CreateMonsterRuntimeContext(new(connection, current));
                    if (!TryPrepareActionContext(_sessions, context, current, out var actual) ||
                        !IsActionLifecycleCurrent(_sessions, actual)) return false;
                }
            }
            else if (_ownedAdmissionRuntime is not null) return false;

            // A clean refusal discards all staged ticker clocks, counters,
            // status words and invitation slots. No output precedes commitment.
            var prepared = ticker.CopyForPreparation();
            var due = ownedStamina ? prepared.CreateDueNonStaminaUpdates(now, current.Character) :
                prepared.CreateDueUpdates(now, current.Character);
            var character = current.Character;
            foreach (var update in due)
            {
                if (update.RuntimeStamina is { } stamina) character = ProjectLegacyRuntimeStamina(character, stamina);
                if (update.RuntimeXpCount is not null || update.RuntimeXpListActive is not null)
                    character = ProjectLegacyRuntimeXpState(character, update.RuntimeXpCount, update.RuntimeXpListActive);
            }
            // Only the single actual XP-circle output has a complete closed
            // gate-time owner here. Other ticker families retain their prior path.
            var candidateWire = PreparePlayerTickXpWire(connection, uid, workerIdentity, current, due);
            if (!ReferenceEquals(character, current.Character) &&
                !_sessions.TryCompareAndSetWithNativeIdentity(connection, current, workerIdentity,
                    current with { Character = character }, out _)) { DiscardPlayerTickXpWire(candidateWire); return false; }
            if (ReferenceEquals(character, current.Character) &&
                (!LegacyNativeClientPublication.TryCurrent(_sessions, connection, uid, workerIdentity, out var unchanged) ||
                    !ReferenceEquals(unchanged, current))) { DiscardPlayerTickXpWire(candidateWire); return false; }
            if (candidateWire is not null) CommitPlayerTickXpWire(candidateWire);
            wirePermit = candidateWire;
            nextTicker = prepared;
            updates = due;
            return true;
        }
    }
}
