using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeCapturedCharacterPrefix
{
    internal static bool TryPrepare(ConquerTcpConnection connection, LegacyCapturedCharacterPrefix? captured,
        out LegacyNativeCapturedCharacterPrefix? prepared, out string reason)
    {
        prepared = null; reason = "capturedNativePrefixImageMissing";
        if (captured is null) return false;
        reason = "capturedNativePrefixOwnerUnavailable";
        try
        {
            if (connection is null || connection.HasObservedTransportClose || connection.NativeGameClient is not { } client || client.Player is not { } player ||
                !ReferenceEquals(player.Owner, client) || client.FullLoading || client.Fake) return false;
            // Capture all actual native participants before any catalog access.
            var playerBefore = new LegacyNativeExistingPrefixBefore(client);
            var beastsBefore = new LegacyNativeBeastsRuntime.PrefixBefore(client);
            var hairfaceBefore = new LegacyNativeHairfaceStorageRuntime.PrefixBefore(client);
            var playerLoad = LegacyNativePlayerRuntime.ExistingPrefixLoad.TryPrepareCaptured(client, captured);
            if (playerLoad is null) { reason = "capturedNativePrefixPlayerUnavailable"; return false; }
            if (!LegacyNativeBeastsRuntime.PrefixLoad.TryPrepare(client, captured.BeastActivated, captured.BeastTotalPoints,
                    captured.BeastFlag, out var beasts))
            { reason = "capturedNativePrefixBeastsUnavailable"; return false; }
            if (!LegacyNativeHairfaceStorageRuntime.PrefixLoad.TryPrepare(client, captured.HairfaceLine, out var hairface))
            { reason = "capturedNativePrefixHairfaceUnavailable"; return false; }
            var snapshot = captured.Snapshot;
            var fields = new Dictionary<string, string>(snapshot.Attributes, StringComparer.OrdinalIgnoreCase) { ["Online"] = "1" };
            var candidate = new LegacyNativeCapturedCharacterPrefix(connection, client, player, captured,
                snapshot with { Online = 1, Attributes = fields }, playerLoad, beasts!, hairface!, playerBefore, beastsBefore, hairfaceBefore);
            if (!candidate.IsCurrent()) { reason = "capturedNativePrefixPreparationChanged"; return false; }
            prepared = candidate; reason = string.Empty; return true;
        }
        catch (OverflowException) { reason = "capturedNativePrefixOnlineOverflow"; return false; }
        catch (Exception error) when (error is FormatException or InvalidDataException or InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        { reason = "capturedNativePrefixInvalidData"; return false; }
    }
}
