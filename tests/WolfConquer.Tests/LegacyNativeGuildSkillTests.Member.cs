using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGuildSkillTests
{
    [Fact]
    public void ExplicitNativeConstructorSlotAssignsFreshActualOwnerWithoutAutomaticallyBindingFullBirth()
    {
        using var scope = new Scope();
        var client = new LegacyNativeGameClientRuntime();
        Assert.Null(client.GuildSkill);
        LegacyNativeGuildSkillMemberPhases.AssignGuildSkillAtNativeConstructorSlot(client);
        var first = Assert.IsType<LegacyNativeGuildSkillRuntime>(client.GuildSkill);
        Assert.Equal("0/", first.ToString());
        first.Load("1/17/9/");
        var escapedRow = first.Skills[17];
        LegacyNativeGuildSkillMemberPhases.AssignGuildSkillAtNativeConstructorSlot(client);
        var second = Assert.IsType<LegacyNativeGuildSkillRuntime>(client.GuildSkill);
        Assert.NotSame(first, second);
        Assert.NotSame(first.Skills, second.Skills);
        Assert.Empty(second.Skills);
        Assert.Same(escapedRow, Assert.Single(first.Skills).Value);
        RowIs(escapedRow, 17, 9);
        Assert.Throws<NullReferenceException>(() =>
            LegacyNativeGuildSkillMemberPhases.AssignGuildSkillAtNativeConstructorSlot(null!));
        Assert.Same(second, client.GuildSkill);
        Assert.Null(client.MyExchangeShop);
        Assert.Null(client.CollectionStorage);
    }
}
