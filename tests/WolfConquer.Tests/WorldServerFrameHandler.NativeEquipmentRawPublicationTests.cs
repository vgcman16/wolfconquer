using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static ConcurrentDictionary<uint, LegacyNativeEquipmentRawItem> RawObservationItems(
        LegacyNativeEquipmentRuntime equipment) =>
        (ConcurrentDictionary<uint, LegacyNativeEquipmentRawItem>)typeof(LegacyNativeEquipmentRuntime)
            .GetField("clientItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(equipment)!;

    private static byte[] RawObservationImage(int rootCount, byte[][] rows,
        int perfectionCount = 0, byte[]? perfections = null)
    {
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
        writer.Write(rootCount);
        foreach (byte[] row in rows) writer.Write(row);
        writer.Write(perfectionCount);
        if (perfections is not null) writer.Write(perfections);
        return bytes.ToArray();
    }

    private static LegacyNativeEquipmentRuntime NewRawObservation(out LegacyNativeGameClientRuntime owner)
    {
        owner = new LegacyNativeGameClientRuntime();
        var equipment = new LegacyNativeEquipmentRuntime(owner);
        owner.RawEquipmentObservation = equipment;
        return equipment;
    }

    private static void AssertUnavailableRawWithoutPartialPublication(LegacyNativeEquipmentRuntime equipment)
    {
        var observed = equipment.ObserveRawLoadedPosition(4);
        Assert.False(observed.Available); Assert.False(observed.Found); Assert.Null(observed.Item);
        Assert.Empty(observed.RawEntries); Assert.NotNull(observed.UnavailableReason);
        Assert.Empty(RawObservationItems(equipment)); Assert.False(equipment.CanAuthorizeWeaponAdmission);
    }

    [Theory]
    [InlineData(100, 59, 101, 4, true)]
    [InlineData(101, 4, 0, 4, false)]
    [InlineData(0, 64, 100, 4, false)]
    [InlineData(0, 4, 100, 60, true)]
    [InlineData(0, 4, 0, 213, true)]
    [InlineData(100, 60, 0, 4, false)]
    public void RawEquipmentParentRouteNeverBorrowsTheLastChildLocation(
        int parentWarehouse, int parentPosition, int childWarehouse, int childPosition, bool selected)
    {
        var equipment = NewRawObservation(out var owner);
        byte[] bytes = RawObservationImage(1,
            [RawItem(10, 620_003, (ushort)parentPosition, (uint)parentWarehouse, 1),
             RawItem(20, 410_003, (ushort)childPosition, (uint)childWarehouse)]);
        byte[] original = bytes.ToArray();
        equipment.CaptureRawLoad(bytes);
        var observed = equipment.ObserveRawLoadedPosition((ushort)parentPosition);
        Assert.True(observed.Available); Assert.Equal(selected, observed.Found);
        if (selected)
        {
            var entry = Assert.Single(observed.RawEntries); Assert.Equal(10u, entry.Key);
            Assert.Same(entry.Value, observed.Item); Assert.Equal(10u, entry.Value.Uid);
            Assert.Equal((ushort)parentPosition, entry.Value.Position);
            var child = Assert.Single(entry.Value.Deposits); Assert.Equal(20u, child.Key);
            Assert.Equal(20u, child.Value.Uid); Assert.Equal((ushort)childPosition, child.Value.Position);
            Assert.NotSame(entry.Value, child.Value);
        }
        else { Assert.Null(observed.Item); Assert.Empty(observed.RawEntries); }
        Assert.Equal(original, bytes); Assert.Same(owner, equipment.Owner);
        Assert.Null(owner.Equipment); Assert.False(equipment.CanAuthorizeWeaponAdmission);
    }

    [Fact]
    public void CompleteRawGraphPreservesActualParentChildAliasesAndValidatedPerfectionTail()
    {
        var equipment = NewRawObservation(out var owner);
        var perfection = new byte[80]; BinaryPrimitives.WriteUInt32LittleEndian(perfection, 10);
        BinaryPrimitives.WriteUInt32LittleEndian(perfection.AsSpan(4), 9);
        byte[] bytes = RawObservationImage(2,
            [RawItem(10, 620_003, 4, depositCount: 2), RawItem(20, 410_003, 54),
             RawItem(21, 410_004, 0), RawItem(30, 620_439, 54)], 1, perfection);
        byte[] original = bytes.ToArray(); equipment.CaptureRawLoad(bytes);
        var first = equipment.ObserveRawLoadedPosition(4);
        var parent = Assert.IsType<LegacyNativeEquipmentRawItem>(first.Item);
        Assert.Equal(10u, parent.Uid); Assert.Equal(2, parent.Deposits.Count);
        var child = parent.Deposits[20]; Assert.NotSame(parent, child);
        var alternate = equipment.ObserveRawLoadedPosition(54);
        Assert.Equal(30u, Assert.IsType<LegacyNativeEquipmentRawItem>(alternate.Item).Uid);
        Assert.Same(parent, first.RawEntries.Single(entry => entry.Key == 10).Value);
        Assert.Same(parent, equipment.ObserveRawLoadedPosition(4).Item);
        Assert.Same(child, equipment.ObserveRawLoadedPosition(4).Item!.Deposits[20]);
        Assert.Equal(new uint[] { 10, 30 }, first.RawEntries.Select(entry => entry.Key).OrderBy(key => key));
        Assert.Equal(original, bytes); Assert.Same(owner.RawEquipmentObservation, equipment);
        Assert.False(equipment.CanAuthorizeWeaponAdmission); Assert.Null(owner.Equipment);
    }

    [Theory]
    [InlineData("late-root")]
    [InlineData("missing-postlude")]
    [InlineData("late-perfection")]
    [InlineData("trailing")]
    [InlineData("negative-perfection")]
    [InlineData("late-deposit")]
    [InlineData("nested-deposit")]
    [InlineData("filtered-truncated-deposit")]
    [InlineData("present-zero-bytes")]
    public void MalformedLateRawInputCannotPublishEvenPrivatePartialRows(string fault)
    {
        byte[] first = RawItem(10, 620_003, 4);
        byte[] bytes = fault switch
        {
            "late-root" => RawObservationImage(2, [first, RawItem(20, 620_439, 54)])[..^5],
            "missing-postlude" => RawItems(first)[..^4],
            "late-perfection" => RawObservationImage(1, [first], 1, new byte[79]),
            "trailing" => RawItems(first).Concat(new byte[] { 0xA5 }).ToArray(),
            "negative-perfection" => RawObservationImage(1, [first], -1),
            "late-deposit" => RawObservationImage(2,
                [first, RawItem(20, 620_439, 54, depositCount: 2), RawItem(30, 410_003, 0),
                 RawItem(31, 410_003, 0)[..^1]]),
            "nested-deposit" => RawObservationImage(1,
                [RawItem(10, 620_003, 4, depositCount: 1), RawItem(20, 410_003, 0, depositCount: 1)]),
            "filtered-truncated-deposit" => RawObservationImage(2,
                [first, RawItem(20, 205_901, 0, depositCount: 1), RawItem(30, 410_003, 0)[..^1]]),
            _ => Array.Empty<byte>(),
        };
        var equipment = NewRawObservation(out var owner); byte[] original = bytes.ToArray();
        var before = RawObservationItems(equipment);
        equipment.CaptureRawLoad(bytes);
        AssertUnavailableRawWithoutPartialPublication(equipment);
        Assert.Same(before, RawObservationItems(equipment)); Assert.Equal(original, bytes);
        Assert.Same(equipment, owner.RawEquipmentObservation); Assert.Null(owner.Equipment);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LateRuneEvidenceFailureCannotPublishPrefixOrReviveButFreshCorrectedObserverCanLoad(bool deposited)
    {
        byte[][] rows = deposited
            ? [RawItem(10, 620_003, 4), RawItem(20, 620_439, 54, depositCount: 1), RawItem(30, 4_036_109, 103)]
            : [RawItem(10, 620_003, 4), RawItem(20, 4_036_109, 103)];
        byte[] bytes = RawObservationImage(2, rows); byte[] original = bytes.ToArray();
        var equipment = NewRawObservation(out var owner); equipment.CaptureRawLoad(bytes);
        AssertUnavailableRawWithoutPartialPublication(equipment);
        string? reason = equipment.ObserveRawLoadedPosition(4).UnavailableReason;
        Assert.Contains("native rune item constructor", reason);
        byte[] corrected = RawItems(RawItem(10, 620_003, 4));
        equipment.CaptureRawLoad(corrected);
        AssertUnavailableRawWithoutPartialPublication(equipment);
        Assert.Equal(reason, equipment.ObserveRawLoadedPosition(4).UnavailableReason);
        var fresh = new LegacyNativeEquipmentRuntime(owner); owner.RawEquipmentObservation = fresh;
        fresh.CaptureRawLoad(corrected);
        Assert.True(fresh.ObserveRawLoadedPosition(4).Available); Assert.True(fresh.ObserveRawLoadedPosition(4).Found);
        Assert.False(fresh.CanAuthorizeWeaponAdmission); Assert.Equal(original, bytes);
    }

    [Fact]
    public void RepeatedRawLoadInvalidatesWithoutReplacingPublishedGenerationOrEscapedAliases()
    {
        var equipment = NewRawObservation(out _);
        equipment.CaptureRawLoad(RawObservationImage(1,
            [RawItem(10, 620_003, 4, depositCount: 1), RawItem(20, 410_003, 0)]));
        var prior = equipment.ObserveRawLoadedPosition(4);
        var parent = Assert.IsType<LegacyNativeEquipmentRawItem>(prior.Item); var child = parent.Deposits[20];
        var generation = RawObservationItems(equipment);
        equipment.CaptureRawLoad(RawItems(RawItem(30, 620_439, 54)));
        var observed = equipment.ObserveRawLoadedPosition(4);
        Assert.False(observed.Available); Assert.False(observed.Found); Assert.Null(observed.Item);
        Assert.Empty(observed.RawEntries); Assert.Equal("repeatedRawLoad", observed.UnavailableReason);
        Assert.Same(generation, RawObservationItems(equipment)); Assert.Same(parent, generation[10]);
        Assert.Same(child, parent.Deposits[20]); Assert.Same(parent, Assert.Single(prior.RawEntries).Value);
        equipment.CaptureRawLoad(null); equipment.InvalidateRawLoadObservation("laterReason");
        Assert.Equal("repeatedRawLoad", equipment.ObserveRawLoadedPosition(54).UnavailableReason);
        Assert.False(equipment.CanAuthorizeWeaponAdmission);
    }

    [Fact]
    public void MissingFileIsCompletedEmptyButRepeatedCaptureCannotTurnItIntoAnotherGeneration()
    {
        var equipment = NewRawObservation(out _);
        Assert.False(equipment.ObserveRawLoadedPosition(4).Available);
        Assert.Equal("rawLoadNotCompleted", equipment.ObserveRawLoadedPosition(4).UnavailableReason);
        equipment.CaptureRawLoad(null);
        var empty = equipment.ObserveRawLoadedPosition(4);
        Assert.True(empty.Available); Assert.False(empty.Found); Assert.Empty(empty.RawEntries);
        equipment.CaptureRawLoad(RawItems(RawItem(10, 620_003, 4)));
        AssertUnavailableRawWithoutPartialPublication(equipment);
        Assert.Equal("repeatedRawLoad", equipment.ObserveRawLoadedPosition(4).UnavailableReason);
    }

    [Fact]
    public void ExplicitInvalidationBeforeCaptureRemainsUnavailableWithoutPreparingAnEmptyFallback()
    {
        var equipment = NewRawObservation(out var owner);
        equipment.InvalidateRawLoadObservation("ownedUnsupportedMutation");
        equipment.CaptureRawLoad(RawItems(RawItem(10, 620_003, 4)));
        equipment.CaptureRawLoad(null); equipment.InvalidateRawLoadObservation("laterReason");
        AssertUnavailableRawWithoutPartialPublication(equipment);
        Assert.Equal("ownedUnsupportedMutation", equipment.ObserveRawLoadedPosition(4).UnavailableReason);
        Assert.Same(owner, equipment.Owner); Assert.Same(equipment, owner.RawEquipmentObservation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FilteredRootConsumesPhysicalChildrenAndRefusesUnresolvedContainerMigration(bool children)
    {
        var rows = children
            ? new[] { RawItem(10, 205_901, 4, depositCount: 1), RawItem(11, 410_003, 0), RawItem(20, 620_439, 54) }
            : new[] { RawItem(10, 205_901, 4), RawItem(20, 620_439, 54) };
        byte[] bytes = RawObservationImage(2, rows); byte[] original = bytes.ToArray();
        var equipment = NewRawObservation(out _); equipment.CaptureRawLoad(bytes);
        if (children) AssertUnavailableRawWithoutPartialPublication(equipment);
        else
        {
            var selected = equipment.ObserveRawLoadedPosition(54);
            Assert.True(selected.Available); Assert.True(selected.Found);
            Assert.Equal(20u, Assert.Single(selected.RawEntries).Key);
            Assert.False(equipment.ObserveRawLoadedPosition(4).Found);
        }
        Assert.Equal(original, bytes); Assert.False(equipment.CanAuthorizeWeaponAdmission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalUidFailuresApplyEvenToLateDepositsOrIgnoredLegacyRoots(bool zeroChild)
    {
        byte[] bytes = zeroChild
            ? RawObservationImage(2, [RawItem(10, 620_003, 4),
                RawItem(20, 620_439, 54, depositCount: 1), RawItem(0, 410_003, 0)])
            : RawObservationImage(2, [RawItem(10, 620_003, 4), RawItem(10, 205_901, 0)]);
        var equipment = NewRawObservation(out _); byte[] original = bytes.ToArray();
        equipment.CaptureRawLoad(bytes);
        AssertUnavailableRawWithoutPartialPublication(equipment); Assert.Equal(original, bytes);
    }

    [Fact]
    public void RawObservationRequiresItsActualClientOwnerWithoutInventingOne()
    {
        Assert.Throws<ArgumentNullException>(() => new LegacyNativeEquipmentRuntime(null!));
    }
}
