using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Explicit successful-login prerequisite, never a loopback/production fallback.
// Callers are serialized against other native cache tests (a nonparallel xUnit
// collection), so the real cache stays owned. Restore the exact prior dictionary.
internal sealed class NativeLoginBeastMetadataScope : IDisposable
{
    private readonly Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute> prior =
        LegacyNativeBeastsAtrribute.Attributes;

    internal NativeLoginBeastMetadataScope(params int[] requiredPoints)
    {
        if (requiredPoints.Length is < 1 or > 255 || requiredPoints.Any(value => value <= 0))
            throw new ArgumentException("Successful login fixtures require explicit positive contiguous metadata.", nameof(requiredPoints));

        LegacyNativeBeastsAtrribute.Attributes = [];
        try
        {
            var rows = requiredPoints.Select((points, index) =>
            {
                var fields = Enumerable.Repeat("0", 19).ToArray();
                fields[0] = (index + 1).ToString(CultureInfo.InvariantCulture);
                fields[1] = points.ToString(CultureInfo.InvariantCulture);
                fields[^1] = "";
                return BeastParser.ParseRow("explicit-login-fixture", index + 1, string.Join("@@", fields));
            }).ToArray();
            // Same explicit raw/typed bridge as real startup, before any login.
            LegacyNativeBeastsCatalogStartup.AddRows(rows);
        }
        catch
        {
            LegacyNativeBeastsAtrribute.Attributes = prior;
            throw;
        }
    }

    public void Dispose() => LegacyNativeBeastsAtrribute.Attributes = prior;
}
