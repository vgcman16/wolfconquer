using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void RoleCoreRateAlwaysConsumesOneSharedParameterlessDrawAndUsesStrictModuloThreshold(int value)
    {
        using var random = new SkillRandom();
        int draw = random.Control.Next();
        Assert.Equal(value > draw % 100, LegacyNativeRoleCore.Rate(value));
        random.SameNext();
    }

    [Fact]
    public Task NoEligibleKeysReturnZeroWithoutMetadataOrRandomAccess() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var random = new SkillRandom();
        foreach (ushort id in new ushort[] { 16310, 16330, 16350, 16400, 16540 }) scope.Spell(id);
        LegacyNativePoolMagic.Magic = null!;
        scope.Ninja.Items = null!;
        Assert.Equal((ushort)0, scope.Ninja.GetSkill());
        random.SameNext();
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
    });

    [Theory]
    [InlineData((ushort)16320)]
    [InlineData((ushort)16340)]
    [InlineData((ushort)16410)]
    [InlineData((ushort)16430)]
    [InlineData((ushort)16450)]
    [InlineData((ushort)16470)]
    [InlineData((ushort)16490)]
    [InlineData((ushort)16510)]
    [InlineData((ushort)16530)]
    [InlineData((ushort)16550)]
    public Task EveryEligibleDictionaryKeyCanSelectEvenWhenItsPhysicalSpellRowIsNull(ushort id) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var random = new SkillRandom();
        Assert.True(scope.Spells.ClientSpells.TryAdd(id, null!));
        Assert.Equal(0, random.Control.Next(0, 1));
        Assert.Equal(id, scope.Ninja.GetSkill());
        random.SameNext();
        Assert.Null(scope.Spells.ClientSpells[id]);
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task FullCandidateSetUsesFixedNativeOrderRatherThanDictionaryInsertionAndOneDrawPerChoice() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var random = new SkillRandom();
        foreach (ushort id in SelectableSkills.Reverse()) scope.Spell(id);
        for (int index = 0; index < 32; index++)
            Assert.Equal(SelectableSkills[random.Control.Next(0, 10)], scope.Ninja.GetSkill());
        random.SameNext();
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task SpecialSkillUsesPhysicalIdAndLevelMetadataThenWinsBeforeCandidateSelection() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var random = new SkillRandom();
        foreach (ushort id in SelectableSkills) scope.Spell(id);
        var special = scope.Spell(16390, 3);
        special.ID = 17000;
        random.Magic.Add(17000, new() { [3] = new() { Rate = int.MaxValue } });
        _ = random.Control.Next();
        Assert.Equal((ushort)16390, scope.Ninja.GetSkill());
        random.SameNext();
        Assert.Equal((ushort)17000, special.ID);
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailedSpecialRollDrawPrecedesFallbackAndEmptyFallbackDoesNotDrawAgain(bool candidates) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var random = new SkillRandom();
        scope.Spell(16390, 2);
        random.Magic.Add(16390, new() { [2] = new() { Rate = 0 } });
        ushort[] selected = [16320, 16410, 16550];
        if (candidates) foreach (ushort id in selected.Reverse()) scope.Spell(id);
        _ = random.Control.Next();
        ushort expected = candidates ? selected[random.Control.Next(0, 3)] : (ushort)0;
        Assert.Equal(expected, scope.Ninja.GetSkill());
        random.SameNext();
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task RepeatedCallReadsLiveSpecialMetadataRateRatherThanCachingPriorSuccess() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var random = new SkillRandom();
        scope.Spell(16390);
        scope.Spell(16550);
        var magic = new LegacyNativeMagicType.Magic { Rate = 100 };
        random.Magic.Add(16390, new() { [1] = magic });
        _ = random.Control.Next();
        Assert.Equal((ushort)16390, scope.Ninja.GetSkill());
        magic.Rate = 0;
        _ = random.Control.Next();
        _ = random.Control.Next(0, 1);
        Assert.Equal((ushort)16550, scope.Ninja.GetSkill());
        random.SameNext();
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
    });

    [Theory]
    [InlineData("user")]
    [InlineData("spells")]
    [InlineData("row")]
    [InlineData("table")]
    [InlineData("level-table")]
    [InlineData("magic-row")]
    public Task MissingPhysicalOwnersFaultBeforeRateOrFallbackDrawEvenWithNoCandidates(string missing) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var random = new SkillRandom();
        scope.Spell(16390);
        random.Magic.Add(16390, new() { [1] = new() { Rate = 100 } });
        switch (missing)
        {
            case "user": scope.Ninja.user = null!; break;
            case "spells": scope.Client.MySpells = null!; break;
            case "row": scope.Spells.ClientSpells[16390] = null!; break;
            case "table": LegacyNativePoolMagic.Magic = null!; break;
            case "level-table": random.Magic[16390] = null!; break;
            case "magic-row": random.Magic[16390][1] = null!; break;
        }
        Assert.Throws<NullReferenceException>(() => scope.Ninja.GetSkill());
        random.SameNext();
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingMetadataIdOrLevelThrowsBeforeAnyRandomDraw(bool level) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var random = new SkillRandom();
        scope.Spell(16390);
        if (level) random.Magic.Add(16390, new());
        Assert.Throws<KeyNotFoundException>(() => scope.Ninja.GetSkill());
        random.SameNext();
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
    });
}
