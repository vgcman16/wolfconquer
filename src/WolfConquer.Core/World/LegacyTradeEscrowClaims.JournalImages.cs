using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeEscrowClaims
{
    internal sealed record JournalWallet(long Uid, long Money, long ConquerPoints, LegacyTradeEscrowClaim? Claim);

    // Reuse the canonical wallet/claim parser rather than the permissive loader
    // when complete captured WAL images become startup recovery authority.
    internal static JournalWallet ReadJournalWallet(byte[] image)
    {
        var text = CharacterSnapshotFileEncoding.Decode(image, out _);
        var lines = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line) lines.Add(line);
        if (!TryParseDisk(lines, out var wallet, out var reason))
            throw new InvalidDataException("Captured trade wallet is invalid: " + reason);
        return new(wallet.Uid, wallet.Money, wallet.ConquerPoints, wallet.Claim);
    }
}
