using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    private PreparedPrestigeRebuild PreparePrestigeRebuild()
    {
        var owners = CapturePrestigeOwners();
        try
        {
            var includeClass = LegacyNativePrestigeRanking.TryGetIndex(owners.CharacterClass, out var category);
            if (!includeClass)
            {
                if (!LegacyNativePrestigeRanking.IsWorldOnlyClass(owners.CharacterClass))
                    throw new InvalidOperationException("Prestige preparation requires a supported character class.");
                category = LegacyNativePrestigeRanking.Type.World;
            }
            var points = PreparePrestigeComponents(owners);
            var total = PreparePrestigeEquipmentAndTotal(points, owners.EquipmentPoints);
            var entry = new LegacyNativePrestigeRanking.Entry
            {
                type = category,
                UID = owners.Uid,
                Name = owners.Player.Name,
                Class = owners.CharacterClass,
                TotalPoints = total,
                Level = checked((byte)owners.Player.Level),
                Mesh = owners.Player.Mesh,
                Points = (uint[])points.Clone(),
            };
            // The genuine method writes only this unpublished Entry. Its flag read
            // validates the real physical vector before copying any appearance.
            entry.AddInfo(this);
            ValidatePreparedPrestigeText(entry);
            var effects = LegacyNativePerfectionEffect.PrepareUpdate(checked((int)owners.PrestigeLevel), this);
            RequireCurrentPrestigeOwners(owners);
            return new PreparedPrestigeRebuild(owners, points, total, entry, effects,
                owners.ServerId == owners.PlayerServerId,
                includeClass);
        }
        catch (OverflowException error)
        {
            throw new InvalidOperationException("Prestige preparation exceeded a declared score or output width.", error);
        }
    }

    private static uint PreparePrestigeEquipmentAndTotal(uint[] points, uint[] equipmentPoints)
    {
        checked
        {
            foreach (var value in equipmentPoints) points[3] += value;
            uint total = 0;
            foreach (var value in points) total += value;
            foreach (var value in equipmentPoints)
            {
                total += value;
                points[3] += value;
            }
            return total;
        }
    }

    private sealed record PreparedPrestigeRebuild(
        PrestigeOwners Owners,
        uint[] Points,
        uint Total,
        LegacyNativePrestigeRanking.Entry Entry,
        LegacyNativePerfectionEffect Effects,
        bool PublishRanks,
        bool IncludeClass);
}
