using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyAccountManagerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.AccountManager",
                        [LegacyAccountManagerRootDialogText],
                        LegacyAccountManagerRootOptions,
                        LegacyAccountManagerRootAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                {
                    var donatePoints = ReadAttributeUInt64(session.Character.Attributes, "DonatePoints");
                    await SendLegacyNpcDialogAsync(
                            context,
                            "NpcHandler.AccountManager",
                            [$"You currently have {donatePoints.ToString(CultureInfo.InvariantCulture)} donation points."],
                            LegacyAccountManagerShopOptions,
                            LegacyAccountManagerShopAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

            case 2:
                return await TryPurchaseLegacyAccountManagerAttributeFlagAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerChargeChiCost,
                        "ChargeChi",
                        cancellationToken)
                    .ConfigureAwait(false);

            case 3:
                return await TryPurchaseLegacyAccountManagerConquerPointsAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerOneBillionConquerPointsCost,
                        LegacyAccountManagerOneBillionConquerPointsGrant,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 4:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerAnima14ItemId,
                        LegacyAccountManagerAnima14Cost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 5:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerAnima15ItemId,
                        LegacyAccountManagerAnima15Cost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 6:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerAnima16ItemId,
                        LegacyAccountManagerAnima16Cost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 7:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerAnima17ItemId,
                        LegacyAccountManagerAnima17Cost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 8:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerAnima18ItemId,
                        LegacyAccountManagerAnima18Cost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 9:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerHolyGrailItemId,
                        LegacyAccountManagerHolyGrailCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 31:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.AccountManager",
                        ["Choose the rune pack you want."],
                        LegacyAccountManagerRunePackOptions,
                        avatarId: 62,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 12:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerCriticalStrikeRunePackItemId,
                        LegacyAccountManagerRunePackCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 13:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerFireBurstRunePackItemId,
                        LegacyAccountManagerRunePackCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 14:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerMiracleRunePackItemId,
                        LegacyAccountManagerRunePackCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 15:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerNirvanaRunePackItemId,
                        LegacyAccountManagerRunePackCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 16:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerBloodFeastRunePackItemId,
                        LegacyAccountManagerRunePackCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 17:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerUniversalShieldRunePackItemId,
                        LegacyAccountManagerRunePackCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 18:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerMiracleTouchRunePackItemId,
                        LegacyAccountManagerRunePackCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 10:
                return await TryPurchaseLegacyAccountManagerPaidNobilityAsync(
                        context,
                        session,
                        optionId,
                        LegacyNobilityRanking.RankKing,
                        LegacyAccountManagerPaidKingCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 34:
                return await TryPurchaseLegacyAccountManagerPaidNobilityAsync(
                        context,
                        session,
                        optionId,
                        LegacyNobilityRanking.RankPrince,
                        LegacyAccountManagerPaidPrinceCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 35:
                return await TryPurchaseLegacyAccountManagerVipAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerVip6Level,
                        LegacyAccountManagerVip6Cost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 36:
                return await TryPurchaseLegacyAccountManagerFullPromotionAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 37:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.AccountManager",
                        ["Choose the garment or mount armor you want."],
                        LegacyAccountManagerGarmentMountOptions,
                        avatarId: 62,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 38:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerInvincibleOrientalItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 39:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerSkyDragonItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 40:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerFinalSurvivorItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 41:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerHungryDragonAbyssItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 42:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerFloralBrocadeItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 43:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerRoseMistGloriousItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 44:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerPureFragrantLotusItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 45:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerWingedBobcatPhantomItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 46:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerLightningLeopardStarItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 47:
                return await TryPurchaseLegacyAccountManagerItemAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerGhostRocketBiterItemId,
                        LegacyAccountManagerGarmentMountCost,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 166:
                return await TryPurchaseLegacyAccountManagerGuildSkillAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 197:
                return await TryPurchaseLegacyAccountManagerVipAsync(
                        context,
                        session,
                        optionId,
                        LegacyAccountManagerVip7Level,
                        LegacyAccountManagerVip7Cost,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerGuildSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var donatePoints = ReadAttributeUInt64(session.Character.Attributes, "DonatePoints");
        if (donatePoints < LegacyAccountManagerGuildSkillCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.AccountManager",
                    [
                        string.Concat(
                            LegacyAccountManagerInsufficientDonatePointsText,
                            " You need ",
                            LegacyAccountManagerGuildSkillCost.ToString(CultureInfo.InvariantCulture),
                            " donation points."),
                    ],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    LegacyAccountManagerShopAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager guild skill purchase " +
                    $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} " +
                    $"cost={LegacyAccountManagerGuildSkillCost} donatePoints={donatePoints}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedDonatePoints = donatePoints - LegacyAccountManagerGuildSkillCost;
        var currentGuildDonation = ReadAttributeUInt64(
            session.Character.Attributes,
            "GuildDontion",
            "GuildDonation",
            "MyDontion");
        var updatedGuildDonation = checked(currentGuildDonation + LegacyAccountManagerGuildSkillDonationGrant);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(session.Character, "DonatePoints", updatedDonatePoints),
                "GuildDontion",
                updatedGuildDonation),
            "ChargeGuildSkill",
            1UL);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = updatedDonatePoints.ToString(CultureInfo.InvariantCulture),
            ["GuildDontion"] = updatedGuildDonation.ToString(CultureInfo.InvariantCulture),
            ["ChargeGuildSkill"] = "1",
        };
        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc AccountManager guild skill purchase " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"path={Path.GetFileName(path)} keys=DonatePoints,GuildDontion,ChargeGuildSkill.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager guild skill purchase persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"reason={reason}.")
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountManager guild skill purchase " +
                $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                $"option={optionId} cost={LegacyAccountManagerGuildSkillCost} " +
                $"donatePoints={updatedDonatePoints} guildDonation={updatedGuildDonation} " +
                "chargeGuildSkill=1.")
            .ConfigureAwait(false);
        return true;
    }





}
