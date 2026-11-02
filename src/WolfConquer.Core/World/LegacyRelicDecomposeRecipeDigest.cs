using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static class LegacyRelicDecomposeRecipeDigest
{
    private sealed record Recipe(int Version, Guid Domain, uint AccountId, uint CharacterUid,
        byte[] OriginalFrame, LegacyRelicDecomposeSelection[] Selections, ushort Durability,
        byte[] CharacterBefore, byte[] ItemsBefore, byte[]? SpellsBefore);

    // Immutable data digest; it grants no reservation or journal authority.
    internal static string Compute(Guid domain, uint account, uint character, byte[] originalFrame,
        IReadOnlyList<LegacyRelicDecomposeSelection> selections, ushort durability,
        byte[] characterBefore, byte[] itemsBefore, byte[]? spellsBefore)
    {
        ArgumentNullException.ThrowIfNull(originalFrame); ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(characterBefore); ArgumentNullException.ThrowIfNull(itemsBefore);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Recipe(1, domain, account, character,
            originalFrame.ToArray(), selections.ToArray(), durability, characterBefore, itemsBefore, spellsBefore));
        if (bytes.Length > 192 * 1024 * 1024) throw new InvalidDataException("relicDecomposeRecipeTooLarge");
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
