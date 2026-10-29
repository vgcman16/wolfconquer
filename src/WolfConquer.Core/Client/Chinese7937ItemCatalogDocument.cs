using System.Collections.ObjectModel;

namespace WolfConquer.Core.Client;

/// <summary>Data-only, explicitly declared7937/67-field envelope; no old-column role or runtime ownership is inferred.</summary>
public sealed partial class Chinese7937ItemCatalogDocument
{
    public const int ClientVersion = 7937;
    public const int FieldCount = 67;
    public const int MaximumBytes = 64 * 1024 * 1024;
    private readonly ReadOnlyCollection<Chinese7937ItemCatalogRow> rows;
    private readonly int decodedLength;

    private Chinese7937ItemCatalogDocument(Chinese7937ItemCatalogRow[] ownedRows, int length)
    { rows = Array.AsReadOnly(ownedRows); decodedLength = length; }

    public IReadOnlyList<Chinese7937ItemCatalogRow> Rows => rows;

    public static Chinese7937ItemCatalogDocument ParsePacked(ReadOnlySpan<byte> packed, int declaredClientVersion)
    {
        RequireInput(packed.Length, declaredClientVersion);
        var captured = packed.ToArray(); var decoded = ClientItemCatalogCodec.Decode(captured);
        var result = ParseDecoded(decoded, declaredClientVersion);
        if (!decoded.AsSpan().SequenceEqual(result.ToArray()) ||
            !captured.AsSpan().SequenceEqual(ClientItemCatalogCodec.Encode(decoded)))
            throw new InvalidDataException("Chinese7937 catalog exact reconstruction failed.");
        return result;
    }

    public byte[] ToArray()
    {
        using var output = new MemoryStream(decodedLength);
        foreach (var row in rows) row.WriteTo(output);
        return output.ToArray();
    }

    private static void RequireInput(int length, int version)
    {
        if (version != ClientVersion) throw new InvalidDataException("Explicit7937 schema declaration required.");
        if (length is <= 0 or > MaximumBytes) throw new InvalidDataException("Chinese7937 catalog byte bound.");
    }
}
