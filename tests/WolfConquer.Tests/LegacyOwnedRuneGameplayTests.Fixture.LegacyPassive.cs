using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class LegacyOwnedRuneGameplayFixture
{
    private void PrepareLegacyPassiveSavedInput(string mode)
    {
        if (mode is not ("owned" or "unmarked" or "permanent" or "changed" or "opaque" or "unmatched" or "stored"))
            throw new ArgumentException("Unknown declared legacy passive saved input.");
        // SYNTHETIC LEGACY SAVED INPUT, before bootstrap/admission. This declares
        // trusted stored provenance for migration testing. It does not assert
        // that the former producer ran, mint a current origin or fabricate WAL.
        var items = PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(ItemsPath));
        var changed = items with { RuneItems = items.RuneItems.Select(row => row.Uid == First
            ? row.WithPosition(mode == "stored" ? (ushort)212 : (ushort)103) : row).ToArray() };
        File.WriteAllBytes(ItemsPath, PlayerItemSnapshotLoader.ToLegacyBytes(changed));
        byte level = mode == "unmatched" ? (byte)3 : (byte)2;
        PlayerSpellSnapshotEntry oldRow = mode == "permanent" ? new(40301, level, 1234, 2, 3, 1)
            : new(40301, level, mode == "changed" ? 1 : 0, 0, 0, 0);
        var extra = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([oldRow]));
        var image = OriginalSpells.Concat(extra.Skip(sizeof(int))).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(image, BinaryPrimitives.ReadInt32LittleEndian(OriginalSpells) + 1);
        if (mode == "opaque") image[^1] = 0xC7;
        File.WriteAllBytes(SpellsPath, image);
        if (mode is "unmarked" or "permanent") return;
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out var encoding);
        Assert.DoesNotContain(Marker, text, StringComparison.OrdinalIgnoreCase);
        const string unrelated = "[Unrelated]\r\nSentinel=retain\r\n";
        int boundary = text.IndexOf(unrelated, StringComparison.Ordinal);
        Assert.True(boundary >= 0); Assert.Equal(boundary, text.LastIndexOf(unrelated, StringComparison.Ordinal));
        var value = "40301:" + level.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var result = text.Insert(boundary, Marker + "=" + value + "\r\n");
        Assert.Equal(value, CharacterSnapshotParser.ParseText(result).Attributes[Marker]);
        Assert.Contains(unrelated, result, StringComparison.Ordinal);
        File.WriteAllBytes(CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(result)).ToArray());
    }
}
