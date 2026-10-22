namespace WolfConquer.Updates.Manifest;

internal static class ManifestValidation
{
    internal static void Validate(UpdateManifest manifest, bool allowLoopbackHttp)
    {
        if (manifest.SchemaVersion != 1 || manifest.Sequence <= 0)
            throw new InvalidDataException("Unsupported schema or invalid update sequence.");
        Identifier(manifest.ProductId);
        Identifier(manifest.Channel);
        if (manifest.CreatedUtc.Offset != TimeSpan.Zero || manifest.ExpiresUtc.Offset != TimeSpan.Zero ||
            manifest.ExpiresUtc <= manifest.CreatedUtc)
            throw new InvalidDataException("Manifest timestamps must be UTC with expiry after creation.");
        Uri uri = manifest.ContentBaseUrl;
        if (uri is null || !uri.IsAbsoluteUri || string.IsNullOrEmpty(uri.Host) ||
            !(uri.Scheme == Uri.UriSchemeHttps || allowLoopbackHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.OriginalString.EndsWith("/", StringComparison.Ordinal) ||
            uri.OriginalString.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character == '\\'))
            throw new InvalidDataException("Content base must be a credential-free HTTPS directory URL.");
        if (manifest.Files.Count > SignedUpdateManifest.MaximumFiles)
            throw new InvalidDataException("Manifest has too many files.");
        if (manifest.Bundle is { } bundle)
        {
            if (bundle.Length < 0) throw new InvalidDataException("Bundle length is negative.");
            Hash(bundle.Sha256);
        }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (file is null) throw new InvalidDataException("Manifest contains a null file entry.");
            UpdatePathPolicy.ValidateRelativePath(file.Path);
            if (!paths.Add(file.Path)) throw new InvalidDataException("Manifest paths collide.");
            if (file.Length < 0) throw new InvalidDataException("File length is negative.");
            Hash(file.Sha256);
            if (file.BaseSha256 is not null) Hash(file.BaseSha256);
        }
        foreach (string path in paths)
        {
            for (int slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
                if (paths.Contains(path[..slash]))
                    throw new InvalidDataException("A manifest file path is also a parent directory.");
        }
    }

    internal static void ValidateContext(UpdateManifest manifest, UpdateVerificationContext context)
    {
        Identifier(context.ExpectedProductId);
        Identifier(context.ExpectedChannel);
        if (context.MinimumSequence <= 0 || context.NowUtc.Offset != TimeSpan.Zero ||
            context.AllowedClockSkew < TimeSpan.Zero || context.AllowedClockSkew > TimeSpan.FromMinutes(5))
            throw new InvalidDataException("Invalid trusted verification context.");
        if (!string.Equals(manifest.ProductId, context.ExpectedProductId, StringComparison.Ordinal) ||
            !string.Equals(manifest.Channel, context.ExpectedChannel, StringComparison.Ordinal) ||
            manifest.Sequence < context.MinimumSequence)
            throw new InvalidDataException("Manifest product, channel or sequence is not permitted.");
        if (manifest.CreatedUtc > context.NowUtc + context.AllowedClockSkew || manifest.ExpiresUtc <= context.NowUtc)
            throw new InvalidDataException("Manifest is future-dated or expired.");
    }

    internal static void Identifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128 ||
            value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
            throw new InvalidDataException("Manifest identifier is invalid.");
    }

    private static void Hash(string value)
    {
        if (value is null || value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("A SHA-256 value must contain exactly 64 hexadecimal characters.");
    }
}
