namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    internal object SyncRoot { get; } = new();
    private string? _claimFailure;
    internal string? ClaimRoot => Path.IsPathFullyQualified(rootDirectory)
        ? Path.GetFullPath(Path.Combine(rootDirectory, "PlayersMailList")) : null;
    internal void BlockClaims(string reason) => Interlocked.CompareExchange(ref _claimFailure, reason, null);

    private void RequireWritable()
    {
        if (_claimFailure is { } failure) throw new InvalidOperationException("Mailbox requires fresh recovery: " + failure);
    }

    public IReadOnlyList<LegacyMailboxEntry> Load(uint playerUid)
    { lock (SyncRoot) return LoadCore(playerUid); }
    public IReadOnlyList<LegacyMailboxEntry> List(uint playerUid)
    { lock (SyncRoot) return ListCore(playerUid); }
    public void Save(uint playerUid)
    { lock (SyncRoot) { RequireWritable(); SaveCore(playerUid); } }
    public uint GetNextId(uint playerUid)
    { lock (SyncRoot) return GetNextIdCore(playerUid); }
    public LegacyMailboxEntry Add(uint playerUid, LegacyMailboxEntry entry)
    { lock (SyncRoot) { RequireWritable(); return AddCore(playerUid, entry); } }
    public bool TryGet(uint playerUid, uint mailId, out LegacyMailboxEntry entry)
    { lock (SyncRoot) return TryGetCore(playerUid, mailId, out entry); }
    public bool TryReplace(uint playerUid, LegacyMailboxEntry entry)
    { lock (SyncRoot) { RequireWritable(); return TryReplaceCore(playerUid, entry); } }
    public bool TryRemoveIfEmpty(uint playerUid, uint mailId, out LegacyMailboxEntry entry)
    { lock (SyncRoot) { RequireWritable(); return TryRemoveIfEmptyCore(playerUid, mailId, out entry); } }
    public bool TryClaimMoney(uint playerUid, uint mailId, out ulong money, out uint eMoney, out LegacyMailboxEntry entry)
    { lock (SyncRoot) { RequireWritable(); return TryClaimMoneyCore(playerUid, mailId, out money, out eMoney, out entry); } }
    public bool TryClearFlag(uint playerUid, uint mailId, bool packageClaimSucceeded, out LegacyMailboxEntry entry)
    { lock (SyncRoot) { RequireWritable(); return TryClearFlagCore(playerUid, mailId, packageClaimSucceeded, out entry); } }
    public bool TryClearItemId(uint playerUid, uint mailId, bool itemClaimSucceeded, out LegacyMailboxEntry entry)
    { lock (SyncRoot) { RequireWritable(); return TryClearItemIdCore(playerUid, mailId, itemClaimSucceeded, out entry); } }
}
