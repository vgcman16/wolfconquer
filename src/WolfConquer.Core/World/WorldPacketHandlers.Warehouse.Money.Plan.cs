using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyWarehouseMoneyPlan(WorldSession Updated, long Amount, bool Deposit, byte[][] Frames,
        WorldSession PersistenceBefore, LegacyWarehouseMoneyState.NativeValueProof? Inputs);

    private static bool TryPlanWarehouseMoneyMutation(WorldSession current, MsgItemUsagePacket packet,
        out LegacyWarehouseMoneyPlan plan, out string reason)
    {
        plan = null!; reason = "warehouseMoneyAmountOrStateInvalid";
        if (packet.DwParam is 0 or > long.MaxValue || current.Character.Money < 0) return false;
        var amount = checked((long)packet.DwParam);
        var deposit = packet.Action == MsgItemUsageAction.DepositWarehouse;
        if (!deposit && packet.Action != MsgItemUsageAction.WarehouseWithdraw) return false;
        LegacyWarehouseMoneyState.NativeValueProof? inputs = null;
        if (current.NativeGameClient is not null || !string.IsNullOrWhiteSpace(current.UsersRoot))
        {
            try { inputs = new(current.Character); }
            catch (InvalidOperationException) { return false; }
        }
        // Capture caller-map input before Store preparation. The real writer
        // receives only these prepared BCL maps, never a fresh wrapper callback.
        if (!LegacyWarehouseMoneyState.TryCopyPersistenceFields(inputs?.CapturedFields ?? current.Character.Attributes,
                out var savedFields))
        { reason = "warehouseMoneyCharacterFieldsAmbiguous"; return false; }
        if (!LegacyWarehouseMoneyState.TryRead(savedFields, out var warehouse)) return false;
        var persistenceBefore = current with { Character = current.Character with { Attributes = savedFields } };
        if (deposit ? current.Character.Money <= amount : warehouse < amount)
        { reason = "warehouseMoneyFundsInsufficient"; return false; }

        // Both physical wallets are signed-64, but their aggregate need not be.
        // Check the exact final conserved result after the established cap;
        // never overflow/reject solely because an intermediate sum is wider.
        var money = deposit ? (decimal)current.Character.Money - amount :
            Math.Min((decimal)current.Character.Money + amount, LegacyMaxInventoryMoney);
        var bank = (decimal)current.Character.Money + warehouse - money;
        if (bank is < 0 or > long.MaxValue)
        { reason = "warehouseMoneyCapacityInvalid"; return false; }
        var attributes = new Dictionary<string, string>(savedFields, StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = money.ToString(CultureInfo.InvariantCulture),
            ["WHMoney"] = bank.ToString(CultureInfo.InvariantCulture),
        };
        if (attributes.ContainsKey("WarehouseMoney")) attributes["WarehouseMoney"] = attributes["WHMoney"];
        var updated = current with { Character = current.Character with { Money = checked((long)money), Attributes = attributes } };
        var moneyFrame = new LegacyMsgUpdatePacket(checked((uint)current.Character.Uid),
            [new(LegacyMsgUpdateDataType.Money, checked((ulong)money))]).ToFrameBytes();
        // Pre-serialize the original successful sequence before the real WAL.
        byte[][] frames = deposit ? [moneyFrame, moneyFrame, packet.ToFrameBytes()] :
            [moneyFrame, moneyFrame, new LegacyMsgUpdatePacket(checked((uint)current.Character.Uid),
                [new(LegacyMsgUpdateDataType.WarehouseMoney, checked((ulong)bank))]).ToFrameBytes(), packet.ToFrameBytes()];
        plan = new(updated, amount, deposit, frames, persistenceBefore, inputs); reason = string.Empty; return true;
    }
}
