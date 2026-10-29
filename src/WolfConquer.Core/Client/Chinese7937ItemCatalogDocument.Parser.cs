namespace WolfConquer.Core.Client;

public sealed partial class Chinese7937ItemCatalogDocument
{
    public static Chinese7937ItemCatalogDocument ParseDecoded(ReadOnlySpan<byte> decoded, int declaredClientVersion)
    {
        RequireInput(decoded.Length, declaredClientVersion);
        var owned = decoded.ToArray(); var parsed = new List<Chinese7937ItemCatalogRow>();
        var identities = new HashSet<uint>(); var position = 0;
        while (position < owned.Length)
        {
            var start = position;
            while (position < owned.Length && owned[position] != 13 && owned[position] != 10) position++;
            var end = position;
            if (end == start || position + 1 >= owned.Length || owned[position] != 13 || owned[position + 1] != 10)
                throw new InvalidDataException("Chinese7937 requires nonempty CRLF-terminated rows without a header.");
            position += 2;
            var fields = new ReadOnlyMemory<byte>[FieldCount]; var field = 0; var fieldStart = start;
            for (var index = start; index < end; index++)
            {
                if (owned[index] == 0) throw new InvalidDataException("NUL in Chinese7937 catalog row.");
                if (owned[index] != (byte)'@' || index + 1 >= end || owned[index + 1] != (byte)'@') continue;
                if (field == FieldCount) throw new InvalidDataException("More than 67 Chinese7937 fields.");
                fields[field++] = owned.AsMemory(fieldStart, index - fieldStart);
                index++; fieldStart = index + 1;
            }
            if (field != FieldCount || fieldStart != end)
                throw new InvalidDataException("Exactly 67 fields and trailing @@ required for Chinese7937.");
            var id = ReadIdentity(fields[0].Span);
            if (!identities.Add(id)) throw new InvalidDataException("Duplicate Chinese7937 catalog identity.");
            parsed.Add(new(id, fields));
        }
        return new(parsed.ToArray(), owned.Length);
    }

    private static uint ReadIdentity(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) throw new InvalidDataException("Empty Chinese7937 catalog identity.");
        uint value = 0;
        foreach (var current in bytes)
        {
            if (current is < (byte)'0' or > (byte)'9') throw new InvalidDataException("Unsigned decimal catalog identity required.");
            var digit = (uint)(current - (byte)'0');
            if (value > (uint.MaxValue - digit) / 10) throw new InvalidDataException("Catalog identity exceeds the UInt32 tool envelope.");
            value = value * 10 + digit;
        }
        return value;
    }
}
