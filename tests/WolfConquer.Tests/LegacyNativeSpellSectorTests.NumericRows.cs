using System.Globalization;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSpellSectorTests
{
    private readonly ITestOutputHelper geometryOutput;
    public LegacyNativeSpellSectorTests(ITestOutputHelper output) => geometryOutput = output;
    private static bool EmitGeometryRows => Environment.GetEnvironmentVariable("WOLF_NATIVE_WARCRY_GEOMETRY_ROWS") == "1";
    private static void HashGeometry(ref uint digest, int value) =>
        digest = unchecked((digest ^ (uint)value) * 16777619u);
    private static string GeometryValues(IEnumerable<int> values) =>
        string.Join(",", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));

    [Fact]
    public void WholeSourceNumericGateRowsUseActualGeometryOwners()
    {
        // This supplies actual-owner rows for the parent's independent full
        // Framework comparison. Counts/bounds are not numeric parity proof.
        // Root compared all17,512 actual rows against both Framework/x86 builds.
        // These measured fingerprints are regression guards, not the parity gate.
        var degree = ActualDegreeRows();
        var sector = ActualSectorRows();
        Assert.Equal(17148, degree.Count);
        Assert.Equal(364, sector.Count);
        Assert.Equal(3328497569u, degree.Digest);
        Assert.Equal(3619719583u, sector.Digest);
    }

    private static IEnumerable<int[]> DegreeVectors()
    {
        for (int dx = -64; dx <= 64; dx++)
            for (int dy = -64; dy <= 64; dy++)
                yield return new[] { 200, 200 + dx, 300, 300 + dy };
        int[] edge = { int.MinValue, int.MinValue + 1, -65536, -32769, -1, 0, 1, 32767, 32768, 65535, 65536, int.MaxValue - 1, int.MaxValue };
        foreach (int a in edge)
            foreach (int b in edge)
            {
                yield return new[] { a, b, 0, 0 };
                yield return new[] { 0, 0, a, b };
                yield return new[] { a, b, b, a };
            }
    }

    private (int Count, uint Digest) ActualDegreeRows()
    {
        uint digest = 2166136261u;
        int count = 0;
        foreach (int[] row in DegreeVectors())
        {
            int actual = LegacyNativeRoleCore.GetDegree(row[0], row[1], row[2], row[3]);
            Assert.InRange(actual, 0, 360);
            HashGeometry(ref digest, actual);
            if (EmitGeometryRows)
                geometryOutput.WriteLine(FormattableString.Invariant($"D|{count}|{GeometryValues(row)}|{actual}"));
            count++;
        }
        geometryOutput.WriteLine(FormattableString.Invariant($"DegreeRows={count} DegreeDigest={digest}"));
        return (count, digest);
    }

    private (int Count, uint Digest) ActualSectorRows()
    {
        uint digest = 2166136261u;
        int count = 0;
        int[][] origins = { new[] { 0, 0, 1, 0 }, new[] { 10, 10, 10, 11 }, new[] { 10, 10, 11, 9 },
            new[] { -1, 65536, 65536, -1 } };
        int[] sizes = { int.MinValue, -721, -360, -90, -1, 0, 1, 45, 90, 180, 360, 721, int.MaxValue };
        int[] distances = { int.MinValue, -1, 0, 1, 14, 15, int.MaxValue };
        int[][] points = { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 0 },
            new[] { 0, -1 }, new[] { 10, 10 }, new[] { 24, 10 }, new[] { 25, 10 },
            new[] { 32767, 0 }, new[] { 32768, 0 }, new[] { 65535, 65535 },
            new[] { 65536, -1 }, new[] { int.MinValue, int.MaxValue } };
        foreach (int[] origin in origins)
            foreach (int size in sizes)
                foreach (int distance in distances)
                {
                    var sector = new LegacyNativeSpellSector(origin[0], origin[1], origin[2], origin[3]);
                    sector.Arrange(size, distance);
                    int[] firstState = Snapshot(sector);
                    int[] firstInside = new int[points.Length];
                    foreach (int field in firstState) HashGeometry(ref digest, field);
                    for (int i = 0; i < points.Length; i++)
                    {
                        firstInside[i] = sector.Inside(points[i][0], points[i][1]) ? 1 : 0;
                        HashGeometry(ref digest, firstInside[i]);
                    }
                    sector.Arrange(0, distance);
                    int[] secondState = Snapshot(sector);
                    int[] secondInside = new int[points.Length];
                    foreach (int field in secondState) HashGeometry(ref digest, field);
                    for (int i = 0; i < points.Length; i++)
                    {
                        secondInside[i] = sector.Inside(points[i][0], points[i][1]) ? 1 : 0;
                        HashGeometry(ref digest, secondInside[i]);
                    }
                    if (EmitGeometryRows)
                        geometryOutput.WriteLine(FormattableString.Invariant($"S|{count}|{GeometryValues(origin)}|{size}|{distance}|{GeometryValues(firstState)}|{GeometryValues(firstInside)}|{GeometryValues(secondState)}|{GeometryValues(secondInside)}"));
                    count++;
                }
        geometryOutput.WriteLine(FormattableString.Invariant($"SectorRows={count} SectorDigest={digest}"));
        return (count, digest);
    }
}
