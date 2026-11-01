using System.Collections.ObjectModel;
using System.Text;

namespace WolfConquer.Core.Client;

/// <summary>Bounded raw 7937 catalog data; not a native loader or gameplay owner.</summary>
public sealed class Chinese7937CombatGearCatalogDocument
{
    public const int ClientVersion = 7937;
    public const int FieldCount = 22;
    public const int MaximumBytes = 16 * 1024 * 1024;
    public const int MaximumRows = 65_536;
    public const int MaximumFieldDigits = 20;
    private readonly byte[] decoded;
    private readonly ReadOnlyCollection<Chinese7937CombatGearCatalogRow> rows;

    private Chinese7937CombatGearCatalogDocument(byte[] decoded, Chinese7937CombatGearCatalogRow[] rows)
    {
        this.decoded = decoded;
        this.rows = Array.AsReadOnly(rows);
    }

    public IReadOnlyList<Chinese7937CombatGearCatalogRow> Rows => rows;

    public static Chinese7937CombatGearCatalogDocument ParsePacked(ReadOnlySpan<byte> packed, int declaredClientVersion)
    {
        RequireInput(packed.Length, declaredClientVersion);
        var captured = packed.ToArray();
        var decoded = ClientItemCatalogCodec.Decode(captured);
        var document = ParseDecoded(decoded, declaredClientVersion);
        if (!decoded.AsSpan().SequenceEqual(document.decoded) ||
            !captured.AsSpan().SequenceEqual(ClientItemCatalogCodec.Encode(decoded)))
            throw new InvalidDataException("Chinese7937 CombatGear transport reconstruction failed.");
        return document;
    }

    public static Chinese7937CombatGearCatalogDocument ParseDecoded(ReadOnlySpan<byte> input, int declaredClientVersion)
    {
        RequireInput(input.Length, declaredClientVersion);
        var owned = input.ToArray();
        var rows = new List<Chinese7937CombatGearCatalogRow>();
        int position = 0;
        while (position < owned.Length)
        {
            if (rows.Count == MaximumRows) throw Invalid();
            var values = new uint[FieldCount];
            var fields = new string[FieldCount];
            for (int index = 0; index < FieldCount; index++)
            {
                int start = position;
                uint value = 0;
                while (position < owned.Length && owned[position] is >= (byte)'0' and <= (byte)'9')
                {
                    if (position - start == MaximumFieldDigits) throw Invalid();
                    uint digit = (uint)(owned[position++] - (byte)'0');
                    if (value > (uint.MaxValue - digit) / 10) throw Invalid();
                    value = value * 10 + digit;
                }
                if (position == start || position + 1 >= owned.Length ||
                    owned[position] != (byte)'@' || owned[position + 1] != (byte)'@') throw Invalid();
                values[index] = value;
                fields[index] = Encoding.ASCII.GetString(owned, start, position - start);
                position += 2;
            }
            if (position + 1 >= owned.Length || owned[position] != (byte)'\r' || owned[position + 1] != (byte)'\n')
                throw Invalid();
            position += 2;
            rows.Add(new Chinese7937CombatGearCatalogRow(values, fields));
        }
        return new Chinese7937CombatGearCatalogDocument(owned, rows.ToArray());
    }

    public byte[] ToArray() => decoded.ToArray();

    public byte[] ToPacked() => ClientItemCatalogCodec.Encode(decoded);

    private static void RequireInput(int length, int version)
    {
        if (version != ClientVersion || length is <= 0 or > MaximumBytes) throw Invalid();
    }

    private static InvalidDataException Invalid() => new("Chinese7937 CombatGear catalog data is malformed or out of bounds.");
}
