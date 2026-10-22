using System.Collections.ObjectModel;

namespace WolfConquer.Core.Client;

/// <summary>
/// A byte-preserving view of a native-decoded, 65-field item catalog.
/// This type neither identifies a text encoding nor implements a packed-file codec.
/// </summary>
public sealed class ClientItemCatalogDocument
{
    public const int FieldCount = 65;

    private readonly ReadOnlyCollection<ClientItemCatalogRow> _rows;
    private readonly Dictionary<uint, int> _rowIndexes;

    private ClientItemCatalogDocument(ClientItemCatalogRow[] rows)
    {
        _rows = Array.AsReadOnly(rows);
        _rowIndexes = new Dictionary<uint, int>(rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            if (!_rowIndexes.TryAdd(rows[index].ItemId, index))
            {
                throw new InvalidDataException($"Duplicate item ID {rows[index].ItemId}.");
            }
        }
    }

    public IReadOnlyList<ClientItemCatalogRow> Rows => _rows;

    public static ClientItemCatalogDocument Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new InvalidDataException("An item catalog must contain at least one row.");
        }

        // All retained slices belong to this private copy, never to the caller.
        var owned = bytes.ToArray();
        var rows = new List<ClientItemCatalogRow>();
        var position = 0;
        while (position < owned.Length)
        {
            var rowStart = position;
            while (position < owned.Length && owned[position] != (byte)'\r' && owned[position] != (byte)'\n')
            {
                position++;
            }

            var rowEnd = position;
            if (rowEnd == rowStart)
            {
                throw new InvalidDataException($"Empty item catalog row {rows.Count + 1}.");
            }

            if (position < owned.Length)
            {
                position += owned[position] == (byte)'\r' &&
                    position + 1 < owned.Length && owned[position + 1] == (byte)'\n' ? 2 : 1;
            }

            rows.Add(ParseRow(
                owned,
                rowStart,
                rowEnd,
                owned.AsMemory(rowEnd, position - rowEnd),
                rows.Count + 1));
        }

        return new ClientItemCatalogDocument(rows.ToArray());
    }

    public ClientItemCatalogDocument WithField(uint itemId, int fieldIndex, ReadOnlySpan<byte> value)
    {
        if (!_rowIndexes.TryGetValue(itemId, out var index))
        {
            throw new KeyNotFoundException($"Item ID {itemId} is not present in the catalog.");
        }

        var rows = _rows.ToArray();
        rows[index] = rows[index].WithField(fieldIndex, value);
        return new ClientItemCatalogDocument(rows);
    }

    public ClientItemCatalogDocument ReplaceField(uint itemId, int fieldIndex, ReadOnlySpan<byte> value)
    {
        return WithField(itemId, fieldIndex, value);
    }

    /// <summary>Copies only field 1 of existing rows in one batch; no IDs or other fields are changed.</summary>
    public ClientItemCatalogDocument WithNames(IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        if (replacements.Count == 0)
        {
            return this;
        }

        var rows = _rows.ToArray();
        foreach (var replacement in replacements)
        {
            if (!_rowIndexes.TryGetValue(replacement.Key, out var index))
            {
                throw new KeyNotFoundException($"Item ID {replacement.Key} is not present in the catalog.");
            }

            rows[index] = rows[index].WithField(1, replacement.Value.Span);
        }

        return new ClientItemCatalogDocument(rows);
    }

    /// <summary>Copies only the three observed text slots of existing rows in one batch.</summary>
    public ClientItemCatalogDocument WithTextFields(IReadOnlyDictionary<uint, ClientItemCatalogTextFields> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        if (replacements.Count == 0)
        {
            return this;
        }

        var rows = _rows.ToArray();
        foreach (var replacement in replacements)
        {
            if (!_rowIndexes.TryGetValue(replacement.Key, out var index))
            {
                throw new KeyNotFoundException($"Item ID {replacement.Key} is not present in the catalog.");
            }

            var text = replacement.Value ??
                throw new ArgumentException("A text-field replacement cannot be null.", nameof(replacements));
            var row = rows[index];
            if (text.Name is { } name)
            {
                row = row.WithField(1, name.Span);
            }

            if (text.Category is { } category)
            {
                row = row.WithField(52, category.Span);
            }

            if (text.Description is { } description)
            {
                row = row.WithField(53, description.Span);
            }

            rows[index] = row;
        }

        return new ClientItemCatalogDocument(rows);
    }

    public byte[] ToArray()
    {
        using var output = new MemoryStream();
        Write(output);
        return output.ToArray();
    }

    /// <summary>Reconstructs every field, delimiter and original row ending.</summary>
    public void Write(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        foreach (var row in _rows)
        {
            row.WriteTo(destination);
        }
    }

    private static ClientItemCatalogRow ParseRow(
        byte[] owned,
        int start,
        int end,
        ReadOnlyMemory<byte> lineEnding,
        int rowNumber)
    {
        var fields = new ReadOnlyMemory<byte>[FieldCount];
        var fieldStart = start;
        var fieldIndex = 0;
        var position = start;
        while (position < end)
        {
            if (owned[position] == 0)
            {
                throw new InvalidDataException($"NUL byte in item catalog row {rowNumber}.");
            }

            if (owned[position] == (byte)'@' && position + 1 < end && owned[position + 1] == (byte)'@')
            {
                if (fieldIndex == FieldCount)
                {
                    throw new InvalidDataException($"Item catalog row {rowNumber} has more than {FieldCount} fields.");
                }

                fields[fieldIndex++] = owned.AsMemory(fieldStart, position - fieldStart);
                position += 2;
                fieldStart = position;
            }
            else
            {
                position++;
            }
        }

        if (fieldIndex != FieldCount || fieldStart != end)
        {
            throw new InvalidDataException($"Item catalog row {rowNumber} requires exactly {FieldCount} fields and a trailing @@ delimiter.");
        }

        var idBytes = fields[0].Span;
        if (idBytes.IsEmpty)
        {
            throw new InvalidDataException($"Item catalog row {rowNumber} has an empty item ID.");
        }

        uint itemId = 0;
        foreach (var value in idBytes)
        {
            if (value < (byte)'0' || value > (byte)'9')
            {
                throw new InvalidDataException($"Item catalog row {rowNumber} requires a decimal uint item ID.");
            }

            var digit = (uint)(value - (byte)'0');
            if (itemId > (uint.MaxValue - digit) / 10)
            {
                throw new InvalidDataException($"Item ID in catalog row {rowNumber} exceeds uint.MaxValue.");
            }

            itemId = itemId * 10 + digit;
        }

        return new ClientItemCatalogRow(itemId, fields, lineEnding);
    }
}
