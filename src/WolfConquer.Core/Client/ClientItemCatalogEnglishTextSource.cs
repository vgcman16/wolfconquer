using System.Collections.ObjectModel;

namespace WolfConquer.Core.Client;

/// <summary>
/// Extracts eligible ASCII bytes for slots 1, 52 and 53 from a strict 67-field
/// English source. This is not a client wire or statistics schema. Native storage
/// of the three text slots is observed; tooltip rendering semantics are unverified.
/// </summary>
public sealed class ClientItemCatalogEnglishTextSource
{
    public const int FieldCount = 67;

    private readonly Dictionary<uint, byte[]> _names = new();
    private readonly Dictionary<uint, byte[]> _categories = new();
    private readonly Dictionary<uint, byte[]> _descriptions = new();

    private ClientItemCatalogEnglishTextSource() { }

    public int SourceRowCount { get; private set; }

    public int SkippedNameCount { get; private set; }

    public int SkippedCategoryCount { get; private set; }

    public int SkippedDescriptionCount { get; private set; }

    public IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> Names => Copy(_names);

    public IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> Categories => Copy(_categories);

    public IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> Descriptions => Copy(_descriptions);

    public static ClientItemCatalogEnglishTextSource Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new InvalidDataException("An English text source must contain at least one row.");
        }

        var source = new ClientItemCatalogEnglishTextSource();
        var itemIds = new HashSet<uint>();
        var position = 0;
        while (position < bytes.Length)
        {
            var start = position;
            while (position < bytes.Length && bytes[position] != (byte)'\r' && bytes[position] != (byte)'\n')
            {
                position++;
            }

            var row = bytes[start..position];
            source.SourceRowCount++;
            if (row.IsEmpty)
            {
                throw new InvalidDataException($"Empty English text source row {source.SourceRowCount}.");
            }

            var itemId = ParseRow(row, source.SourceRowCount, out var name, out var category, out var description);
            if (!itemIds.Add(itemId))
            {
                throw new InvalidDataException($"Duplicate item ID {itemId} in English text source.");
            }

            if (!AddEligible(source._names, itemId, row[name]))
            {
                source.SkippedNameCount++;
            }

            if (!AddEligible(source._categories, itemId, row[category]))
            {
                source.SkippedCategoryCount++;
            }

            if (!AddEligible(source._descriptions, itemId, row[description]))
            {
                source.SkippedDescriptionCount++;
            }

            if (position < bytes.Length)
            {
                position += bytes[position] == (byte)'\r' &&
                    position + 1 < bytes.Length && bytes[position + 1] == (byte)'\n' ? 2 : 1;
            }
        }

        return source;
    }

    private static uint ParseRow(
        ReadOnlySpan<byte> row, int rowNumber, out Range name, out Range category, out Range description)
    {
        name = category = description = default;
        var idLength = 0;
        var fieldStart = 0;
        var fieldIndex = 0;
        var position = 0;
        while (position < row.Length)
        {
            if (row[position] == 0)
            {
                throw new InvalidDataException($"NUL byte in English text source row {rowNumber}.");
            }

            if (row[position] == (byte)'@' && position + 1 < row.Length && row[position + 1] == (byte)'@')
            {
                if (fieldIndex == FieldCount)
                {
                    throw new InvalidDataException($"English text source row {rowNumber} has more than {FieldCount} fields.");
                }

                switch (fieldIndex)
                {
                    case 0: idLength = position; break;
                    case 1: name = fieldStart..position; break;
                    case 52: category = fieldStart..position; break;
                    case 53: description = fieldStart..position; break;
                }

                fieldIndex++;
                position += 2;
                fieldStart = position;
            }
            else
            {
                position++;
            }
        }

        if (fieldIndex != FieldCount || fieldStart != row.Length)
        {
            throw new InvalidDataException($"English text source row {rowNumber} requires exactly {FieldCount} fields and a trailing @@ delimiter.");
        }

        var id = row[..idLength];
        if (id.IsEmpty)
        {
            throw new InvalidDataException($"English text source row {rowNumber} has an empty item ID.");
        }

        uint itemId = 0;
        foreach (var value in id)
        {
            if (value < (byte)'0' || value > (byte)'9')
            {
                throw new InvalidDataException($"English text source row {rowNumber} requires a decimal uint item ID.");
            }

            var digit = (uint)(value - (byte)'0');
            if (itemId > (uint.MaxValue - digit) / 10)
            {
                throw new InvalidDataException($"Item ID in English text source row {rowNumber} exceeds uint.MaxValue.");
            }

            itemId = itemId * 10 + digit;
        }

        return itemId;
    }

    private static bool AddEligible(Dictionary<uint, byte[]> destination, uint itemId, ReadOnlySpan<byte> text)
    {
        if (text.IsEmpty || text[^1] == (byte)'@' || text.IndexOf("@@"u8) >= 0)
        {
            return false;
        }

        var hasLetter = false;
        foreach (var value in text)
        {
            if (value < 0x20 || value > 0x7E)
            {
                return false;
            }

            hasLetter |= value is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';
        }

        if (!hasLetter)
        {
            return false;
        }

        destination.Add(itemId, text.ToArray());
        return true;
    }

    private static IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> Copy(Dictionary<uint, byte[]> source)
    {
        var copy = new Dictionary<uint, ReadOnlyMemory<byte>>(source.Count);
        foreach (var entry in source)
        {
            copy.Add(entry.Key, entry.Value.AsSpan().ToArray());
        }

        return new ReadOnlyDictionary<uint, ReadOnlyMemory<byte>>(copy);
    }
}
