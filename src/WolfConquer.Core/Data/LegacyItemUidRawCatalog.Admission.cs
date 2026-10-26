namespace WolfConquer.Core.Data;

internal static partial class LegacyItemUidRawCatalog
{
    // Reuse the actual complete physical-row/deposit/perfection parser. This
    // single saved image check does not allocate UIDs or scan/bootstrap a world.
    internal static void ValidateAdmissionPlayerItems(byte[] image) => ReadPlayerItems(image, new Scan());
}
