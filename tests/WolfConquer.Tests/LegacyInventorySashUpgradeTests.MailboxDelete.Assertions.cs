using System.Buffers.Binary;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static byte[] MailboxDeleteRequest(bool batch) => new LegacyMailboxModePacket(
        batch ? LegacyMailboxModeType.DeleteClaim : LegacyMailboxModeType.Delete,
        batch ? 777U : AtomicMailId,
        batch ? [AtomicMailId, 22UL, AtomicMailId, 999_999UL, (ulong)uint.MaxValue + 1 + AtomicMailId, 23UL, 23UL] : [])
        .ToFrameBytes();

    private static Dictionary<string, byte[]> ExpectedMailboxDeleteFiles(Fixture fixture, Snapshot before, bool batch)
    {
        var expected = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        var path = Path.GetRelativePath(fixture.Root, fixture.AtomicMailPath);
        var original = before.Files[path];
        var count = BinaryPrimitives.ReadInt32LittleEndian(original);
        Assert.Equal(batch ? 3 : 2, count);
        Assert.Equal(4 + count * 740, original.Length);
        Assert.Equal(AtomicMailId, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(4 + 704)));
        Assert.Equal(22U, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(4 + 740 + 704)));
        if (batch)
        {
            Assert.Equal(23U, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(4 + 2 * 740 + 704)));
            Assert.Equal((byte)0x5a, original[4 + 740 + 63]);
        }
        // Both requested empty rows surround the retained second row. Copy its
        // complete original 740 bytes, including valid bytes after string NULs.
        var retained = new byte[4 + 740];
        BinaryPrimitives.WriteInt32LittleEndian(retained, 1);
        original.AsSpan(4 + 740, 740).CopyTo(retained.AsSpan(4));
        expected[path] = retained;
        return expected; // Character, all seven item stores and every unrelated file are byte-identical.
    }

    private static void AssertMailboxDeleteCommitted(Fixture fixture, Snapshot before, bool batch)
    {
        fixture.AssertFiles(ExpectedMailboxDeleteFiles(fixture, before, batch));
        var retained = Assert.Single(new LegacyMailboxStore(fixture.Root).Load(Fixture.Uid));
        Assert.Equal(22U, retained.Id);
        Assert.Equal("Unselected sentinel", retained.Title);
        Assert.Equal(37UL, retained.Money);
        Assert.Equal(11U, retained.EMoney);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static void AssertMailboxDeleteFrames(Fixture fixture, Snapshot before, bool batch)
    {
        var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(batch ? 2 : 1, frames.Length);
        foreach (var frame in frames) Assert.Equal(MailboxDeleteRequest(batch), frame);
    }

    private static async Task AssertMailboxDeleteReplayAsync(Fixture fixture, Snapshot before, bool batch)
    {
        var first = fixture.Sent.Count;
        await fixture.FrameAsync(MailboxDeleteRequest(batch));
        AssertMailboxDeleteCommitted(fixture, before, batch);
        Assert.Equal(first, fixture.Sent.Count); // Removed IDs, duplicates, nonempty and missing rows produce no echo.
    }
}
