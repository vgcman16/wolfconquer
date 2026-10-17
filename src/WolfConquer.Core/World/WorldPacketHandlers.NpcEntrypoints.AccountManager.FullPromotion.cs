using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerFullPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var donatePoints = ReadAttributeUInt64(session.Character.Attributes, "DonatePoints");
        if (donatePoints < LegacyAccountManagerFullPromotionCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.AccountManager",
                    [
                        string.Concat(
                            LegacyAccountManagerInsufficientDonatePointsText,
                            " You need ",
                            LegacyAccountManagerFullPromotionCost.ToString(CultureInfo.InvariantCulture),
                            " donation points."),
                    ],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    LegacyAccountManagerShopAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager full promotion " +
                    $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} " +
                    $"cost={LegacyAccountManagerFullPromotionCost} donatePoints={donatePoints}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedDonatePoints = donatePoints - LegacyAccountManagerFullPromotionCost;
        var updatedChargePromotion = checked(ReadAttributeUInt64(
            session.Character.Attributes,
            "ChargePromotion") + 1UL);
        var promotedClass = ResolveLegacyAccountManagerFullPromotionClass(session.Character.Class);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(session.Character with { Class = promotedClass }, "DonatePoints", updatedDonatePoints),
                "Class",
                checked((ulong)promotedClass)),
            "ChargePromotion",
            updatedChargePromotion);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = updatedDonatePoints.ToString(CultureInfo.InvariantCulture),
            ["Class"] = promotedClass.ToString(CultureInfo.InvariantCulture),
            ["ChargePromotion"] = updatedChargePromotion.ToString(CultureInfo.InvariantCulture),
        };
        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc AccountManager full promotion " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"path={Path.GetFileName(path)} keys=DonatePoints,Class,ChargePromotion.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager full promotion persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"reason={reason}.")
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountManager full promotion " +
                $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                $"option={optionId} cost={LegacyAccountManagerFullPromotionCost} class={session.Character.Class} " +
                $"promotedClass={promotedClass} donatePoints={updatedDonatePoints} " +
                $"chargePromotion={updatedChargePromotion}.")
            .ConfigureAwait(false);
        return true;
    }

    private static long ResolveLegacyAccountManagerFullPromotionClass(long classCode)
    {
        return classCode switch
        {
            >= 1_000 and <= 1_051 => 1_051,
            >= 2_000 and <= 2_051 => 2_051,
            >= 4_000 and <= 4_051 => 4_051,
            >= 5_000 and <= 5_051 => 5_051,
            >= 6_000 and <= 6_051 => 6_051,
            >= 7_000 and <= 7_051 => 7_051,
            >= 8_000 and <= 8_051 => 8_051,
            >= 9_000 and <= 9_051 => 9_051,
            >= 13_000 and <= 13_051 => 13_051,
            >= 14_000 and <= 14_051 => 14_051,
            >= 16_000 and <= 16_051 => 16_051,
            _ => classCode,
        };
    }
}
