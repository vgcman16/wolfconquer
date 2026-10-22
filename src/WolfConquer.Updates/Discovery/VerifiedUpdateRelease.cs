using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Discovery;

/// <summary>Binds a small signed discovery index to the exact full signed envelope.</summary>
public sealed class VerifiedUpdateRelease
{
    public VerifiedUpdateManifest FullManifest { get; }
    public VerifiedUpdateManifest IndexManifest { get; }
    public Uri ContentBaseUrl => IndexManifest.Manifest.ContentBaseUrl;

    private VerifiedUpdateRelease(VerifiedUpdateManifest index, VerifiedUpdateManifest full)
    {
        IndexManifest = index;
        FullManifest = full;
    }

    public static VerifiedUpdateRelease Bind(VerifiedUpdateManifest index, VerifiedUpdateManifest full)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(full);
        var discovery = index.Manifest;
        var release = full.Manifest;
        ValidateIndex(index);
        var entry = discovery.Files[0];
        if (discovery.ProductId != release.ProductId + ".index" || discovery.Channel != release.Channel ||
            discovery.Sequence != release.Sequence || index.KeyId != full.KeyId || index.SignerKeySha256 != full.SignerKeySha256 ||
            entry.Length != full.EnvelopeLength ||
            !entry.Sha256.Equals(full.EnvelopeSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Signed discovery does not identify this exact release envelope.");
        return new VerifiedUpdateRelease(index, full);
    }

    public static void ValidateIndex(VerifiedUpdateManifest index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var manifest = index.Manifest;
        if (!manifest.ProductId.EndsWith(".index", StringComparison.Ordinal) || manifest.Bundle is not null ||
            manifest.Files.Count != 1 || manifest.Files[0].Path != "manifest.json" ||
            manifest.Files[0].BaseSha256 is not null || manifest.Files[0].Length is < 1 or > SignedUpdateManifest.MaximumEnvelopeBytes)
            throw new InvalidDataException("Signed discovery index has an invalid shape.");
    }
}
