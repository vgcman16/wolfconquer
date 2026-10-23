using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClanTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Wolf", true)]
    [InlineData("狼", true)]
    [InlineData("gm", true)]
    [InlineData("GM", false)]
    [InlineData("Rayzo", false)]
    [InlineData("None", false)]
    [InlineData("guard1", false)]
    [InlineData("guard", true)]
    [InlineData("A B", true)]
    [InlineData("A@B", true)]
    [InlineData("@", false)]
    [InlineData("A/B", false)]
    [InlineData("A\\B", false)]
    [InlineData("A'B", false)]
    [InlineData("A[B", false)]
    [InlineData("A\u0019B", false)]
    [InlineData("12345678901234567", false)]
    public void WholeNamePolicyPreservesTrailingOnlyRegexAndCaseSensitiveForbiddenTokens(string? name, bool expected) =>
        Assert.Equal(expected, LegacyNativeBaseFunc.NameStrCheck(name));

    [Fact]
    public void OptionalSizeExemptionDoesNotBypassOtherNameRules()
    {
        Assert.True(LegacyNativeBaseFunc.NameStrCheck("12345678901234567", false));
        Assert.False(LegacyNativeBaseFunc.NameStrCheck("12345678901234567GM", false));
    }

    [Fact]
    public void RenameQueueOverwritesWithoutValidationAndIsNotConsumedByProcessing()
    {
        using var state = new State();
        var clan = Clan(10, "Before");
        LegacyNativeClanRuntime.Clans[10] = clan;
        LegacyNativeClanRuntime.RegisterChangeName(10, "First");
        LegacyNativeClanRuntime.RegisterChangeName(10, null);
        LegacyNativeClanRuntime.RegisterChangeName(99, "MissingClan");
        LegacyNativeClanRuntime.ProcessChangeNames();
        Assert.Null(clan.Name);
        Assert.Equal(2, LegacyNativeClanRuntime.ChangeNameRecords.Count);
        LegacyNativeClanRuntime.RegisterChangeName(10, "After");
        LegacyNativeClanRuntime.ProcessChangeNames();
        Assert.Equal("After", clan.Name);
        Assert.Equal(2, LegacyNativeClanRuntime.ChangeNameRecords.Count);
    }

    [Fact]
    public void CreationChecksQueuedAndLiveExactNamesAfterTheRealNamePolicy()
    {
        using var state = new State();
        LegacyNativeClanRuntime.Clans[1] = Clan(1, "Wolf");
        LegacyNativeClanRuntime.ChangeNameRecords[100] = "Queued";
        Assert.False(LegacyNativeClanRuntime.AllowCreateClan("Wolf"));
        Assert.False(LegacyNativeClanRuntime.AllowCreateClan("Queued"));
        Assert.False(LegacyNativeClanRuntime.AllowCreateClan("GM"));
        Assert.True(LegacyNativeClanRuntime.AllowCreateClan("wolf"));
        Assert.True(LegacyNativeClanRuntime.AllowCreateClan("queued"));
        Assert.True(LegacyNativeClanRuntime.AllowCreateClan("A@B"));
    }

    [Theory]
    [InlineData(0u, 30u)]
    [InlineData(1u, 40u)]
    [InlineData(2u, 50u)]
    [InlineData(3u, 60u)]
    [InlineData(4u, 70u)]
    [InlineData(5u, 30u)]
    [InlineData(4294967295u, 30u)]
    public void BattlePowerLookupReturnsTheNativeBandValue(uint input, uint expected) =>
        Assert.Equal(expected, new LegacyNativeClanRuntime().ProcentClanBp(input));

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)4)]
    [InlineData((byte)255)]
    public void CapacityIgnoresLevel(byte level) => Assert.Equal(11, LegacyNativeClanRuntime.MaxPlayersInClan(level));

    [Fact]
    public void LookupFamiliesUseValueNamesAndReturnTheSameNativeOwner()
    {
        using var state = new State();
        var clan = Clan(1, "Owner");
        var other = Clan(20, "Wolf");
        var enemy = Clan(30, "Enemy");
        LegacyNativeClanRuntime.Clans[999] = other;
        clan.Ally[888] = other;
        clan.Enemy[777] = enemy;
        var member = new LegacyNativeClanMember { UID = 9, Name = "Member" };
        clan.Members[123] = member;
        Assert.True(clan.TryGetClan("Wolf", out var found));
        Assert.Same(other, found);
        Assert.False(clan.TryGetClan("wolf", out found));
        Assert.Null(found);
        Assert.True(clan.TryGetMember("Member", out var row));
        Assert.Same(member, row);
        Assert.False(clan.TryGetMember("member", out row));
        Assert.Null(row);
        Assert.True(clan.IsAlly("Wolf"));
        Assert.False(clan.IsAlly("wolf"));
        Assert.Equal(20u, clan.GetClanAlly("Wolf"));
        Assert.Equal(0u, clan.GetClanAlly("Missing"));
        Assert.True(clan.IsEnemy("Enemy"));
        Assert.False(clan.IsEnemy("enemy"));
        Assert.Equal(30u, clan.GetClanEnemy("Enemy"));
        Assert.Equal(0u, clan.GetClanEnemy("Missing"));
    }
}
