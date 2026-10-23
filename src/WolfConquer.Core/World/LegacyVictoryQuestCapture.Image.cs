using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyVictoryQuestCapture
{
    internal static bool TryParseImage(ReadOnlySpan<byte> image,
        out PlayerQuestSnapshot snapshot, out string reason)
    {
        snapshot = null!;
        reason = Prefix + "ImageLengthInvalid";
        if (image.Length < sizeof(int) || image.Length > MaximumImageBytes) return false;
        // Bound the only pre-walk allocation, then validate an owned image so
        // the caller cannot change count fields between our walk and Parse.
        var owned = image.ToArray();
        if (!TryWalkImage(owned, out reason)) return false;
        try
        {
            var parsed = PlayerQuestSnapshotLoader.Parse(owned);
            if (!TryCapture(parsed, out var captured, out reason)) return false;
            if (!captured.MatchesImage(owned))
            { reason = Prefix + "RoundTripChanged"; return false; }
            snapshot = parsed;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsCaptureFailure(error) || error is FormatException)
        { reason = Prefix + "ImageParse:" + error.GetType().Name; return false; }
    }

    private static bool TryWalkImage(ReadOnlySpan<byte> image, out string reason)
    {
        reason = Prefix + "QuestCountInvalid";
        var count = BinaryPrimitives.ReadInt32LittleEndian(image);
        var offset = sizeof(int);
        if (count < 0 || count > (image.Length - offset) / PlayerQuestSnapshotLoader.LegacyQuestHeaderSize)
            return false;
        var seen = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            reason = Prefix + "HeaderTruncated";
            if (offset > image.Length - PlayerQuestSnapshotLoader.LegacyQuestHeaderSize) return false;
            var header = image.Slice(offset, PlayerQuestSnapshotLoader.LegacyQuestHeaderSize);
            var uid = BinaryPrimitives.ReadUInt32LittleEndian(header);
            var status = (PlayerQuestStatus)BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            var intentions = BinaryPrimitives.ReadInt32LittleEndian(header[12..]);
            reason = Prefix + "QuestIdentityInvalid";
            if (uid == 0 || !seen.Add(uid)) return false;
            reason = Prefix + "StatusInvalid";
            if (!ValidStatus(status)) return false;
            offset += PlayerQuestSnapshotLoader.LegacyQuestHeaderSize;
            reason = Prefix + "IntentionCountInvalid";
            if (intentions < 0 || intentions > (image.Length - offset) / sizeof(uint)) return false;
            offset += intentions * sizeof(uint);
        }
        reason = Prefix + "ImageTailInvalid";
        if (offset != image.Length) return false;
        reason = string.Empty;
        return true;
    }
}
