using System.Text.Json;

namespace WolfConquer.Updates.Manifest;

internal static class ManifestJson
{
    internal static JsonDocument Read(ReadOnlySpan<byte> utf8) => JsonDocument.Parse(utf8.ToArray(),
        new JsonDocumentOptions { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });

    internal static Dictionary<string, JsonElement> Fields(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected JSON object.");
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !fields.TryAdd(property.Name, property.Value))
                throw new InvalidDataException("Duplicate or unknown JSON property.");
        if (fields.Count != names.Length) throw new InvalidDataException("Required JSON property is missing.");
        return fields;
    }

    internal static string Text(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? element.GetString()! : throw new InvalidDataException("Expected JSON string.");

    internal static UpdateManifest ReadPayload(ReadOnlySpan<byte> payload)
    {
        using var json = Read(payload);
        string[] names = ["schemaVersion", "productId", "channel", "sequence",
            "createdUtc", "expiresUtc", "contentBaseUrl", "files"];
        bool hasBundle = json.RootElement.ValueKind == JsonValueKind.Object &&
            json.RootElement.TryGetProperty("bundle", out _);
        var fields = Fields(json.RootElement, hasBundle ? [.. names, "bundle"] : names);
        if (!fields["schemaVersion"].TryGetInt32(out int schema) || !fields["sequence"].TryGetInt64(out long sequence) ||
            fields["files"].ValueKind != JsonValueKind.Array || fields["files"].GetArrayLength() > SignedUpdateManifest.MaximumFiles)
            throw new InvalidDataException("Manifest numeric, timestamp or files field is invalid.");
        var created = Timestamp(fields["createdUtc"]);
        var expires = Timestamp(fields["expiresUtc"]);
        var files = new List<UpdateFileEntry>();
        foreach (var element in fields["files"].EnumerateArray())
        {
            var file = Fields(element, "path", "length", "sha256", "baseSha256");
            if (!file["length"].TryGetInt64(out long length)) throw new InvalidDataException("Invalid file length.");
            files.Add(new UpdateFileEntry(Text(file["path"]), length, Text(file["sha256"]),
                file["baseSha256"].ValueKind == JsonValueKind.Null ? null : Text(file["baseSha256"])));
        }
        if (!Uri.TryCreate(Text(fields["contentBaseUrl"]), UriKind.Absolute, out var contentBase))
            throw new InvalidDataException("Invalid content base URL.");
        UpdateBundle? bundle = null;
        if (hasBundle && fields["bundle"].ValueKind != JsonValueKind.Null)
        {
            var transport = Fields(fields["bundle"], "length", "sha256");
            if (!transport["length"].TryGetInt64(out long length))
                throw new InvalidDataException("Invalid bundle length.");
            bundle = new UpdateBundle(length, Text(transport["sha256"]));
        }
        return new UpdateManifest(schema, Text(fields["productId"]), Text(fields["channel"]), sequence,
            created, expires, contentBase, files, bundle);
    }

    private static DateTimeOffset Timestamp(JsonElement element)
    {
        string value = Text(element);
        if (!value.Contains('T') || !(value.EndsWith("Z", StringComparison.Ordinal) ||
                                      value.EndsWith("+00:00", StringComparison.Ordinal)) ||
            !element.TryGetDateTimeOffset(out var timestamp) || timestamp.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Manifest timestamps require an explicit UTC timezone.");
        return timestamp;
    }

    internal static byte[] WritePayload(UpdateManifest manifest)
    {
        using var output = new MemoryStream();
        using (var json = new Utf8JsonWriter(output))
        {
            json.WriteStartObject();
            json.WriteNumber("schemaVersion", manifest.SchemaVersion);
            json.WriteString("productId", manifest.ProductId);
            json.WriteString("channel", manifest.Channel);
            json.WriteNumber("sequence", manifest.Sequence);
            json.WriteString("createdUtc", manifest.CreatedUtc.UtcDateTime);
            json.WriteString("expiresUtc", manifest.ExpiresUtc.UtcDateTime);
            json.WriteString("contentBaseUrl", manifest.ContentBaseUrl.OriginalString);
            json.WriteStartArray("files");
            foreach (var file in manifest.Files)
            {
                json.WriteStartObject();
                json.WriteString("path", file.Path);
                json.WriteNumber("length", file.Length);
                json.WriteString("sha256", file.Sha256);
                if (file.BaseSha256 is null) json.WriteNull("baseSha256");
                else json.WriteString("baseSha256", file.BaseSha256);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            if (manifest.Bundle is { } bundle)
            {
                json.WriteStartObject("bundle");
                json.WriteNumber("length", bundle.Length);
                json.WriteString("sha256", bundle.Sha256);
                json.WriteEndObject();
            }
            json.WriteEndObject();
        }
        return output.ToArray();
    }
}
