using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed record BasicPhysicalRetirement(SessionHolder Holder, WorldSession Expected,
        LegacyOrdinarySpellBinding Binding, LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot Before);
    private readonly ConditionalWeakTable<LegacyOrdinarySpellBinding, BasicPhysicalRetirement> basicPhysicalRetirements = new();

    internal bool IsBasicPhysicalRetirementOwnerCurrent(LegacyOrdinarySpellBinding binding,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot snapshot) =>
        basicPhysicalRetirements.TryGetValue(binding, out var actual) && ReferenceEquals(actual.Before, snapshot) &&
        IsActualBasicPhysicalRetirement(actual, binding, actual.Expected, snapshot);

    private bool IsActualBasicPhysicalRetirement(BasicPhysicalRetirement actual, LegacyOrdinarySpellBinding binding,
        WorldSession expected, LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot snapshot) =>
        basicPhysicalRetirements.TryGetValue(binding, out var active) && ReferenceEquals(active, actual) &&
        ReferenceEquals(actual.Binding, binding) && ReferenceEquals(actual.Expected, expected) &&
        ReferenceEquals(actual.Before, snapshot) && ReferenceEquals(binding.Store, this) &&
        _connectionGates.TryGetValue(binding.Connection, out var gate) && Monitor.IsEntered(gate) &&
        IsCurrentThreadInsideAnyConnectionGate && _sessions.TryGetValue(binding.Connection, out var holder) &&
        ReferenceEquals(holder, actual.Holder) && ReferenceEquals(holder.Read(), expected) &&
        ReferenceEquals(holder.SpellBinding, binding) && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity) &&
        ReferenceEquals(expected.NativeGameClient, binding.Client) && expected.AccountId == binding.AccountId &&
        expected.Character.Uid == binding.Uid && Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) &&
        Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        framePersistenceOwner?.IsActualBasicPhysicalRetirementSnapshot(binding, snapshot) == true;

    // Only SessionHolder.Clear invokes this. No live action can use a blocked
    // recovery owner: the sole mutation here is actual retirement's true->false
    // native repeat flag. Descriptor, stamp, files and resources stay untouched.
    private void RetireBasicPhysicalBinding(SessionHolder holder, LegacyOrdinarySpellBinding binding)
    {
        if (framePersistenceOwner is not { } frame) return;
        try
        {
            if (holder.Read() is not { } expected || !ReferenceEquals(holder.SpellBinding, binding) ||
                !frame.TryGetBasicPhysicalRetirementSnapshot(binding, out var before) || before is null) return;
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
            {
                var retirement = new BasicPhysicalRetirement(holder, expected, binding, before);
                basicPhysicalRetirements.Add(binding, retirement);
                try
                {
                    if (!before.IsCurrentForRetirementPlain(binding) || !before.OnAutoAttack) return;
                    var change = IssueBasicPhysicalChange(frame, binding, expected, before, 0,
                        LegacyNativeGameClientRuntime.BasicPhysicalNativeKind.CancelRepeat, null, null, null, retirement);
                    var publication = LegacyNativeGameClientRuntime.OwnedBasicPhysicalPublication.Prepare(binding, change);
                    if (publication.IsCurrentPlain()) publication.PublishPrepared();
                }
                finally { basicPhysicalRetirements.Remove(binding); }
            }
        }
        finally { frame.RetireBasicPhysicalGeneration(binding); }
    }
}
