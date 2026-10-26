using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    private static T AdmissionOwner<T>(WorldServerFrameHandler handler, string field) where T : class =>
        Assert.IsType<T>(typeof(WorldServerFrameHandler).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(handler)); // Observe the actual owner; no field injection.

    private static string AdmissionRoot(LoginInput input) => Path.GetDirectoryName(input.Users)!;

    private static void PrepareAdmissionInput(LoginInput input, bool starter = false)
    {
        foreach (var name in new[] { "PlayersItems", "PlayersSpells", "PlayersProfs", "Quests", "PlayersCombatGear", "PlayersMailList" })
            Directory.CreateDirectory(Path.Combine(AdmissionRoot(input), name));
        if (!starter) return;
        var text = Encoding.UTF8.GetString(input.InitialBytes)
            .Replace("Class=7051\r\n", "Class=1000\r\n", StringComparison.Ordinal)
            .Replace("SecoundeClass=7050\r\n", "SecoundeClass=0\r\n", StringComparison.Ordinal)
            .Replace("Level=140\r\n", "Level=1\r\n", StringComparison.Ordinal)
            .Replace("Reborn=2\r\n", "Reborn=0\r\n", StringComparison.Ordinal)
            .Replace("Agility=36\r\n", "Agility=0\r\n", StringComparison.Ordinal)
            .Replace("Vitaliti=447\r\n", "Vitaliti=0\r\n", StringComparison.Ordinal)
            .Replace("Atributes=12\r\n", "Atributes=0\r\n", StringComparison.Ordinal)
            .Replace("MinHitPoints=10836\r\n", "MinHitPoints=1\r\n", StringComparison.Ordinal)
            .Replace("MinMana=5000\r\n", "MinMana=0\r\n", StringComparison.Ordinal);
        File.WriteAllBytes(input.FilePath, Encoding.UTF8.GetBytes(text));
    }

    private static WorldDataCatalog AdmissionCatalog(Action? read = null)
    {
        var items = new[] { 132009, 420301, 150003 }.Select(id =>
        {
            var fields = Enumerable.Repeat("0", 60).ToArray();
            fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedStarter" + id;
            fields[ItemType.DurabilityFieldIndex] = "321";
            return new ItemType(id, fields[1], read is null ? fields : new ObservedAdmissionFields(fields, read));
        }).ToArray();
        return LegacyItemWorldDataFixture.Create(items, [], classLevelStats:
            [new ClassLevelStat("Trojan", 1, 5, 2, 3, 0, "owned", 1, "1", "5,2,3,0", "1=5,2,3,0", ["5", "2", "3", "0"])]);
    }

    private sealed class ObservedAdmissionFields(string[] values, Action read) : IReadOnlyList<string>
    {
        public int Count => values.Length;
        public string this[int index] { get { read(); return values[index]; } }
        public IEnumerator<string> GetEnumerator()
        {
            foreach (var value in values) { read(); yield return value; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static Dictionary<string, byte[]?> CaptureAdmissionFiles(LoginInput input)
    {
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        return roots.Kinds.ToDictionary(kind => roots.PathFor(kind, Uid), kind =>
        {
            var path = roots.PathFor(kind, Uid);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        });
    }

    private static void AssertAdmissionFiles(Dictionary<string, byte[]?> expected)
    {
        foreach (var (path, bytes) in expected)
            if (bytes is null) Assert.False(File.Exists(path), path);
            else Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    private sealed class AdmissionUidFixture : IDisposable
    {
        private readonly LoginInput input;
        internal LegacyItemUidRegistry Registry { get; private set; } = null!;
        internal LegacyOrdinaryAdmissionUidOwner Owner { get; private set; } = null!;
        internal string StatePath => Path.Combine(AdmissionRoot(input), "PlayersItems", ".managed-item-uids", "state.json");
        internal Guid Domain { get; private set; }
        internal AdmissionUidFixture(LoginInput input) { this.input = input; Open(bootstrap: true); }
        private LegacyItemUidRegistryOptions Options => new(input.Users, Path.Combine(AdmissionRoot(input), "PlayersItems"),
            Path.Combine(AdmissionRoot(input), "PlayersMailList"), Path.Combine(AdmissionRoot(input), "RedeemContainer.bin"),
            Path.Combine(AdmissionRoot(input), "ClaimContainer.bin"), 2_000_000, "owned synthetic quiescent fixture floor");
        internal LegacyItemUidRegistryOptions OptionsForReopen => Options;
        internal void Open(bool bootstrap)
        {
            var opened = bootstrap ? LegacyItemUidRegistry.TryBootstrap(Options, out var registry, out var reason)
                : LegacyItemUidRegistry.TryOpen(Options, out registry, out reason);
            Assert.True(opened, reason);
            Registry = Assert.IsType<LegacyItemUidRegistry>(registry);
            Domain = Guid.ParseExact(JsonNode.Parse(File.ReadAllBytes(StatePath))!["Domain"]!.GetValue<string>(), "N");
            Assert.True(LegacyOrdinaryAdmissionUidOwner.TryCreate(Registry, Domain,
                LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users)),
                Options.MailboxRoot, out var owner, out reason), reason);
            Owner = Assert.IsType<LegacyOrdinaryAdmissionUidOwner>(owner);
        }
        internal byte[] StateBytes() => File.ReadAllBytes(StatePath);
        public void Dispose() => Registry?.Dispose();
    }
}
