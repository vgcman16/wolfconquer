namespace WolfConquer.Core.Client;

/// <summary>
/// A compatible name-only view of the strict 67-field English text source reader.
/// Only field 1 is exposed; no source rows, statistics or missing IDs are inserted.
/// </summary>
public sealed class ClientItemCatalogEnglishNameSource
{
    public const int FieldCount = ClientItemCatalogEnglishTextSource.FieldCount;

    private readonly ClientItemCatalogEnglishTextSource _source;

    private ClientItemCatalogEnglishNameSource(ClientItemCatalogEnglishTextSource source)
    {
        _source = source;
    }

    public int SourceRowCount => _source.SourceRowCount;

    public int SkippedNameCount => _source.SkippedNameCount;

    /// <summary>Returns a read-only dictionary whose name buffers are defensive copies.</summary>
    public IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> Names => _source.Names;

    public static ClientItemCatalogEnglishNameSource Parse(ReadOnlySpan<byte> bytes)
    {
        return new ClientItemCatalogEnglishNameSource(ClientItemCatalogEnglishTextSource.Parse(bytes));
    }
}
