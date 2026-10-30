using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyBankCpsAsync(WorldPacketHandlerContext context,
        WorldSession session, byte optionId, string input, CancellationToken cancellationToken)
    {
        if (IsBankCpsMutationOption(optionId))
            return await TryHandleLegacyBankCpsMutationAsync(context, session, optionId, input,
                requireActiveNpc: false, cancellationToken).ConfigureAwait(false);
        switch (optionId)
        {
            case 0:
                await SendLegacyBankOverviewAsync(context, session, cancellationToken).ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyBankInputDialogAsync(context, LegacyBankCpsSource,
                    "How many CPs do you want to deposit?", "Deposit CPs", 10, cancellationToken).ConfigureAwait(false);
                return true;
            case 5:
                await SendLegacyBankInputDialogAsync(context, LegacyBankCpsSource,
                    "How many CPs do you want to withdraw?", "Withdraw CPs", 20, cancellationToken).ConfigureAwait(false);
                return true;
            default: return false;
        }
    }

    private static async ValueTask SendLegacyBankOverviewAsync(WorldPacketHandlerContext context,
        WorldSession session, CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(context, LegacyBankCpsSource,
            [string.Concat("Dear ", session.Character.Name, ", you have ",
                session.Character.BankConquerPoints.ToString(CultureInfo.InvariantCulture), " CPs in the bank."),
                "What would you like to do?"],
            [new LegacyNpcDialogOption("Deposit CPs.", 1), new LegacyNpcDialogOption("Withdraw CPs.", 5),
                new LegacyNpcDialogOption("Later.", byte.MaxValue)], avatarId: 183, cancellationToken).ConfigureAwait(false);
    }
}
