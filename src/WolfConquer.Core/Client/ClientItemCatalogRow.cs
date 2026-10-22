namespace WolfConquer.Core.Client;

/// <summary>An immutable catalog row whose field bytes have no assumed encoding.</summary>
public sealed class ClientItemCatalogRow
{
    private static ReadOnlySpan<byte> Delimiter => "@@"u8;

    private readonly ReadOnlyMemory<byte>[] _fields;
    private readonly ReadOnlyMemory<byte> _lineEnding;

    internal ClientItemCatalogRow(
        uint itemId,
        ReadOnlyMemory<byte>[] ownedFields,
        ReadOnlyMemory<byte> ownedLineEnding)
    {
        ItemId = itemId;
        _fields = ownedFields;
        _lineEnding = ownedLineEnding;
    }

    public uint ItemId { get; }

    public int FieldCount => _fields.Length;

    /// <summary>Returns read-only field views backed by defensive copies.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> Fields
    {
        get
        {
            var fields = new ReadOnlyMemory<byte>[_fields.Length];
            for (var index = 0; index < fields.Length; index++)
            {
                fields[index] = _fields[index].ToArray();
            }

            return Array.AsReadOnly(fields);
        }
    }

    public ReadOnlyMemory<byte> LineEnding => _lineEnding.ToArray();

    public ReadOnlyMemory<byte> GetField(int fieldIndex)
    {
        ValidateIndex(fieldIndex);
        return _fields[fieldIndex].ToArray();
    }

    public ClientItemCatalogRow WithField(int fieldIndex, ReadOnlySpan<byte> value)
    {
        ValidateIndex(fieldIndex);
        if (fieldIndex == 0)
        {
            throw new ArgumentException("Catalog item IDs cannot be edited through WithField.", nameof(fieldIndex));
        }

        if (value.IndexOfAny((byte)0, (byte)'\r', (byte)'\n') >= 0 || value.IndexOf(Delimiter) >= 0)
        {
            throw new ArgumentException("A catalog field cannot contain NUL, a line ending, or @@.", nameof(value));
        }

        // A terminal single '@' would merge with the following delimiter and
        // change field boundaries when the serialized row is parsed again.
        if (!value.IsEmpty && value[^1] == (byte)'@')
        {
            throw new ArgumentException("A catalog field cannot end with @.", nameof(value));
        }

        var fields = (ReadOnlyMemory<byte>[])_fields.Clone();
        fields[fieldIndex] = value.ToArray();
        return new ClientItemCatalogRow(ItemId, fields, _lineEnding);
    }

    internal void WriteTo(Stream destination)
    {
        foreach (var field in _fields)
        {
            destination.Write(field.Span);
            destination.Write(Delimiter);
        }

        destination.Write(_lineEnding.Span);
    }

    private void ValidateIndex(int fieldIndex)
    {
        if ((uint)fieldIndex >= (uint)_fields.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldIndex));
        }
    }
}
