namespace WolfConquer.Core.World;

public enum LegacyNobilityDonationCurrency
{
    None = 0,
    Money = 1,
    ConquerPoints = 2,
    BoundConquerPoints = 3,
}

public enum LegacyNobilityDonationFailure
{
    None = 0,
    ZeroDonation = 1,
    UnsupportedDonationType = 2,
    InsufficientFunds = 3,
    CostOverflow = 4,
    DonationOverflow = 5,
}

public readonly record struct LegacyNobilityDonationWallet(
    long Money,
    long ConquerPoints,
    long BoundConquerPoints);

public readonly record struct LegacyNobilityDonationSpend(
    LegacyNobilityDonationCurrency Currency,
    ulong Amount);

public readonly record struct LegacyNobilityIconState(
    ulong Uid,
    ulong Donation,
    byte Rank,
    int Position)
{
    public byte TitleLevel => Rank;

    public string TitleName => LegacyNobilityRanking.RankName(Rank);

    public string Text => $"{Uid} {Donation} {Rank} {Position}";
}

public sealed record LegacyNobilityDonationResult(
    bool Applied,
    LegacyNobilityDonationFailure Failure,
    LegacyNobilityState DonorBefore,
    LegacyNobilityState DonorAfter,
    LegacyNobilityDonationWallet WalletBefore,
    LegacyNobilityDonationWallet WalletAfter,
    LegacyNobilityDonationSpend Spend,
    IReadOnlyList<LegacyNobilityState> RankingBefore,
    IReadOnlyList<LegacyNobilityState> RankingAfter,
    IReadOnlyList<LegacyNobilityIconState> ChangedIcons,
    LegacyNobilityThresholds Thresholds)
{
    public LegacyNobilityIconState DonorIcon => LegacyNobilityDonationRuntime.CreateIconState(DonorAfter);

    public bool Promoted => DonorAfter.Rank > DonorBefore.Rank;
}

public static class LegacyNobilityDonationRuntime
{
    public static LegacyNobilityDonationResult ApplyExternalDonation(
        LegacyNobilityState donor,
        ulong donationAmount,
        IEnumerable<LegacyNobilityState>? rankingCandidates = null)
    {
        var wallet = default(LegacyNobilityDonationWallet);
        var beforeCandidates = MergeCandidate(rankingCandidates, donor);
        var rankingBefore = CalculateRankedStates(beforeCandidates);
        var donorBefore = ResolveFromRanking(donor, rankingBefore);

        if (donationAmount == 0)
        {
            return NotApplied(
                LegacyNobilityDonationFailure.ZeroDonation,
                donorBefore,
                wallet,
                rankingBefore);
        }

        if (ulong.MaxValue - donor.Donation < donationAmount)
        {
            return NotApplied(
                LegacyNobilityDonationFailure.DonationOverflow,
                donorBefore,
                wallet,
                rankingBefore);
        }

        return CreateAppliedResult(
            donor,
            donationAmount,
            rankingCandidates,
            beforeCandidates,
            rankingBefore,
            donorBefore,
            wallet,
            wallet,
            new LegacyNobilityDonationSpend(LegacyNobilityDonationCurrency.None, 0));
    }

    public static LegacyNobilityDonationResult ApplyDonation(
        LegacyNobilityState donor,
        LegacyNobilityDonationWallet wallet,
        ulong donationAmount,
        LegacyNobilityDonationType donationType,
        IEnumerable<LegacyNobilityState>? rankingCandidates = null)
    {
        return ApplyDonation(
            donor,
            wallet,
            donationAmount,
            (byte)donationType,
            rankingCandidates);
    }

    public static LegacyNobilityDonationResult ApplyDonation(
        LegacyNobilityState donor,
        LegacyNobilityDonationWallet wallet,
        ulong donationAmount,
        byte donationType,
        IEnumerable<LegacyNobilityState>? rankingCandidates = null)
    {
        var beforeCandidates = MergeCandidate(rankingCandidates, donor);
        var rankingBefore = CalculateRankedStates(beforeCandidates);
        var donorBefore = ResolveFromRanking(donor, rankingBefore);

        if (donationAmount == 0)
        {
            return NotApplied(
                LegacyNobilityDonationFailure.ZeroDonation,
                donorBefore,
                wallet,
                rankingBefore);
        }

        if (!TrySpend(wallet, donationAmount, donationType, out var walletAfter, out var spend, out var failure))
        {
            return NotApplied(failure, donorBefore, wallet, rankingBefore);
        }

        if (ulong.MaxValue - donor.Donation < donationAmount)
        {
            return NotApplied(
                LegacyNobilityDonationFailure.DonationOverflow,
                donorBefore,
                wallet,
                rankingBefore);
        }

        return CreateAppliedResult(
            donor,
            donationAmount,
            rankingCandidates,
            beforeCandidates,
            rankingBefore,
            donorBefore,
            wallet,
            walletAfter,
            spend);
    }

    private static LegacyNobilityDonationResult CreateAppliedResult(
        LegacyNobilityState donor,
        ulong donationAmount,
        IEnumerable<LegacyNobilityState>? rankingCandidates,
        IReadOnlyList<LegacyNobilityState> beforeCandidates,
        IReadOnlyList<LegacyNobilityState> rankingBefore,
        LegacyNobilityState donorBefore,
        LegacyNobilityDonationWallet walletBefore,
        LegacyNobilityDonationWallet walletAfter,
        LegacyNobilityDonationSpend spend)
    {
        var donorAfterBase = donor with
        {
            Donation = donor.Donation + donationAmount,
            Position = LegacyNobilityRanking.DefaultPosition,
            Rank = LegacyNobilityRanking.ComputeRank(
                donor.Donation + donationAmount,
                LegacyNobilityRanking.DefaultPosition),
        };
        var afterCandidates = MergeCandidate(rankingCandidates, donorAfterBase);
        var rankingAfter = CalculateRankedStates(afterCandidates);
        var donorAfter = ResolveFromRanking(donorAfterBase, rankingAfter);
        var changedIcons = CreateChangedIcons(
            beforeCandidates,
            rankingBefore,
            afterCandidates,
            rankingAfter,
            donorAfter.Uid);

        return new LegacyNobilityDonationResult(
            Applied: true,
            LegacyNobilityDonationFailure.None,
            donorBefore,
            donorAfter,
            walletBefore,
            walletAfter,
            spend,
            rankingBefore,
            rankingAfter,
            changedIcons,
            CreateThresholdsFromRanked(rankingAfter));
    }

    public static IReadOnlyList<LegacyNobilityState> CalculateRankedStates(
        IEnumerable<LegacyNobilityState> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(candidate => candidate.Donation > LegacyNobilityRanking.EarlDonation)
            .OrderByDescending(candidate => candidate.Donation)
            .ThenBy(candidate => candidate.Uid)
            .Take(50)
            .Select((candidate, position) => candidate with
            {
                Position = position,
                Rank = LegacyNobilityRanking.ComputeRank(candidate.Donation, position),
            })
            .ToArray();
    }

    public static LegacyNobilityState ResolveState(
        LegacyNobilityState state,
        IEnumerable<LegacyNobilityState>? rankingCandidates = null)
    {
        var candidates = MergeCandidate(rankingCandidates, state);
        return ResolveFromRanking(state, CalculateRankedStates(candidates));
    }

    public static LegacyNobilityThresholds CreateThresholds(
        IEnumerable<LegacyNobilityState> candidates)
    {
        return CreateThresholdsFromRanked(CalculateRankedStates(candidates));
    }

    public static LegacyNobilityIconState CreateIconState(LegacyNobilityState state)
    {
        return new LegacyNobilityIconState(
            state.Uid,
            state.Donation,
            state.Rank,
            state.Position);
    }

    private static LegacyNobilityDonationResult NotApplied(
        LegacyNobilityDonationFailure failure,
        LegacyNobilityState donorBefore,
        LegacyNobilityDonationWallet wallet,
        IReadOnlyList<LegacyNobilityState> rankingBefore)
    {
        return new LegacyNobilityDonationResult(
            Applied: false,
            failure,
            donorBefore,
            donorBefore,
            wallet,
            wallet,
            new LegacyNobilityDonationSpend(LegacyNobilityDonationCurrency.None, 0),
            rankingBefore,
            rankingBefore,
            Array.Empty<LegacyNobilityIconState>(),
            CreateThresholdsFromRanked(rankingBefore));
    }

    private static bool TrySpend(
        LegacyNobilityDonationWallet wallet,
        ulong donationAmount,
        byte donationType,
        out LegacyNobilityDonationWallet walletAfter,
        out LegacyNobilityDonationSpend spend,
        out LegacyNobilityDonationFailure failure)
    {
        walletAfter = wallet;
        spend = new LegacyNobilityDonationSpend(LegacyNobilityDonationCurrency.None, 0);
        failure = LegacyNobilityDonationFailure.None;

        if (donationType == (byte)LegacyNobilityDonationType.Money)
        {
            return TrySpendBalance(
                wallet.Money,
                donationAmount,
                LegacyNobilityDonationCurrency.Money,
                wallet,
                static (source, cost) => source with { Money = source.Money - cost },
                out walletAfter,
                out spend,
                out failure);
        }

        if (donationType != (byte)LegacyNobilityDonationType.ConquerPoints)
        {
            failure = LegacyNobilityDonationFailure.UnsupportedDonationType;
            return false;
        }

        var conquerPointCost = donationAmount / 500;
        if (conquerPointCost > long.MaxValue)
        {
            failure = LegacyNobilityDonationFailure.CostOverflow;
            return false;
        }

        if (wallet.ConquerPoints >= (long)conquerPointCost)
        {
            walletAfter = wallet with { ConquerPoints = wallet.ConquerPoints - (long)conquerPointCost };
            spend = new LegacyNobilityDonationSpend(
                LegacyNobilityDonationCurrency.ConquerPoints,
                conquerPointCost);
            return true;
        }

        var boundCost = donationAmount / 50;
        return TrySpendBalance(
            wallet.BoundConquerPoints,
            boundCost,
            LegacyNobilityDonationCurrency.BoundConquerPoints,
            wallet,
            static (source, cost) => source with { BoundConquerPoints = source.BoundConquerPoints - cost },
            out walletAfter,
            out spend,
            out failure);
    }

    private static bool TrySpendBalance(
        long balance,
        ulong cost,
        LegacyNobilityDonationCurrency currency,
        LegacyNobilityDonationWallet wallet,
        Func<LegacyNobilityDonationWallet, long, LegacyNobilityDonationWallet> spendWallet,
        out LegacyNobilityDonationWallet walletAfter,
        out LegacyNobilityDonationSpend spend,
        out LegacyNobilityDonationFailure failure)
    {
        walletAfter = wallet;
        spend = new LegacyNobilityDonationSpend(LegacyNobilityDonationCurrency.None, 0);

        if (cost > long.MaxValue)
        {
            failure = LegacyNobilityDonationFailure.CostOverflow;
            return false;
        }

        if (balance < (long)cost)
        {
            failure = LegacyNobilityDonationFailure.InsufficientFunds;
            return false;
        }

        walletAfter = spendWallet(wallet, (long)cost);
        spend = new LegacyNobilityDonationSpend(currency, cost);
        failure = LegacyNobilityDonationFailure.None;
        return true;
    }

    private static IReadOnlyList<LegacyNobilityIconState> CreateChangedIcons(
        IReadOnlyList<LegacyNobilityState> beforeCandidates,
        IReadOnlyList<LegacyNobilityState> rankingBefore,
        IReadOnlyList<LegacyNobilityState> afterCandidates,
        IReadOnlyList<LegacyNobilityState> rankingAfter,
        ulong donorUid)
    {
        var affectedUids = rankingBefore
            .Select(state => state.Uid)
            .Concat(rankingAfter.Select(state => state.Uid))
            .Append(donorUid)
            .Distinct()
            .OrderBy(uid => uid)
            .ToArray();
        var changed = new List<LegacyNobilityIconState>();

        foreach (var uid in affectedUids)
        {
            if (!TryResolveByUid(beforeCandidates, rankingBefore, uid, out var before) ||
                !TryResolveByUid(afterCandidates, rankingAfter, uid, out var after))
            {
                continue;
            }

            if (before.Donation != after.Donation ||
                before.Rank != after.Rank ||
                before.Position != after.Position)
            {
                changed.Add(CreateIconState(after));
            }
        }

        return changed;
    }

    private static bool TryResolveByUid(
        IReadOnlyList<LegacyNobilityState> candidates,
        IReadOnlyList<LegacyNobilityState> ranking,
        ulong uid,
        out LegacyNobilityState resolved)
    {
        foreach (var ranked in ranking)
        {
            if (ranked.Uid == uid)
            {
                resolved = ranked;
                return true;
            }
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Uid == uid)
            {
                resolved = candidate with
                {
                    Position = LegacyNobilityRanking.DefaultPosition,
                    Rank = LegacyNobilityRanking.ComputeRank(
                        candidate.Donation,
                        LegacyNobilityRanking.DefaultPosition),
                };
                return true;
            }
        }

        resolved = default;
        return false;
    }

    private static LegacyNobilityState ResolveFromRanking(
        LegacyNobilityState state,
        IReadOnlyList<LegacyNobilityState> ranking)
    {
        foreach (var ranked in ranking)
        {
            if (ranked.Uid == state.Uid)
            {
                return ranked;
            }
        }

        return state with
        {
            Position = LegacyNobilityRanking.DefaultPosition,
            Rank = LegacyNobilityRanking.ComputeRank(
                state.Donation,
                LegacyNobilityRanking.DefaultPosition),
        };
    }

    private static IReadOnlyList<LegacyNobilityState> MergeCandidate(
        IEnumerable<LegacyNobilityState>? candidates,
        LegacyNobilityState state)
    {
        var merged = new Dictionary<ulong, LegacyNobilityState>();
        if (candidates is not null)
        {
            foreach (var candidate in candidates)
            {
                merged[candidate.Uid] = candidate;
            }
        }

        merged[state.Uid] = state;
        return merged.Values.ToArray();
    }

    private static ulong DonationAtPosition(IReadOnlyList<LegacyNobilityState> ranked, int position)
    {
        return ranked.Count > position ? ranked[position].Donation : 0;
    }

    private static LegacyNobilityThresholds CreateThresholdsFromRanked(
        IReadOnlyList<LegacyNobilityState> ranked)
    {
        return new LegacyNobilityThresholds(
            DonationAtPosition(ranked, 49),
            DonationAtPosition(ranked, 14),
            DonationAtPosition(ranked, 2));
    }
}
