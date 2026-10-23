using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PlayerQuestSnapshotStructureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MissingCountRejectsAsFormat(int length)
    {
        var error = Assert.Throws<FormatException>(
            () => PlayerQuestSnapshotLoader.Parse(new byte[length], "short.bin"));
        Assert.Contains("missing the quest count", error.Message);
        Assert.Contains("short.bin", error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeQuestCountsRejectAsFormat(int count)
    {
        var error = Assert.Throws<FormatException>(
            () => PlayerQuestSnapshotLoader.Parse(HeaderOnly(count), "negative.bin"));
        Assert.Contains("negative quest count", error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeIntentionCountsRejectAsFormat(int count)
    {
        var error = Assert.Throws<FormatException>(
            () => PlayerQuestSnapshotLoader.Parse(OneHeader(count), "negative-intention.bin"));
        Assert.Contains("negative intention count", error.Message);
    }

    [Theory]
    [InlineData(536_870_912)]
    [InlineData(int.MaxValue)]
    public void UnrepresentableIntentionBodiesRejectAsFormatNotArithmeticOverflow(int count)
    {
        var error = Assert.Throws<FormatException>(
            () => PlayerQuestSnapshotLoader.Parse(OneHeader(count), "oversized-intention.bin"));
        Assert.Contains("truncated", error.Message);
        Assert.Contains("oversized-intention.bin", error.Message);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(27)]
    public void TruncatedValidRecordRejectsAsFormat(int length)
    {
        var firstRecord = Convert.FromHexString(
            "01000000D08800000000000040E20100020000000700000009000000");
        var truncated = firstRecord.AsSpan(0, length).ToArray();
        var error = Assert.Throws<FormatException>(
            () => PlayerQuestSnapshotLoader.Parse(truncated, "truncated.bin"));
        Assert.Contains("truncated", error.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(16)]
    public void TrailingBytesRejectBeforePublishingASnapshot(int extra)
    {
        var original = ValidImage();
        var image = new byte[original.Length + extra];
        original.CopyTo(image, 0);
        var error = Assert.Throws<FormatException>(
            () => PlayerQuestSnapshotLoader.Parse(image, "tail.bin"));
        Assert.Contains("trailing bytes", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImpossibleQuestCountDoesNotAllocateTheDeclaredRowArray(bool includeOneRealHeader)
    {
        // Both inputs are tiny; the old parser requests a real, finite ~512KiB
        // row array. This is an allocation regression control, not an OOM test.
        var image = includeOneRealHeader ? OneHeader(0) : HeaderOnly(65_536);
        BinaryPrimitives.WriteInt32LittleEndian(image, 65_536);
        var attempt = MeasureFailure(image);
        Assert.IsType<FormatException>(attempt.Error);
        Assert.InRange(attempt.Allocated, 0L, 64L * 1024);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedTailDoesNotMaterializeAnOtherwiseValidLargePrefix(bool missingLastBody)
    {
        const int prefixRows = 8_192;
        var rowCount = prefixRows + (missingLastBody ? 1 : 0);
        var image = new byte[sizeof(int) +
            rowCount * PlayerQuestSnapshotLoader.LegacyQuestHeaderSize + (missingLastBody ? 0 : 1)];
        BinaryPrimitives.WriteInt32LittleEndian(image, rowCount);
        for (var index = 0; index < rowCount; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(
                image.AsSpan(sizeof(int) + index * PlayerQuestSnapshotLoader.LegacyQuestHeaderSize), (uint)index + 1);
        if (missingLastBody)
            BinaryPrimitives.WriteInt32LittleEndian(
                image.AsSpan(sizeof(int) + prefixRows * PlayerQuestSnapshotLoader.LegacyQuestHeaderSize + 12), 1);

        var attempt = MeasureFailure(image);
        Assert.IsType<FormatException>(attempt.Error);
        Assert.InRange(attempt.Allocated, 0L, 64L * 1024);
    }
}
