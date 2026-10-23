using System.Buffers.Binary;

namespace WolfConquer.Core.Data;

public sealed partial class PlayerQuestSnapshotLoader
{
    // A structural walk has no quest/intentions allocations. Counts must be
    // representable by this very image, not by a separate gameplay-size cap.
    private static int ValidateStructure(ReadOnlySpan<byte> bytes, string sourceFile)
    {
        if (bytes.Length < sizeof(int))
            throw new FormatException($"Player quest snapshot '{sourceFile}' is missing the quest count.");

        var questCount = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (questCount < 0)
            throw new FormatException($"Player quest snapshot '{sourceFile}' has negative quest count {questCount}.");

        var offset = sizeof(int);
        if (questCount > (bytes.Length - offset) / LegacyQuestHeaderSize)
            throw new FormatException($"Player quest snapshot '{sourceFile}' is truncated.");

        for (var index = 0; index < questCount; index++)
        {
            EnsureAvailable(bytes, offset, LegacyQuestHeaderSize, sourceFile);
            var intentionCount = BinaryPrimitives.ReadInt32LittleEndian(
                bytes.Slice(offset + IntentionCountOffset, sizeof(int)));
            offset += LegacyQuestHeaderSize;
            offset += GetIntentionByteCount(bytes, offset, intentionCount, sourceFile);
        }

        if (offset != bytes.Length)
            throw new FormatException(
                $"Player quest snapshot '{sourceFile}' has {bytes.Length - offset} trailing bytes.");
        return questCount;
    }

    private static int GetIntentionByteCount(
        ReadOnlySpan<byte> bytes, int offset, int intentionCount, string sourceFile)
    {
        if (intentionCount < 0)
            throw new FormatException(
                $"Player quest snapshot '{sourceFile}' has negative intention count {intentionCount}.");

        EnsureAvailable(bytes, offset, 0, sourceFile);
        // Division proves multiplication and the resulting end offset fit the
        // actual span. Impossible positive counts are malformed, not overflow.
        if (intentionCount > (bytes.Length - offset) / sizeof(uint))
            throw new FormatException($"Player quest snapshot '{sourceFile}' is truncated.");
        return intentionCount * sizeof(uint);
    }
}
