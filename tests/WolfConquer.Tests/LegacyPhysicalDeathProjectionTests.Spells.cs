using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPhysicalDeathProjectionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NoSpellChangesSupportRealAbsenceAndPreserveEveryExistingRawByte(bool present)
    {
        byte[]? before = present ? PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(
            [new(5000, 3, 40, 2, 1, 1), new(1000, 1, 7, 0, 0, 0)])) : null;
        if (before is not null) { before[15] = 0xA7; before[27] = 0x5C; }
        var expected = before?.ToArray() ?? [0, 0, 0, 0];
        Assert.Equal(expected, Denied().ProjectSpells(before, Character()));
        Assert.Equal(expected, present ? before : [0, 0, 0, 0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ValidOverallOrWeeklyTenthRankCreatesTheExactExistingLevelOneRow(bool weeklyOverridesOverall)
    {
        var rows = weeklyOverridesOverall ? new[] { Rank(Uid, 42, 999) }.Concat(Tenth(40)).ToArray() : Tenth(42);
        var operation = Ranked(rows);
        var after = operation.ProjectSpells(null, Character(7051));
        // Independent complete12-byte row: count1, ID21930, level1, all other
        // fields including opaque byte11 are0. No serializer constructs this.
        Assert.Equal(Convert.FromHexString("01000000AA5501000000000000000000"), after);
    }

    [Fact]
    public void UnmarkedExistingDerivedRowRetainsEveryProgressionAndOpaqueByte()
    {
        var before = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(
            [new(5000, 3, 40, 2, 1, 1), new(21_930, 7, 99, 2, 1, 1)]));
        before[15] = 0xA7; before[27] = 0x5C;
        var expected = before.ToArray();
        var operation = new LegacyPhysicalDeathOperation(Uid, true, false, EmptyItems, Tenth(42), [], []);
        var after = operation.ProjectSpells(before, Character(7051));
        Assert.Equal(expected, after);
        Assert.Equal((byte)0x5C, after[27]); Assert.Equal((byte)0xA7, after[15]);
    }

    [Fact]
    public void RecordedOwnedRowChangesOnlyThroughTheRecordedDerivedReconciliation()
    {
        // Recorded DATA only, not an issued live grant or journal owner.
        var before = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(
            [new(5000, 3, 40, 2, 1, 1), new(21_930, 7, 0, 0, 0, 0)]));
        before[15] = 0xA7;
        var expected = before.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(18, 2), 1);
        var character = Character(7051) with { Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { [LegacyDerivedSpellOrigins.Key] = "21930:7" } };
        Assert.Equal(expected, Ranked(Tenth(42)).ProjectSpells(before, character));
    }

    [Theory]
    [InlineData("xp")] [InlineData("soul")] [InlineData("duplicate")]
    public void InvalidSavedMembershipNeverBecomesAProjectedSpellBook(string invalid)
    {
        var entries = invalid switch
        {
            "xp" => new PlayerSpellSnapshotEntry[] { new(5000, 1, -1, 0, 0, 0) },
            "soul" => [new(5000, 1, 0, 0, 0, 5)],
            _ => [new(5000, 1, 0, 0, 0, 0), new(5000, 1, 0, 0, 0, 0)],
        };
        var bytes = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(entries));
        Assert.Throws<InvalidDataException>(() => Denied().ProjectSpells(bytes, Character()));
    }

    [Fact]
    public void RecordedRankIdentityAndRequiredSetRowMustMatchTheActualRecipe()
    {
        var wrong = Tenth(42); wrong[^1] = wrong[^1] with { Fields = ["42", "RankData", "42", "1"] };
        Assert.Throws<InvalidDataException>(() => Ranked(wrong));
        Assert.Throws<InvalidDataException>(() => new LegacyPhysicalDeathOperation(Uid, true, false, EmptyItems, Tenth(42),
            [new WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange(21_930, new(21_930, 0, 0, 0, 0, 0))], [RankOneRow()]));
    }
}
