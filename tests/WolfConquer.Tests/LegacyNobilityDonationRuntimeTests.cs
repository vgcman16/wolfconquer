using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNobilityDonationRuntimeTests
{
    [Theory]
    [InlineData(30_000_000UL, 1, "Knight")]
    [InlineData(100_000_000UL, 3, "Baron")]
    [InlineData(200_000_000UL, 5, "Earl")]
    public void ApplyDonationWithMoneyUsesLegacySourceBackedDonationThresholds(
        ulong donationAmount,
        byte expectedRank,
        string expectedTitleName)
    {
        var donor = State(1_293_600, 0);
        var wallet = new LegacyNobilityDonationWallet(
            Money: 200_000_000,
            ConquerPoints: 0,
            BoundConquerPoints: 0);

        var result = LegacyNobilityDonationRuntime.ApplyDonation(
            donor,
            wallet,
            donationAmount,
            LegacyNobilityDonationType.Money);

        Assert.True(result.Applied);
        Assert.Equal(donationAmount, result.DonorAfter.Donation);
        Assert.Equal(expectedRank, result.DonorAfter.Rank);
        Assert.Equal(expectedTitleName, result.DonorIcon.TitleName);
        Assert.Equal(-1, result.DonorAfter.Position);
    }

    [Fact]
    public void CalculateRankedStatesUsesLegacyTopFiftyThresholdPositions()
    {
        var candidates = Enumerable
            .Range(0, 50)
            .Select(index => State(
                (ulong)(1_000 + index),
                1_000_000_000UL - ((ulong)index * 1_000_000UL)))
            .ToArray();

        var ranked = LegacyNobilityDonationRuntime.CalculateRankedStates(candidates);
        var thresholds = LegacyNobilityDonationRuntime.CreateThresholds(candidates);

        Assert.Equal(50, ranked.Count);
        Assert.Equal((byte)12, ranked[0].Rank);
        Assert.Equal((byte)12, ranked[2].Rank);
        Assert.Equal((byte)9, ranked[3].Rank);
        Assert.Equal((byte)9, ranked[16].Rank);
        Assert.Equal((byte)7, ranked[17].Rank);
        Assert.Equal((byte)7, ranked[49].Rank);
        Assert.Equal(ranked[2].Donation, thresholds.KingDonation);
        Assert.Equal(ranked[14].Donation, thresholds.PrinceDonation);
        Assert.Equal(ranked[49].Donation, thresholds.DukeDonation);
    }

    [Fact]
    public void CalculateRankedStatesModelsKingAsHighestRank()
    {
        var candidates = new[]
        {
            State(10, 500_000_000),
            State(20, 400_000_000),
            State(30, 300_000_000),
            State(40, 250_000_000),
        };

        var ranked = LegacyNobilityDonationRuntime.CalculateRankedStates(candidates);

        Assert.Equal((byte)12, ranked[0].Rank);
        Assert.Equal((byte)12, ranked[1].Rank);
        Assert.Equal((byte)12, ranked[2].Rank);
        Assert.Equal("King", LegacyNobilityRanking.RankName(ranked[0].Rank));
        Assert.All(ranked, state => Assert.True(state.Rank <= 12));
    }

    [Fact]
    public void ApplyDonationWithMoneyPromotesToEarlAtLegacyDonationThreshold()
    {
        var donor = State(1_293_600, 0);
        var wallet = new LegacyNobilityDonationWallet(
            Money: 200_000_000,
            ConquerPoints: 0,
            BoundConquerPoints: 0);

        var result = LegacyNobilityDonationRuntime.ApplyDonation(
            donor,
            wallet,
            donationAmount: 200_000_000,
            LegacyNobilityDonationType.Money);

        Assert.True(result.Applied);
        Assert.Equal(LegacyNobilityDonationFailure.None, result.Failure);
        Assert.Equal(LegacyNobilityDonationCurrency.Money, result.Spend.Currency);
        Assert.Equal(200_000_000UL, result.Spend.Amount);
        Assert.Equal(0, result.WalletAfter.Money);
        Assert.Equal(200_000_000UL, result.DonorAfter.Donation);
        Assert.Equal((byte)5, result.DonorAfter.Rank);
        Assert.Equal(-1, result.DonorAfter.Position);
        Assert.Equal("Earl", result.DonorIcon.TitleName);
        Assert.Equal((byte)5, result.DonorIcon.TitleLevel);
        Assert.Equal("1293600 200000000 5 -1", result.DonorIcon.Text);
        Assert.Empty(result.RankingAfter);
    }

    [Fact]
    public void ApplyDonationWithConquerPointsUsesOnePointPerFiveHundredDonationAndRanksKing()
    {
        var donor = State(1_293_600, 0);
        var wallet = new LegacyNobilityDonationWallet(
            Money: 0,
            ConquerPoints: 500_000,
            BoundConquerPoints: 0);

        var result = LegacyNobilityDonationRuntime.ApplyDonation(
            donor,
            wallet,
            donationAmount: 250_000_000,
            LegacyNobilityDonationType.ConquerPoints);

        Assert.True(result.Applied);
        Assert.Equal(LegacyNobilityDonationCurrency.ConquerPoints, result.Spend.Currency);
        Assert.Equal(500_000UL, result.Spend.Amount);
        Assert.Equal(0, result.WalletAfter.ConquerPoints);
        Assert.Equal(250_000_000UL, result.DonorAfter.Donation);
        Assert.Equal((byte)12, result.DonorAfter.Rank);
        Assert.Equal(0, result.DonorAfter.Position);
        Assert.Equal("King", result.DonorIcon.TitleName);
        Assert.Single(result.RankingAfter);
    }

    [Fact]
    public void ApplyDonationWithMoneyPreservesBeforeAndMutatesAfterWalletAndDonation()
    {
        var donor = State(1_293_600, 100_000_000);
        var wallet = new LegacyNobilityDonationWallet(
            Money: 35_000_000,
            ConquerPoints: 111,
            BoundConquerPoints: 222);

        var result = LegacyNobilityDonationRuntime.ApplyDonation(
            donor,
            wallet,
            donationAmount: 30_000_000,
            LegacyNobilityDonationType.Money);

        Assert.True(result.Applied);
        Assert.Equal(LegacyNobilityDonationFailure.None, result.Failure);
        Assert.Equal(donor, result.DonorBefore);
        Assert.Equal(100_000_000UL, result.DonorBefore.Donation);
        Assert.Equal(130_000_000UL, result.DonorAfter.Donation);
        Assert.Equal((byte)3, result.DonorAfter.Rank);
        Assert.Equal(wallet, result.WalletBefore);
        Assert.Equal(
            new LegacyNobilityDonationWallet(
                Money: 5_000_000,
                ConquerPoints: 111,
                BoundConquerPoints: 222),
            result.WalletAfter);
        Assert.Equal(
            new LegacyNobilityDonationSpend(
                LegacyNobilityDonationCurrency.Money,
                30_000_000),
            result.Spend);

        var changedIcon = Assert.Single(result.ChangedIcons);
        Assert.Equal(result.DonorAfter.Uid, changedIcon.Uid);
        Assert.Equal(result.DonorAfter.Donation, changedIcon.Donation);
        Assert.Equal(result.DonorAfter.Rank, changedIcon.Rank);
        Assert.Equal(result.DonorAfter.Position, changedIcon.Position);
    }

    [Fact]
    public void ApplyDonationWithConquerPointsFallsBackToBoundPointsAtLegacyRatio()
    {
        var donor = State(1_293_600, 0);
        var wallet = new LegacyNobilityDonationWallet(
            Money: 0,
            ConquerPoints: 399_999,
            BoundConquerPoints: 4_000_000);

        var result = LegacyNobilityDonationRuntime.ApplyDonation(
            donor,
            wallet,
            donationAmount: 200_000_000,
            LegacyNobilityDonationType.ConquerPoints);

        Assert.True(result.Applied);
        Assert.Equal(LegacyNobilityDonationCurrency.BoundConquerPoints, result.Spend.Currency);
        Assert.Equal(4_000_000UL, result.Spend.Amount);
        Assert.Equal(399_999, result.WalletAfter.ConquerPoints);
        Assert.Equal(0, result.WalletAfter.BoundConquerPoints);
        Assert.Equal((byte)5, result.DonorAfter.Rank);
        Assert.Equal(-1, result.DonorAfter.Position);
    }

    [Fact]
    public void ApplyDonationWithInsufficientFundsDoesNotMutateWalletDonationOrRanking()
    {
        var donor = State(1_293_600, 100_000_000);
        var wallet = new LegacyNobilityDonationWallet(
            Money: 29_999_999,
            ConquerPoints: 7,
            BoundConquerPoints: 11);

        var result = LegacyNobilityDonationRuntime.ApplyDonation(
            donor,
            wallet,
            donationAmount: 30_000_000,
            LegacyNobilityDonationType.Money);

        Assert.False(result.Applied);
        Assert.Equal(LegacyNobilityDonationFailure.InsufficientFunds, result.Failure);
        Assert.Equal(donor, result.DonorBefore);
        Assert.Equal(donor, result.DonorAfter);
        Assert.Equal(wallet, result.WalletBefore);
        Assert.Equal(wallet, result.WalletAfter);
        Assert.Equal(
            new LegacyNobilityDonationSpend(
                LegacyNobilityDonationCurrency.None,
                0),
            result.Spend);
        Assert.Empty(result.ChangedIcons);
        Assert.Equal(result.RankingBefore, result.RankingAfter);
    }

    [Fact]
    public void ApplyDonationRecalculatesKingMovementAndReportsChangedIconStates()
    {
        var donor = State(40, 0, "Donor");
        var candidates = new[]
        {
            State(10, 400_000_000, "First"),
            State(20, 390_000_000, "Second"),
            State(30, 380_000_000, "Third"),
        };
        var wallet = new LegacyNobilityDonationWallet(
            Money: 500_000_000,
            ConquerPoints: 0,
            BoundConquerPoints: 0);

        var result = LegacyNobilityDonationRuntime.ApplyDonation(
            donor,
            wallet,
            donationAmount: 385_000_000,
            LegacyNobilityDonationType.Money,
            candidates);

        Assert.True(result.Applied);
        Assert.True(result.Promoted);
        Assert.Equal(
            new ulong[] { 10, 20, 40, 30 },
            result.RankingAfter.Select(state => state.Uid).ToArray());
        Assert.Equal((byte)12, result.DonorAfter.Rank);
        Assert.Equal(2, result.DonorAfter.Position);

        var displaced = Assert.Single(result.ChangedIcons, icon => icon.Uid == 30);
        Assert.Equal((byte)9, displaced.Rank);
        Assert.Equal(3, displaced.Position);
        Assert.Contains(result.ChangedIcons, icon => icon.Uid == 40 && icon.Rank == 12 && icon.Position == 2);
        Assert.DoesNotContain(result.ChangedIcons, icon => icon.Uid == 10);
        Assert.DoesNotContain(result.ChangedIcons, icon => icon.Uid == 20);
    }

    [Fact]
    public void ApplyExternalDonationAddsLiteralRewardWithoutWalletSpendAndMatchesDonationRankingFanout()
    {
        var donor = State(40, 100_000_000, "Donor");
        var candidates = new[]
        {
            State(10, 400_000_000, "First"),
            State(20, 390_000_000, "Second"),
            State(30, 380_000_000, "Third"),
        };

        var result = LegacyNobilityDonationRuntime.ApplyExternalDonation(
            donor,
            donationAmount: 2_000_000_000,
            candidates);
        var walletDonation = LegacyNobilityDonationRuntime.ApplyDonation(
            donor,
            new LegacyNobilityDonationWallet(
                Money: 2_000_000_000,
                ConquerPoints: 123,
                BoundConquerPoints: 456),
            donationAmount: 2_000_000_000,
            LegacyNobilityDonationType.Money,
            candidates);

        Assert.True(result.Applied);
        Assert.Equal(LegacyNobilityDonationFailure.None, result.Failure);
        Assert.Equal(100_000_000UL, result.DonorBefore.Donation);
        Assert.Equal(2_100_000_000UL, result.DonorAfter.Donation);
        Assert.Equal(2_000_000_000UL, result.DonorAfter.Donation - result.DonorBefore.Donation);
        Assert.Equal(default, result.WalletBefore);
        Assert.Equal(default, result.WalletAfter);
        Assert.Equal(
            new LegacyNobilityDonationSpend(LegacyNobilityDonationCurrency.None, 0),
            result.Spend);

        Assert.Equal(walletDonation.DonorBefore, result.DonorBefore);
        Assert.Equal(walletDonation.DonorAfter, result.DonorAfter);
        Assert.Equal(walletDonation.RankingBefore, result.RankingBefore);
        Assert.Equal(walletDonation.RankingAfter, result.RankingAfter);
        Assert.Equal(walletDonation.ChangedIcons, result.ChangedIcons);
        Assert.Equal(walletDonation.Thresholds, result.Thresholds);
        Assert.Equal(
            new ulong[] { 40, 10, 20, 30 },
            result.RankingAfter.Select(state => state.Uid).ToArray());
        Assert.Equal(
            new ulong[] { 10, 20, 30, 40 },
            result.ChangedIcons.Select(icon => icon.Uid).ToArray());
        Assert.Contains(result.ChangedIcons, icon => icon.Uid == 30 && icon.Rank == 9 && icon.Position == 3);
    }

    [Fact]
    public void ApplyExternalDonationRejectsZeroWithoutMutation()
    {
        var donor = State(40, 100_000_000, "Donor");

        var result = LegacyNobilityDonationRuntime.ApplyExternalDonation(
            donor,
            donationAmount: 0);

        Assert.False(result.Applied);
        Assert.Equal(LegacyNobilityDonationFailure.ZeroDonation, result.Failure);
        Assert.Equal(result.DonorBefore, result.DonorAfter);
        Assert.Equal(result.RankingBefore, result.RankingAfter);
        Assert.Equal(default, result.WalletBefore);
        Assert.Equal(result.WalletBefore, result.WalletAfter);
        Assert.Equal(LegacyNobilityDonationCurrency.None, result.Spend.Currency);
        Assert.Equal(0UL, result.Spend.Amount);
        Assert.Empty(result.ChangedIcons);
    }

    [Fact]
    public void ApplyExternalDonationOverflowFailsClosedWithoutSpendOrRankingMutation()
    {
        var donor = State(40, ulong.MaxValue - 1, "Donor");

        var result = LegacyNobilityDonationRuntime.ApplyExternalDonation(
            donor,
            donationAmount: 2);

        Assert.False(result.Applied);
        Assert.Equal(LegacyNobilityDonationFailure.DonationOverflow, result.Failure);
        Assert.Equal(ulong.MaxValue - 1, result.DonorAfter.Donation);
        Assert.Equal(result.DonorBefore, result.DonorAfter);
        Assert.Equal(result.RankingBefore, result.RankingAfter);
        Assert.Equal(default, result.WalletBefore);
        Assert.Equal(result.WalletBefore, result.WalletAfter);
        Assert.Equal(LegacyNobilityDonationCurrency.None, result.Spend.Currency);
        Assert.Equal(0UL, result.Spend.Amount);
        Assert.Empty(result.ChangedIcons);
    }

    private static LegacyNobilityState State(
        ulong uid,
        ulong donation,
        string? name = null)
    {
        return new LegacyNobilityState(
            uid,
            name ?? $"Player{uid}",
            Gender: 1,
            Mesh: 1_541_008,
            donation,
            LegacyNobilityRanking.ComputeRank(donation, LegacyNobilityRanking.DefaultPosition),
            LegacyNobilityRanking.DefaultPosition);
    }
}
