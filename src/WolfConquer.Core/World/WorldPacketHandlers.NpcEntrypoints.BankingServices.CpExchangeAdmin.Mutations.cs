using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyCpExchangeDragonBallItemId = 1_088_000;
    private const uint LegacyCpExchangeDragonBallScrollItemId = 720_028;
    private const long LegacyCpExchangeConquerPointsInventoryCap = 2_000_000_000;
    private const long LegacyCpExchangeMoneyInventoryCap = 500_000_000_000;

    private enum LegacyCpExchangeMutationKind
    {
        ItemToConquerPoints,
        ItemToChiPoints,
        ConquerPointsToMoney,
        MoneyToConquerPoints,
    }

    private sealed record LegacyCpExchangeMutation(
        LegacyCpExchangeMutationKind Kind,
        uint ItemId,
        long DebitAmount,
        long CreditAmount,
        string InsufficientText,
        string InsufficientOption,
        string SuccessMessage);

    private static async ValueTask<bool> TryApplyLegacyCpExchangeAdminMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyCpExchangeMutation(sourceMethod, optionId, out var mutation))
        {
            return false;
        }

        if (context.Sessions is null)
        {
            await SendLegacyCpExchangeAdminUnavailableAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "sessionStoreMissing",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await SendLegacyCpExchangeAdminUnavailableAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "usersRootMissing",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (mutation.ItemId != 0 && string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await SendLegacyCpExchangeAdminUnavailableAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "playerItemsRootMissing",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return mutation.Kind switch
        {
            LegacyCpExchangeMutationKind.ItemToConquerPoints or
            LegacyCpExchangeMutationKind.ItemToChiPoints =>
                await TryApplyLegacyCpExchangeItemMutationAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        mutation,
                        cancellationToken)
                    .ConfigureAwait(false),
            LegacyCpExchangeMutationKind.ConquerPointsToMoney or
            LegacyCpExchangeMutationKind.MoneyToConquerPoints =>
                await TryApplyLegacyCpExchangeCurrencyMutationAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        mutation,
                        cancellationToken)
                    .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryApplyLegacyCpExchangeItemMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        LegacyCpExchangeMutation mutation,
        CancellationToken cancellationToken)
    {
        var sourceItem = session.PlayerItems.InventoryItems.FirstOrDefault(
            item => item.ItemId == mutation.ItemId && item.StackSize > 0);
        if (sourceItem is null)
        {
            await SendLegacyCpExchangeInsufficientDialogAsync(
                    context,
                    sourceMethod,
                    mutation,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            sourceItem,
            out var consumeFrame);
        CharacterSnapshot updatedCharacter;
        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (mutation.Kind == LegacyCpExchangeMutationKind.ItemToConquerPoints)
        {
            if (session.Character.ConquerPoints < 0 ||
                session.Character.ConquerPoints >
                LegacyCpExchangeConquerPointsInventoryCap - mutation.CreditAmount)
            {
                await SendLegacyCpExchangeAdminUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "conquerPointsOverflowSetterBranchNotAudited",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var updatedConquerPoints = checked(session.Character.ConquerPoints + mutation.CreditAmount);
            updatedCharacter = WithCharacterAttribute(
                session.Character with { ConquerPoints = updatedConquerPoints },
                "ConquerPoints",
                checked((ulong)updatedConquerPoints));
            persistenceValues["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var currentChiPoints = LegacyChiState.FromSnapshot(session.Character).ChiPoints;
            if (currentChiPoints < 0 || currentChiPoints > int.MaxValue - mutation.CreditAmount)
            {
                await SendLegacyCpExchangeAdminUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "chiPointsOverflowBranchNotAudited",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var updatedChiPoints = checked(currentChiPoints + (int)mutation.CreditAmount);
            updatedCharacter = WithCharacterAttribute(
                session.Character,
                "ChiPoints",
                checked((ulong)updatedChiPoints));
            persistenceValues["ChiPoints"] = updatedChiPoints.ToString(CultureInfo.InvariantCulture);
        }

        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        if (!TryPersistLegacyCpExchangeAdminMutation(
                session,
                updatedSession,
                context.PlayerItemsRoot,
                persistenceValues,
                persistPlayerItems: true,
                out var characterPath,
                out var playerItemsPath,
                out var persistenceReason))
        {
            await SendLegacyCpExchangeAdminUnavailableAsync(
                    context,
                    sourceMethod,
                    optionId,
                    persistenceReason,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        context.Sessions!.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        if (mutation.Kind == LegacyCpExchangeMutationKind.ItemToConquerPoints)
        {
            await SendLegacyCpExchangeAdminUpdateAsync(
                    context,
                    updatedCharacter,
                    LegacyMsgUpdateDataType.ConquerPoints,
                    checked((ulong)updatedCharacter.ConquerPoints),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyCpExchangeSuccessMessageAsync(context, mutation.SuccessMessage, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc CpExchangeAdmin item mutation " +
                $"{FormatSession(updatedContext)} source={sourceMethod} optionId={optionId} " +
                $"itemId={mutation.ItemId} itemCount=1 " +
                $"reward={mutation.CreditAmount} kind={mutation.Kind} " +
                $"characterPath={Path.GetFileName(characterPath)} " +
                $"playerItemsPath={Path.GetFileName(playerItemsPath)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryApplyLegacyCpExchangeCurrencyMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        LegacyCpExchangeMutation mutation,
        CancellationToken cancellationToken)
    {
        CharacterSnapshot updatedCharacter;
        if (mutation.Kind == LegacyCpExchangeMutationKind.ConquerPointsToMoney)
        {
            if (session.Character.ConquerPoints < mutation.DebitAmount)
            {
                await SendLegacyCpExchangeInsufficientDialogAsync(
                        context,
                        sourceMethod,
                        mutation,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (session.Character.Money < 0 ||
                session.Character.Money > LegacyCpExchangeMoneyInventoryCap - mutation.CreditAmount)
            {
                await SendLegacyCpExchangeAdminUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "warehouseMoneyOverflowSetterBranchNotAudited",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var updatedConquerPoints = checked(session.Character.ConquerPoints - mutation.DebitAmount);
            var updatedMoney = checked(session.Character.Money + mutation.CreditAmount);
            updatedCharacter = WithLegacyCpExchangeBalances(
                session.Character,
                updatedConquerPoints,
                updatedMoney);
        }
        else
        {
            if (session.Character.Money < mutation.DebitAmount)
            {
                await SendLegacyCpExchangeInsufficientDialogAsync(
                        context,
                        sourceMethod,
                        mutation,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (session.Character.ConquerPoints < 0 ||
                session.Character.ConquerPoints >
                LegacyCpExchangeConquerPointsInventoryCap - mutation.CreditAmount)
            {
                await SendLegacyCpExchangeAdminUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "conquerPointsOverflowSetterBranchNotAudited",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var updatedMoney = checked(session.Character.Money - mutation.DebitAmount);
            var updatedConquerPoints = checked(session.Character.ConquerPoints + mutation.CreditAmount);
            updatedCharacter = WithLegacyCpExchangeBalances(
                session.Character,
                updatedConquerPoints,
                updatedMoney);
        }

        var updatedSession = session with { Character = updatedCharacter };
        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedCharacter.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["Money"] = updatedCharacter.Money.ToString(CultureInfo.InvariantCulture),
        };
        if (!TryPersistLegacyCpExchangeAdminMutation(
                session,
                updatedSession,
                playerItemsRoot: null,
                persistenceValues,
                persistPlayerItems: false,
                out var characterPath,
                out _,
                out var persistenceReason))
        {
            await SendLegacyCpExchangeAdminUnavailableAsync(
                    context,
                    sourceMethod,
                    optionId,
                    persistenceReason,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        context.Sessions!.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyCpExchangeCurrencyUpdatesAsync(
                context,
                sourceMethod,
                updatedCharacter,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCpExchangeSuccessMessageAsync(context, mutation.SuccessMessage, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc CpExchangeAdmin currency mutation " +
                $"{FormatSession(updatedContext)} source={sourceMethod} optionId={optionId} " +
                $"debit={mutation.DebitAmount} credit={mutation.CreditAmount} kind={mutation.Kind} " +
                $"conquerPoints={updatedCharacter.ConquerPoints} money={updatedCharacter.Money} " +
                $"characterPath={Path.GetFileName(characterPath)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static CharacterSnapshot WithLegacyCpExchangeBalances(
        CharacterSnapshot character,
        long conquerPoints,
        long money)
    {
        var updated = character with
        {
            ConquerPoints = conquerPoints,
            Money = money,
        };
        updated = WithCharacterAttribute(updated, "ConquerPoints", checked((ulong)conquerPoints));
        return WithCharacterAttribute(updated, "Money", checked((ulong)money));
    }

    private static bool TryGetLegacyCpExchangeMutation(
        string sourceMethod,
        byte optionId,
        out LegacyCpExchangeMutation mutation)
    {
        mutation = (sourceMethod, optionId) switch
        {
            (LegacyMarketCpAdminSource or LegacyJailCpAdminSource, 1) => new(
                LegacyCpExchangeMutationKind.ItemToConquerPoints,
                LegacyCpExchangeDragonBallScrollItemId,
                DebitAmount: 1,
                CreditAmount: 2_150,
                "Where~is~your~DBScroll?",
                "Er,~sorry.~I'm not have DBScroll.",
                "Congratulations!You~have~got~2150~CPs~in~your~inventory"),
            (LegacyMarketCpAdminSource or LegacyJailCpAdminSource, 2) => new(
                LegacyCpExchangeMutationKind.ItemToConquerPoints,
                LegacyCpExchangeDragonBallItemId,
                DebitAmount: 1,
                CreditAmount: 215,
                "Where~is~your~DragonBall?",
                "Er,~sorry.~I'm not have Dragon Ball.",
                "Congratulations!You~have~got~215~CPs~in~your~inventory"),
            (LegacyMarketCpAdminSource, 9) => new(
                LegacyCpExchangeMutationKind.ItemToChiPoints,
                LegacyCpExchangeDragonBallItemId,
                DebitAmount: 1,
                CreditAmount: 800,
                "Where~is~your~DBScroll?",
                "Er,~sorry.~I'm not have DBScroll.",
                "Congratulations!You~have~got~800~ChiPoints."),
            (LegacyMarketCpAdminSource, 10) => new(
                LegacyCpExchangeMutationKind.ItemToChiPoints,
                LegacyCpExchangeDragonBallScrollItemId,
                DebitAmount: 1,
                CreditAmount: 8_000,
                "Where~is~your~DBScroll?",
                "Er,~sorry.~I'm not have DBScroll.",
                "Congratulations!You~have~got~8000~ChiPoints."),
            (LegacyMarketCpAdminSource, 4) => new(
                LegacyCpExchangeMutationKind.ConquerPointsToMoney,
                ItemId: 0,
                DebitAmount: 1_000_000,
                CreditAmount: 100_000_000,
                "You not have 1m cps with you.",
                "I~see",
                "Congratulations!You~have~got~100m~Money~in~your~inventory"),
            (LegacyMarketCpAdminSource, 5) => new(
                LegacyCpExchangeMutationKind.ConquerPointsToMoney,
                ItemId: 0,
                DebitAmount: 100_000_000,
                CreditAmount: 1_000_000_000,
                "You not have 100m cps with you.",
                "I~see",
                "Congratulations!You~have~got~1kkk~Money~in~your~inventory"),
            (LegacyJailCpAdminSource, 4) => new(
                LegacyCpExchangeMutationKind.MoneyToConquerPoints,
                ItemId: 0,
                DebitAmount: 10_000_000,
                CreditAmount: 100,
                "You do not have 10.000.000 Money with you.",
                "I~see",
                "Congratulations!You~have~got~100~CPs~in~your~inventory"),
            (LegacyJailCpAdminSource, 5) => new(
                LegacyCpExchangeMutationKind.MoneyToConquerPoints,
                ItemId: 0,
                DebitAmount: 100_000_000,
                CreditAmount: 1_000,
                "You do not have 100.000.000 Money with you.",
                "I~see",
                "Congratulations!You~have~got~1000~CPs~in~your~inventory"),
            _ => null!,
        };
        return mutation is not null;
    }
}
