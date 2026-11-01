namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    internal sealed partial class CompletedMaximums
    {
        // DATA is compared with this real completed query; matching values
        // alone cannot mint, refresh or replace the private completion token.
        internal bool MatchesReceiveData(LegacyOrdinarySpellBinding binding, WorldSession current,
            LegacyNativeMagicQueryFields fields, LegacyNativeMagicQueryFields jiangHu,
            LegacyNativePerfectionSnapshot perfection, uint prestige, uint maximumHitpoints, uint maximumMana) =>
            IsCurrentPlain(binding, current) && MaximumCompletions.TryGetValue(this, out var completed) &&
            completed.Fields == fields && completed.JiangHu == jiangHu &&
            completed.Perfection == perfection && completed.Prestige == prestige &&
            current.EquipmentHitpointsStatus?.MaximumHitpoints == maximumHitpoints &&
            current.PhysicalStatusOverlay?.MaxMana == maximumMana;
    }
}
