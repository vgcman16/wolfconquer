namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    // Read the already-owned cache only. Do not call List/Load: those prune or
    // replace it, and would turn an inspection into a mutation or file read.
    internal sealed record GmCachedMail(uint MailId, uint AttachedItemUid, ulong Gold,
        uint ConquerPoints, uint PackageFlag);

    internal bool TryReadGmDiagnostic(uint uid, out GmCachedMail[] rows)
    {
        rows = [];
        lock (SyncRoot)
        {
            if (uid == 0 || _claimFailure is not null ||
                !mailboxByPlayerUid.TryGetValue(uid, out var cached) || cached.Count > 200)
                return false;
            var now = nowProvider();
            var ids = new HashSet<uint>();
            var result = new List<GmCachedMail>();
            foreach (var entry in cached)
            {
                if (entry is null || entry.Id == 0 || !ids.Add(entry.Id) || entry.Timer <= 0)
                    return false;
                bool live;
                try { live = entry.IsLive(now); }
                catch (ArgumentException) { return false; }
                if (live) result.Add(new(entry.Id, entry.ItemId, entry.Money, entry.EMoney, entry.Flag));
            }
            rows = result.ToArray();
            return true;
        }
    }
}
