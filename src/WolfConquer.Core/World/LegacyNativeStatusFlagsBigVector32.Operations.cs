using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    public bool TryAdd(int flag, int Secounds, bool RemoveOnDead, int InvokerSecouds)
    {
        lock (SyncRoot)
        {
            try { return TryPrepareAdd(flag, Secounds, RemoveOnDead, InvokerSecouds, false, out var change) && change!.TryCommit(this); }
            catch (Exception) { return false; }
        }
    }

    public bool TryReplace(int flag, int seconds, bool removeOnDeath, int invoker)
    {
        lock (SyncRoot)
        {
            try { return TryPrepareAdd(flag, seconds, removeOnDeath, invoker, true, out var change) && change!.TryCommit(this); }
            catch (Exception) { return false; }
        }
    }

    public bool TryRemove(int flag)
    {
        lock (SyncRoot)
        {
            try
            {
                if (!TryCapture(out var state) || !state!.Registrations.ContainsKey(flag)) return false;
                var staged = new ConcurrentDictionary<int, Flag>(state.Registrations, state.Registrations.Comparer);
                if (!staged.TryRemove(flag, out _)) return false;
                var words = (uint[])state.Words.Clone();
                words[flag / 32] &= ~(1u << (flag % 32));
                return PrepareChange(state, staged, words).TryCommit(this);
            }
            catch (Exception) { return false; }
        }
    }

    public bool TryContainFlag(int flag, out bool present)
    {
        present = false;
        lock (SyncRoot)
        {
            try
            {
                if (!TryCapture(out var state) || !ValidIndex(flag, state!.Words) || !IsCurrent(state)) return false;
                bool candidate = state!.Registrations.ContainsKey(flag);
                if (!HasUnchangedCapturedGraph(state) || candidate != (FindCapturedFlag(state, flag) is not null)) return false;
                present = candidate;
                return true;
            }
            catch (Exception) { return false; }
        }
    }

    public bool ContainFlag(int flag)
    {
        if (!TryContainFlag(flag, out bool present))
            throw new InvalidDataException("Status flag membership is unavailable for an invalid index or malformed physical graph.");
        return present;
    }

    public void GetClear() => TryClearDeath();

    public bool TryClearDeath()
    {
        lock (SyncRoot)
        {
            try
            {
                if (!TryCapture(out var state)) return false;
                var marked = state!.Rows.Where(row => row.RemoveOnDeath).ToArray();
                if (marked.Length == 0) return true;
                var staged = new ConcurrentDictionary<int, Flag>(state.Registrations, state.Registrations.Comparer);
                var words = (uint[])state.Words.Clone();
                foreach (var row in marked)
                {
                    if (!staged.TryRemove(row.Key, out _)) return false;
                    words[row.Key / 32] &= ~(1u << (row.Key % 32));
                }
                return PrepareChange(state, staged, words).TryCommit(this);
            }
            catch (Exception) { return false; }
        }
    }

    public Flag[] GetFlags()
    {
        lock (SyncRoot)
        {
            try
            {
                if (!TryCapture(out var state)) return [];
                // Never use an exposed cache array as authority for expiry/death removal.
                // Refresh from the real registrations; callers receive a defensive array.
                var current = state!.Rows.Select(row => row.Row).ToArray();
                if (!IsCurrent(state)) return [];
                var exposed = (Flag[])current.Clone();
                if (Array is null || !Array.SequenceEqual(current, ReferenceEqualityComparer.Instance)) Array = current;
                return exposed;
            }
            catch (Exception) { return []; }
        }
    }
}
