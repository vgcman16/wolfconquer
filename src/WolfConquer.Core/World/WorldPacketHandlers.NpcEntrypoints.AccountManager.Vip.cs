using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerVipAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ulong vipLevel,
        ulong cost,
        CancellationToken cancellationToken)
    {
        var donatePoints = ReadAttributeUInt64(session.Character.Attributes, "DonatePoints");
        if (donatePoints < cost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.AccountManager",
                    [
                        string.Concat(
                            LegacyAccountManagerInsufficientDonatePointsText,
                            " You need ",
                            cost.ToString(CultureInfo.InvariantCulture),
                            " donation points."),
                    ],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    LegacyAccountManagerShopAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager VIP purchase " +
                    $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} " +
                    $"cost={cost} donatePoints={donatePoints}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedDonatePoints = donatePoints - cost;
        var now = DateTime.Now;
        var vipExpiresAt = ResolveLegacyAccountManagerVipBaseTime(session.Character, now)
            .AddDays(LegacyAccountManagerVipDays);
        var vipTime = vipExpiresAt.ToBinary();
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(session.Character, "DonatePoints", updatedDonatePoints),
                "VipLevel",
                vipLevel),
            "VipTime",
            vipTime.ToString(CultureInfo.InvariantCulture));
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = updatedDonatePoints.ToString(CultureInfo.InvariantCulture),
            ["VipLevel"] = vipLevel.ToString(CultureInfo.InvariantCulture),
            ["VipTime"] = vipTime.ToString(CultureInfo.InvariantCulture),
        };
        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc AccountManager VIP purchase " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"path={Path.GetFileName(path)} keys=DonatePoints,VipLevel,VipTime.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager VIP purchase persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"reason={reason}.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyVipLevelStartupUpdate(updatedCharacter).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyVipStatusPacket.FromSnapshot(updatedCharacter).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(
                        $"You have received VIP{vipLevel.ToString(CultureInfo.InvariantCulture)} (30 days).")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountManager VIP purchase " +
                $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                $"option={optionId} cost={cost} vipLevel={vipLevel} donatePoints={updatedDonatePoints} " +
                $"vipTime={vipTime}.")
            .ConfigureAwait(false);
        return true;
    }

    private static DateTime ResolveLegacyAccountManagerVipBaseTime(
        CharacterSnapshot character,
        DateTime now)
    {
        var raw = ReadAttributeString(character.Attributes, "VipTime", "ExpireVip");
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var binary))
        {
            try
            {
                var existing = DateTime.FromBinary(binary);
                if (existing > now)
                {
                    return existing;
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return now;
    }
}
