using System.Buffers.Binary;
using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public sealed partial class ClientItemCatalog7937Alignment
{
    private sealed record JoinedRows(ClientItemCatalogRow[] Reference, Chinese7937ItemCatalogRow[] Current)
    {
        internal int Count => Reference.Length;
        internal ReadOnlySpan<byte> Field(bool current, int row, int column) =>
            current ? Current[row].FieldBytes(column) : Reference[row].FieldBytes(column);
    }

    private static JoinedRows JoinRows(ClientItemCatalogDocument reference, Chinese7937ItemCatalogDocument current)
    {
        var currentRows = current.Rows.ToDictionary(row => row.ItemId);
        var shared = reference.Rows.Where(row => currentRows.ContainsKey(row.ItemId)).OrderBy(row => row.ItemId).ToArray();
        return new(shared, shared.Select(row => currentRows[row.ItemId]).ToArray());
    }

    private static bool TryMeasureVectors(JoinedRows rows, int budget, out int[][] lengths, out int total)
    {
        lengths = [new int[64], new int[66]]; total = 0;
        for (var side = 0; side < lengths.Length; side++)
        {
            for (var column = 0; column < lengths[side].Length; column++)
            {
                long length = 0;
                for (var row = 0; row < rows.Count; row++)
                {
                    length += sizeof(uint) + (long)rows.Field(side == 1, row, column + 1).Length;
                    if (length > budget - total) return false;
                }
                lengths[side][column] = (int)length;
                total += (int)length;
            }
        }
        return true;
    }

    private static byte[][] BuildVectors(JoinedRows rows, bool current, int[] lengths)
    {
        var vectors = new byte[lengths.Length][];
        for (var column = 0; column < lengths.Length; column++)
        {
            var vector = new byte[lengths[column]];
            var offset = 0;
            for (var row = 0; row < rows.Count; row++)
            {
                var field = rows.Field(current, row, column + 1);
                BinaryPrimitives.WriteUInt32LittleEndian(vector.AsSpan(offset), (uint)field.Length);
                offset += sizeof(uint);
                field.CopyTo(vector.AsSpan(offset)); offset += field.Length;
            }
            vectors[column] = vector;
        }
        return vectors;
    }

    private static ClientItemColumnCandidates[] CompareVectors(byte[][] reference, byte[][] current)
    {
        // Hashes narrow candidates only. Full length-framed byte equality is
        // still required, so a digest collision cannot establish correspondence.
        var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var column = 0; column < current.Length; column++)
        {
            var digest = Convert.ToHexString(SHA256.HashData(current[column]));
            if (!index.TryGetValue(digest, out var columns)) index.Add(digest, columns = []);
            columns.Add(column);
        }
        var result = new ClientItemColumnCandidates[reference.Length];
        for (var column = 0; column < reference.Length; column++)
        {
            var vector = reference[column];
            var digest = Convert.ToHexString(SHA256.HashData(vector));
            int[] candidates = index.TryGetValue(digest, out var possible)
                ? possible.Where(target => vector.AsSpan().SequenceEqual(current[target])).Select(target => target + 1).ToArray()
                : [];
            result[column] = new(column + 1, candidates);
        }
        return result;
    }
}
