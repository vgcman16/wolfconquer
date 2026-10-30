using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleWarehouseMoneyMutationAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        if (!TryGetLegacyMoneyAmount(packet.DwParam, out var amount))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage warehouse money mutation " +
                    $"{FormatSession(context)} action={packet.Action}; amount is outside legacy signed money range.")
                .ConfigureAwait(false);
            return;
        }

        var money = session.Character.Money;
        var warehouseMoney = ReadWarehouseMoney(session.Character);

        if (packet.Action == MsgItemUsageAction.DepositWarehouse)
        {
            if (money <= amount ||
                !TryAddWarehouseMoney(warehouseMoney, amount, out var updatedWarehouseMoney))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage warehouse deposit " +
                        $"{FormatSession(context)} amount={amount} money={money} warehouseMoney={warehouseMoney}.")
                    .ConfigureAwait(false);
                return;
            }

            var updatedMoney = money - amount;
            var updatedSession = UpdateSessionWallet(context, session, updatedMoney, updatedWarehouseMoney);
            await SendWalletMoneyUpdateAsync(context, updatedSession.Character, cancellationToken).ConfigureAwait(false);
            await SendWalletMoneyUpdateAsync(context, updatedSession.Character, cancellationToken).ConfigureAwait(false);
            await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgItemUsage warehouse deposit " +
                    $"{FormatSession(context)} amount={amount} money={updatedMoney} warehouseMoney={updatedWarehouseMoney}.")
                .ConfigureAwait(false);
            return;
        }

        if (warehouseMoney < (ulong)amount)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage warehouse withdraw " +
                    $"{FormatSession(context)} amount={amount} money={money} warehouseMoney={warehouseMoney}.")
                .ConfigureAwait(false);
            return;
        }

        if (money > long.MaxValue - amount)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage warehouse withdraw " +
                    $"{FormatSession(context)} amount={amount}; signed money range would overflow.")
                .ConfigureAwait(false);
            return;
        }

        var newMoney = money + amount;
        var newWarehouseMoney = warehouseMoney;
        if (newMoney > LegacyMaxInventoryMoney)
        {
            var overflow = (ulong)(newMoney - LegacyMaxInventoryMoney);
            newMoney = LegacyMaxInventoryMoney;
            if (newWarehouseMoney > ulong.MaxValue - overflow)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage warehouse withdraw " +
                        $"{FormatSession(context)} amount={amount}; warehouse overflow would exceed unsigned range.")
                    .ConfigureAwait(false);
                return;
            }

            newWarehouseMoney += overflow;
        }

        newWarehouseMoney -= (ulong)amount;
        var sessionAfterWithdraw = UpdateSessionWallet(context, session, newMoney, newWarehouseMoney);
        await SendWalletMoneyUpdateAsync(context, sessionAfterWithdraw.Character, cancellationToken).ConfigureAwait(false);
        await SendWalletMoneyUpdateAsync(context, sessionAfterWithdraw.Character, cancellationToken).ConfigureAwait(false);
        await SendWarehouseMoneyUpdateAsync(context, sessionAfterWithdraw.Character, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage warehouse withdraw " +
                $"{FormatSession(context)} amount={amount} money={newMoney} warehouseMoney={newWarehouseMoney}.")
            .ConfigureAwait(false);
    }

    private const long LegacyMaxInventoryMoney = 500_000_000_000L;

    private static ulong ReadWarehouseMoney(CharacterSnapshot character)
    {
        return ReadAttributeUInt64(character.Attributes, "WHMoney", "WarehouseMoney");
    }

    private static bool TryGetLegacyMoneyAmount(ulong value, out long amount)
    {
        if (value > long.MaxValue)
        {
            amount = 0;
            return false;
        }

        amount = (long)value;
        return true;
    }

    private static bool TryAddWarehouseMoney(ulong warehouseMoney, long amount, out ulong updatedWarehouseMoney)
    {
        var unsignedAmount = (ulong)amount;
        if (warehouseMoney > ulong.MaxValue - unsignedAmount)
        {
            updatedWarehouseMoney = 0;
            return false;
        }

        updatedWarehouseMoney = warehouseMoney + unsignedAmount;
        return true;
    }

    private static WorldSession UpdateSessionWallet(
            WorldPacketHandlerContext context,
            WorldSession session,
            long money,
            ulong warehouseMoney)
    {
        var attributes = new Dictionary<string, string>(
            session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["WHMoney"] = warehouseMoney.ToString(CultureInfo.InvariantCulture),
        };
        var character = session.Character with
        {
            Money = money,
            Attributes = attributes,
        };
        var updated = session with { Character = character };
        return PublishLegacyItemUsageSession(context, updated);
    }

    private static ValueTask SendWalletMoneyUpdateAsync(
            WorldPacketHandlerContext context,
            CharacterSnapshot character,
            CancellationToken cancellationToken)
    {
        return SendScalarUpdateAsync(
            context,
            character,
            LegacyMsgUpdateDataType.Money,
            checked((ulong)character.Money),
            cancellationToken);
    }

    private static ValueTask SendWarehouseMoneyUpdateAsync(
            WorldPacketHandlerContext context,
            CharacterSnapshot character,
            CancellationToken cancellationToken)
    {
        return SendScalarUpdateAsync(
            context,
            character,
            LegacyMsgUpdateDataType.WarehouseMoney,
            ReadWarehouseMoney(character),
            cancellationToken);
    }
}
