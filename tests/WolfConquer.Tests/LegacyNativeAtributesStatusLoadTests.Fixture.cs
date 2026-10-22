using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAtributesStatusLoadTests
{
    // Owned GUID Stats.ini fixture only; no preserved database files or host/startup publication.
    private sealed class LoadFixture : IDisposable
    {
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-atributes-" + Guid.NewGuid().ToString("N"));
        internal string DataDirectory => Path.Combine(Root, "db");
        internal string StatsPath => Path.Combine(DataDirectory, "Stats.ini");
        internal LegacyNativeAtributesStatus Table { get; } = new();

        internal LoadFixture()
        {
            Directory.CreateDirectory(DataDirectory);
            LegacyNativeServerConfig.DbLocation = DataDirectory + Path.DirectorySeparatorChar;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        internal void WriteRows(params string[] rows) => File.WriteAllLines(StatsPath, rows, new UTF8Encoding(false));

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = priorLocation;
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
            var full = Path.GetFullPath(Root);
            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), expectedParent) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-atributes-[0-9a-f]{32}$"))
                throw new InvalidOperationException("Owned AtributesStatus fixture cleanup containment failed.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
