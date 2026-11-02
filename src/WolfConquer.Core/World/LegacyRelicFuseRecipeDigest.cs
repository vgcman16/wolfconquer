using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static class LegacyRelicFuseRecipeDigest
{
    private sealed record Recipe(int Version, Guid Domain, uint AccountId, uint CharacterUid,
        uint FirstUid, uint SecondUid, byte Bound, ushort Durability, uint[] Words,
        byte[] CharacterBefore, byte[] ItemsBefore, byte[]? SpellsBefore);

    // Data digest only; no allocation, journal or write capability.
    internal static string Compute(Guid domain, uint accountId, uint characterUid,
        uint firstUid, uint secondUid, byte bound, ushort durability, uint[] words,
        byte[] characterBefore, byte[] itemsBefore, byte[]? spellsBefore)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Recipe(1, domain, accountId, characterUid,
            firstUid, secondUid, bound, durability, words, characterBefore, itemsBefore, spellsBefore));
        if (bytes.Length > 192 * 1024 * 1024) throw new InvalidDataException("relicFuseRecipeTooLarge");
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
