using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void XpExpiryPreservesOpaqueCaseVariantsAndExpiresTheActualOwnedKey()
    {
        var now = DateTimeOffset.UtcNow;
        var character = CreateSession().Character;
        var fields = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal)
        {
            ["Opaque"] = "one", ["opaque"] = "two",
            ["StatusFlags"] = BuildStatusFlags(0x12),
            ["legacysupermanexpiresatutc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
        };
        var before = character with { Attributes = fields };
        Assert.True(InvokeRootXpExpiry(before, now, out var after, out var visible));
        Assert.True(visible);
        Assert.False(HasLegacyStatusFlag(after.Attributes, 0x12));
        Assert.Equal("one", after.Attributes["Opaque"]);
        Assert.Equal("two", after.Attributes["opaque"]);
        Assert.DoesNotContain("legacysupermanexpiresatutc", after.Attributes.Keys);
        Assert.IsType<Dictionary<string, string>>(after.Attributes);
        Assert.Same(StringComparer.Ordinal, ((Dictionary<string, string>)after.Attributes).Comparer);
        Assert.Contains("legacysupermanexpiresatutc", before.Attributes.Keys);
    }

    [Fact]
    public void XpExpiryRejectsAmbiguousOwnedTailWithoutPublishingEarlierLocalEdits()
    {
        var now = DateTimeOffset.UtcNow;
        var character = CreateSession().Character;
        var fields = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal)
        {
            ["Opaque"] = "one", ["opaque"] = "two", ["StatusFlags"] = BuildStatusFlags(0x12, 0x17),
            ["LegacyCycloneExpiresAtUtc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
            ["LegacySupermanExpiresAtUtc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
            ["legacysupermanexpiresatutc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
        };
        var before = character with { Attributes = fields };
        var image = fields.ToArray();
        Assert.False(InvokeRootXpExpiry(before, now, out var after, out var visible));
        Assert.Same(before, after);
        Assert.False(visible);
        Assert.Equal(image, fields.ToArray());
    }

    [Theory]
    [InlineData("broken/")]
    [InlineData("-1/")]
    [InlineData("18446744073709551616/")]
    [InlineData("0//0/")]
    [InlineData("0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/")]
    public void XpExpiryRejectsMalformedStatusWithoutConsumingDeadline(string vector)
    {
        var now = DateTimeOffset.UtcNow;
        var character = CreateSession().Character;
        var fields = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal)
        {
            ["StatusFlags"] = vector,
            ["LegacySupermanExpiresAtUtc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
        };
        AssertXpExpiryUnchanged(character with { Attributes = fields }, now);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void XpExpiryRejectsConflictingOrDuplicateStatusRepresentations(bool duplicate)
    {
        var now = DateTimeOffset.UtcNow;
        var character = CreateSession().Character;
        var fields = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal)
        {
            ["StatusFlags"] = BuildStatusFlags(0x12),
            [duplicate ? "statusflags" : "BitVector"] = BuildStatusFlags(duplicate ? 0x12 : 0x17),
            ["LegacySupermanExpiresAtUtc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
        };
        AssertXpExpiryUnchanged(character with { Attributes = fields }, now);
    }

    [Fact]
    public void XpExpiryUpdatesBothAgreeingStatusRepresentationsAndRetainsOtherFlags()
    {
        var now = DateTimeOffset.UtcNow;
        var character = CreateSession().Character;
        var fields = new Dictionary<string, string>(character.Attributes, StringComparer.Ordinal)
        {
            ["StatusFlags"] = BuildStatusFlags(0x12, 0x17),
            ["bitvector"] = BuildStatusFlags(0x12, 0x17),
            ["LegacySupermanExpiresAtUtc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
            ["Opaque"] = "one", ["opaque"] = "two",
        };
        var before = character with { Attributes = fields };
        Assert.True(InvokeRootXpExpiry(before, now, out var after, out var visible));
        Assert.True(visible);
        Assert.Equal(after.Attributes["StatusFlags"], after.Attributes["bitvector"]);
        Assert.False(HasLegacyStatusFlag(after.Attributes, 0x12));
        Assert.True(HasLegacyStatusFlag(after.Attributes, 0x17));
        Assert.Equal("one", after.Attributes["Opaque"]);
        Assert.Equal("two", after.Attributes["opaque"]);
        Assert.False(after.Attributes.ContainsKey("LegacySupermanExpiresAtUtc"));
        Assert.True(HasLegacyStatusFlag(before.Attributes, 0x12));
    }

    private static void AssertXpExpiryUnchanged(CharacterSnapshot before, DateTimeOffset now)
    {
        var image = before.Attributes.ToArray();
        Assert.False(InvokeRootXpExpiry(before, now, out var after, out var visible));
        Assert.Same(before, after);
        Assert.False(visible);
        Assert.Equal(image, before.Attributes.ToArray());
    }

    private static bool InvokeRootXpExpiry(CharacterSnapshot character, DateTimeOffset now,
        out CharacterSnapshot result, out bool visible)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod("TryExpireLegacyXpSkillFlags",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        object?[] args = [character, now, null, false];
        bool changed = Assert.IsType<bool>(method.Invoke(null, args));
        result = Assert.IsType<CharacterSnapshot>(args[2]);
        visible = Assert.IsType<bool>(args[3]);
        return changed;
    }
}
