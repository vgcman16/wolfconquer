using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static class LegacyRuneDecomposeRecipeDigest
{
    private sealed record Recipe(int Version, Guid Domain, uint AccountId, uint CharacterUid, uint SelectedUid,
        LegacyRuneDecomposeProgressRow Progress, LegacyRuneMetadata[] RuneMetadata,
        LegacyRuneDecomposeRewardMetadata[] RewardMetadata, byte? FragmentColor,
        byte[] CharacterBefore, byte[] ItemsBefore, byte[]? SpellsBefore);

    // Binds the reservation to complete saved inputs and the exact preflight
    // recipe. A digest is not gameplay, registry or publication authority.
    internal static string Compute(Guid domain, uint accountId, uint characterUid, uint selectedUid,
        LegacyRuneDecomposeProgressRow progress, LegacyRuneMetadata[] runeMetadata,
        LegacyRuneDecomposeRewardMetadata[] rewardMetadata, byte? fragmentColor,
        byte[] characterBefore, byte[] itemsBefore, byte[]? spellsBefore)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Recipe(1, domain, accountId, characterUid, selectedUid,
            progress, runeMetadata, rewardMetadata, fragmentColor, characterBefore, itemsBefore, spellsBefore));
        if (payload.Length > 192 * 1024 * 1024) throw new InvalidDataException("Rune decomposition recipe exceeds its bound.");
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }
}
