using System.Globalization;
using System.Reflection;
using WolfConquer.Core.World;
using Boxes = WolfConquer.Core.World.LegacyNativeRefinaryBoxes;
using Rifinery = WolfConquer.Core.World.LegacyNativeRifinery;
using Owned = WolfConquer.Tests.LegacyNativeRefinaryBoxesOwnedFiles;
using ItemType = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRefinaryBoxesTests
{
    private static string Box(uint id = 100, uint position = 4, byte type = 1, byte untradable = 0) =>
        "Count=1\n" + string.Join(',', id.ToString(CultureInfo.InvariantCulture),
            position.ToString(CultureInfo.InvariantCulture), type.ToString(CultureInfo.InvariantCulture),
            untradable.ToString(CultureInfo.InvariantCulture)) + "\n";

    private static string Two(string first = "Normal", string second = "Refined") =>
        Owned.Rows(Owned.Row(101, first), Owned.Row(102, second));

    [Fact]
    public void BoxStorageRetainsNativeOrderedFieldsWidthsAndDefaults()
    {
        var fields = typeof(Boxes.Boxe).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "Identifier", "Position", "Untradable", "Type" }, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(uint), typeof(uint), typeof(bool), typeof(Rifinery.RefineryType) },
            fields.Select(field => field.FieldType));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        var item = new Boxes.Boxe();
        Assert.Equal(0u, item.Identifier);
        Assert.Equal(0u, item.Position);
        Assert.False(item.Untradable);
        Assert.Equal(Rifinery.RefineryType.None, item.Type);
        Assert.Equal(typeof(Dictionary<uint, Boxes.Boxe>), typeof(Boxes).BaseType);
        Assert.False(typeof(Boxes).IsSealed);
    }

    [Fact]
    public void PoolFieldsAreRealSharedMutableReferencesInNativeRelativeOrder()
    {
        var names = new[] { "ItemCounter", "RifineryItems", "DBRerinaryBoxes", "ItemsBase" };
        var fields = typeof(LegacyNativePoolItems).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => names.Contains(field.Name)).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(LegacyNativeCounter), typeof(Rifinery), typeof(Boxes), typeof(ItemType) },
            fields.Select(field => field.FieldType));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        // Declaration defaults are source-pinned; these metadata reads do not assert cold-start global values.
    }

    [Fact]
    public void PoolReferencesRemainIndependentExplicitModelSlotsWithoutLazyBirth()
    {
        using var owned = new Owned(Box(), Two());
        var itemsBase = LegacyNativePoolItems.ItemsBase;
        var counter = LegacyNativePoolItems.ItemCounter;
        Assert.Null(LegacyNativePoolItems.RifineryItems);
        Assert.Null(LegacyNativePoolItems.DBRerinaryBoxes);
        var rifinery = new Rifinery();
        var boxes = new Boxes();
        Assert.Null(LegacyNativePoolItems.RifineryItems);
        Assert.Null(LegacyNativePoolItems.DBRerinaryBoxes);
        LegacyNativePoolItems.RifineryItems = rifinery;
        Assert.Same(rifinery, LegacyNativePoolItems.RifineryItems);
        Assert.Null(LegacyNativePoolItems.DBRerinaryBoxes);
        LegacyNativePoolItems.DBRerinaryBoxes = boxes;
        Assert.Same(boxes, LegacyNativePoolItems.DBRerinaryBoxes);
        LegacyNativePoolItems.RifineryItems = null;
        Assert.Same(boxes, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.Same(itemsBase, LegacyNativePoolItems.ItemsBase);
        Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
    }

    [Fact]
    public void ConstructorUsesTheExactRawConfigPrefixAndRealFourFieldReaders()
    {
        using var owned = new Owned(Box(7, 5, 2, 1), rawPrefix: true);
        Assert.EndsWith("prefixdatabaserefineryboxes.txt", owned.BoxesPath, StringComparison.Ordinal);
        var boxes = new Boxes();
        var row = Assert.Single(boxes).Value;
        Assert.Equal(7u, row.Identifier);
        Assert.Equal(5u, row.Position);
        Assert.Equal((Rifinery.RefineryType)2, row.Type);
        Assert.True(row.Untradable);
        Assert.Same(row, boxes[7]);
        Assert.Null(LegacyNativePoolItems.DBRerinaryBoxes);
    }

    [Fact]
    public void ConstructorKeepsUnsignedMaximaAndUnvalidatedByteEnumValues()
    {
        using var owned = new Owned(Box(uint.MaxValue, uint.MaxValue, byte.MaxValue, 1));
        var row = new Boxes()[uint.MaxValue];
        Assert.Equal(uint.MaxValue, row.Identifier);
        Assert.Equal(uint.MaxValue, row.Position);
        Assert.Equal((Rifinery.RefineryType)255, row.Type);
        Assert.True(row.Untradable);
    }

    [Fact]
    public void ConstructorDefaultsMalformedOrOverflowingFieldsThroughRealReadLine()
    {
        foreach (var text in new[] { "bad,4294967296,256,256", "no-comma", "", "0,0" })
        {
            using var owned = new Owned("Count=1\n" + text + "\n");
            var row = new Boxes()[0];
            Assert.Equal(0u, row.Identifier);
            Assert.Equal(0u, row.Position);
            Assert.Equal(Rifinery.RefineryType.None, row.Type);
            Assert.False(row.Untradable);
        }
    }

    [Fact]
    public void ConstructorIgnoresExtraCommaFieldsAfterTheNativeFour()
    {
        using var owned = new Owned("Count=1\n8,3,12,1,invalid,999\n");
        var row = new Boxes()[8];
        Assert.Equal(3u, row.Position);
        Assert.Equal((Rifinery.RefineryType)12, row.Type);
        Assert.True(row.Untradable);
    }

    [Fact]
    public void ConstructorUntradableIsExactlyByteOneNotAnyNonzeroValue()
    {
        using var owned = new Owned("Count=3\n1,4,1,1\n2,4,1,2\n3,4,1,255\n");
        var boxes = new Boxes();
        Assert.True(boxes[1].Untradable);
        Assert.False(boxes[2].Untradable);
        Assert.False(boxes[3].Untradable);
    }

    [Fact]
    public void PrematureEofUsesReadStringZeroCommaZeroAndAddsARealDefaultBox()
    {
        using var owned = new Owned("Count=1\n");
        var boxes = new Boxes();
        var row = Assert.Single(boxes).Value;
        Assert.Equal(0u, row.Identifier);
        Assert.Equal(0u, row.Position);
        Assert.Equal(Rifinery.RefineryType.None, row.Type);
        Assert.False(row.Untradable);
    }

    [Fact]
    public void DuplicateBoxIdentifierEscapesDictionaryAddRatherThanOverwriting()
    {
        using var owned = new Owned("Count=2\n9,4,1,0\n9,5,2,1\n");
        Assert.Throws<ArgumentException>(() => new Boxes());
        Assert.Null(LegacyNativePoolItems.DBRerinaryBoxes);
    }

    [Fact]
    public void TwoPrematureEofRowsCollideAtZeroInsteadOfBeingSkipped()
    {
        using var owned = new Owned("Count=2\n");
        Assert.Throws<ArgumentException>(() => new Boxes());
        Assert.Null(LegacyNativePoolItems.DBRerinaryBoxes);
    }

    [Fact]
    public void ZeroCountReaderFalseLeavesTheNewBoxesDictionaryEmpty()
    {
        using var owned = new Owned("Count=0\n");
        Assert.Empty(new Boxes());
        Assert.Null(LegacyNativePoolItems.DBRerinaryBoxes);
    }

    [Fact]
    public void ConstructorDoesNotPublishOverAnExistingBoxOwnerOrInvokeItemTypeLoading()
    {
        using var owned = new Owned(Box());
        var first = new Boxes();
        LegacyNativePoolItems.DBRerinaryBoxes = first;
        var itemType = LegacyNativePoolItems.ItemsBase;
        var second = new Boxes();
        Assert.NotSame(first, second);
        Assert.Same(first, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.Same(itemType, LegacyNativePoolItems.ItemsBase);
        Assert.False(File.Exists(Path.Combine(owned.Root, "ItemAdd.ini")));
    }

    [Fact]
    public void MissingLookupKeyReturnsZeroBeforeDereferencingTheNullPoolOwner()
    {
        using var owned = new Owned(Box());
        var boxes = new Boxes();
        Assert.Null(LegacyNativePoolItems.RifineryItems);
        Assert.Equal(0u, boxes.GainRefineryItem(999));
    }

    [Fact]
    public void ExistingLookupKeyDereferencesTheRealNullPoolWithoutFallback()
    {
        using var owned = new Owned(Box());
        var boxes = new Boxes();
        Assert.Throws<NullReferenceException>(() => boxes.GainRefineryItem(100));
    }

    [Fact]
    public void EmptyRealRifineryProducesZeroWithoutSynthesizingAnItem()
    {
        using var owned = new Owned(Box());
        owned.LoadRifinery();
        Assert.Equal(0u, new Boxes().GainRefineryItem(100));
    }

    [Fact]
    public void AnInjectedNullBoxWithAnEmptyPoolReturnsZeroWithoutTouchingItsFields()
    {
        using var owned = new Owned(Box());
        owned.LoadRifinery();
        var boxes = new Boxes { [100] = null! };
        Assert.Equal(0u, boxes.GainRefineryItem(100));
    }

    [Fact]
    public void AnInjectedNullBoxWithAnEnumeratedItemFaultsInsteadOfBeingRepaired()
    {
        using var owned = new Owned(Box(), Two());
        owned.LoadRifinery();
        var boxes = new Boxes { [100] = null! };
        Assert.Throws<NullReferenceException>(() => boxes.GainRefineryItem(100));
    }

    [Fact]
    public void AnInjectedNullRifineryRowFaultsDuringTheLiteralLiveEnumeration()
    {
        using var owned = new Owned(Box(), Two());
        var items = owned.LoadRifinery();
        items[101] = null!;
        Assert.Throws<NullReferenceException>(() => new Boxes().GainRefineryItem(100));
    }

    [Fact]
    public void MismatchedTypeProducesNoCandidatesEvenAtTheMatchingPosition()
    {
        using var owned = new Owned(Box(type: 2), Two());
        owned.LoadRifinery();
        Assert.Equal(0u, new Boxes().GainRefineryItem(100));
    }

    [Fact]
    public void MismatchedPositionProducesNoCandidatesEvenAtTheMatchingType()
    {
        using var owned = new Owned(Box(position: 3), Two());
        owned.LoadRifinery();
        Assert.Equal(0u, new Boxes().GainRefineryItem(100));
    }

    [Fact]
    public void TwoMatchingCandidatesDeterministicallyChooseTheActualSecondValue()
    {
        using var owned = new Owned(Box(), Two());
        var items = owned.LoadRifinery();
        var values = items.Values.ToArray();
        Assert.Equal(2, values.Length);
        var boxes = new Boxes();
        for (var i = 0; i < 8; i++)
            Assert.Equal(values[1].ItemID, boxes.GainRefineryItem(100));
    }

    [Fact]
    public void OneMatchingCandidatePreservesNextOneOneThenListIndexOneFault()
    {
        using var owned = new Owned(Box(), Owned.Rows(Owned.Row(101, "Normal")));
        owned.LoadRifinery();
        var boxes = new Boxes();
        Assert.Throws<ArgumentOutOfRangeException>(() => boxes.GainRefineryItem(100));
    }

    [Fact]
    public void ThreeCandidatesReturnOnlyActualSecondOrThirdNotAClaimedDistribution()
    {
        using var owned = new Owned(Box(), Owned.Rows(
            Owned.Row(101, "Normal"), Owned.Row(102, "Refined"), Owned.Row(103, "Unique")));
        var values = owned.LoadRifinery().Values.ToArray();
        var boxes = new Boxes();
        for (var i = 0; i < 32; i++)
            Assert.Contains(boxes.GainRefineryItem(100), new[] { values[1].ItemID, values[2].ItemID });
        // Membership only; no seed injection, exact Random stream or distribution assertion.
    }

    [Fact]
    public void EachInclusiveSpecialRangeEndpointRequiresLevelStrictlyGreaterThanThree()
    {
        foreach (var id in new[] { 3004197u, 3004226u, 3004266u, 3004280u })
        {
            using var owned = new Owned(Box(id), Owned.Rows(
                Owned.Row(101, "Unique"), Owned.Row(102, "Elite"), Owned.Row(103, "Super")));
            owned.LoadRifinery();
            Assert.Equal(103u, new Boxes().GainRefineryItem(id));
        }
    }

    [Fact]
    public void EachOutsideRangeNeighborUsesLevelStrictlyLessThanSix()
    {
        foreach (var id in new[] { 3004196u, 3004227u, 3004265u, 3004281u })
        {
            using var owned = new Owned(Box(id), Owned.Rows(
                Owned.Row(101, "Elite"), Owned.Row(102, "Super"), Owned.Row(103, "Sacred")));
            owned.LoadRifinery();
            Assert.Equal(102u, new Boxes().GainRefineryItem(id));
        }
    }

    [Fact]
    public void SpecialRangesHaveNoUpperLevelCapOnTheRealMutableItemRow()
    {
        using var owned = new Owned(Box(3004197), Two("Sacred", "Sacred"));
        var items = owned.LoadRifinery();
        items[102].Level = uint.MaxValue; // Explicit mutable-row MODEL, not a parsed native level.
        Assert.Equal(102u, new Boxes().GainRefineryItem(3004197));
    }

    [Fact]
    public void OrdinaryBoxesIncludeLevelZeroAndExcludeLevelSix()
    {
        using var owned = new Owned(Box(), Owned.Rows(
            Owned.Row(101, "unrecognized"), Owned.Row(102, "Normal"), Owned.Row(103, "Sacred")));
        owned.LoadRifinery();
        Assert.Equal(102u, new Boxes().GainRefineryItem(100));
    }

    [Fact]
    public void SpecialRangeDecisionUsesTheAliasedBoxIdentifierNotItsOrdinaryLookupKey()
    {
        using var owned = new Owned(Box(), Two());
        owned.LoadRifinery();
        var boxes = new Boxes();
        var held = boxes[100];
        held.Identifier = 3004197;
        Assert.Same(held, boxes[100]);
        Assert.Equal(0u, boxes.GainRefineryItem(100));
    }

    [Fact]
    public void OrdinaryDecisionUsesTheAliasedIdentifierNotItsSpecialLookupKey()
    {
        using var owned = new Owned(Box(3004197), Two());
        owned.LoadRifinery();
        var boxes = new Boxes();
        boxes[3004197].Identifier = 100;
        Assert.Equal(102u, boxes.GainRefineryItem(3004197));
    }

    [Fact]
    public void ReplacingTheRealPoolOwnerIsObservedOnTheNextCall()
    {
        using var owned = new Owned(Box(), Two());
        var first = owned.LoadRifinery();
        var boxes = new Boxes();
        Assert.Equal(102u, boxes.GainRefineryItem(100));
        var second = new Rifinery();
        second[102].ItemID = 999;
        LegacyNativePoolItems.RifineryItems = second;
        Assert.NotSame(first, second);
        Assert.Equal(999u, boxes.GainRefineryItem(100));
        Assert.Equal(102u, first[102].ItemID);
        Assert.Same(first[102], ItemType.Refinary[first[102].Level][102]);
    }

    [Fact]
    public void MutatingTheSameBoxPositionAndTypeImmediatelyChangesEligibility()
    {
        using var owned = new Owned(Box(), Two());
        owned.LoadRifinery();
        var boxes = new Boxes();
        var same = boxes[100];
        Assert.Equal(102u, boxes.GainRefineryItem(100));
        same.Position = 3;
        Assert.Equal(0u, boxes.GainRefineryItem(100));
        same.Position = 4;
        same.Type = Rifinery.RefineryType.Immunity;
        Assert.Equal(0u, boxes.GainRefineryItem(100));
        Assert.Same(same, boxes[100]);
    }

    [Fact]
    public void ReturnedItemIdComesFromTheSameMutableRifineryObjectAndCacheAlias()
    {
        using var owned = new Owned(Box(), Two());
        var items = owned.LoadRifinery();
        Assert.Same(items[102], ItemType.Refinary[2][102]);
        ItemType.Refinary[2][102].ItemID = uint.MaxValue;
        Assert.Equal(uint.MaxValue, new Boxes().GainRefineryItem(100));
    }

    [Fact]
    public void SuccessfulSelectionDoesNotConsumeTheCounterOrPublishAnyOwner()
    {
        using var owned = new Owned(Box(), Two());
        var items = owned.LoadRifinery();
        var boxes = new Boxes();
        var published = new Boxes();
        LegacyNativePoolItems.DBRerinaryBoxes = published;
        var counter = LegacyNativePoolItems.ItemCounter;
        var count = counter.Count;
        var itemType = LegacyNativePoolItems.ItemsBase;
        Assert.Equal(102u, boxes.GainRefineryItem(100));
        Assert.Same(items, LegacyNativePoolItems.RifineryItems);
        Assert.Same(published, LegacyNativePoolItems.DBRerinaryBoxes);
        Assert.Same(counter, LegacyNativePoolItems.ItemCounter);
        Assert.Equal(count, counter.Count);
        Assert.Same(itemType, LegacyNativePoolItems.ItemsBase);
    }

    [Fact]
    public void ConstructorHeaderAndFieldParsersUseTheCurrentCulture()
    {
        using var owned = new Owned("Count=p1\np7,p4,p1,p1\n");
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        var row = new Boxes()[7];
        Assert.Equal(4u, row.Position);
        Assert.Equal(Rifinery.RefineryType.MDefence, row.Type);
        Assert.True(row.Untradable);
    }

    [Fact]
    public void UnknownByteEnumValuesCanMatchWithoutAValidationOrPositionFacade()
    {
        using var owned = new Owned(Box(position: 0, type: 255), Owned.Rows(
            Owned.Row(101, "Normal", "unrecognized", 255),
            Owned.Row(102, "Refined", "unrecognized", 255)));
        var items = owned.LoadRifinery();
        Assert.All(items.Values, item =>
        {
            Assert.Equal((Rifinery.RefineryType)255, item.Type);
            Assert.Equal(0u, item.ForItemPosition);
        });
        Assert.Equal(102u, new Boxes().GainRefineryItem(100));
    }
}
