using System.Collections.Concurrent;
using System.Globalization;
using Storage = WolfConquer.Core.World.LegacyNativeAnimaSkinRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAnimaSkinTests
{
    [Fact]
    public Task ConstructorKeepsPhysicalUserFreshDictionaryAndUnchangedLoadFlag() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var other = new Storage(scope.Client);
        Assert.Same(scope.Client, scope.Storage.user);
        Assert.NotSame(scope.Storage.Items, other.Items);
        Assert.Empty(scope.Storage.Items);
        Assert.Equal(0u, scope.Storage.isEquip);
        Assert.False(scope.Storage.Load);
        RowIs(new Storage.Item(), 0, false);
        Assert.Equal("0/0/", scope.Storage.ToString());
        Assert.Null(scope.Client.MyAnimaSkin);
        Assert.Empty(scope.Queue);
        Assert.Equal(3, scope.Available);
    });

    [Fact]
    public void AddSkinRejectsZeroAndExistingKeyWithoutChangingPhysicalValueOrEquipFlag()
    {
        using var scope = new Records();
        scope.Storage.AddSkin(0, true);
        Assert.Empty(scope.Storage.Items);
        var old = scope.Row(17, 777, false);
        scope.Storage.AddSkin(17, true);
        Assert.Same(old, Assert.Single(scope.Storage.Items).Value);
        RowIs(old, 777, false);
        Assert.Equal(0u, scope.Storage.isEquip);
    }

    [Fact]
    public void AddSkinAcceptsFullUnsignedIdAndDoesNotUnequipOtherRows()
    {
        using var scope = new Records();
        scope.Storage.AddSkin(17, true);
        scope.Storage.AddSkin(uint.MaxValue, true);
        RowIs(scope.Storage.Items[17], 17, true);
        RowIs(scope.Storage.Items[uint.MaxValue], uint.MaxValue, true);
        uint first = scope.Storage.Items.Values.First(row => row.isEquip).ID;
        Assert.Equal(first, scope.Storage.isEquip);
        Assert.False(scope.Storage.Load);
    }

    [Fact]
    public void EquippedGetterUsesFirstEquippedPhysicalValueIncludingZeroId()
    {
        using var scope = new Records();
        scope.Row(100, 17, false);
        scope.Row(200, 19, false);
        var rows = scope.Storage.Items.Values.ToArray();
        rows[0].isEquip = true; rows[0].ID = 0;
        rows[1].isEquip = true; rows[1].ID = 777;
        Assert.Equal(0u, scope.Storage.isEquip);
        rows[0].isEquip = false;
        Assert.Equal(777u, scope.Storage.isEquip);
        Assert.False(scope.Storage.Items.ContainsKey(777));
    }

    [Fact]
    public void MissingRemoveDoesNotEnumerateBrokenOtherValuesOrTouchFlags()
    {
        using var scope = new Records();
        scope.Storage.Items[17] = null!;
        scope.Storage.RemoveSkin(19);
        Assert.Null(Assert.Single(scope.Storage.Items).Value);
    }

    [Fact]
    public void RemoveSkinRemovesRequestedKeyButEmptyBranchDoesNotUnequipRemainingSkin()
    {
        using var scope = new Records();
        var removed = scope.Row(17, 777, true);
        var retained = scope.Row(19, 999, true);
        scope.Storage.Load = true;
        scope.Storage.RemoveSkin(17);
        Assert.False(scope.Storage.Items.ContainsKey(17));
        Assert.Same(retained, Assert.Single(scope.Storage.Items).Value);
        RowIs(retained, 999, true);
        RowIs(removed, 777, true);
        Assert.Equal(999u, scope.Storage.isEquip);
        Assert.True(scope.Storage.Load);
        scope.Storage.RemoveSkin(19);
        Assert.Empty(scope.Storage.Items);
        Assert.Equal(0u, scope.Storage.isEquip);
    }

    [Fact]
    public void RemoveSkinPublishesRemovalBeforeRemainingNullValueMakesEquippedScanFault()
    {
        using var scope = new Records();
        var removed = scope.Row(17, 777, true);
        scope.Storage.Items[19] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Storage.RemoveSkin(17));
        Assert.False(scope.Storage.Items.ContainsKey(17));
        Assert.Null(Assert.Single(scope.Storage.Items).Value);
        RowIs(removed, 777, true);
    }

    [Fact]
    public void InvalidCountsNeverClearRowsOrReconcileExistingEquippedFlags()
    {
        using var scope = new Records();
        var row = scope.Row(17, 17, true);
        var dictionary = scope.Storage.Items;
        foreach (string? line in new string?[] { null, "", "17", "bad/invalid/", "17/-1/", "0/0/ignored/" })
        {
            scope.Storage.LLoad(line);
            Assert.Same(dictionary, scope.Storage.Items);
            Assert.Same(row, Assert.Single(dictionary).Value);
            RowIs(row, 17, true);
            Assert.Equal(17u, scope.Storage.isEquip);
        }
    }

    [Fact]
    public void LoadDefaultsMalformedOrTruncatedIdsToZeroWhichAddSkinIgnores()
    {
        using var scope = new Records();
        scope.Storage.LLoad("4294967296/4/bad/-1/4294967296/17/");
        RowIs(Assert.Single(scope.Storage.Items).Value, 17, false);
        scope.Storage.LLoad("19/2/19/");
        RowIs(scope.Storage.Items[19], 19, true);
        Assert.False(scope.Storage.Items.ContainsKey(0));
        Assert.Equal(2, scope.Storage.Items.Count);
        Assert.False(scope.Storage.Load);
    }

    [Fact]
    public void RepeatedLoadAndDuplicateRecordsKeepExistingObjectsAndCanLeaveMultipleEquippedRows()
    {
        using var scope = new Records();
        var old = scope.Row(17, 777, true);
        scope.Storage.LLoad("19/4/17/19/19/23/");
        Assert.Same(old, scope.Storage.Items[17]);
        RowIs(old, 777, true);
        RowIs(scope.Storage.Items[19], 19, true);
        RowIs(scope.Storage.Items[23], 23, false);
        scope.Storage.LLoad("0/1/19/");
        Assert.True(scope.Storage.Items[19].isEquip); // Loading a different header does not repair old flags.
        Assert.Equal(3, scope.Storage.Items.Count);
        Assert.Equal(2, scope.Storage.Items.Values.Count(row => row.isEquip));
    }

    [Fact]
    public void SaveAndFreshLoadUsePhysicalIdsCollapseAliasesAndKeepRowsIndependent()
    {
        using var scope = new Records();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        scope.Storage.LLoad(" p4294967295 / p2 / p17 / p4294967295 /ignored/");
        Assert.Equal(uint.MaxValue, scope.Storage.isEquip);
        string record = scope.Storage.ToString();
        var fresh = new Storage(null!);
        fresh.LLoad(record);
        Assert.Equal(uint.MaxValue, fresh.isEquip);
        RowIs(fresh.Items[17], 17, false);
        RowIs(fresh.Items[uint.MaxValue], uint.MaxValue, true);
        Assert.NotSame(scope.Storage.Items[17], fresh.Items[17]);
        scope.Storage.Items.Clear();
        scope.Row(100, 17, false);
        scope.Row(200, 17, true);
        Assert.Equal("17/2/17/17/", scope.Storage.ToString());
        var collapsed = new Storage(null!);
        collapsed.LLoad(scope.Storage.ToString());
        RowIs(Assert.Single(collapsed.Items).Value, 17, true);
        Assert.False(collapsed.Items.ContainsKey(100));
        Assert.False(collapsed.Items.ContainsKey(200));
    }

    [Fact]
    public void SavedPhysicalZeroIdIsCountedButFreshLoadDropsItThroughAddSkinZeroGate()
    {
        using var scope = new Records();
        var zero = scope.Row(100, 0, true);
        scope.Row(200, 17, false);
        string record = scope.Storage.ToString();
        string expected = "0/2/" + string.Concat(scope.Storage.Items.Values.Select(row => row.ID + "/"));
        Assert.Equal(expected, record);
        var fresh = new Storage(null!);
        fresh.LLoad(record);
        RowIs(Assert.Single(fresh.Items).Value, 17, false);
        Assert.Equal(0u, fresh.isEquip);
        RowIs(zero, 0, true);
        Assert.Equal(2, scope.Storage.Items.Count);
    }

    [Fact]
    public void MethodsUseCurrentMutableDictionaryAndRetainZeroShortCircuitAndNullFaults()
    {
        using var scope = new Records();
        var old = scope.Storage.Items;
        var escaped = scope.Row(17, 17, true);
        scope.Storage.Items = new ConcurrentDictionary<uint, Storage.Item>();
        Assert.Equal("0/0/", scope.Storage.ToString());
        Assert.Same(escaped, Assert.Single(old).Value);
        scope.Storage.Items[17] = null!;
        scope.Storage.LLoad("17/1/17/");
        Assert.Null(scope.Storage.Items[17]); // ContainsKey prevents overwriting an existing null value too.
        Assert.Throws<NullReferenceException>(() => scope.Storage.ToString());
        scope.Storage.Items = null!;
        scope.Storage.AddSkin(0, true);
        scope.Storage.LLoad("0/2/0/0/"); // Both IDs are skipped before touching Items.
        Assert.Throws<NullReferenceException>(() => scope.Storage.AddSkin(17));
        Assert.Throws<NullReferenceException>(() => scope.Storage.LLoad("0/1/17/"));
        Assert.Throws<NullReferenceException>(() => scope.Storage.RemoveSkin(0));
        Assert.Throws<NullReferenceException>(() => scope.Storage.ToString());
        Assert.Throws<NullReferenceException>(() => { _ = scope.Storage.isEquip; });
        Assert.Null(scope.Storage.Items);
    }
}
