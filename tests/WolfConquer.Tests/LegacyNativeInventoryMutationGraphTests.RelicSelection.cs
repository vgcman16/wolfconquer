using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullOrEmptyGivingNumsReturnsZeroBeforeReadingNullSharedRandom(bool empty) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativePoolRandom.GetRandom = null!;
            Assert.Equal(0u, LegacyNativeBaseFunc.RandFromGivingNums(empty ? [] : null!));
        });

    [Fact]
    public Task SingletonGivingNumsStillConsumesOneGenuineSharedDraw() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Equal(0, scope.Control.Next(0, 1));
        Assert.Equal(uint.MaxValue, LegacyNativeBaseFunc.RandFromGivingNums([uint.MaxValue]));
        scope.AssertTail();
    });

    [Fact]
    public Task GivingNumsUsesActualIndexWeightsDuplicatesAndCurrentArrayWithoutMutation() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            uint[] values = [0, 1, 1, 1, uint.MaxValue, 777];
            uint[] initial = (uint[])values.Clone();
            for (int index = 0; index < 128; index++)
            {
                uint expected = values[scope.Control.Next(0, values.Length)];
                Assert.Equal(expected, LegacyNativeBaseFunc.RandFromGivingNums(values));
            }
            Assert.Equal(initial, values);
            values[0] = 901;
            for (int index = 0; index < 16; index++)
                Assert.Equal(values[scope.Control.Next(0, values.Length)],
                    LegacyNativeBaseFunc.RandFromGivingNums(values));
            scope.AssertTail();
        });

    private static LegacyNativeChiAttribute[] Rejected(uint item) => item switch
    {
        4100001 => [LegacyNativeChiAttribute.SkillCriticalStrike, LegacyNativeChiAttribute.CriticalStrike,
            LegacyNativeChiAttribute.HPAdd, LegacyNativeChiAttribute.AddAttack],
        4100002 => [LegacyNativeChiAttribute.SkillCriticalStrike, LegacyNativeChiAttribute.CriticalStrike,
            LegacyNativeChiAttribute.AddMagicAttack, LegacyNativeChiAttribute.AddAttack],
        4100003 => [LegacyNativeChiAttribute.HPAdd, LegacyNativeChiAttribute.CriticalStrike,
            LegacyNativeChiAttribute.AddMagicAttack, LegacyNativeChiAttribute.AddAttack],
        4100004 => [LegacyNativeChiAttribute.HPAdd, LegacyNativeChiAttribute.CriticalStrike,
            LegacyNativeChiAttribute.AddMagicAttack, LegacyNativeChiAttribute.SkillCriticalStrike],
        4100005 => [LegacyNativeChiAttribute.HPAdd, LegacyNativeChiAttribute.AddAttack,
            LegacyNativeChiAttribute.AddMagicAttack, LegacyNativeChiAttribute.SkillCriticalStrike],
        _ => []
    };

    public static IEnumerable<object[]> AllowedRelicAttributes()
    {
        foreach (uint item in new uint[] { 4100001, 4100002, 4100003, 4100004, 4100005, 999 })
            foreach (LegacyNativeChiAttribute attribute in Enum.GetValues<LegacyNativeChiAttribute>())
                if (!Rejected(item).Contains(attribute))
                    yield return [item, (int)attribute];
    }

    public static IEnumerable<object[]> RejectedRelicAttributes()
    {
        for (uint item = 4100001; item <= 4100005; item++)
            foreach (LegacyNativeChiAttribute attribute in Rejected(item))
                yield return [item, (int)attribute];
    }

    [Theory]
    [MemberData(nameof(AllowedRelicAttributes))]
    public Task AllowedAttributeIncludingNoneIsNotNormalizedOrFiltered(uint item, int attributeValue) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var attribute = (LegacyNativeChiAttribute)attributeValue;
            scope.ResetRandom(1);
            LegacyNativeRuneTable.RandomAttributes =
            [
                Attribute(888, LegacyNativeChiAttribute.LukyStrike),
                Attribute(item, attribute), Attribute(item, LegacyNativeChiAttribute.Immunity)
            ];
            Assert.Equal(0, scope.Control.Next(0, 2));

            Assert.Equal(attribute, LegacyNativeItemTypeRuneMetadata.RelicAttribute(item));

            scope.AssertTail();
            Assert.Equal(3, LegacyNativeRuneTable.RandomAttributes.Count);
            Assert.Empty(scope.Queue);
        });

    [Theory]
    [MemberData(nameof(RejectedRelicAttributes))]
    public Task EveryNativeExcludedFamilyRedrawsFromWholeFilteredPoolAndConsumesRealSequence(
        uint item, int attributeValue) => NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var attribute = (LegacyNativeChiAttribute)attributeValue;
            scope.ResetRandom(1);
            LegacyNativeRuneTable.RandomAttributes =
                [Attribute(item, attribute), Attribute(item, LegacyNativeChiAttribute.Immunity)];
            int rejectedDraws = 0;
            while (scope.Control.Next(0, 2) == 0)
            {
                rejectedDraws++;
                Assert.InRange(rejectedDraws, 1, 64);
            }
            Assert.True(rejectedDraws > 0);

            Assert.Equal(LegacyNativeChiAttribute.Immunity,
                LegacyNativeItemTypeRuneMetadata.RelicAttribute(item));

            scope.AssertTail();
        });

    [Fact]
    public Task RelicSelectionReadsReplacedMutableTableOnEachCallRatherThanCachingCandidates() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativeRuneTable.RandomAttributes = [Attribute(4100001, LegacyNativeChiAttribute.Immunity)];
            scope.Control.Next(0, 1);
            Assert.Equal(LegacyNativeChiAttribute.Immunity, LegacyNativeItemTypeRuneMetadata.RelicAttribute(4100001));
            LegacyNativeRuneTable.RandomAttributes =
                [Attribute(4100001, LegacyNativeChiAttribute.AddMagicDefense)];
            scope.Control.Next(0, 1);
            Assert.Equal(LegacyNativeChiAttribute.AddMagicDefense,
                LegacyNativeItemTypeRuneMetadata.RelicAttribute(4100001));
            scope.AssertTail();
        });

    [Fact]
    public Task EmptyFilteredTableFaultsAfterTheGenuineZeroRangeDraw() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        LegacyNativeRuneTable.RandomAttributes = [Attribute(4100002, LegacyNativeChiAttribute.Immunity)];
        scope.Control.Next(0, 0);
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeItemTypeRuneMetadata.RelicAttribute(4100001));
        scope.AssertTail();
    });

    [Fact]
    public Task NullTableFaultsBeforeDrawingOrRepairingIt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        LegacyNativeRuneTable.RandomAttributes = null!;
        Assert.Throws<ArgumentNullException>(() => LegacyNativeItemTypeRuneMetadata.RelicAttribute(4100001));
        Assert.Null(LegacyNativeRuneTable.RandomAttributes);
        scope.AssertTail();
    });

    [Fact]
    public Task NullCatalogRowFaultsDuringFilteringBeforeDrawing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        LegacyNativeRuneTable.RandomAttributes = [null!];
        Assert.Throws<NullReferenceException>(() => LegacyNativeItemTypeRuneMetadata.RelicAttribute(4100001));
        scope.AssertTail();
    });
}
