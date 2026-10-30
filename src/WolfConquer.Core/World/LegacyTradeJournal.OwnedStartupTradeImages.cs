using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateOwnedStartupPairImages(RecoveryCapture capture, uint highWater)
    {
        var characters = capture.Targets.Where(target => target.Change.Kind == LegacyTradeJournalFileKind.Character)
            .Select(target => target.Change).ToArray();
        var items = capture.Targets.Where(target => target.Change.Kind == LegacyTradeJournalFileKind.Items)
            .Select(target => target.Change).ToArray();
        if (capture.Targets.Length != 4 || characters.Length != 2 || items.Length != 2 ||
            capture.Targets[0].Change.Kind != LegacyTradeJournalFileKind.Character ||
            capture.Targets[1].Change.Kind != LegacyTradeJournalFileKind.Character ||
            capture.Targets[2].Change.Kind != LegacyTradeJournalFileKind.Items ||
            capture.Targets[3].Change.Kind != LegacyTradeJournalFileKind.Items ||
            capture.Targets.Any(target => target.Change.FileId <= 0 || target.Change.Before is null ||
                target.Change.Kind == LegacyTradeJournalFileKind.Items && target.Change.FileId > uint.MaxValue))
            throw new InvalidDataException("Owned pair startup requires two complete existing Character/Items pairs.");

        var uids = new HashSet<long>();
        var beforeWallets = new LegacyTradeEscrowClaims.JournalWallet[2];
        var afterWallets = new LegacyTradeEscrowClaims.JournalWallet[2];
        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            var uid = ReadCharacterUid(character.Before!);
            if (!uids.Add(uid) || ReadCharacterUid(character.After) != uid || items[index].FileId != uid)
                throw new InvalidDataException("Owned pair Character identities do not map to distinct Items owners.");
            foreach (var image in new[] { character.Before!, character.After })
            {
                var text = CharacterSnapshotFileEncoding.Decode(image, out _);
                var parsed = CharacterSnapshotParser.ParseText(text);
                LegacyOrdinaryAdmissionValidation.ValidateCharacter(parsed);
                if (!LegacyWarehouseMoneyState.TryRead(parsed.Attributes, out _))
                    throw new InvalidDataException("Owned pair Character warehouse currency is invalid or ambiguous.");
            }
            beforeWallets[index] = LegacyTradeEscrowClaims.ReadJournalWallet(character.Before!);
            afterWallets[index] = LegacyTradeEscrowClaims.ReadJournalWallet(character.After);
        }

        var beforeUids = ReadOwnedPairUidSet(items.Select(item => item.Before!), highWater);
        var afterUids = ReadOwnedPairUidSet(items.Select(item => item.After), highWater);
        if (!beforeUids.SetEquals(afterUids))
            throw new InvalidDataException("Owned pair startup must conserve the complete physical UID set.");
        if (afterWallets.Any(wallet => wallet.Claim is not null))
            throw new InvalidDataException("Owned pair recovery cannot retain an unsettled afterimage claim.");
        var tradeProjection = characters.Select((character, index) =>
            LegacyVendorPurchasePersistence.ProjectTradeCharacterImage(character.Before!,
                afterWallets[index].Money, afterWallets[index].ConquerPoints).AsSpan().SequenceEqual(character.After)).All(matches => matches);
        var unchangedWallets = beforeWallets.Select((wallet, index) =>
            wallet.Money == afterWallets[index].Money && wallet.ConquerPoints == afterWallets[index].ConquerPoints).All(matches => matches);
        // A real claim selects trade settlement exclusively. A failed claimed
        // trade must never borrow the claim-free vendor interpretation.
        if (beforeWallets.Any(wallet => wallet.Claim is not null) || unchangedWallets && tradeProjection)
        {
            if (!tradeProjection) throw new InvalidDataException("Owned trade changes unrelated Character state.");
            var exchange = ValidateOwnedTradeWallets(beforeWallets, afterWallets);
            ValidateOwnedTradeItemClosures(items, exchange);
            return;
        }
        ValidateOwnedStartupVendorImages(characters, items, beforeWallets, afterWallets);
    }

    private static HashSet<uint> ReadOwnedPairUidSet(IEnumerable<byte[]> images, uint highWater)
    {
        var result = new HashSet<uint>();
        foreach (var image in images)
            foreach (var uid in LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(image))
                if (uid > highWater || !result.Add(uid))
                    throw new InvalidDataException("Owned pair contains a duplicate or unregistered physical UID.");
        return result;
    }

    private static bool ValidateOwnedTradeWallets(LegacyTradeEscrowClaims.JournalWallet[] before,
        LegacyTradeEscrowClaims.JournalWallet[] after)
    {
        foreach (var wallet in before)
        {
            _ = checked(wallet.Money + (wallet.Claim?.Money ?? 0));
            if (checked(wallet.ConquerPoints + (wallet.Claim?.ConquerPoints ?? 0)) > uint.MaxValue)
                throw new InvalidDataException("Captured trade escrow exceeds the original wallet bounds.");
        }
        if (after.Any(wallet => wallet.Claim is not null) ||
            before[0].Claim is { } first && before[1].Claim is { } second && first.TradeId != second.TradeId)
            throw new InvalidDataException("Owned trade recovery requires one settled durable escrow identity.");
        var exchange = MatchesOwnedTradeSettlement(before, after, exchange: true);
        if (!exchange && !MatchesOwnedTradeSettlement(before, after, exchange: false))
            throw new InvalidDataException("Captured trade wallets are not an exact exchange or refund of their durable claims.");
        return exchange;
    }

    private static bool MatchesOwnedTradeSettlement(LegacyTradeEscrowClaims.JournalWallet[] before,
        LegacyTradeEscrowClaims.JournalWallet[] after, bool exchange)
    {
        try
        {
            for (var index = 0; index < before.Length; index++)
            {
                var credited = before[exchange ? 1 - index : index].Claim;
                if (after[index].Uid != before[index].Uid ||
                    checked(before[index].Money + (credited?.Money ?? 0)) != after[index].Money ||
                    checked(before[index].ConquerPoints + (credited?.ConquerPoints ?? 0)) != after[index].ConquerPoints)
                    return false;
            }
            return true;
        }
        catch (OverflowException) { return false; }
    }
}
