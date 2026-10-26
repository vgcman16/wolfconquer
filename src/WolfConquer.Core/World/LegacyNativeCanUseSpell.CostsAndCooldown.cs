using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCanUseSpell
{
    private static bool TryPrepareResources(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeMagicType.Magic row,
        out int staminaCost, out int manaCost)
    {
        staminaCost = manaCost = 0;
        if (player.Map == 1039)
            return true;
        staminaCost = row.UseStamina +
            (player.ContainFlag(LegacyNativeMsgUpdate.Flags.ScurvyBomb) ? 6 : 0);
        if (staminaCost > player.Stamina)
            return false;

        manaCost = row.UseMana;
        if (manaCost == 0)
            return true; // Optional Rune/catalog state is unrelated to a zero-mana action.
        if (client.Rune is null)
            return false;
        byte level = 0;
        if (client.Rune.IsEquipped("MPMaster", ref level))
        {
            int percent = level switch
            {
                2 => 15, 3 => 25, 7 => 75, 8 => 85, 9 => 90,
                _ => level * 10
            };
            percent = Math.Clamp(percent, 0, 100);
            // Maximum product65535*100 is safe in Int32. Ordinary positive
            // truncation is retained without negative costs or overflow refunds.
            manaCost -= manaCost * percent / 100;
        }
        return manaCost <= player.Mana;
    }

    private static bool TryPrepareCooldown(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeMsgSpell learned,
        LegacyNativeMagicType.Magic row, out bool startCooldown, out DateTime deadline,
        out LegacyNativeNinja.EquippedSigilCapture? sigilCapture)
    {
        sigilCapture = null;
        deadline = learned.ColdTime;
        startCooldown = row.IsSpellWithColdTime && row.ID != 15710 && row.ID != 20280 &&
            !player.ContainFlag(LegacyNativeMsgUpdate.Flags.CannonBarrage) &&
            !player.ContainFlag(LegacyNativeMsgUpdate.Flags.BlackbeardsRage);
        if (!startCooldown)
            return true;
        DateTime now = DateTime.Now;
        if (learned.ColdTime > now || !TryGetSigilReduction(client, learned.ID, row.ColdTime, out int reduction, out sigilCapture))
            return false;
        deadline = now.AddMilliseconds(row.ColdTime - reduction);
        return true;
    }

    private static bool TryGetSigilReduction(LegacyNativeGameClientRuntime client,
        ushort spellId, int cooldown, out int reduction, out LegacyNativeNinja.EquippedSigilCapture? sigilCapture)
    {
        reduction = 0;
        sigilCapture = null;
        LegacyNativeNinja.ItemType? relevant = spellId switch
        {
            16520 => LegacyNativeNinja.ItemType.WildSigilRapid,
            16540 => LegacyNativeNinja.ItemType.FlameSigilScorch,
            16460 => LegacyNativeNinja.ItemType.DustSigilExtinction,
            16500 => LegacyNativeNinja.ItemType.SickleSigilGallop,
            _ => null
        };
        if (!relevant.HasValue)
            return true;
        if (client.MyNinja is null)
            return false;
        sigilCapture = client.MyNinja.CaptureEquippedSigil(client, relevant.Value);
        if (sigilCapture.Found)
            reduction = Math.Clamp(sigilCapture.Power, 0, cooldown);
        return true;
    }
}
