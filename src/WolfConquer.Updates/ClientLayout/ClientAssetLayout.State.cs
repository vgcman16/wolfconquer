using WolfConquer.Updates.Install;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.ClientLayout;

public static partial class ClientAssetLayout
{
    private static void RequireCommittedRelease(InstallSession session, VerifiedUpdateManifest verified)
    {
        var state = session.State() ?? throw new InvalidDataException("DX8 layout requires a committed signed installation.");
        var manifest = verified.Manifest;
        if (state.ProductId != manifest.ProductId || state.Channel != manifest.Channel || state.Sequence != manifest.Sequence ||
            !InstallFileSystem.Same(state.PayloadSha256, verified.PayloadSha256) ||
            !InstallFileSystem.Same(state.EnvelopeSha256, verified.EnvelopeSha256) || state.EnvelopeLength != verified.EnvelopeLength ||
            state.Files.Count != manifest.Files.Count)
            throw new InvalidDataException("DX8 layout release does not match the exact committed installation.");
        var files = state.Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        foreach (var file in manifest.Files)
            if (!files.TryGetValue(file.Path, out var installed) || file.Length != installed.Length ||
                !InstallFileSystem.Same(file.Sha256, installed.Sha256))
                throw new InvalidDataException("DX8 layout signed file map differs from the committed installation.");
        // This is authenticated release selection plus state-map equality, not a
        // rehash of all payload bytes. Launch verification/explicit repair are separate.
    }

    private static void RequireStateUnchanged(InstallSession session)
    {
        if (session.InitialStateHash is null ||
            !InstallFileSystem.Same(session.Fs.Hash(session.Fs.StatePath), session.InitialStateHash))
            throw new InvalidDataException("Committed installation state changed during DX8 layout preparation.");
    }
}
