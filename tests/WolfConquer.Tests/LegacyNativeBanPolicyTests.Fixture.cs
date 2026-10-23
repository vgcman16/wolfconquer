using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using IpBan = WolfConquer.Core.Data.LegacyNativeSystemBanned;
using AccountBan = WolfConquer.Core.Data.LegacyNativeSystemBannedAccount;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeBanPolicyTests
{
    // Owned component state only: no login, native client birth, worker, or socket.
    private sealed class State : IDisposable
    {
        private readonly LegacyNativeDbSafeDictionary<string, IpBan.Client> ips = IpBan.BannedPoll;
        private readonly LegacyNativeDbSafeDictionary<uint, AccountBan.Client> accounts = AccountBan.BannedPoll;
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;

        internal State()
        {
            IpBan.BannedPoll = new();
            AccountBan.BannedPoll = new();
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(83);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        public void Dispose()
        {
            IpBan.BannedPoll = ips;
            AccountBan.BannedPoll = accounts;
            LegacyNativePoolRandom.GetRandom = random;
            LegacyNativePoolRandom.ResetRandom = reset;
            CultureInfo.CurrentCulture = culture;
        }
    }

    private sealed class Files : IDisposable
    {
        private readonly string previous = LegacyNativeServerConfig.DbLocation;
        private readonly HashSet<string> owned = new(StringComparer.OrdinalIgnoreCase);
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-ban-" + Guid.NewGuid().ToString("N"));

        internal Files()
        {
            CheckChain(Path.GetTempPath());
            Directory.CreateDirectory(Root);
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
        }

        internal string PathFor(string file)
        {
            string full = Path.GetFullPath(Path.Combine(Root, file));
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full), Path.GetFullPath(Root)))
                throw new IOException("Not an owned ban file.");
            owned.Add(full);
            return full;
        }

        internal void Seed(string file, params string[] rows) =>
            File.WriteAllLines(PathFor(file), new[] { "Count=" + rows.Length }.Concat(rows));

        private static void CheckChain(string path)
        {
            for (string? cursor = Path.GetFullPath(path); cursor != null; cursor = Path.GetDirectoryName(cursor))
                if ((Directory.Exists(cursor) || File.Exists(cursor)) &&
                    (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked ban fixture path rejected.");
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previous;
            string full = Path.GetFullPath(Root);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full),
                    Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-ban-[0-9a-f]{32}$"))
                throw new IOException("Owned ban cleanup containment failed.");
            CheckChain(full);
            foreach (string entry in Directory.EnumerateFileSystemEntries(full))
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0 || !owned.Contains(entry))
                    throw new IOException("Unexpected or linked ban fixture child; preserving files.");
            foreach (string file in owned)
                if (File.Exists(file)) File.Delete(file);
            Directory.Delete(full, recursive: false);
        }
    }

    private static IpBan.Client Ip(string ip, uint hours = 0, long start = 0) =>
        new() { IP = ip, Hours = hours, StartBan = start };
    private static AccountBan.Client Account(uint uid, string? name = "Wolf", uint hours = 0, long start = 0, string? reason = "") =>
        new() { UID = uid, Name = name, Hours = hours, StartBan = start, Reason = reason };

    private static void AssertRemaining(string actual, DateTime expiry, DateTime before, DateTime after, bool account, string? reason = "")
    {
        string shape = account
            ? @"^(\d+) Days, (\d+) Hours and (\d+) Minutes\. \(Due to " + Regex.Escape(reason ?? "") + @"\)$"
            : @"^ (\d+) Days (\d+) Hours (\d+) Minutes$";
        Match match = Regex.Match(actual, shape);
        Assert.True(match.Success, actual);
        long minutes = long.Parse(match.Groups[1].Value) * 24 * 60 +
            long.Parse(match.Groups[2].Value) * 60 + long.Parse(match.Groups[3].Value);
        Assert.InRange(minutes, (long)Math.Floor((expiry - after).TotalMinutes),
            (long)Math.Floor((expiry - before).TotalMinutes));
    }
}
