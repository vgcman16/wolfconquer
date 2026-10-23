using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSpellSectorTests
{
    private static FieldInfo Slot(string name) => typeof(LegacyNativeSpellSector)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static int[] Snapshot(LegacyNativeSpellSector sector) =>
        new[] { "attackerX", "attackerY", "attackX", "attackY", "degree", "sectorsize", "leftside", "rightside", "distance" }
            .Select(name => (int)Slot(name).GetValue(sector)!)
            .Append((bool)Slot("addextra").GetValue(sector)! ? 1 : 0).ToArray();

    [Fact]
    public void WholeNativeOwnerPreservesTenPrivateMutableSlotsAndUnarrangedDefaults()
    {
        var sector = new LegacyNativeSpellSector(10, 20, 11, 20);
        var fields = typeof(LegacyNativeSpellSector).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "attackerX", "attackerY", "attackX", "attackY", "degree", "sectorsize", "leftside", "rightside", "distance", "addextra" },
            fields.Select(field => field.Name));
        Assert.All(fields, field => { Assert.True(field.IsPrivate); Assert.False(field.IsInitOnly); });
        Assert.Equal(Enumerable.Repeat(typeof(int), 9).Append(typeof(bool)), fields.Select(field => field.FieldType));
        Assert.Equal(new[] { 10, 20, 11, 20, 360, 0, 0, 0, 0, 0 }, Snapshot(sector));
        Assert.False(sector.Inside(10, 20));
    }

    [Fact]
    public void ConstructorKeepsRawIntCoordinatesButNarrowsEachDegreeOperandToUshort()
    {
        var sector = new LegacyNativeSpellSector(-1, 65536, 65536, -1);
        Assert.Equal(new[] { -1, 65536, 65536, -1, 225, 0, 0, 0, 0, 0 }, Snapshot(sector));
    }

    [Theory]
    [InlineData(90, 99, 315, 765, 14, true)]
    [InlineData(0, 14, 360, 360, 14, false)]
    [InlineData(-90, 14, 405, 315, 14, false)]
    [InlineData(1, 14, 360, 720, 14, true)]
    [InlineData(-1, 14, 360, 720, 14, true)]
    [InlineData(360, -1, 180, 900, -1, true)]
    [InlineData(int.MinValue, int.MinValue, 1073742184, -1073741464, int.MinValue, false)]
    public void ArrangePreservesNativeIntegerBoundsClampAndNonpositiveSectorRules(
        int size, int requestedDistance, int left, int right, int distance, bool extra)
    {
        var sector = new LegacyNativeSpellSector(0, 0, 1, 0);
        sector.Arrange(size, requestedDistance);
        Assert.Equal(size, Slot("sectorsize").GetValue(sector));
        Assert.Equal(left, Slot("leftside").GetValue(sector));
        Assert.Equal(right, Slot("rightside").GetValue(sector));
        Assert.Equal(distance, Slot("distance").GetValue(sector));
        Assert.Equal(extra, Slot("addextra").GetValue(sector));
        Assert.Equal(360, Slot("degree").GetValue(sector));
    }

    [Fact]
    public void RepeatedArrangeDoesNotResetTheSourceStickyAddextra()
    {
        var sector = new LegacyNativeSpellSector(0, 0, 1, 0);
        sector.Arrange(0, 14);
        Assert.True(sector.Inside(1, 0));
        sector.Arrange(90, 14);
        Assert.True(sector.Inside(1, 0));
        sector.Arrange(0, 14);
        Assert.Equal(new[] { 0, 0, 1, 0, 360, 0, 360, 360, 14, 1 }, Snapshot(sector));
        Assert.False(sector.Inside(1, 0));
    }
}
