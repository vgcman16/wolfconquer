using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionUidOwner
{
    private bool TryReconcile(out string reason)
    {
        reason = "ordinaryAdmissionRecoveryRequired:retainedWork";
        try
        {
            if (!TryReadStarterHistory(out _, out var retained, out reason)) return false;
            foreach (var row in retained.OrderBy(row => row.PreparedGeneration))
            {
                var recipe = ReadRecipe(row);
                var current = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(Roots, recipe.Account);
                if (row.Allocation is { } receipt && recipe.IsAfter(current, receipt))
                {
                    if (!TryComplete(row.Request.OperationId, out reason)) return false;
                }
                else if (recipe.IsBefore(current))
                {
                    if (!pending.TryAdd(recipe.Account, row.Request.OperationId))
                        throw new InvalidDataException("More than one retained ordinary admission owns an account.");
                }
                else throw new InvalidDataException("Retained ordinary-admission images are mixed or foreign.");
            }
            reason = string.Empty; return true;
        }
        catch (Exception error) when (OwnerFailure(error))
        { reason = "ordinaryAdmissionRecoveryRequired:" + error.GetType().Name; return false; }
    }

    private LegacyOrdinaryAdmissionRecipe ReadRecipe(LegacyItemUidProducerIntentSnapshot row)
    {
        var payload = row.CopyPayload();
        var recipe = LegacyOrdinaryAdmissionRecipe.Decode(payload, Roots);
        if (row.DomainId != domain || row.Request.Writer != Writer || row.Request.AccountId != recipe.Account ||
            row.Request.CharacterUid != recipe.Account || row.Request.Count != recipe.Items.Length ||
            row.Request.OperationId != new Guid(SHA256.HashData(payload).AsSpan(0, 16)))
            throw new InvalidDataException("Retained ordinary-admission identity is invalid.");
        return recipe;
    }

    internal bool TryReadCompletedIntent(Guid operation, out LegacyItemUidProducerIntentSnapshot? intent, out string reason)
    {
        intent = null; reason = "ordinaryAdmissionCompletionUnproven";
        try
        {
            if (!IsCurrent(Roots) || !registry.TryReadProducerIntent(domain, operation, out var row, out reason) ||
                row?.Allocation is not { } receipt) return false;
            var recipe = ReadRecipe(row);
            if (!recipe.IsAfter(LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(Roots, recipe.Account), receipt))
            { reason = "ordinaryAdmissionCompletionUnproven:afterImages"; return false; }
            intent = row; reason = string.Empty; return true;
        }
        catch (Exception error) when (OwnerFailure(error))
        { reason = "ordinaryAdmissionCompletionUnproven:" + error.GetType().Name; return false; }
    }

    internal bool TryComplete(Guid operation, out string reason)
    {
        lock (allocationGate)
        {
            if (!LegacyOrdinaryAdmissionCompletedWorkProof.TryCapture(this, operation, out var completed, out reason) ||
                !registry.TryArchiveCompletedOrdinaryAdmission(domain, operation, completed!, out reason)) return false;
            foreach (var account in pending.Where(row => row.Value == operation).Select(row => row.Key).ToArray()) pending.TryRemove(account, out _);
            return true;
        }
    }

    private static bool OwnerFailure(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or FormatException or OverflowException or NotSupportedException or
        System.Security.SecurityException;
}
