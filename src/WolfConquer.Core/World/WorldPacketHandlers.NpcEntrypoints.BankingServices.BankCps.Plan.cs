using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyBankCpsTransferPlan(WorldSession Updated, WorldSession PersistenceBefore,
        LegacyWarehouseMoneyState.NativeValueProof Inputs, uint Amount, bool Deposit, byte[][] Frames);

    private static byte[][] BankCpsDialogFrames(string text, byte avatar, params LegacyNpcDialogOption[] options) =>
        [LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(),
            .. options.Select(option => LegacyNpcReplyPacket.ForOption(option.Text, option.OptionId).ToFrameBytes()),
            LegacyNpcReplyPacket.ForAvatar(avatar).ToFrameBytes(), LegacyNpcReplyPacket.ForFinish().ToFrameBytes()];

    private static byte[][] BankCpsFundsFrames(bool deposit) => BankCpsDialogFrames(deposit ?
        "You do not have that many CPs with you." : "You do not have that many CPs in the bank.", 48,
        new LegacyNpcDialogOption("I see.", byte.MaxValue));

    private static bool TryPrepareBankCpsTransfer(WorldSession current, bool deposit,
        out LegacyBankCpsTransferPlan plan, out string reason, out byte[][] rejection)
    {
        plan = null!; reason = "bankCpsConfirmationInvalid"; rejection = [];
        if (current.PendingBankConquerPoints is not { } amount || amount == 0 ||
            current.PendingBankConquerPointsIsDeposit != deposit)
        {
            rejection = BankCpsDialogFrames("Please enter the amount again before confirming.", 48,
                new LegacyNpcDialogOption("I see.", byte.MaxValue));
            return false;
        }
        var character = current.Character;
        if (character.ConquerPoints < 0 || character.BankConquerPoints < 0)
        { reason = "bankCpsWalletInvalid"; return false; }
        if ((deposit ? character.ConquerPoints : character.BankConquerPoints) < amount)
        { reason = "bankCpsFundsChanged"; rejection = BankCpsFundsFrames(deposit); return false; }
        long cps, bank;
        try
        {
            cps = checked(character.ConquerPoints + (deposit ? -(long)amount : amount));
            bank = checked(character.BankConquerPoints + (deposit ? amount : -(long)amount));
        }
        catch (OverflowException) { reason = "bankCpsCapacityInvalid"; return false; }
        // Capture caller-map callbacks before actual Store preparation. The
        // writer uses only these prepared BCL maps; source rows stay witnessed.
        LegacyWarehouseMoneyState.NativeValueProof inputs;
        try { inputs = new(character); }
        catch (InvalidOperationException) { reason = "bankCpsCharacterFieldsInvalid"; return false; }
        if (!LegacyWarehouseMoneyState.TryCopyPersistenceFields(inputs.CapturedFields, out var beforeFields))
        { reason = "bankCpsCharacterFieldsAmbiguous"; return false; }
        var persistenceBefore = current with { Character = character with { Attributes = beforeFields } };
        var attributes = new Dictionary<string, string>(beforeFields, StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = cps.ToString(CultureInfo.InvariantCulture),
            ["BankCps"] = bank.ToString(CultureInfo.InvariantCulture),
        };
        var updated = current with { Character = character with { ConquerPoints = cps, BankConquerPoints = bank, Attributes = attributes },
            PendingBankConquerPoints = null, PendingBankConquerPointsIsDeposit = false };
        if (current.NativeGameClient is not null)
        {
            try { LegacyNativePlayerRuntime.OwnedCpsPublication.RequireRepresentable(updated.Character); }
            catch (InvalidOperationException) { reason = "bankCpsNativeRepresentationUnavailable"; return false; }
        }
        var dialog = BankCpsDialogFrames("Done. You now have " + cps.ToString(CultureInfo.InvariantCulture) +
            " CPs with you and " + bank.ToString(CultureInfo.InvariantCulture) + " CPs in the bank.", 183,
            new LegacyNpcDialogOption("Thanks.", byte.MaxValue));
        var update = new LegacyMsgUpdatePacket(checked((uint)character.Uid),
            [new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)cps))]).ToFrameBytes();
        plan = new(updated, persistenceBefore, inputs, amount, deposit, [update, .. dialog]); reason = string.Empty; return true;
    }
}
