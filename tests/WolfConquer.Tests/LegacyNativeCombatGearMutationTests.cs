using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearMutationTests
{
    [Theory]
    [InlineData(0u, 0u, 23u)]
    [InlineData(1u, 1u, 0u)]
    [InlineData(2u, 1u, 23u)]
    [InlineData(4294967295u, 4294967294u, 23u)]
    public void AddItemUsesTheNativePreviousLevelLookupAndIgnoresCallerExp(uint level, uint lookup, uint expectedExp)
    {
        using var f = new NativeCombatGearMutationScope();
        var metadata = NativeCombatGearMutationScope.Row(201, lookup, 23);
        if (lookup != level) NativeCombatGearMutationScope.Row(201, level, 99);

        var frame = f.User.AddItem(17, Gear.TypeID.Dragonhowl, level, uint.MaxValue, 1, 2);

        var added = Assert.Single(f.User.Items);
        Assert.Equal(Gear.TypeID.Dragonhowl, added.Key);
        Assert.Equal(Gear.TypeID.Dragonhowl, added.Value.ItemID);
        Assert.Equal(level, added.Value.Level);
        Assert.Equal(expectedExp, added.Value.Exp);
        Assert.Equal(1u, added.Value.Equip);
        Assert.Equal(2u, added.Value.Transform);
        Assert.Equal(0u, added.Value.MasteryScore);
        var fields = NativeCombatGearMutationScope.ReadAdded(frame, 17);
        Assert.Equal(new ulong[] { 201, level, expectedExp, 1, 2, 0, 0, 0, 0, 0, 0, 0 }, fields.Values);
        Assert.Equal(23u, metadata.Exp);
        if (level == 1)
            Assert.Equal(Convert.FromHexString("23007409080210111A1908C901100118002001280230003800400048005000580060005451536572766572"), frame);
    }

    [Fact]
    public void AddItemFalseGatePrecedesNullOwnerAndMetadataAndDoesNotRepairThem()
    {
        using var f = new NativeCombatGearMutationScope();
        GearTable.m_Load = false;
        GearTable.Info = null!;
        f.User.Items = null!;
        Assert.Null(f.User.AddItem(17, Gear.TypeID.Dragonhowl, 1, 2, 3, 4));
        Assert.Null(f.User.Items);
        Assert.Null(GearTable.Info);
    }

    [Fact]
    public void ExistingDictionaryKeyReturnsBeforeMetadataLookupRegardlessOfValueIdentity()
    {
        using var f = new NativeCombatGearMutationScope();
        var existing = f.Weapon(201, 401, 2, 37, 41);
        GearTable.Info = null!;
        Assert.Null(f.User.AddItem(17, Gear.TypeID.Dragonhowl, 4, 999, 1, 2));
        Assert.Same(existing, Assert.Single(f.User.Items).Value);
        Assert.Equal(Gear.TypeID.StoneCracker, existing.ItemID);
        Assert.Equal(2u, existing.Equip);
        Assert.Equal(37u, existing.Exp);
        Assert.Equal(41u, existing.Transform);
        Assert.Null(GearTable.Info);
    }

    [Fact]
    public void ExistingNullDictionaryValueStillSuppressesTheMetadataLookupAndFreshAdd()
    {
        using var f = new NativeCombatGearMutationScope();
        f.User.Items[Gear.TypeID.Dragonhowl] = null!;
        GearTable.Info = null!;

        Assert.Null(f.User.AddItem(17, Gear.TypeID.Dragonhowl, 2, 999, 1, 2));

        var retained = Assert.Single(f.User.Items);
        Assert.Equal(Gear.TypeID.Dragonhowl, retained.Key);
        Assert.Null(retained.Value);
        Assert.Null(GearTable.Info);
    }

    [Fact]
    public void MissingOuterMetadataIdDoesNotVisitOtherNullRowsButMatchingNullInnerCacheFaults()
    {
        using var f = new NativeCombatGearMutationScope();
        GearTable.Info[999] = null!;
        var metadata = GearTable.Info;

        Assert.Null(f.User.AddItem(17, Gear.TypeID.Dragonhowl, 2, 999, 1, 2));
        Assert.Empty(f.User.Items);
        Assert.Same(metadata, GearTable.Info);
        Assert.Null(GearTable.Info[999]);

        GearTable.Info[201] = null!;
        Assert.Throws<NullReferenceException>(() => f.User.AddItem(17, Gear.TypeID.Dragonhowl, 2, 999, 1, 2));
        Assert.Empty(f.User.Items);
        Assert.Same(metadata, GearTable.Info);
        Assert.Null(GearTable.Info[201]);
    }

    [Theory]
    [InlineData(0u, 0UL)]
    [InlineData(uint.MaxValue, ulong.MaxValue)]
    [InlineData(0xDEADBEEFu, 0xFFFFFFFFDEADBEEFUL)]
    public void RawTypeIdsAreNotRejectedWhenTheExactMetadataKeyExists(uint id, ulong expectedWireId)
    {
        using var f = new NativeCombatGearMutationScope();
        var metadata = NativeCombatGearMutationScope.Row(id, 1, 77);
        metadata.Level = 99; // The dictionary key, not row fields, selects this lookup.

        var frame = f.User.AddItem(17, (Gear.TypeID)id, 2, 999, 3, 4);

        var added = Assert.Single(f.User.Items);
        Assert.Equal((Gear.TypeID)id, added.Key);
        Assert.Equal((Gear.TypeID)id, added.Value.ItemID);
        Assert.Equal(id, (uint)added.Value.ItemID);
        // Storage remains uint; the pinned registered protobuf enum uses signed-int32 varints.
        Assert.Equal(new ulong[] { expectedWireId, 2, 77, 3, 4, 0, 0, 0, 0, 0, 0, 0 },
            NativeCombatGearMutationScope.ReadAdded(frame, 17).Values);
        Assert.Same(metadata, GearTable.Info[id][1]);
        Assert.Equal(99u, metadata.Level);
    }

    [Fact]
    public void ExistingValueWithTheRequestedIdAtAnotherKeyDoesNotBlockAFreshAdd()
    {
        using var f = new NativeCombatGearMutationScope();
        var existing = f.Weapon(999, 201);
        var metadata = NativeCombatGearMutationScope.Row(201, 2, 77);
        var frame = f.User.AddItem(29, Gear.TypeID.Dragonhowl, 3, 88, uint.MaxValue, uint.MaxValue);
        var current = f.User.Items[Gear.TypeID.Dragonhowl];
        Assert.NotSame(existing, current);
        Assert.Same(existing, f.User.Items[(Gear.TypeID)999]);
        Assert.Equal(77u, current.Exp);
        Assert.Equal(uint.MaxValue, current.Equip);
        Assert.Equal(uint.MaxValue, current.Transform);
        Assert.Same(metadata, GearTable.Info[201][2]);
        Assert.Equal(new ulong[] { 201, 3, 77, uint.MaxValue, uint.MaxValue, 0, 0, 0, 0, 0, 0, 0 },
            NativeCombatGearMutationScope.ReadAdded(frame, 29).Values);
    }

    [Fact]
    public void MissingPreviousLevelReturnsNullWithoutInsertingOrFallingBackToCurrentLevel()
    {
        using var f = new NativeCombatGearMutationScope();
        NativeCombatGearMutationScope.Row(201, 3, 99);
        Assert.Null(f.User.AddItem(17, Gear.TypeID.Dragonhowl, 3, 23, 1, 2));
        Assert.Empty(f.User.Items);
        Assert.Single(GearTable.Info[201]);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    public void FoundNullMetadataIsNotDereferencedForLevelOneButFaultsBeforePublicationOtherwise(uint level)
    {
        using var f = new NativeCombatGearMutationScope();
        GearTable.Info[201] = new() { [1] = null! };
        if (level == 1)
        {
            var frame = f.User.AddItem(17, Gear.TypeID.Dragonhowl, level, 999, 1, 2);
            Assert.Equal(0u, Assert.Single(f.User.Items).Value.Exp);
            Assert.Equal(0UL, NativeCombatGearMutationScope.ReadAdded(frame, 17)[3]);
        }
        else
        {
            Assert.Throws<NullReferenceException>(() => f.User.AddItem(17, Gear.TypeID.Dragonhowl, level, 999, 1, 2));
            Assert.Empty(f.User.Items);
        }
        Assert.Null(GearTable.Info[201][1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullItemsOrMetadataFaultWithoutPublishingAnItem(bool nullItems)
    {
        using var f = new NativeCombatGearMutationScope();
        if (nullItems) f.User.Items = null!;
        else GearTable.Info = null!;
        var items = f.User.Items;
        var metadata = GearTable.Info;
        Assert.Throws<NullReferenceException>(() => f.User.AddItem(17, Gear.TypeID.Dragonhowl, 2, 3, 4, 5));
        Assert.Same(items, f.User.Items);
        Assert.Same(metadata, GearTable.Info);
        if (items is not null) Assert.Empty(items);
    }

    [Fact]
    public void EachAddOwnsFreshNativeWeaponStorageAndAFreshFrameIndependentOfLaterMutations()
    {
        using var f = new NativeCombatGearMutationScope();
        NativeCombatGearMutationScope.Row(201, 1, 7);
        NativeCombatGearMutationScope.Row(202, 1, 8);
        var firstFrame = f.User.AddItem(17, Gear.TypeID.Dragonhowl, 2, 999, 1, 2);
        var secondFrame = f.User.AddItem(17, Gear.TypeID.Bloodlust, 2, 999, 1, 2);
        var first = f.User.Items[Gear.TypeID.Dragonhowl];
        var second = f.User.Items[Gear.TypeID.Bloodlust];
        Assert.NotSame(first, second);
        Assert.NotSame(first.BlackJades, second.BlackJades);
        Assert.Equal(6, first.BlackJades.Distinct().Count());
        Assert.All(first.BlackJades, jade => { Assert.Equal(0u, jade.Type); Assert.Equal(0u, jade.Data); });
        Assert.NotSame(first.TypeAnima, second.TypeAnima);
        Assert.NotSame(first.TypeAnima[0], first.TypeAnima[1]);
        Assert.All(first.TypeAnima, row => { Assert.Equal(7, row.Length); Assert.All(row, value => Assert.Equal(0u, value)); });
        Assert.NotSame(firstFrame, secondFrame);
        var retained = firstFrame.ToArray();
        first.Exp = 123;
        first.BlackJades[0].Type = 456;
        Assert.Equal(retained, firstFrame);
        Assert.Equal(7UL, NativeCombatGearMutationScope.ReadAdded(firstFrame, 17)[3]);
        Assert.Equal(8UL, NativeCombatGearMutationScope.ReadAdded(secondFrame, 17)[3]);
    }
}
