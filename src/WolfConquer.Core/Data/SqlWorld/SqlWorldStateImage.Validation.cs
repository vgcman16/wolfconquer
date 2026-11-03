using WolfConquer.Core.World;

namespace WolfConquer.Core.Data.SqlWorld;

public sealed partial class SqlWorldStateImage
{
    private static bool TryValidateCharacter(byte[] image, uint characterUid, out SqlWorldStateFailure failure)
    {
        CharacterSnapshot character;
        failure = SqlWorldStateFailure.InvalidCharacterImage;
        try { character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(image, out _)); }
        catch (Exception error) when (InvalidImage(error)) { return false; }
        if (character.Uid != characterUid) { failure = SqlWorldStateFailure.InvalidCharacterUid; return false; }
        failure = SqlWorldStateFailure.InvalidFunds;
        if (character.Money < 0 || character.ConquerPoints is < 0 or > uint.MaxValue ||
            character.BoundConquerPoints is < 0 or > int.MaxValue || character.BankConquerPoints < 0 ||
            !LegacyWarehouseMoneyState.TryRead(character.Attributes, out _)) return false;
        try
        {
            var wallet = LegacyTradeEscrowClaims.ReadJournalWallet(image);
            if (wallet.Uid != characterUid || wallet.Money != character.Money || wallet.ConquerPoints != character.ConquerPoints)
                return false;
        }
        catch (Exception error) when (InvalidImage(error)) { return false; }
        failure = SqlWorldStateFailure.InvalidCharacterImage;
        try { LegacyOrdinaryAdmissionValidation.ValidateCharacter(character); }
        catch (Exception error) when (InvalidImage(error)) { return false; }
        failure = SqlWorldStateFailure.None;
        return true;
    }

    private static bool TryValidateItems(byte[] image, out uint[] uids, out SqlWorldStateFailure failure)
    {
        uids = [];
        failure = SqlWorldStateFailure.InvalidItemImage;
        try
        {
            // Raw validation must precede the loader, which deliberately sorts,
            // deduplicates and omits rows it cannot place in the visible graph.
            var physical = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(image);
            var parsed = PlayerItemSnapshotLoader.Parse(image);
            if (!LegacyItemSaveGraph.TryCapture(parsed, out _) ||
                !WorldPacketHandlers.ItemSpellRawRecordsMatch(image, PlayerItemSnapshotLoader.ToLegacyBytes(parsed))) return false;
            uids = physical;
            failure = SqlWorldStateFailure.None;
            return true;
        }
        catch (Exception error) when (InvalidImage(error)) { return false; }
    }

    private static bool InvalidImage(Exception error) => error is FormatException or InvalidDataException or
        ArgumentException or InvalidOperationException or OverflowException or KeyNotFoundException or IndexOutOfRangeException;
}
