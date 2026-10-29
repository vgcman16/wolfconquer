namespace WolfConquer.Core.Client;

/// <summary>Strict7937 envelope row. All non-ID fields retain opaque source bytes without semantic mapping.</summary>
public sealed class Chinese7937ItemCatalogRow
{
    private readonly ReadOnlyMemory<byte>[] fields;
    internal Chinese7937ItemCatalogRow(uint itemId, ReadOnlyMemory<byte>[] ownedFields)
    { ItemId = itemId; fields = ownedFields; }

    public uint ItemId { get; }
    public int FieldCount => fields.Length;
    public ReadOnlyMemory<byte> GetField(int index)
    {
        if ((uint)index >= (uint)fields.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return fields[index].ToArray();
    }

    internal void WriteTo(Stream output)
    {
        foreach (var field in fields) { output.Write(field.Span); output.Write("@@"u8); }
        output.Write("\r\n"u8);
    }
}
