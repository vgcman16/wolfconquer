using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Deterministic DATA only: neither these inputs nor this result authorize
    // a hit, persistence, native publication or packet delivery.
    internal sealed class BasicPhysicalProficiencyCalculation
    {
        internal PlayerProficiencySnapshot Before { get; }
        internal PlayerProficiencySnapshot After { get; }
        internal uint RatedExperience { get; }
        private readonly byte[][] frames;
        internal IReadOnlyList<ReadOnlyMemory<byte>> Frames => Array.AsReadOnly(frames.Select(frame => (ReadOnlyMemory<byte>)frame.ToArray()).ToArray());
        internal bool HasChanges => frames.Length != 0;

        internal BasicPhysicalProficiencyCalculation(PlayerProficiencySnapshot before, IReadOnlyList<uint> weaponIds, uint rawExperience)
        {
            ArgumentNullException.ThrowIfNull(before); ArgumentNullException.ThrowIfNull(weaponIds);
            Before = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(before);
            int count = weaponIds.Count;
            if (count is < 0 or > 2) throw new InvalidDataException("basicPhysicalProficiencyCalculationInvalid");
            var ids = new uint[count];
            for (int index = 0; index < count; index++) ids[index] = weaponIds[index];
            if (weaponIds.Count != count || ids.Any(id => id == 0 || !LegacyProficiencyPacket.IsLegacyWeaponProficiencyId(id)) ||
                ids.Distinct().Count() != ids.Length) throw new InvalidDataException("basicPhysicalProficiencyCalculationInvalid");
            RatedExperience = ApplyLegacyProficiencyExperienceRate(rawExperience);
            var rows = Before.Proficiencies.ToList(); var output = new List<byte[]>();
            if (rawExperience != 0 && RatedExperience != 0)
            {
                foreach (uint id in ids)
                {
                    int index = rows.FindIndex(row => row.Id == id);
                    if (index < 0)
                    {
                        var added = new PlayerProficiencySnapshotEntry(id, 0, 0, 0, LegacyProficiencyPacket.GetNeededExperience(0));
                        rows.Add(added); output.Add(LegacyProficiencyPacket.FromSnapshot(added).ToFrameBytes());
                    }
                    else
                    {
                        var result = LegacyProficiencyPacket.ApplyExperience(rows[index], RatedExperience);
                        if (!result.Updated) continue;
                        rows[index] = result.Proficiency; output.Add(result.Packet.ToFrameBytes());
                    }
                }
            }
            After = output.Count == 0 ? Before : LegacyNativeProficiencyStorage.FreezeSessionSnapshot(new PlayerProficiencySnapshot(rows));
            frames = output.ToArray();
        }
    }
}

// Projects complete bounded file DATA only; it is not a file writer or owner.
internal static class LegacyBasicPhysicalProficiencyImage
{
    internal static byte[] Project(byte[]? before, WorldPacketHandlers.BasicPhysicalProficiencyCalculation calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        if (before is { Length: > CharacterSnapshotFileBaseline.MaximumBytes }) throw Invalid();
        before = before?.ToArray();
        var old = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(before is null ? PlayerProficiencySnapshot.Empty : PlayerProficiencySnapshotLoader.Parse(before));
        if (!PlayerProficiencySnapshotLoader.ToLegacyBytes(old).AsSpan().SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(calculation.Before))) throw Invalid();
        var result = PlayerProficiencySnapshotLoader.ToLegacyBytes(calculation.After);
        if (result.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid();
        if (before is not null)
        {
            var offsets = old.Proficiencies.Select((row, index) => (row.Id, Offset: sizeof(int) + index * PlayerProficiencySnapshotLoader.LegacyProficiencySize))
                .ToDictionary(row => row.Id, row => row.Offset);
            for (int index = 0; index < calculation.After.Proficiencies.Count; index++)
                if (offsets.TryGetValue(calculation.After.Proficiencies[index].Id, out int prior))
                    before.AsSpan(prior + 9, 3).CopyTo(result.AsSpan(sizeof(int) + index * PlayerProficiencySnapshotLoader.LegacyProficiencySize + 9, 3));
        }
        var projected = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(PlayerProficiencySnapshotLoader.Parse(result));
        if (!PlayerProficiencySnapshotLoader.ToLegacyBytes(projected).AsSpan()
            .SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(calculation.After))) throw Invalid();
        return result;
    }

    private static InvalidDataException Invalid() => new("basicPhysicalProficiencyImageInvalid");
}
