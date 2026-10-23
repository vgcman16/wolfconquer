using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    private readonly record struct RowState(int Key, Flag Row, int Seconds, int Invoker,
        DateTime Timer, DateTime InvokeTimer, bool RemoveOnDeath);
    private sealed record State(ConcurrentDictionary<int, Flag> Registrations, uint[] Words, uint[] SnapshotWords,
        RowState[] Rows);
    private sealed record PreparedState(State Before, ConcurrentDictionary<int, Flag> Registrations,
        uint[] Words, Flag[] Cache);

    // The physical vector, not a caller-held plan, owns all mutable staged objects.
    // Weak keys avoid retaining abandoned preparations; tokens contain no row/word data.
    private readonly ConditionalWeakTable<PreparedChange, PreparedState> pendingChanges = new();

    internal sealed class PreparedChange
    {
        internal bool TryCommit(LegacyNativeStatusFlagsBigVector32 owner) =>
            owner is not null && owner.TryCommitPrepared(this);

        internal bool TryCommit(LegacyNativeStatusFlagsBigVector32 owner, LegacyNativePlayerRuntime player,
            LegacyNativeGameClientRuntime client, LegacyNativeRoleView view, uint uid,
            LegacyNativePreparedFlagPublication? publication = null, LegacyNativePreparedPotionMessage? message = null) =>
            owner is not null && owner.TryCommitPrepared(this, player, client, view, uid, publication, message);
    }

    private PreparedChange PrepareChange(State before, ConcurrentDictionary<int, Flag> registrations, uint[] words)
    {
        var token = new PreparedChange();
        var staged = new PreparedState(before, registrations, words, registrations.Values.ToArray());
        pendingChanges.Add(token, staged);
        return token;
    }

    internal bool TryGetPreparedWords(PreparedChange? token, out uint[] words)
    {
        words = [];
        if (!Monitor.IsEntered(SyncRoot) || token is null ||
            !pendingChanges.TryGetValue(token, out var staged) || !IsCurrent(staged.Before)) return false;
        // Serialization receives a private copy, never the words later published as authoritative.
        words = (uint[])staged.Words.Clone();
        return true;
    }

    private bool TryCommitPrepared(PreparedChange token)
    {
        if (!TryTakeCurrent(token, out var staged)) return false;
        CommitPreparedState(staged!);
        return true;
    }

    private bool TryCommitPrepared(PreparedChange token, LegacyNativePlayerRuntime player,
        LegacyNativeGameClientRuntime client, LegacyNativeRoleView view, uint uid,
        LegacyNativePreparedFlagPublication? publication, LegacyNativePreparedPotionMessage? message)
    {
        if (!Monitor.IsEntered(client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(player.SpellAdmissionSyncRoot)) return false;
        if (!TryTakeCurrent(token, out var staged)) return false;
        // IsCurrent can invoke the actual registration comparer. Check physical
        // publication ownership AFTER it, with only plain assignments remaining.
        if (!ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) ||
            !ReferenceEquals(player.BitVector, this) || !ReferenceEquals(player.View, view) ||
            !ReferenceEquals(view.Owner, client) || player.UID != uid ||
            (publication is not null && !publication.HasPreparedStorage()) ||
            (message is not null && !message.HasPreparedStorage())) return false;
        CommitPreparedState(staged!);
        return true;
    }

    private bool TryTakeCurrent(PreparedChange token, out PreparedState? staged)
    {
        staged = null;
        if (!Monitor.IsEntered(SyncRoot) || !pendingChanges.TryGetValue(token, out staged)) return false;
        // Consume before every final state check. A rejected or successful attempt under
        // the genuine owner gate cannot be replayed after a raw writer restores old state.
        pendingChanges.Remove(token);
        if (!IsCurrent(staged.Before)) { staged = null; return false; }
        return true;
    }

    private void CommitPreparedState(PreparedState staged)
    {
        // No allocation, comparison, dictionary insertion, or serialization after this check.
        ArrayFlags = staged.Registrations;
        bits = staged.Words;
        Array = staged.Cache;
    }

    private bool TryCapture(out State? state)
    {
        state = null;
        var registrations = ArrayFlags;
        var words = bits;
        if (registrations is null || words is null) return false;
        var entries = registrations.ToArray();
        var expected = new uint[words.Length];
        var rows = new RowState[entries.Length];
        var aliases = new HashSet<Flag>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < entries.Length; index++)
        {
            var pair = entries[index]; var row = pair.Value;
            if (!ValidIndex(pair.Key, words) || row is null || row.Key != pair.Key || !aliases.Add(row) ||
                !TryDeadline(row.Timer, row.Secounds, out _) ||
                (row.InvokerSecouds != 0 && !TryDeadline(row.TimerInvoker, row.InvokerSecouds, out _))) return false;
            expected[pair.Key / 32] |= 1u << (pair.Key % 32);
            rows[index] = new(pair.Key, row, row.Secounds, row.InvokerSecouds,
                row.Timer, row.TimerInvoker, row.RemoveOnDead);
        }
        if (!expected.AsSpan().SequenceEqual(words) || !ReferenceEquals(registrations, ArrayFlags) ||
            !ReferenceEquals(words, bits) || registrations.Count != rows.Length) return false;
        state = new(registrations, words, expected, rows);
        return true;
    }

    private bool IsCurrent(State state)
    {
        if (!ReferenceEquals(ArrayFlags, state.Registrations) || !ReferenceEquals(bits, state.Words) ||
            state.Registrations.Count != state.Rows.Length || !state.Words.AsSpan().SequenceEqual(state.SnapshotWords)) return false;
        foreach (var saved in state.Rows)
            if (!state.Registrations.TryGetValue(saved.Key, out var row) || !ReferenceEquals(row, saved.Row) ||
                row.Key != saved.Key || row.Secounds != saved.Seconds || row.InvokerSecouds != saved.Invoker ||
                row.Timer != saved.Timer || row.TimerInvoker != saved.InvokeTimer || row.RemoveOnDead != saved.RemoveOnDeath)
                return false;
        return HasUnchangedCapturedGraph(state);
    }

    private static bool ValidIndex(int index, uint[] words) => index >= 0 && index / 32 < words.Length;

    internal bool TryPrepareAdd(int flag, int seconds, bool removeOnDeath, int invoker,
        bool replace, out PreparedChange? change)
    {
        change = null;
        if (!Monitor.IsEntered(SyncRoot) || !TryCapture(out var state) || !ValidIndex(flag, state!.Words)) return false;
        if (!replace && state.Registrations.ContainsKey(flag)) return false;
        var row = new Flag(flag, seconds, removeOnDeath, invoker);
        if (!TryDeadline(row.Timer, seconds, out _) || (invoker != 0 && !TryDeadline(row.TimerInvoker, invoker, out _))) return false;
        var staged = new ConcurrentDictionary<int, Flag>(state.Registrations, state.Registrations.Comparer);
        staged[flag] = row;
        var words = (uint[])state.Words.Clone();
        words[flag / 32] |= 1u << (flag % 32);
        change = PrepareChange(state, staged, words);
        return true;
    }
}
