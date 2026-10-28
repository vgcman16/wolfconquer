using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Byte preparation only; the caller separately owns its real Store/recovery transaction.
    private static byte[] PrepareLegacySpellMutationImage(PlayerSpellSnapshotEntry[] expected,
        PlayerSpellSnapshot after, byte[]? saved)
    {
        var frozen = new LegacyPerformerSpellSource(after.Spells);
        var result = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(new LegacyPerformerSpellRows(frozen.Rows)));
        var originalById = expected.ToDictionary(row => row.Id);
        foreach (var row in frozen.Rows)
            if (!originalById.TryGetValue(row.Id, out var prior) || prior != row)
                _ = LegacySpellPacket.FromSnapshot(row).ToFrameBytes();
        if (saved is null) return result;
        var parsed = PlayerSpellSnapshotLoader.Parse(saved);
        if (parsed.Spells.Count != expected.Length || !parsed.Spells.SequenceEqual(expected))
            throw new LegacySpellMutationRefusalException("savedSpellsChanged");
        var offsets = expected.Select((row, index) => (row.Id, Offset: sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize))
            .ToDictionary(row => row.Id, row => row.Offset);
        for (var index = 0; index < frozen.Rows.Length; index++)
            if (offsets.TryGetValue(frozen.Rows[index].Id, out var original))
                result[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11] = saved[original + 11];
        return result;
    }

    private static byte[]? ReadLegacySpellMutationFile(string path)
    {
        try
        {
            if (File.Exists(path) && !CharacterSnapshotFileBaseline.TryValidateSafePath(path, "spellMutation", out var reason))
                throw new LegacySpellMutationRefusalException(reason);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw InvalidSpellMutation();
            var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || stream.Length != bytes.Length) throw InvalidSpellMutation();
            return bytes;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static bool SpellMutationImagesEqual(byte[]? first, byte[]? second) =>
        first is null ? second is null : second is not null && first.AsSpan().SequenceEqual(second);
}
