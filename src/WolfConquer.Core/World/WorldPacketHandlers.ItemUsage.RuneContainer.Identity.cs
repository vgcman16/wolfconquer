namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool RuneContainerOperationsEqual(LegacyRuneContainerOperation? first, LegacyRuneContainerOperation? second) =>
        first is null ? second is null : second is not null && first.Kind == second.Kind && first.SelectedUid == second.SelectedUid &&
            first.AlternateEquipmentActive == second.AlternateEquipmentActive && first.Metadata.SequenceEqual(second.Metadata) &&
            (first.Transform is null ? second.Transform is null : first.Transform.SameAs(second.Transform));
}
