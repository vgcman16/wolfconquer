using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal void ThrowDragonhowlFailure(WorldPacketHandlers.OwnedDragonhowlPreparation plan, Exception primary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        if (!dragonhowlOwners.TryGetValue(plan.Owner, out var state) || !ReferenceEquals(state.Plan, plan) || !state.Committed)
        { ExceptionDispatchInfo.Capture(primary).Throw(); return; }
        var failures = new List<Exception> { primary };
        lock (state.Frame.RequireActionLifecycleGate(this))
        {
            state.Failed = true;
            // Denial-only cleanup retains the originally registered bindings,
            // even if a publication observer failed before the caller returned.
            foreach (var recovery in state.Bindings.Values.Select(row => row.Recovery).Distinct())
                try { recovery.Block("ownedDragonhowlCommittedProgressionInterrupted"); }
                catch (Exception error) { failures.Add(error); }
            foreach (var binding in state.Bindings.Values)
                try { FenceBasicPhysicalBinding(binding); }
                catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count != 1)
            throw new AggregateException("Owned Dragonhowl progression and required cleanup failed.", failures);
        ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
