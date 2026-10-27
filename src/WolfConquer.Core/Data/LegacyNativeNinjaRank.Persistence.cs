#nullable disable
using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    internal const long MaximumPersistenceBytes = 1024L * 1024;
    internal const int MaximumPersistenceRows = 6 * 10;
    internal const int MaximumPersistenceLineCharacters = 4096;
    private const string PersistenceFileName = "NinjaRank.txt";

    public static void Load()
    {
        var location = CapturePersistenceLocation();
        RejectPersistenceGraphGateReentry();
        SnapshotFileLockCoordinator.ExecuteLocked(location.Path, () =>
        {
            LoadLocked(location);
            return true;
        });
    }

    private static void LoadLocked(PersistenceLocation location)
    {
        RequirePersistenceLocationCurrent(location);
        var owner = Ranks ?? throw PersistenceOwnerChanged();
        var ownerGate = owner.SyncRoot ?? throw PersistenceOwnerChanged();
        var culture = CaptureCulture();
        using var input = ReadPersistenceInput(location.Path);
        var replacement = ParsePersistenceGraph(input.Lines, culture);
        lock (ownerGate)
        {
            if (!ReferenceEquals(Ranks, owner) || !ReferenceEquals(owner.SyncRoot, ownerGate))
                throw PersistenceOwnerChanged();
            RequirePersistenceLocationCurrent(location);
            Ranks = replacement;
        }
    }

    public static void Save()
    {
        var location = CapturePersistenceLocation();
        RejectPersistenceGraphGateReentry();
        SnapshotFileLockCoordinator.ExecuteLocked(location.Path, () =>
        {
            RequirePersistenceLocationCurrent(location);
            var image = CapturePersistenceImage(CaptureCulture());
            RequirePersistenceLocationCurrent(location);
            WritePersistenceImage(location, image);
            return true;
        });
    }

    private static CultureInfo CaptureCulture() =>
        CultureInfo.ReadOnly((CultureInfo)CultureInfo.CurrentCulture.Clone());

    private static PersistenceLocation CapturePersistenceLocation()
    {
        var prefix = LegacyNativeServerConfig.DbLocation;
        return new(prefix, prefix + PersistenceFileName);
    }

    private static void RequirePersistenceLocationCurrent(PersistenceLocation location)
    {
        if (!string.Equals(LegacyNativeServerConfig.DbLocation, location.Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Ninja rank persistence location changed during the operation.");
    }

    private static void RejectPersistenceGraphGateReentry()
    {
        var registry = Ranks ?? throw PersistenceOwnerChanged();
        var registryGate = registry.SyncRoot ?? throw PersistenceOwnerChanged();
        if (Monitor.IsEntered(registryGate)) throw PersistenceGateOrderFailure();
        for (var index = 0; index < (int)Type.Count; index++)
            if (registry.TryGetValue((Type)index, out var rank) && rank?.SynRoot is { } gate && Monitor.IsEntered(gate))
                throw PersistenceGateOrderFailure();
    }

    private static InvalidOperationException PersistenceGateOrderFailure() =>
        new("Ninja rank persistence cannot begin while the caller holds a ranking graph gate.");

    private static InvalidOperationException PersistenceOwnerChanged() =>
        new("Ninja rank persistence requires a complete current registry owner.");

    private sealed record PersistenceLocation(string Prefix, string Path);
}
