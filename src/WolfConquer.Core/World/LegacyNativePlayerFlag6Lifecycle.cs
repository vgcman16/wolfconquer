namespace WolfConquer.Core.World;

// Only the native TeamLeader/Flag6 slot. Other bits remain the caller's existing projection.
// Projection only: no mutation/expiry model, sends, awaits, locking or implicit birth.
public static class LegacyNativePlayerFlag6Lifecycle
{
    public const ulong Mask = 1UL << 6;

    // Only at actual publication capture. Unbound/manual sessions preserve compatibility bits.
    // For a bound physical Player, overwrite ONLY bit6; never synthesize a registration from attrs,
    // never expire on read, and never write Character/Player/registration state as a side effect.
    public static ulong[] CaptureVector(
        LegacyNativePlayerRuntime? currentPlayer, IReadOnlyList<ulong> compatibilityWords)
    {
        ArgumentNullException.ThrowIfNull(compatibilityWords);
        if (compatibilityWords.Count != LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)
            throw new ArgumentOutOfRangeException(nameof(compatibilityWords));
        var copy = compatibilityWords.ToArray();
        if (currentPlayer is not null)
            copy[0] = currentPlayer.BitVector.Contain(6) ? copy[0] | Mask : copy[0] & ~Mask;
        return copy;
    }
}
