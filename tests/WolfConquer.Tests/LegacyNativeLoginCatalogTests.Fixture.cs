using System.Collections.Concurrent;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativeRuneRank;
using Recharge = WolfConquer.Core.Data.LegacyNativeRechargeShop;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeLoginCatalogTests
{
    // Current genuine component owners only, not the original full client constructor.
    // No packet, eager ServerDatabase field, SQL provider, worker, socket or startup is used.
    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<uint, Ranking.Entry>? ranks = Ranking.RankingList;
        private readonly object gate = Ranking.SynRoot;
        private readonly ConcurrentDictionary<uint, LegacyNativePlayerRuntime.RechargeType> recharges = Recharge.RechargeAccounts;
        private readonly string db = LegacyNativeServerConfig.DbLocation;
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture, ui = CultureInfo.CurrentUICulture;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-login-catalog-" + Guid.NewGuid().ToString("N"));
        private readonly HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);

        internal Scope()
        {
            RejectLinks(Path.GetTempPath());
            if (Directory.Exists(Root) || File.Exists(Root)) throw new IOException("Fresh owned directory required.");
            Directory.CreateDirectory(Root);
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
            Ranking.RankingList = new();
            Ranking.SynRoot = new();
            Recharge.RechargeAccounts = new();
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        internal string Target(string name)
        {
            if (name is not ("RuneRanking.txt" or "RechargeShop.txt")) throw new IOException("Unknown owned filename.");
            string path = Path.GetFullPath(Path.Combine(Root, name));
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(path), Path.GetFullPath(Root)))
                throw new IOException("Owned file containment failed.");
            files.Add(path);
            return path;
        }

        internal void Rows(string name, params string[] rows) =>
            File.WriteAllLines(Target(name), new[] { "Count=" + rows.Length }.Concat(rows));

        internal LegacyNativeGameClientRuntime Client(uint uid = 17, int runeCount = 20, string name = "Hero")
        {
            var client = new LegacyNativeGameClientRuntime();
            client.Player.UID = uid;
            client.Player.Name = name;
            client.Rune = new LegacyNativeRuneItemStorage(client);
            // Native Rune.Add accepts these real position1, ID0 items without catalog checks.
            // Each contributes the literal native500; no private backing or score stub is used.
            for (uint i = 1; i <= runeCount; i++)
                Assert.True(client.Rune.Add(new LegacyNativeGameItemRuntime { Uid = i, Position = 1 }));
            return client;
        }

        private static void RejectLinks(string path)
        {
            for (string? cursor = Path.GetFullPath(path); cursor != null; cursor = Path.GetDirectoryName(cursor))
                if ((File.Exists(cursor) || Directory.Exists(cursor)) &&
                    (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked owned-fixture path rejected.");
        }

        public void Dispose()
        {
            Ranking.RankingList = ranks;
            Ranking.SynRoot = gate;
            Recharge.RechargeAccounts = recharges;
            LegacyNativeServerConfig.DbLocation = db;
            LegacyNativePoolRandom.GetRandom = random;
            LegacyNativePoolRandom.ResetRandom = reset;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = ui;
            string root = Path.GetFullPath(Root), temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string name = Path.GetFileName(root);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(root), temp) ||
                !name.StartsWith("wolf-native-login-catalog-", StringComparison.Ordinal) ||
                !Guid.TryParseExact(name["wolf-native-login-catalog-".Length..], "N", out _))
                throw new IOException("Owned cleanup root check failed.");
            RejectLinks(root);
            foreach (string child in Directory.EnumerateFileSystemEntries(root))
                if (!files.Contains(Path.GetFullPath(child)) || Directory.Exists(child) ||
                    (File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Unexpected child preserved in owned fixture.");
            foreach (string file in files)
                if (File.Exists(file)) File.Delete(file);
            Directory.Delete(root, recursive: false);
        }
    }

    private static Ranking.Entry Row(uint score, string name = "N") => new() { TotalPoints = score, Name = name };
}
