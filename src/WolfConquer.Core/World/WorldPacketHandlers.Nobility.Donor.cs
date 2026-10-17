using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyNobilityDonorNpcId = 2_541;

    private const uint LegacyNobilityGeneralJuddNpcId = 1_611;

    private const ushort LegacyNobilityDonorAvatarId = 6;

    private const string LegacyNobilityDonorDialogText =
        "Here you can quickly make a Nobility donation. CPs count as 500 donation points each.";

    private const string LegacyNobilityDonorInputText = "CP donation amount";

    private const string LegacyNobilityDonorCloseOptionText = "Just passing by.";

    private const string LegacyNobilityDonorConfirmOptionText = "Yes.";

    private const string LegacyNobilityDonorCancelOptionText = "Never mind.";

    private const string LegacyNobilityDonorRetryText = "Please enter the amount again.";

    private const string LegacyNobilityDonorMissingPendingText = "Please enter a donation amount first.";


    private static bool IsLegacyNobilityDonorNpc(uint npcId)
    {
        return npcId is LegacyNobilityDonorNpcId or LegacyNobilityGeneralJuddNpcId;
    }


    private static async ValueTask<bool> TryHandleLegacyNobilityDonorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        if (optionId != 2 &&
            session.PendingNobilityDonationConquerPoints is not null)
        {
            session = session with { PendingNobilityDonationConquerPoints = null };
            context = context with { Session = session };
            context.Sessions?.Set(context.Connection, session);
        }

        switch (optionId)
        {
            case 0:
                {
                    await SendLegacyNobilityDonorInitialDialogAsync(
                            context,
                            LegacyNobilityDonorDialogText,
                            maxLength: 10,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

            case 1:
                {
                    if (!uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var conquerPoints) ||
                        conquerPoints == 0)
                    {
                        await SendLegacyNobilityDonorInitialDialogAsync(
                                context,
                                LegacyNobilityDonorRetryText,
                                maxLength: 16,
                                cancellationToken)
                            .ConfigureAwait(false);
                        return true;
                    }

                    if (session.Character.ConquerPoints < conquerPoints)
                    {
                        await context.Connection.SendAsync(
                                LegacyNpcReplyPacket.ForMessageBox(
                                        string.Concat(
                                            "You do not have: ",
                                            conquerPoints.ToString(CultureInfo.InvariantCulture),
                                            " CPs."))
                                    .ToFrameBytes(),
                                cancellationToken)
                            .ConfigureAwait(false);
                        await context.Log.WriteLineAsync(
                                "WorldServer skipped LegacyNpc NobilityDonor pending donation " +
                                $"{FormatSession(context)} cp={conquerPoints} conquerPoints={session.Character.ConquerPoints}.")
                            .ConfigureAwait(false);
                        return true;
                    }

                    var pendingSession = session with { PendingNobilityDonationConquerPoints = conquerPoints };
                    context.Sessions?.Set(context.Connection, pendingSession);
                    var donation = checked((ulong)conquerPoints * 500UL);
                    await SendLegacyNobilityDonorConfirmDialogAsync(
                            context with { Session = pendingSession },
                            conquerPoints,
                            donation,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

            case 2:
                {
                    if (session.PendingNobilityDonationConquerPoints is not { } conquerPoints ||
                        conquerPoints == 0)
                    {
                        await context.Connection.SendAsync(
                                LegacyNpcReplyPacket.ForMessageBox(LegacyNobilityDonorMissingPendingText).ToFrameBytes(),
                                cancellationToken)
                            .ConfigureAwait(false);
                        await context.Log.WriteLineAsync(
                                $"WorldServer skipped LegacyNpc NobilityDonor confirm {FormatSession(context)}; no pending CP amount.")
                            .ConfigureAwait(false);
                        return true;
                    }

                    if (session.Character.ConquerPoints < conquerPoints)
                    {
                        var resetSession = session with { PendingNobilityDonationConquerPoints = null };
                        context.Sessions?.Set(context.Connection, resetSession);
                        await context.Connection.SendAsync(
                                LegacyNpcReplyPacket.ForMessageBox(
                                        string.Concat(
                                            "You do not have: ",
                                            conquerPoints.ToString(CultureInfo.InvariantCulture),
                                            " CPs."))
                                    .ToFrameBytes(),
                                cancellationToken)
                            .ConfigureAwait(false);
                        await context.Log.WriteLineAsync(
                                "WorldServer skipped LegacyNpc NobilityDonor confirm " +
                                $"{FormatSession(context with { Session = resetSession })} cp={conquerPoints} " +
                                $"conquerPoints={session.Character.ConquerPoints}.")
                            .ConfigureAwait(false);
                        return true;
                    }

                    var donation = checked((ulong)conquerPoints * 500UL);
                    var donatingSession = session with { PendingNobilityDonationConquerPoints = null };
                    context.Sessions?.Set(context.Connection, donatingSession);
                    await HandleLegacyNobilityDonationAsync(
                            context with { Session = donatingSession },
                            new LegacyNobilityRequestPacket(
                                LegacyNobilityAction.Donate,
                                donation,
                                (byte)LegacyNobilityDonationType.ConquerPoints),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer routed LegacyNpc NobilityDonor donation " +
                            $"{FormatSession(context)} cp={conquerPoints} donation={donation}; " +
                            "source=NpcHandler.NobilityDonor option 2.")
                        .ConfigureAwait(false);
                    return true;
                }

            case LegacyNpcClosedOption:
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer closed LegacyNpc NobilityDonor " +
                            $"{FormatSession(context)}; pending donation cleared.")
                        .ConfigureAwait(false);
                    return true;
                }
        }

        return false;
    }


    private static async ValueTask SendLegacyNobilityDonorInitialDialogAsync(
        WorldPacketHandlerContext context,
        string text,
        ushort maxLength,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForInput(LegacyNobilityDonorInputText, optionId: 1, maxLength).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForClosedOption(LegacyNobilityDonorCloseOptionText).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForAvatar(LegacyNobilityDonorAvatarId).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyNpc NobilityDonor input dialog {FormatSession(context)} maxLength={maxLength}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask SendLegacyNobilityDonorConfirmDialogAsync(
        WorldPacketHandlerContext context,
        uint conquerPoints,
        ulong donation,
        CancellationToken cancellationToken)
    {
        var text = string.Concat(
            "Are you sure you want to donate ",
            conquerPoints.ToString(CultureInfo.InvariantCulture),
            " CPs? This will add ",
            donation.ToString(CultureInfo.InvariantCulture),
            " Nobility donation points.");
        await context.Connection.SendAsync(LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForOption(LegacyNobilityDonorConfirmOptionText, 2).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForClosedOption(LegacyNobilityDonorCancelOptionText).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForAvatar(LegacyNobilityDonorAvatarId).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc NobilityDonor confirm dialog " +
                $"{FormatSession(context)} cp={conquerPoints} donation={donation}.")
            .ConfigureAwait(false);
    }
}
