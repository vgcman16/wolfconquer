using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyVendorPurchasePersistence
{
    // Durability and escrow policy are separate. Only Trade is allowed to consume
    // claims; booth settlement must preserve their absence, not erase evidence.
    private enum PersistenceMode { Compatibility, Trade, Vendor }

    private sealed record JournalParticipant(string CharacterPath, string ItemPath,
        WorldSession Updated, string[] CharacterLines, Encoding Encoding, byte[] ItemBytes);

    internal static bool TryPersistVendorWithBaselineValidation(
        WorldSession originalBuyer, WorldSession updatedBuyer,
        WorldSession originalSeller, WorldSession updatedSeller,
        string? playerItemsRoot, Func<bool> validateLocked, out string reason)
    {
        ArgumentNullException.ThrowIfNull(originalBuyer);
        ArgumentNullException.ThrowIfNull(updatedBuyer);
        ArgumentNullException.ThrowIfNull(originalSeller);
        ArgumentNullException.ThrowIfNull(updatedSeller);
        ArgumentNullException.ThrowIfNull(validateLocked);
        lock (PersistenceGate)
        {
            return TryPersistCore(originalBuyer, updatedBuyer, originalSeller, updatedSeller,
                playerItemsRoot, out reason, validateLocked, PersistenceMode.Vendor);
        }
    }

    private static bool TryPersistJournal(JournalParticipant buyer, JournalParticipant seller,
        string itemsRoot, PersistenceMode mode, out string reason)
    {
        var usersRoot = Path.GetDirectoryName(buyer.CharacterPath)!;
        if (!PathsEqual(usersRoot, Path.GetDirectoryName(seller.CharacterPath)!))
        {
            reason = mode == PersistenceMode.Trade ? "tradeUsersRootMismatch" : "vendorUsersRootMismatch";
            return false;
        }
        try
        {
            return LegacyTradeJournal.TryCommit(usersRoot, itemsRoot,
            [
                new(LegacyTradeJournalFileKind.Character, buyer.Updated.AccountId,
                    File.ReadAllBytes(buyer.CharacterPath),
                    EncodeTradeCharacterLines(buyer.CharacterLines, buyer.Encoding)),
                new(LegacyTradeJournalFileKind.Character, seller.Updated.AccountId,
                    File.ReadAllBytes(seller.CharacterPath),
                    EncodeTradeCharacterLines(seller.CharacterLines, seller.Encoding)),
                new(LegacyTradeJournalFileKind.Items, buyer.Updated.Character.Uid,
                    PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(buyer.ItemPath), buyer.ItemBytes),
                new(LegacyTradeJournalFileKind.Items, seller.Updated.Character.Uid,
                    PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(seller.ItemPath), seller.ItemBytes),
            ], out reason);
        }
        catch (Exception error) when (IsPersistenceException(error))
        {
            reason = (mode == PersistenceMode.Trade ? "tradeJournalPreparation:" : "vendorJournalPreparation:") +
                error.GetType().Name;
            return false;
        }
    }
}
