using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyNobilityDonationAsync(
        WorldPacketHandlerContext context,
        LegacyNobilityRequestPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyNobility donation {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Value == 0)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyNobility donation {FormatSession(context)}; donation amount is zero.")
                .ConfigureAwait(false);
            return;
        }

        if (TryGetLegacyNobilityDonationGuardReason(session, out var guardReason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped source-backed LegacyNobility donation guard " +
                    $"{FormatSession(context)} reason={guardReason} donationType={packet.DonationType} " +
                    $"amount={packet.Value}.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.DonationType != (byte)LegacyNobilityDonationType.ConquerPoints)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored source-backed LegacyNobility direct donation " +
                    $"{FormatSession(context)} donationType={packet.DonationType} amount={packet.Value}; " +
                    "legacy MsgNobility only mutates ConquerPoints donations.")
                .ConfigureAwait(false);
            return;
        }

        var character = session.Character;
        var donation = packet.Value;
        var donorState = LegacyNobilityRanking.ResolveForSnapshot(
            character,
            session.UsersRoot,
            context.Sessions);
        var rankingCandidates = LegacyNobilityRanking
            .CreateRankListEntries(session.UsersRoot, context.Sessions)
            .Select(static entry => new LegacyNobilityState(
                entry.Uid,
                entry.Name,
                entry.Gender,
                entry.Mesh,
                entry.Donation,
                entry.Rank,
                entry.Position))
            .ToArray();
        var result = LegacyNobilityDonationRuntime.ApplyDonation(
            donorState,
            new LegacyNobilityDonationWallet(
                character.Money,
                character.ConquerPoints,
                character.BoundConquerPoints),
            donation,
            packet.DonationType,
            rankingCandidates);

        if (!result.Applied)
        {
            await LogLegacyNobilityDonationFailureAsync(context, packet, result.Failure).ConfigureAwait(false);
            return;
        }

        var updatedCharacter = character with
        {
            Money = result.WalletAfter.Money,
            ConquerPoints = result.WalletAfter.ConquerPoints,
            BoundConquerPoints = result.WalletAfter.BoundConquerPoints,
        };
        var totalDonation = result.DonorAfter.Donation;
        var nobilityState = result.DonorAfter;
        updatedCharacter = WithLegacyNobilityState(
            updatedCharacter,
            nobilityState);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        await TryPersistLegacyNobilityDonationAsync(context, updatedSession).ConfigureAwait(false);
        var currencyUpdateType = result.Spend.Currency switch
        {
            LegacyNobilityDonationCurrency.Money => LegacyMsgUpdateDataType.Money,
            LegacyNobilityDonationCurrency.ConquerPoints => LegacyMsgUpdateDataType.ConquerPoints,
            LegacyNobilityDonationCurrency.BoundConquerPoints => LegacyMsgUpdateDataType.BoundConquerPoints,
            _ => (LegacyMsgUpdateDataType?)null,
        };
        var currencyUpdateValue = result.Spend.Currency switch
        {
            LegacyNobilityDonationCurrency.Money => checked((ulong)updatedCharacter.Money),
            LegacyNobilityDonationCurrency.ConquerPoints => checked((ulong)updatedCharacter.ConquerPoints),
            LegacyNobilityDonationCurrency.BoundConquerPoints => checked((ulong)updatedCharacter.BoundConquerPoints),
            _ => 0UL,
        };

        await context.Connection.SendAsync(
                LegacyNobilityIconPacket.FromSnapshot(
                        updatedCharacter,
                        session.UsersRoot,
                        context.Sessions)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (currencyUpdateType is { } updateType)
        {
            await SendScalarUpdateAsync(
                    context,
                    updatedCharacter,
                    updateType,
                    currencyUpdateValue,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyNobilityChangedIconsAsync(
                context with { Session = updatedSession },
                result,
                cancellationToken)
            .ConfigureAwait(false);

        if (ShouldBroadcastLegacyNobilityPromotion(result))
        {
            await BroadcastLegacyNobilityPromotionAsync(
                    context with { Session = updatedSession },
                    result.DonorAfter,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNobility donation " +
                $"{FormatSession(context with { Session = updatedSession })} amount={donation} " +
                $"totalDonation={totalDonation} rank={nobilityState.Rank} position={nobilityState.Position} " +
                $"spentCurrency={result.Spend.Currency} spentAmount={result.Spend.Amount}; " +
                "source=MsgNobility donation.")
            .ConfigureAwait(false);
    }


    private static bool TryGetLegacyNobilityDonationGuardReason(WorldSession session, out string reason)
    {
        if (TryReadAttributeBool(session.Character.Attributes, out var onMyOwnServer, "OnMyOwnServer") &&
            !onMyOwnServer)
        {
            reason = "notOwnServer";
            return true;
        }

        if (ReadAttributeBool(session.Character.Attributes, "CrossServer", "InCrossServer", "IsCrossServer"))
        {
            reason = "notOwnServer";
            return true;
        }

        if (session.LegacyTradeActive)
        {
            reason = "inTrade";
            return true;
        }

        if (ReadAttributeUInt64(session.Character.Attributes, "PokerTableID", "PokerTableId") != 0 ||
            ReadAttributeUInt64(session.Character.Attributes, "PokerSeat") != 0)
        {
            reason = "poker";
            return true;
        }

        reason = string.Empty;
        return false;
    }


    private static async ValueTask LogLegacyNobilityDonationFailureAsync(
        WorldPacketHandlerContext context,
        LegacyNobilityRequestPacket packet,
        LegacyNobilityDonationFailure failure)
    {
        var character = context.Session?.Character;
        switch (failure)
        {
            case LegacyNobilityDonationFailure.UnsupportedDonationType:
                await context.Log.WriteLineAsync(
                        "PARITY-GAP unsupported LegacyNobility donation type " +
                        $"{FormatSession(context)} donationType={packet.DonationType} amount={packet.Value}.")
                    .ConfigureAwait(false);
                break;
            case LegacyNobilityDonationFailure.CostOverflow:
                await context.Log.WriteLineAsync(
                        $"WorldServer skipped LegacyNobility donation {FormatSession(context)}; CP cost is too large.")
                    .ConfigureAwait(false);
                break;
            case LegacyNobilityDonationFailure.DonationOverflow:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyNobility donation " +
                        $"{FormatSession(context)} donationType={packet.DonationType} amount={packet.Value}; donation total is too large.")
                    .ConfigureAwait(false);
                break;
            case LegacyNobilityDonationFailure.InsufficientFunds when
                packet.DonationType == (byte)LegacyNobilityDonationType.Money:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyNobility donation " +
                        $"{FormatSession(context)} donationType={packet.DonationType} amount={packet.Value} money={character?.Money ?? 0}.")
                    .ConfigureAwait(false);
                break;
            case LegacyNobilityDonationFailure.InsufficientFunds:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyNobility donation " +
                        $"{FormatSession(context)} donationType={packet.DonationType} amount={packet.Value} " +
                        $"conquerPoints={character?.ConquerPoints ?? 0} boundConquerPoints={character?.BoundConquerPoints ?? 0}.")
                    .ConfigureAwait(false);
                break;
            default:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyNobility donation " +
                        $"{FormatSession(context)} donationType={packet.DonationType} amount={packet.Value} failure={failure}.")
                    .ConfigureAwait(false);
                break;
        }
    }


    private static async ValueTask SendLegacyNobilityChangedIconsAsync(
        WorldPacketHandlerContext context,
        LegacyNobilityDonationResult result,
        CancellationToken cancellationToken)
    {
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is not { Count: > 0 } ||
            result.ChangedIcons.Count == 0)
        {
            return;
        }

        var sent = 0;
        foreach (var icon in result.ChangedIcons)
        {
            if (icon.Uid == result.DonorAfter.Uid)
            {
                continue;
            }

            var frame = new LegacyNobilityIconPacket(icon.Uid, icon.Donation, icon.Rank, icon.Position)
                .ToFrameBytes();
            foreach (var active in activeSessions)
            {
                if ((ulong)Math.Max(0, active.Session.Character.Uid) != icon.Uid)
                {
                    continue;
                }

                var updatedCharacter = WithLegacyNobilityState(
                    active.Session.Character,
                    new LegacyNobilityState(
                        icon.Uid,
                        active.Session.Character.Name,
                        Gender: 0,
                        Mesh: 0,
                        icon.Donation,
                        icon.Rank,
                        icon.Position));
                context.Sessions?.Set(active.Connection, active.Session with { Character = updatedCharacter });
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                sent++;
            }
        }

        if (sent > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyNobility changed icon fanout " +
                    $"{FormatSession(context)} count={sent}.")
                .ConfigureAwait(false);
        }
    }


    private static bool ShouldBroadcastLegacyNobilityPromotion(LegacyNobilityDonationResult result)
    {
        return result.DonorAfter.Position != result.DonorBefore.Position &&
            result.DonorAfter.Rank > result.DonorBefore.Rank;
    }


    private static async ValueTask BroadcastLegacyNobilityPromotionAsync(
        WorldPacketHandlerContext context,
        LegacyNobilityState donor,
        CancellationToken cancellationToken)
    {
        var messageFrame = LegacyMsgMessagePacket
            .SystemRedCenter($"{donor.Name} has been promoted to {LegacyNobilityRanking.RankName(donor.Rank)}.")
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        var sent = 0;
        if (activeSessions is { Count: > 0 })
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
                sent++;
            }
        }
        else
        {
            await context.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
            sent = 1;
        }

        await context.Log.WriteLineAsync(
                "WorldServer broadcast LegacyNobility promotion " +
                $"{FormatSession(context)} uid={donor.Uid} rank={donor.Rank} count={sent}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask TryPersistLegacyNobilityDonationAsync(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var character = session.Character;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonationNobility"] = LegacyNobilityRanking.ReadRegularDonation(character).ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = LegacyNobilityRanking.ReadRegularDonation(character).ToString(CultureInfo.InvariantCulture),
            ["NobilityRank"] = ReadAttributeUInt64(character.Attributes, "NobilityRank").ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = character.Attributes.TryGetValue("NobilityPosition", out var nobilityPosition)
                ? nobilityPosition
                : LegacyNobilityRanking.DefaultPosition.ToString(CultureInfo.InvariantCulture),
            ["Money"] = character.Money.ToString(CultureInfo.InvariantCulture),
            ["ConquerPoints"] = character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["BoundConquerPoints"] = character.BoundConquerPoints.ToString(CultureInfo.InvariantCulture),
        };

        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNobility persistence " +
                    $"{FormatSession(context with { Session = session })} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNobility " +
                $"{FormatSession(context with { Session = session })} path={Path.GetFileName(path)} " +
                "keys=DonationNobility,LastNobilityDonation,NobilityRank,NobilityPosition,Money,ConquerPoints,BoundConquerPoints.")
            .ConfigureAwait(false);
    }
}
