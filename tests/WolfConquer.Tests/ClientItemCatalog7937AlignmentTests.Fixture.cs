using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientItemCatalog7937AlignmentTests
{
    internal readonly record struct CatalogPair(byte[] Reference, byte[] Current);

    internal static byte[][] Fields(string id, int width, string value = "x")
    {
        var fields = Enumerable.Range(0, width).Select(_ => Encoding.ASCII.GetBytes(value)).ToArray();
        fields[0] = Encoding.ASCII.GetBytes(id); return fields;
    }

    internal static byte[] Row(byte[][] fields, string ending = "\r\n")
    {
        using var bytes = new MemoryStream();
        foreach (var field in fields) { bytes.Write(field); bytes.Write("@@"u8); }
        bytes.Write(Encoding.ASCII.GetBytes(ending)); return bytes.ToArray();
    }

    internal static CatalogPair UniquePair(bool lateMismatch = false, int sharedIdentities = 2)
    {
        var targets = Enumerable.Range(1, 66).Where(column => column is not (7 or 41)).Reverse().ToArray();
        string[] referenceIds = sharedIdentities == 2 ? ["00020", "3"] : Enumerable.Range(1, sharedIdentities).Reverse()
            .Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        string[] currentIds = sharedIdentities == 2 ? ["3", "20", "99"] : Enumerable.Range(1, sharedIdentities + 1)
            .Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var lateId = sharedIdentities == 2 ? "20" : sharedIdentities.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var reference = new MemoryStream(); using var current = new MemoryStream();
        foreach (var id in referenceIds)
        {
            var source = Fields(id, 65);
            var numericId = uint.Parse(id, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (var column = 1; column < 65; column++) source[column] = Encoding.ASCII.GetBytes($"private-source-{column}-id-{numericId}");
            reference.Write(Row(source));
        }
        foreach (var id in currentIds)
        {
            var destination = Fields(id, 67, "private-current-only");
            for (var column = 1; column < 65; column++)
                destination[targets[column - 1]] = Encoding.ASCII.GetBytes($"private-source-{column}-id-{id}");
            if (lateMismatch && id == lateId) destination[66] = "private-late-change"u8.ToArray();
            current.Write(Row(destination));
        }
        return new(reference.ToArray(), current.ToArray());
    }

    private static ClientItemCatalog7937Alignment Analyze(CatalogPair bytes, int budget = ClientItemCatalog7937Alignment.MaximumVectorBytes)
    {
        var reference = ClientItemCatalogDocument.Parse(bytes.Reference);
        var current = Chinese7937ItemCatalogDocument.ParseDecoded(bytes.Current, 7937);
        Assert.True(ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out var result, out var failure, budget), failure.ToString());
        Assert.Equal(ClientItemCatalogAlignmentFailure.None, failure);
        return Assert.IsType<ClientItemCatalog7937Alignment>(result);
    }
}
