namespace WolfConquer.Core.World;

// Immutable evidence of the last cooperating durable Spells image, independent
// of the current in-memory spell snapshot. Null means genuinely absent.
internal sealed class LegacySavedSpellImage
{
    private readonly byte[]? bytes;
    internal LegacySavedSpellImage(byte[]? image) => bytes = image?.ToArray();
    internal byte[]? Copy() => bytes?.ToArray();
    internal bool Matches(byte[]? image) => bytes is null ? image is null :
        image is not null && bytes.AsSpan().SequenceEqual(image);
}
