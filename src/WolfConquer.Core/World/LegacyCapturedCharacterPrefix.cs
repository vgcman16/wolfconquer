using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.World;

/// <summary>Immutable captured storage DATA; this does not grant native ownership or publication.</summary>
internal sealed partial class LegacyCapturedCharacterPrefix
{
    private readonly CharacterSnapshot snapshot;
    private readonly byte[] onlineBytes;

    private LegacyCapturedCharacterPrefix(SqlWorldStateImage image, CharacterSnapshot snapshot,
        LegacyNativePrefixValues savedPlayerValues, bool beastActivated, uint beastTotalPoints,
        uint beastFlag, string hairfaceLine, byte[] onlineBytes)
    {
        Image = image;
        this.snapshot = snapshot;
        SavedPlayerValues = savedPlayerValues;
        BeastActivated = beastActivated;
        BeastTotalPoints = beastTotalPoints;
        BeastFlag = beastFlag;
        HairfaceLine = hairfaceLine;
        this.onlineBytes = onlineBytes;
    }

    internal SqlWorldStateImage Image { get; }
    internal CharacterSnapshot Snapshot => CopySnapshot(snapshot);
    internal LegacyNativePrefixValues SavedPlayerValues { get; }
    internal bool BeastActivated { get; }
    internal uint BeastTotalPoints { get; }
    internal uint BeastFlag { get; }
    internal string HairfaceLine { get; }
    internal byte[] CopyOnlineCharacterBytes() => onlineBytes.ToArray();

    internal static bool TryCreate(SqlWorldStateImage? image, out LegacyCapturedCharacterPrefix? captured, out string reason)
    {
        captured = null;
        reason = "capturedPrefixImageMissing";
        if (image is null) return false;
        try
        {
            if (!CharacterSnapshotFileBaseline.TryCaptureBytes(image.CharacterBytes, "capturedPrefix", out var baseline, out reason) || baseline is null)
                return false;
            if (!TryParseCaptured(image, baseline, out var snapshot, out var values, out var beastActivated,
                    out var beastTotalPoints, out var beastFlag, out var hairfaceLine, out reason)) return false;
            if (!TryPrepareOnlineBytes(baseline, out var onlineBytes, out reason)) return false;
            captured = new(image, CopySnapshot(snapshot!), values!, beastActivated, beastTotalPoints, beastFlag, hairfaceLine!, onlineBytes);
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is FormatException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        { reason = "capturedPrefixInvalidData"; return false; }
    }

    private static CharacterSnapshot CopySnapshot(CharacterSnapshot value) => value with
    {
        Attributes = new Dictionary<string, string>(value.Attributes, StringComparer.OrdinalIgnoreCase),
        PaidNobility = new Dictionary<string, string>(value.PaidNobility, StringComparer.OrdinalIgnoreCase),
    };
}
