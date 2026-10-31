using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    internal bool TryPrepareWindWalkerQuery(bool desired, out WindWalkerChange? change)
    {
        change = null;
        if (!Monitor.IsEntered(SyncRoot)) return false;
        try
        {
            return TryCapture(out var before) && before!.Words.Length == LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength &&
                ValidIndex(WindWalkerFlag, before.Words) &&
                TryPrepareWindWalkerTransition(before, desired, WindWalkerPreparationKind.Query, out change);
        }
        catch (Exception) { return false; }
    }

    internal bool TryPrepareWindWalkerExpiry(out WindWalkerChange? change)
    {
        change = null;
        if (!Monitor.IsEntered(SyncRoot)) return false;
        try
        {
            if (!TryCapture(out var before) || before!.Words.Length != LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength ||
                !ValidIndex(WindWalkerFlag, before.Words)) return false;
            foreach (var row in before.Rows)
            {
                if (row.Key != WindWalkerFlag) continue;
                // The real native local clock, never request/test time. An
                // absent or not-due row cannot issue expiry-kind authority.
                if (!TryDeadline(row.Timer, row.Seconds, out var deadline) || DateTime.Now < deadline) return false;
                return TryPrepareWindWalkerTransition(before, false, WindWalkerPreparationKind.Expiry, out change);
            }
            return false;
        }
        catch (Exception) { return false; }
    }

    private bool TryPrepareWindWalkerTransition(State before, bool desired, WindWalkerPreparationKind kind,
        out WindWalkerChange? change)
    {
        change = null;
        var present = before.Rows.Any(row => row.Key == WindWalkerFlag);
        PreparedChange? nativeChange = null;
        var after = before;
        if (present != desired)
        {
            var registrations = new ConcurrentDictionary<int, Flag>(before.Registrations, before.Registrations.Comparer);
            var words = (uint[])before.Words.Clone();
            RowState[] rows;
            if (desired)
            {
                // Native Flag construction owns the timestamp. Duplicate adds
                // never enter this branch and cannot renew the existing row.
                var row = new Flag(WindWalkerFlag, PermanentFlag, false, 0);
                if (!TryDeadline(row.Timer, row.Secounds, out _) || !registrations.TryAdd(WindWalkerFlag, row)) return false;
                words[WindWalkerFlag / 32] |= 1u << (WindWalkerFlag % 32);
                rows = before.Rows.Append(new RowState(WindWalkerFlag, row, row.Secounds, row.InvokerSecouds,
                    row.Timer, row.TimerInvoker, row.RemoveOnDead)).ToArray();
            }
            else
            {
                var existing = FindCapturedFlag(before, WindWalkerFlag);
                if (!registrations.TryRemove(WindWalkerFlag, out var removed) || !ReferenceEquals(existing, removed)) return false;
                words[WindWalkerFlag / 32] &= ~(1u << (WindWalkerFlag % 32));
                rows = before.Rows.Where(row => row.Key != WindWalkerFlag).ToArray();
            }
            after = new(registrations, words, (uint[])words.Clone(), rows);
            if (!WindWalkerGraphMatchesPlain(after)) return false;
            nativeChange = PrepareChange(before, registrations, words);
        }
        // Copy/comparer operations above may have changed the live public raw
        // fields. Revalidate both complete graphs before exposing any token.
        if (!IsCurrent(before) || !HasUnchangedCapturedGraph(before) || !WindWalkerGraphMatchesPlain(after)) return false;
        var proposed = CaptureWindWalkerInfo(after);
        var token = new WindWalkerChange(proposed, present != desired);
        windWalkerChanges.Add(token, new(before, after, nativeChange));
        windWalkerOrigins.Add(token, new(kind)); change = token; return true;
    }
}
