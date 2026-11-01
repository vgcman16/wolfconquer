using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBasicPhysicalProficiencyCalculationTests
{
    [Fact]
    public void ExistingAndMissingWeaponRowsPreserveOrderAndOpaqueBytes()
    {
        byte[] before = Book((410, 1, 0, 100, 1200, 0xA1, 0xB2, 0xC3), (500, 3, 2, 99, 250000, 0xD4, 0xE5, 0xF6));
        var input = PlayerProficiencySnapshotLoader.Parse(before);
        var calculation = new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(input, [410, 421], 1);
        Assert.Equal(1000U, calculation.RatedExperience);
        Assert.Equal(2, calculation.Frames.Count);
        byte[] expected = Book((410, 2, 0, 48900, 68000, 0xA1, 0xB2, 0xC3),
            (500, 3, 2, 99, 250000, 0xD4, 0xE5, 0xF6), (421, 0, 0, 0, 0, 0, 0, 0));
        Assert.Equal(expected, LegacyBasicPhysicalProficiencyImage.Project(before, calculation));
        Assert.Equal(new uint[] { 410, 500, 421 }, calculation.After.Proficiencies.Select(row => row.Id));
        Assert.Equal(100U, input.Proficiencies[0].Experience);
    }

    [Fact]
    public void RateSaturatesButTheFullWidenedLevelZeroGainReachesTheNativeCap()
    {
        var before = PlayerProficiencySnapshotLoader.Parse(Book((410, 0, 0, 0, 0, 1, 2, 3)));
        var calculation = new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(before, [410], uint.MaxValue);
        Assert.Equal(uint.MaxValue, calculation.RatedExperience);
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 20, 0, 0, 3_200_000_000),
            Assert.Single(calculation.After.Proficiencies));
        Assert.Equal(Book((410, 20, 0, 0, 3_200_000_000, 1, 2, 3)),
            LegacyBasicPhysicalProficiencyImage.Project(Book((410, 0, 0, 0, 0, 1, 2, 3)), calculation));
        Assert.Equal(new byte[] { 20, 0, 0xF9, 0x07, 0x9A, 1, 0, 0, 20, 0, 0, 0,
            0, 0, 0, 0, 0, 0x20, 0xBC, 0xBE }, Assert.Single(calculation.Frames).ToArray());
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 0, 0, 0, 0), Assert.Single(before.Proficiencies));
    }

    [Theory]
    [InlineData(0, true)] [InlineData(1, false)]
    public void NeutralCalculationRetainsExactInputImage(uint damage, bool selected)
    {
        byte[] bytes = Book((410, 1, 0, 100, 1200, 0xA1, 0xB2, 0xC3));
        var calculation = new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(PlayerProficiencySnapshotLoader.Parse(bytes),
            selected ? new uint[] { 410 } : [], damage);
        Assert.False(calculation.HasChanges); Assert.Empty(calculation.Frames);
        Assert.Equal(bytes, LegacyBasicPhysicalProficiencyImage.Project(bytes, calculation));
    }

    [Fact]
    public void RejectsDuplicateOrAlienSelectionAndMismatchedOrMalformedImage()
    {
        byte[] bytes = Book((410, 1, 0, 100, 1200, 1, 2, 3));
        var before = PlayerProficiencySnapshotLoader.Parse(bytes);
        Assert.Throws<InvalidDataException>(() => new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(before, [410, 410], 1));
        Assert.Throws<InvalidDataException>(() => new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(before, [999999], 1));
        var calculation = new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(before, [410], 1);
        byte[] mismatch = bytes.ToArray(); mismatch[16] = 101;
        Assert.Throws<InvalidDataException>(() => LegacyBasicPhysicalProficiencyImage.Project(mismatch, calculation));
        Assert.Throws<FormatException>(() => LegacyBasicPhysicalProficiencyImage.Project(bytes[..^1], calculation));
        Assert.Throws<InvalidOperationException>(() => new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(
            PlayerProficiencySnapshotLoader.Parse(Book((410, 1, 0, 100, 1200, 0, 0, 0), (410, 1, 0, 100, 1200, 0, 0, 0))), [410], 1));
    }

    [Fact]
    public void SelectorUsesRealEquipmentOrderAlternateOverrideAndDeduplicatesHands()
    {
        var items = new PlayerItemSnapshot([], [], [],
        [
            new() { Uid = 11, ItemId = 410001, Position = 4, Durability = 10 },
            new() { Uid = 12, ItemId = 410002, Position = 5, Durability = 10 },
            new() { Uid = 13, ItemId = 421001, Position = 54, Durability = 10 },
            new() { Uid = 14, ItemId = 500001, Position = 55, Durability = 0 }
        ], []);
        Assert.Equal(new uint[] { 410 }, WorldPacketHandlers.GetLegacyCurrentWeaponProficiencyIds(items, false));
        Assert.Equal(new uint[] { 421 }, WorldPacketHandlers.GetLegacyCurrentWeaponProficiencyIds(items, true));
    }

    [Fact]
    public void SelectorIgnoresUnsupportedAndBrokenWeaponsAndRejectsOutOfRangeType()
    {
        var items = new PlayerItemSnapshot([], [], [],
        [
            new() { Uid = 11, ItemId = 999001, Position = 4, Durability = 10 },
            new() { Uid = 12, ItemId = 500001, Position = 5, Durability = 0 }
        ], []);
        Assert.Empty(WorldPacketHandlers.GetLegacyCurrentWeaponProficiencyIds(items, false));
        var overflow = new PlayerItemSnapshot([], [], [],
            [new() { Uid = 13, ItemId = uint.MaxValue, Position = 4, Durability = 1 }], []);
        Assert.Throws<OverflowException>(() => WorldPacketHandlers.GetLegacyCurrentWeaponProficiencyIds(overflow, false));
    }
    [Fact]
    public void OversizedSelectionRefusesBeforeReadingOrEnumeratingRows()
    {
        Assert.Throws<InvalidDataException>(() => new WorldPacketHandlers.BasicPhysicalProficiencyCalculation(
            PlayerProficiencySnapshot.Empty, new OversizedWeaponList(), 1));
    }

    private sealed class OversizedWeaponList : IReadOnlyList<uint>
    {
        public int Count => 3;
        public uint this[int index] => throw new InvalidOperationException("oversized list was read");
        public IEnumerator<uint> GetEnumerator() => throw new InvalidOperationException("oversized list was enumerated");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private static byte[] Book(params (uint Id, uint Level, byte Previous, uint Xp, uint Needed, byte A, byte B, byte C)[] rows)
    {
        var bytes = new byte[4 + rows.Length * 20]; BinaryPrimitives.WriteInt32LittleEndian(bytes, rows.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            var span = bytes.AsSpan(4 + i * 20, 20); var row = rows[i];
            BinaryPrimitives.WriteUInt32LittleEndian(span, row.Id); BinaryPrimitives.WriteUInt32LittleEndian(span[4..], row.Level);
            span[8] = row.Previous; span[9] = row.A; span[10] = row.B; span[11] = row.C;
            BinaryPrimitives.WriteUInt32LittleEndian(span[12..], row.Xp); BinaryPrimitives.WriteUInt32LittleEndian(span[16..], row.Needed);
        }
        return bytes;
    }
}
