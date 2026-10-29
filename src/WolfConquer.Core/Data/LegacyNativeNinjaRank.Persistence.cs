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
        var admitted = RejectPersistenceGraphGateReentry();
        SnapshotFileLockCoordinator.ExecuteLocked(location.Path, () =>
        {
            LoadLocked(location, admitted);
            return true;
        });
    }

    private static void LoadLocked(PersistenceLocation location, PersistenceRegistry admitted)
    {
        RequirePersistenceLocationCurrent(location);
        RequirePersistenceRegistryCurrent(admitted);
        var owner = admitted.Registry;
        var ownerGate = admitted.Gate;
        var culture = CaptureCulture();
        using var input = ReadPersistenceInput(location.Path);
        var replacement = ParsePersistenceGraph(input.Lines, culture);
        lock (ownerGate)
        {
            RequirePersistenceRegistryCurrent(admitted);
            RequirePersistenceLocationCurrent(location);
            // Public Ranks can be rebound without this old gate. Consume the
            // exact admitted reference atomically rather than check-then-assign.
            if (!ReferenceEquals(Interlocked.CompareExchange(ref Ranks, replacement, owner), owner))
                throw PersistenceOwnerChanged();
        }
    }

    public static void Save()
    {
        var location = CapturePersistenceLocation();
        var admitted = RejectPersistenceGraphGateReentry();
        SnapshotFileLockCoordinator.ExecuteLocked(location.Path, () =>
        {
            RequirePersistenceLocationCurrent(location);
            lock (admitted.Gate)
            {
                RequirePersistenceRegistryCurrent(admitted);
                var image = CapturePersistenceImage(admitted, CaptureCulture());
                RequirePersistenceLocationCurrent(location);
                WritePersistenceImage(location, image, admitted);
            }
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

    private static PersistenceRegistry RejectPersistenceGraphGateReentry()
    {
        var registry = Volatile.Read(ref Ranks) ?? throw PersistenceOwnerChanged();
        var registryGate = registry.SyncRoot ?? throw PersistenceOwnerChanged();
        var admitted = new PersistenceRegistry(registry, registryGate);
        if (Monitor.IsEntered(registryGate)) throw PersistenceGateOrderFailure();
        for (var index = 0; index < (int)Type.Count; index++)
            if (registry.TryGetValue((Type)index, out var rank) && rank?.SynRoot is { } gate && Monitor.IsEntered(gate))
                throw PersistenceGateOrderFailure();
        RequirePersistenceRegistryCurrent(admitted);
        return admitted;
    }

    private static void RequirePersistenceRegistryCurrent(PersistenceRegistry admitted)
    {
        if (!ReferenceEquals(Volatile.Read(ref Ranks), admitted.Registry) ||
            !ReferenceEquals(admitted.Registry.SyncRoot, admitted.Gate)) throw PersistenceOwnerChanged();
    }

    private static InvalidOperationException PersistenceGateOrderFailure() =>
        new("Ninja rank persistence cannot begin while the caller holds a ranking graph gate.");

    private static InvalidOperationException PersistenceOwnerChanged() =>
        new("Ninja rank persistence requires a complete current registry owner.");

    private sealed record PersistenceRegistry(LegacyNativeDbSafeDictionary<Type, Rank> Registry, object Gate);
    private sealed record PersistenceLocation(string Prefix, string Path);
}
