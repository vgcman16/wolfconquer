using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Complete, freshly authored smoke inputs. Only this offline test setup
// bootstraps empty issuance history; the real runtime then opens its lease.
internal sealed class OwnedSmokeWorldFixture : IAsyncDisposable
{
    private const string Evidence = "new smoke fixture; complete empty item issuance history";
    private readonly LegacyOwnedAdmissionRuntime runtime;
    private readonly string root;
    private readonly string ownerLease;
    internal CharacterSnapshotLoader Loader { get; }
    internal WorldServerFrameHandler Handler { get; }
    internal string QuestFile { get; }
    internal StringWriter Log { get; } = new();

    internal OwnedSmokeWorldFixture(string usersRoot, uint uid)
    {
        root = Path.GetDirectoryName(Path.GetFullPath(usersRoot))!;
        RequireOwnedRoot(root);
        var items = Path.Combine(root, "PlayersItems");
        ownerLease = Path.GetFullPath(Path.Combine(items, ".managed-item-uids", "owner.lock"));
        var spells = Path.Combine(root, "PlayersSpells");
        var profs = Path.Combine(root, "PlayersProfs");
        var quests = Path.Combine(root, "Quests");
        var gear = Path.Combine(root, "PlayersCombatGear");
        var mailbox = Path.Combine(root, "PlayersMailList");
        foreach (var directory in new[] { items, spells, profs, quests, gear, mailbox })
        {
            Assert.False(Directory.Exists(directory), "Fresh smoke participants must not overwrite retained state.");
            Directory.CreateDirectory(directory);
        }
        Assert.Single(Directory.GetFiles(usersRoot, "*.ini"));
        new PlayerItemSnapshotLoader(items).SaveByCharacterUid(uid, PlayerItemSnapshot.Empty);
        new PlayerSpellSnapshotLoader(spells).SaveByCharacterUid(uid, PlayerSpellSnapshot.Empty);
        new PlayerProficiencySnapshotLoader(profs).SaveByCharacterUid(uid, PlayerProficiencySnapshot.Empty);
        new PlayerQuestSnapshotLoader(quests).SaveByCharacterUid(uid, PlayerQuestSnapshot.Empty);
        new CombatGearSnapshotLoader(gear).SaveByCharacterUid(uid, CombatGearSnapshot.Empty);
        QuestFile = Path.Combine(quests, uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var redeem = Path.Combine(root, "RedeemContainer.bin");
        var claim = Path.Combine(root, "ClaimContainer.bin");
        var registryOptions = new LegacyItemUidRegistryOptions(usersRoot, items, mailbox, redeem, claim, 0, Evidence);
        Assert.True(LegacyItemUidRegistry.TryBootstrap(registryOptions, out var bootstrapped, out var reason), reason);
        Guid domain;
        using (var bootstrap = Assert.IsType<LegacyItemUidRegistry>(bootstrapped))
            domain = bootstrap.ReadSyntheticFixtureBootstrapDomain();
        var options = new LegacyOwnedAdmissionOptions(domain, usersRoot, items, spells, profs,
            quests, gear, mailbox, redeem, claim, 0, Evidence);
        Loader = options.CreateSnapshotLoader();
        Assert.Equal((long)uid, Loader.TryLoadByAccountIdForAdmission(uid).Snapshot?.Uid);
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Loader, options, out var opened, out reason), reason);
        runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        try
        {
            Handler = new(Loader, null, LegacyItemWorldDataFixture.Create([], []),
                LegacyRuntimeUpdateLoopOptions.Disabled, Log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
        }
        catch { runtime.Dispose(); throw; }
    }

    internal Dictionary<string, byte[]> Files()
    {
        var lease = new FileInfo(ownerLease);
        Assert.True(lease.Exists, "The genuine opened runtime must retain its exact registry lease file.");
        Assert.False((lease.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0,
            "The exact registry lease must be an ordinary file.");
        Assert.Equal(0L, lease.Length);
        var paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        Assert.Contains(ownerLease, paths);
        return paths.Where(path => !string.Equals(Path.GetFullPath(path), ownerLease, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);
    }

    internal void AssertFiles(Dictionary<string, byte[]> expected)
    {
        var actual = Files();
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    public async ValueTask DisposeAsync()
    {
        // The caller declares its actual server after this fixture, so server
        // connections drain first; release the genuine domain lease afterward.
        await runtime.DisposeAsync();
        Log.Dispose();
    }

    private static void RequireOwnedRoot(string path)
    {
        var name = Path.GetFileName(path);
        var prefix = new[] { "wolf-world-smoke-", "wolf-local-live-smoke-" }
            .SingleOrDefault(value => name.StartsWith(value, StringComparison.Ordinal));
        if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                StringComparison.OrdinalIgnoreCase) || prefix is null || !Guid.TryParseExact(name[prefix.Length..], "D", out _))
            throw new InvalidOperationException("Only a fresh owned smoke fixture root is supported.");
    }
}
