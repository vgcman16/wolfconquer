using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuneChancePlanTests
{
    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)1)]
    public void AChanceAttemptPreservesTheRawSingleItemQuantity(ushort stack)
    {
        var source = Items(1500);
        var item = Assert.Single(source.RuneItems).WithStackSize(stack);
        var before = source with { RuneItems = [item] };
        var plan = WorldPacketHandlers.PlanRecordedRuneChance(before, 901, Curves(), 0);
        Assert.True(plan.Passed); Assert.Equal(stack, plan.Target.StackSize);
        Assert.Equal(item.Uid, plan.Target.Uid); Assert.Equal(1500U, item.RuneExperience);
    }

    [Theory]
    [InlineData("missing")] [InlineData("duplicate")] [InlineData("stacked")]
    [InlineData("family")] [InlineData("zero-level")] [InlineData("over-maximum")]
    public void AnInvalidPhysicalTargetCannotConsumeProgress(string fault)
    {
        var before = Items(1500);
        var item = Assert.Single(before.RuneItems);
        before = fault switch
        {
            "missing" => before with { InventoryItems = [item.WithPosition(0)], RuneItems = [] },
            "duplicate" => before with { RuneItems = [item, item.WithPosition(211)] },
            "stacked" => before with { RuneItems = [item.WithStackSize(2)] },
            "family" => before with { RuneItems = [item.WithItemId(4_040_001)] },
            "zero-level" => before with { RuneItems = [item.WithItemId(4_020_500)] },
            _ => before with { RuneItems = [item.WithItemId(4_010_002)] },
        };
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneChance(before, 901, Curves(), 0));
        Assert.Equal(1500U, item.RuneExperience); Assert.Equal(4_020_517U, item.ItemId);
    }

    [Theory]
    [InlineData(0U, false)] [InlineData(7U, true)]
    [InlineData(149U, false)] [InlineData(15000U, false)]
    public void ADeterministicOutcomeCannotCarryAnUnusedRoll(uint experience, bool maximum)
    {
        var before = Items(experience, maximum ? 4_020_527U : 4_020_517U);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneChance(before, 901, Curves(), 0));
        Assert.Equal(experience, Assert.Single(before.RuneItems).RuneExperience);
    }
}
