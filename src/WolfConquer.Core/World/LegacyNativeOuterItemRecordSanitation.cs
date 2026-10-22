namespace WolfConquer.Core.World;

// Literal ServerDatabase.LoadClientItems1176..1220, OUTER record only.
// Caller has already performed the Demon/205901 branches. Never apply to child records.
internal static class LegacyNativeOuterItemRecordSanitation
{
    internal static void SanitizeBeforeConversion(ref LegacyNativeClientItemRecord record)
    {
        if (record.ITEM_ID / 1000 == 203)
        {
            if (record.Enchant > 0)
            {
                record.Enchant = 0;
            }
            if (record.Bless > 1)
            {
                record.Bless = 0;
            }
        }
        if (record.Bless > 7)
        {
            record.Bless = 7;
        }
        if (record.Plus > 12)
        {
            record.Plus = 12;
        }
        if (record.Enchant > byte.MaxValue)
        {
            record.Enchant = byte.MaxValue;
        }
        if (record.ITEM_ID / 1000 == 201 || record.ITEM_ID / 1000 == 202 || record.ITEM_ID / 1000 == 203)
        {
            if (record.Bless > 1)
            {
                record.Bless = 1;
            }
            if (record.Enchant > 0)
            {
                record.Enchant = 0;
            }
        }
        if (record.ITEM_ID / 1000 == 204)
        {
            if (record.Bless > 0)
            {
                record.Bless = 0;
            }
            if (record.Enchant > 0)
            {
                record.Enchant = 0;
            }
        }
    }
}
