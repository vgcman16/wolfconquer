using System.Reflection;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;
using Row = WolfConquer.Core.World.LegacyNativeItemRuneMetadata;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypeRuneCheckTests
{
    [Fact]
    public void NativeSignatureRetainsExactCasePublicStaticBoolAndUInt32Parameters()
    {
        var method = typeof(Table).GetMethod("isRuneCheck", BindingFlags.Public | BindingFlags.Static)!;
        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method.ReturnType);
        Assert.Equal(new[] { typeof(uint), typeof(uint) }, method.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(new[] { "ID", "Position" }, method.GetParameters().Select(p => p.Name));
        Assert.All(method.GetParameters(), p => Assert.False(p.IsOptional));
        Assert.Single(typeof(Table).GetMethods(BindingFlags.Public | BindingFlags.Static),
            m => m.Name == "isRuneCheck");
    }

    [Fact]
    public void FirstFamilyAllowsOnly101And121AcrossLowMiddleAndHighIdSuffixes()
    {
        foreach (var id in new uint[] { 4010000, 4014567, 4019999 })
            for (uint position = 0; position <= 160; position++)
                Assert.Equal(position is 101 or 121, Table.isRuneCheck(id, position));
    }

    [Fact]
    public void SecondFamilyAllowsOnly102And122AcrossLowMiddleAndHighIdSuffixes()
    {
        foreach (var id in new uint[] { 4020000, 4024567, 4029999 })
            for (uint position = 0; position <= 160; position++)
                Assert.Equal(position is 102 or 122, Table.isRuneCheck(id, position));
    }

    [Fact]
    public void ThirdFamilyKeepsBothInclusiveTwelveSlotRangesAndAllAdjacentGaps()
    {
        foreach (var id in new uint[] { 4030000, 4034567, 4039999 })
            for (uint position = 0; position <= 160; position++)
                Assert.Equal(position is >= 103 and <= 114 or >= 123 and <= 134,
                    Table.isRuneCheck(id, position));
    }

    [Fact]
    public void AdjacentFamiliesAndUInt32IdExtremesCannotUseAnyRuneSlot()
    {
        foreach (var id in new uint[] { 0, 1, 4000000, 4009999, 4040000, 4049999, uint.MaxValue })
            for (uint position = 101; position <= 134; position++)
                Assert.False(Table.isRuneCheck(id, position));
    }

    [Fact]
    public void FullUInt32IdsAndPositionsNeverAliasNarrowedRuneValues()
    {
        foreach (var pair in new (uint Id, uint Position)[]
        {
            (4010000, 101), (4020000, 102), (4030000, 103),
            (4019999, 121), (4029999, 122), (4039999, 134),
        })
        {
            Assert.True(Table.isRuneCheck(pair.Id, pair.Position));
            foreach (var familyDelta in new uint[] { 65536, 131072, 262144 })
                Assert.False(Table.isRuneCheck(pair.Id + familyDelta * 10000u, pair.Position));
            foreach (var positionDelta in new uint[] { 65536, 131072, 262144 })
                Assert.False(Table.isRuneCheck(pair.Id, pair.Position + positionDelta));
            Assert.False(Table.isRuneCheck(pair.Id, uint.MaxValue));
        }
    }

    [Fact]
    public void NullGlobalItemTableDoesNotBlockThePureIdAndPositionPredicate()
    {
        var previous = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = null;
            Assert.True(Table.isRuneCheck(4010000, 121));
            Assert.True(Table.isRuneCheck(4029999, 122));
            Assert.True(Table.isRuneCheck(4030001, 134));
            Assert.False(Table.isRuneCheck(4030001, 122));
            Assert.False(Table.isRuneCheck(4009999, 101));
            Assert.Null(LegacyNativePoolItems.ItemsBase);
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = previous;
        }
    }

    [Fact]
    public void ChecksIgnoreRealGlobalRowContentsAndPreserveDictionaryIdentity()
    {
        var previous = LegacyNativePoolItems.ItemsBase;
        try
        {
            var items = new Table();
            var row = new Row { ID = 1, Class = uint.MaxValue, Level = 255, GoldWorth = 777 };
            items.Add(4030001, row);
            LegacyNativePoolItems.ItemsBase = items;
            Assert.True(Table.isRuneCheck(4030001, 123));
            Assert.Same(items, LegacyNativePoolItems.ItemsBase);
            Assert.Same(row, items[4030001]);
            Assert.Equal(1u, row.ID);
            Assert.Equal(uint.MaxValue, row.Class);
            Assert.Equal((byte)255, row.Level);
            Assert.Equal(777u, row.GoldWorth);

            row.ID = 4010000;
            row.Class = 0;
            row.Level = 0;
            row.GoldWorth = 999;
            Assert.True(Table.isRuneCheck(4030001, 123));
            Assert.False(Table.isRuneCheck(4030001, 121));
            Assert.Single(items);
            Assert.Same(items, LegacyNativePoolItems.ItemsBase);
            Assert.Same(row, items[4030001]);
            Assert.Equal(4010000u, row.ID);
            Assert.Equal(0u, row.Class);
            Assert.Equal((byte)0, row.Level);
            Assert.Equal(999u, row.GoldWorth);
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = previous;
        }
    }
}
