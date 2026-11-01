namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    internal readonly record struct RecordedReceiveValues(LegacyNativeMagicQueryFields Magic,
        LegacyNativeMagicQueryFields JiangHu, LegacyNativePerfectionSnapshot Perfection, uint Prestige,
        uint MaximumHitpoints, uint MaximumMana);

    // Read-only capture of an actual completed query. This neither starts a
    // query nor grants the later scalar award/death publication.
    internal static bool TryCaptureRecordedReceive(LegacyOrdinarySpellBinding binding, WorldSession current,
        out CompletedMaximums? token, out RecordedReceiveValues values)
    {
        token = null; values = default;
        if (!Entries.TryGetValue(binding.NativeIdentity, out var entry) ||
            !CurrentMaximumCompletions.TryGetValue(entry, out var actual) || !actual.IsCurrentPlain(binding, current) ||
            !MaximumCompletions.TryGetValue(actual, out var completion) ||
            current.EquipmentHitpointsStatus is not { } hp || current.PhysicalStatusOverlay?.MaxMana is not { } mana) return false;
        values = new(completion.Fields, completion.JiangHu, completion.Perfection, completion.Prestige, hp.MaximumHitpoints, mana);
        token = actual; return true;
    }
}
