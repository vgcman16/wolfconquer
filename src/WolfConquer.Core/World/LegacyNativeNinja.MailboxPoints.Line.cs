namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    internal sealed partial class MailboxPointsCapture
    {
        internal bool MatchesLine(string line, out string[]? parts)
        {
            parts = null;
            if (!IsCurrent() || !MailboxRecoveryLine.TryRead(line, out var parsed)) return false;
            var captured = rows.Select(row => new KeyValuePair<uint, Item>(row.Key, row.Item)).ToArray();
            if (!parsed!.Matches(ninja, items, captured) || !IsCurrent()) return false;
            // Strict shared parser retains the validated stored Score field;
            // native Load ignores it and no mutable metadata query is needed.
            parts = parsed.CopyFields();
            return true;
        }
    }
}
