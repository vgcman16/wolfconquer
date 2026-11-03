using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PaidNobilityInputSafetyTests
{
    internal static readonly DateTime Future = new(2099, 11, 3, 12, 0, 0, DateTimeKind.Unspecified);
    internal static readonly DateTime Past = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
    internal static string Binary(DateTime time) => time.ToBinary().ToString(CultureInfo.InvariantCulture);
    internal static string Active(string? period, string? donation, string flag = "True") =>
        "IsActive=" + flag + "\r\n" + (period is null ? "" : "PeriodTime=" + period + "\r\n") +
        (donation is null ? "" : "LastNobilityDonation=" + donation + "\r\n");

    internal static string CharacterWithPaid(string paid, uint uid = 9001) =>
        Encoding.UTF8.GetString(SqlWorldStateTestData.Character(uid)).Replace("[Unrelated]",
            "DonationNobility=777777777\r\nLastNobilityDonation=777777777\r\nNobilityRank=7\r\nNobilityPosition=2\r\n[Unrelated]",
            StringComparison.Ordinal) + "[PaidNobility]\r\n" + paid + "OpaqueKeep=paid-input\r\n";

    public static IEnumerable<object[]> InvalidPaidInputs()
    {
        yield return ["empty-active", Active(Binary(Future), "30000000", "")];
        yield return ["word-active", Active(Binary(Future), "30000000", "bad")];
        yield return ["quoted-active", Active(Binary(Future), "30000000", "\"True\"")];
        yield return ["overflow-active", Active(Binary(Future), "30000000", "256")];
        yield return ["negative-active", Active(Binary(Future), "30000000", "-1")];
        yield return ["missing-period", Active(null, "30000000")];
        yield return ["empty-period", Active("", "30000000")];
        yield return ["bad-period", Active("not-a-date", "30000000")];
        yield return ["overflow-period", Active("9223372036854775808", "30000000")];
        yield return ["outside-datetime-domain", Active("3155378976000000000", "30000000")];
        // These have a genuine future deadline: validation cannot wait until expiry to notice corrupt donation.
        yield return ["future-missing-donation", Active(Binary(Future), null)];
        yield return ["future-empty-donation", Active(Binary(Future), "")];
        yield return ["future-negative-donation", Active(Binary(Future), "-1")];
        yield return ["future-overflow-donation", Active(Binary(Future), "18446744073709551616")];
        yield return ["future-bad-donation", Active(Binary(Future), "bad")];
    }

    private sealed class PaidFile : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-paid-input-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(root, "Users");
        internal string Pathname => Path.Combine(Users, "9001.ini");
        internal string OtherPath => Path.Combine(Users, "9002.ini");
        internal byte[] Original { get; }
        private readonly byte[] other;
        internal CharacterSnapshotLoader Loader { get; }
        private readonly string[] originalFiles;

        internal PaidFile(string fields)
        {
            Directory.CreateDirectory(Users);
            Original = Encoding.UTF8.GetBytes(CharacterWithPaid(fields));
            other = SqlWorldStateTestData.Character(9002);
            File.WriteAllBytes(Pathname, Original); File.WriteAllBytes(OtherPath, other);
            originalFiles = Directory.GetFiles(Users).Order(StringComparer.Ordinal).ToArray();
            Loader = new CharacterSnapshotLoader(Users, null, null, null, null, null);
        }

        internal void AssertIntact()
        {
            Assert.Equal(Original, File.ReadAllBytes(Pathname)); Assert.Equal(other, File.ReadAllBytes(OtherPath));
            Assert.Equal(originalFiles, Directory.GetFiles(Users).Order(StringComparer.Ordinal).ToArray());
        }

        public void Dispose()
        {
            var absolute = Path.GetFullPath(root);
            var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(absolute), temporary, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(absolute).StartsWith("wolf-paid-input-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the owned paid-input fixture.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, recursive: true);
        }
    }
}
