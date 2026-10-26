using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHouseRuntime
{
    private static readonly ConcurrentDictionary<string, string> IndeterminateSavePaths = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    internal static void Save(LegacyNativeGameClientRuntime client)
    {
        if (client is null) throw new InvalidOperationException("houseSaveClientMissing");
        lock (LoadGate)
        {
            if (client.MyHouse is null) return;
            var capture = CaptureSave(client);
            try
            {
                CharacterSnapshotFileCoordinator.ExecuteLocked(capture.Path, () =>
                {
                    SaveCapturedHouse(capture);
                    return true;
                });
            }
            catch (Exception error) when (error is UnauthorizedAccessException or ArgumentException or
                NotSupportedException or System.Security.SecurityException)
            { throw new IOException("houseSaveIoRefused", error); }
        }
    }

    private static void SaveCapturedHouse(HouseSaveCapture capture)
    {
        if (IndeterminateSavePaths.TryGetValue(capture.Path, out var held))
            throw new IOException("houseSaveRecoveryRequired:" + held);
        var existed = HouseSaveLeafExists(capture.Path);
        byte[] before = [];
        if (existed)
        {
            if (!CharacterSnapshotFileBaseline.TryReadBytes(capture.Path, "houseSave", out before, out var readReason))
                throw new IOException(readReason);
            if (!TryParseHouse(before, out _)) throw new InvalidOperationException("houseSaveExistingImageInvalid");
        }
        if (!capture.IsCurrent() || HouseSaveLeafExists(capture.Path) != existed)
            throw new InvalidOperationException("houseSaveOwnerGraphOrPathChanged");

        bool uncertain;
        string reason;
        var saved = existed
            ? CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(capture.Path, before, capture.Image, out uncertain, out reason)
            : CharacterSnapshotFileCoordinator.TryCreateCapturedImage(capture.Path, capture.Image, out uncertain, out reason);
        if (saved) return;
        if (uncertain)
        {
            IndeterminateSavePaths.TryAdd(capture.Path, reason);
            throw new IOException("houseSaveRecoveryRequired:" + reason);
        }
        throw new IOException("houseSaveNotCommitted:" + reason);
    }
}
