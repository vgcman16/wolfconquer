using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Closed data decoding for the two actual ordinary producer generations.
// Decoding does not issue a creation permit, allocation or replay permission.
internal sealed class LegacyOrdinaryProducerRecipe
{
    private readonly LegacyOrdinaryAdmissionRecipe? legacy;
    internal LegacyAuthenticatedOrdinaryCreationRecipe? Creation { get; }
    internal uint Account => Creation?.Account ?? legacy!.Account;
    private readonly LegacyOrdinaryAdmissionRoots roots;

    private LegacyOrdinaryProducerRecipe(LegacyOrdinaryAdmissionRoots roots,
        LegacyOrdinaryAdmissionRecipe? legacy, LegacyAuthenticatedOrdinaryCreationRecipe? creation)
    { this.roots = roots; this.legacy = legacy; Creation = creation; }

    internal static LegacyOrdinaryProducerRecipe Read(LegacyItemUidProducerIntentSnapshot row, Guid domain,
        LegacyOrdinaryAdmissionRoots roots, string mailbox)
    {
        var payload = row.CopyPayload();
        var digest = SHA256.HashData(payload);
        var creation = LegacyAuthenticatedOrdinaryCreationRecipe.HasKind(payload)
            ? LegacyAuthenticatedOrdinaryCreationRecipe.Decode(payload, roots, mailbox) : null;
        var legacy = creation is null ? LegacyOrdinaryAdmissionRecipe.Decode(payload, roots) : null;
        var account = creation?.Account ?? legacy!.Account;
        var count = creation?.Projection.Items.Length ?? legacy!.Items.Length;
        if (row.DomainId != domain || row.Request.Writer != LegacyOrdinaryAdmissionUidOwner.Writer ||
            row.Request.AccountId != account || row.Request.CharacterUid != account || row.Request.Count != count ||
            row.Request.OperationId != new Guid(digest.AsSpan(0, 16)) ||
            row.Request.PayloadSha256 != Convert.ToHexString(digest).ToLowerInvariant() || row.PreparedGeneration == 0)
            throw new InvalidDataException("Retained ordinary producer identity is invalid.");
        if (row.Allocation is { } receipt && (receipt.DomainId != domain || receipt.OperationId != row.Request.OperationId ||
            receipt.Writer != row.Request.Writer || receipt.AccountId != account || receipt.CharacterUid != account ||
            receipt.PayloadSha256 != row.Request.PayloadSha256 || receipt.Count != count ||
            receipt.Generation <= row.PreparedGeneration || receipt.First == 0 ||
            (ulong)receipt.First + (uint)receipt.Count - 1 > uint.MaxValue))
            throw new InvalidDataException("Retained ordinary producer allocation is invalid.");
        return new(roots, legacy, creation);
    }

    internal Dictionary<LegacyTradeJournalFileKind, byte[]?> CaptureCurrent() => Creation is { } creation
        ? LegacyTradeJournal.CaptureAuthenticatedCreationInputs(roots, creation.MailboxRoot, Account)
        : LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, Account);

    internal bool IsBefore(IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> images) =>
        Creation?.IsBefore(images) ?? legacy!.IsBefore(images);

    internal bool IsAfter(IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> images,
        LegacyItemUidReservationReceipt receipt) => Creation?.IsAfter(images, receipt) ?? legacy!.IsAfter(images, receipt);
}
