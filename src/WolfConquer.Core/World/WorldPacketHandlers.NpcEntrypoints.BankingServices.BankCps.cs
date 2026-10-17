using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyBankCpsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyBankOverviewAsync(context, session, cancellationToken).ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyBankInputDialogAsync(
                        context,
                        LegacyBankCpsSource,
                        "How many CPs do you want to deposit?",
                        "Deposit CPs",
                        inputOptionId: 10,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                return await TryBeginLegacyBankTransferAsync(
                        context,
                        session,
                        input,
                        deposit: true,
                        confirmationOptionId: 2,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 2:
                return await TryApplyLegacyBankTransferAsync(
                        context,
                        session,
                        deposit: true,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 5:
                await SendLegacyBankInputDialogAsync(
                        context,
                        LegacyBankCpsSource,
                        "How many CPs do you want to withdraw?",
                        "Withdraw CPs",
                        inputOptionId: 20,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
                return await TryBeginLegacyBankTransferAsync(
                        context,
                        session,
                        input,
                        deposit: false,
                        confirmationOptionId: 3,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 3:
                return await TryApplyLegacyBankTransferAsync(
                        context,
                        session,
                        deposit: false,
                        cancellationToken)
                    .ConfigureAwait(false);

            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyBankOverviewAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyBankCpsSource,
                [
                    string.Concat(
                        "Dear ",
                        session.Character.Name,
                        ", you have ",
                        session.Character.BankConquerPoints.ToString(CultureInfo.InvariantCulture),
                        " CPs in the bank."),
                    "What would you like to do?",
                ],
                [
                    new LegacyNpcDialogOption("Deposit CPs.", 1),
                    new LegacyNpcDialogOption("Withdraw CPs.", 5),
                    new LegacyNpcDialogOption("Later.", byte.MaxValue),
                ],
                avatarId: 183,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryBeginLegacyBankTransferAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string input,
        bool deposit,
        byte confirmationOptionId,
        CancellationToken cancellationToken)
    {
        if (!uint.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
            amount == 0)
        {
            await SendLegacyBankInputDialogAsync(
                    context,
                    LegacyBankCpsSource,
                    "Please enter a valid CP amount.",
                    deposit ? "Deposit CPs" : "Withdraw CPs",
                    deposit ? (byte)10 : (byte)20,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var available = deposit
            ? session.Character.ConquerPoints
            : session.Character.BankConquerPoints;
        if (available < amount)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyBankCpsSource,
                    [
                        deposit
                            ? "You do not have that many CPs with you."
                            : "You do not have that many CPs in the bank.",
                    ],
                    [new LegacyNpcDialogOption("I see.", byte.MaxValue)],
                    avatarId: 48,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var pendingSession = session with
        {
            PendingBankConquerPoints = amount,
            PendingBankConquerPointsIsDeposit = deposit,
        };
        context.Sessions?.Set(context.Connection, pendingSession);

        await SendLegacyNpcDialogAsync(
                context with { Session = pendingSession },
                LegacyBankCpsSource,
                [
                    string.Concat(
                        "Are you sure you want to ",
                        deposit ? "deposit " : "withdraw ",
                        amount.ToString(CultureInfo.InvariantCulture),
                        " CPs",
                        deposit ? " into your bank?" : " from your bank?"),
                ],
                [
                    new LegacyNpcDialogOption("Yes.", confirmationOptionId),
                    new LegacyNpcDialogOption("No thanks.", byte.MaxValue),
                ],
                avatarId: 48,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryApplyLegacyBankTransferAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool deposit,
        CancellationToken cancellationToken)
    {
        if (session.PendingBankConquerPoints is not { } amount ||
            session.PendingBankConquerPointsIsDeposit != deposit ||
            amount == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyBankCpsSource,
                    ["Please enter the amount again before confirming."],
                    [new LegacyNpcDialogOption("I see.", byte.MaxValue)],
                    avatarId: 48,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var character = session.Character;
        if (deposit && character.ConquerPoints < amount)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyBankCpsSource,
                    ["You do not have that many CPs with you."],
                    [new LegacyNpcDialogOption("I see.", byte.MaxValue)],
                    avatarId: 48,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!deposit && character.BankConquerPoints < amount)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyBankCpsSource,
                    ["You do not have that many CPs in the bank."],
                    [new LegacyNpcDialogOption("I see.", byte.MaxValue)],
                    avatarId: 48,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedConquerPoints = deposit
            ? checked(character.ConquerPoints - amount)
            : checked(character.ConquerPoints + amount);
        var updatedBankConquerPoints = deposit
            ? checked(character.BankConquerPoints + amount)
            : checked(character.BankConquerPoints - amount);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                character with
                {
                    ConquerPoints = updatedConquerPoints,
                    BankConquerPoints = updatedBankConquerPoints,
                },
                "ConquerPoints",
                checked((ulong)updatedConquerPoints)),
            "BankCps",
            checked((ulong)updatedBankConquerPoints));
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PendingBankConquerPoints = null,
            PendingBankConquerPointsIsDeposit = false,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                    CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)updatedConquerPoints))])
                .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await PersistLegacyBankCpsAsync(context, updatedSession).ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context with { Session = updatedSession },
                LegacyBankCpsSource,
                [
                    string.Concat(
                        "Done. You now have ",
                        updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
                        " CPs with you and ",
                        updatedBankConquerPoints.ToString(CultureInfo.InvariantCulture),
                        " CPs in the bank."),
                ],
                [new LegacyNpcDialogOption("Thanks.", byte.MaxValue)],
                avatarId: 183,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc BankCps transfer " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyBankCpsSource} " +
                $"mode={(deposit ? "deposit" : "withdraw")} amount={amount} " +
                $"conquerPoints={updatedConquerPoints} bankCps={updatedBankConquerPoints}.")
            .ConfigureAwait(false);
        return true;
    }
}
