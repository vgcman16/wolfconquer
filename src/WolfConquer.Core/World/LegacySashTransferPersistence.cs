using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// A move changes one complete item file; the character file is a read-only
// ownership/wallet/escrow/PIN baseline, never a fabricated second mutation.
internal static class LegacySashTransferPersistence
{
    internal const string RecoveryRequired = "sashTransferRecoveryRequired";

    internal static bool TryPersist(WorldSession expected, WorldSession updated, string itemsRoot,
        LegacyItemSaveGraph before, LegacyItemSaveGraph after, out string reason)
    {
        reason = "sashTransferStorageBindingInvalid";
        if (expected.AccountId <= 0 || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            !ReferenceEquals(expected.Character, updated.Character) || (expected with { PlayerItems = updated.PlayerItems }) != updated ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || !Path.IsPathFullyQualified(expected.UsersRoot) ||
            string.IsNullOrWhiteSpace(itemsRoot) || !Path.IsPathFullyQualified(itemsRoot)) return false;
        try
        {
            var users = Path.GetFullPath(expected.UsersRoot);
            var name = expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
            var character = Path.Combine(users, name);
            var items = Path.Combine(Path.GetFullPath(itemsRoot), expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var source = expected.Character.SourceFile;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(source, name, comparison) && (string.IsNullOrWhiteSpace(source) ||
                !Path.IsPathFullyQualified(source) || !string.Equals(Path.GetFullPath(source), character, comparison))) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked([character, items], () =>
            {
                if (LegacyTradeJournal.HasPending(users))
                { failure = RecoveryRequired + ":pendingJournal"; return false; }
                if (!CharacterSnapshotFileBaseline.TryRead(character, "sashTransfer", out var file, out failure) ||
                    !ValidateCharacter(file!, expected) ||
                    !LegacyTradeEscrowClaims.TryValidateBaseline(character, expected, out failure) ||
                    !before.IsCurrent(expected.PlayerItems) || !after.IsCurrent(updated.PlayerItems))
                { if (failure.Length == 0) failure = "sashTransferCharacterOrGraphStale"; return false; }
                if (!CharacterSnapshotFileBaseline.TryReadBytes(items, "sashTransfer", out var original, out failure) ||
                    !before.HasBytes(original))
                { if (failure.Length == 0) failure = "sashTransferItemBaselineStale"; return false; }
                var candidate = PlayerItemSnapshotLoader.ToLegacyBytes(after.WithOwnedItemsForPersistence(updated).PlayerItems);
                if (!CharacterSnapshotFileBaseline.TryReadBytes(character, "sashTransfer", out var currentCharacter, out failure) ||
                    !currentCharacter.AsSpan().SequenceEqual(file!.Bytes) ||
                    !before.IsCurrent(expected.PlayerItems) || !after.IsCurrent(updated.PlayerItems))
                { failure = "sashTransferGraphChanged"; return false; }
                var result = CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(items, original, candidate,
                    out var uncertain, out failure);
                if (!result && uncertain) failure = RecoveryRequired + ":" + failure;
                return result;
            });
            reason = failure;
            return saved;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException or FormatException or OverflowException)
        { reason = "sashTransferBaseline:" + error.GetType().Name; return false; }
    }

    private static bool ValidateCharacter(CharacterSnapshotFileBaseline.Snapshot file, WorldSession expected)
    {
        var text = CharacterSnapshotFileEncoding.Decode(file.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(file.Bytes)) return false;
        var parsed = CharacterSnapshotParser.ParseText(text, expected.Character.SourceFile);
        if (parsed.Uid != expected.Character.Uid || parsed.Money != expected.Character.Money ||
            parsed.ConquerPoints != expected.Character.ConquerPoints ||
            !LegacyTradeEscrowClaims.TryRead(parsed.Attributes, out var diskClaim, out _) ||
            !LegacyTradeEscrowClaims.TryRead(expected.Character.Attributes, out var liveClaim, out _) || diskClaim != liveClaim ||
            !LegacySecondaryPasswordState.TryRead(parsed.Attributes, out _, out _)) return false;
        var current = new Dictionary<string, string>(expected.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        var hasLivePin = current.TryGetValue("SecurityPass", out var livePin);
        return hasLivePin == parsed.Attributes.TryGetValue("SecurityPass", out var diskPin) &&
            (!hasLivePin || string.Equals(livePin, diskPin, StringComparison.Ordinal));
    }
}
