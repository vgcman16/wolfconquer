using System.Reflection;
using WolfConquer.Core.World;
using Beasts = WolfConquer.Core.World.LegacyNativeBeastsRuntime;
using Cache = WolfConquer.Core.World.LegacyNativeBeastsAtrribute;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeBeastsStateTests
{
    [Fact]
    public void ConstructorRetainsActualClientReferenceAndOnlyInitializesFixedLevel()
    {
        using var scope = new CacheScope();
        var client = new LegacyNativeGameClientRuntime();
        Assert.Null(client.Beasts);
        var beasts = new Beasts(client);
        Assert.Same(client, beasts.Owner);
        Assert.Equal((byte)1, beasts.FixedLevel);
        Assert.Equal((byte)0, beasts.Level);
        Assert.Equal(0u, beasts.Points);
        Assert.Equal(0u, beasts.flag);
        Assert.False(beasts.Activated);
        Assert.Null(client.Beasts);
        client.Beasts = beasts; // Explicit MODEL member phase, not a host/LoadCharacter call.
        Assert.Same(beasts, client.Beasts);
        Assert.Same(client, client.Beasts.Owner);
        Assert.Equal(typeof(byte), typeof(Beasts).GetField("_level", BindingFlags.NonPublic | BindingFlags.Instance)!.FieldType);
        Assert.Equal(typeof(byte), typeof(Beasts).GetField("_fixedlevel", BindingFlags.NonPublic | BindingFlags.Instance)!.FieldType);
    }

    [Fact]
    public void ConstructorRejectsNullBeforeCreatingAnUnusableOwner()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new Beasts(null!));
        Assert.Equal("client", error.ParamName);
    }

    [Fact]
    public void LevelUsesLiveDictionaryCountAndNativeFixedLevelByteWrap()
    {
        using var scope = new CacheScope(Row(1), Row(2));
        var beasts = Create();
        beasts.Level = 5;
        Assert.Equal((byte)2, beasts.Level);
        Assert.Equal((byte)4, beasts.FixedLevel);
        Cache.Attributes = new Dictionary<byte, Cache.Attribute> { [1] = Row(1) };
        beasts.FixedLevel = byte.MaxValue;
        beasts.Level = 3;
        Assert.Equal((byte)1, beasts.Level);
        Assert.Equal((byte)1, beasts.FixedLevel);
    }

    [Fact]
    public void NullCacheLevelFaultPrecedesCommittingTheNewLevel()
    {
        using var scope = new CacheScope(Row(1));
        var beasts = Create();
        beasts.Level = 1;
        Cache.Attributes = null!;
        Assert.Throws<NullReferenceException>(() => beasts.Level = 2);
        Assert.Equal((byte)1, beasts.Level);
        Assert.Equal((byte)1, beasts.FixedLevel);
    }

    [Fact]
    public void TotalPointsGetterUsesNativeUInt32AdditionWithoutWidening()
    {
        using var scope = new CacheScope(Row(1, uint.MaxValue), Row(2));
        var beasts = Create();
        beasts.Level = 2;
        beasts.Points = 10;
        Assert.Equal(9u, beasts.TotalPoints);
        Assert.Equal(10u, beasts.Points);
    }

    [Fact]
    public void TotalPointsGetterKeepsMissingContiguousKeyFault()
    {
        using var scope = new CacheScope(Row(2), Row(3));
        var beasts = Create();
        beasts.Level = 2;
        beasts.Points = 7;
        Assert.Throws<KeyNotFoundException>(() => _ = beasts.TotalPoints);
        Assert.Equal(7u, beasts.Points);
        Assert.Equal((byte)2, beasts.Level);
    }

    [Fact]
    public void TotalPointsSetterCommitsActivatedLevelResetBeforeMissingKeyFault()
    {
        using var scope = new CacheScope(Row(2));
        var beasts = Create();
        beasts.Activated = true;
        beasts.Points = 7;
        Assert.Throws<KeyNotFoundException>(() => beasts.TotalPoints = 5);
        Assert.Equal((byte)1, beasts.Level);
        Assert.Equal(7u, beasts.Points);
        Assert.Equal((byte)1, beasts.FixedLevel);
    }

    [Fact]
    public void TotalPointsSetterAddsExistingPointsAndPreservesFixedByteWrapOrder()
    {
        using var scope = new CacheScope(Row(1, 10));
        var beasts = Create();
        beasts.FixedLevel = byte.MaxValue;
        beasts.Activated = true;
        beasts.Points = 7;
        beasts.TotalPoints = 25;
        Assert.Equal((byte)1, beasts.Level);
        Assert.Equal(2u, beasts.Points);
        Assert.Equal((byte)2, beasts.FixedLevel);
        Assert.Equal(2u, beasts.TotalPoints);
    }

    [Fact]
    public void EmptyCacheTotalSetterCommitsPointsBeforeLastRowNullFault()
    {
        using var scope = new CacheScope();
        var beasts = Create();
        beasts.Points = 7;
        Assert.Throws<NullReferenceException>(() => beasts.TotalPoints = 5);
        Assert.Equal(12u, beasts.Points);
        Assert.Equal((byte)0, beasts.Level);
        Assert.Equal((byte)1, beasts.FixedLevel);
    }

    [Fact]
    public void PhaseUsesActivationAndNativeIntegerLevelDivision()
    {
        using var scope = new CacheScope(Enumerable.Range(1, 30).Select(i => Row((byte)i)).ToArray());
        var beasts = Create();
        beasts.Level = 19;
        Assert.Equal((byte)0, beasts.Phase);
        beasts.Activated = true;
        Assert.Equal((byte)2, beasts.Phase);
        beasts.Level = 20;
        Assert.Equal((byte)3, beasts.Phase);
    }

    [Fact]
    public void YellowSlotsStartAtFiveWithoutActivationOrLevelMetadata()
    {
        using var scope = new CacheScope();
        var beasts = Create();
        Assert.False(beasts.Activated);
        Assert.Equal((byte)5, beasts.YellowRuneSlots());
        beasts.Activated = true;
        Assert.Equal((byte)5, beasts.YellowRuneSlots());
    }

    [Fact]
    public void YellowSlotsRereadMutableRowsAndReplacementCacheReferences()
    {
        var row = Row(1, slots: 2);
        using var scope = new CacheScope(row);
        var beasts = Create();
        beasts.Level = 1;
        Assert.Equal((byte)7, beasts.YellowRuneSlots());
        row.YellowRuneSlots = 4;
        Assert.Equal((byte)9, beasts.YellowRuneSlots());
        Cache.Attributes = new Dictionary<byte, Cache.Attribute> { [1] = Row(1, slots: 3) };
        Assert.Equal((byte)8, beasts.YellowRuneSlots());
        Assert.Equal((byte)4, row.YellowRuneSlots);
    }

    [Fact]
    public void YellowSlotsCountExactlyFiveNamedBitsAndIgnoreHigherBits()
    {
        using var scope = new CacheScope();
        var beasts = Create();
        foreach (var bit in new uint[] { 1, 2, 4, 8, 16 })
        {
            beasts.flag = bit | 0xFFFF0000u;
            Assert.Equal((byte)6, beasts.YellowRuneSlots());
        }
        beasts.flag = 0xFFFF0000u;
        Assert.Equal((byte)5, beasts.YellowRuneSlots());
        beasts.flag |= 31;
        Assert.Equal((byte)10, beasts.YellowRuneSlots());
    }

    [Fact]
    public void YellowSlotsKeepByteOverflowInsteadOfClampingOrReturningUInt32()
    {
        using var scope = new CacheScope(Row(0, slots: byte.MaxValue));
        var beasts = Create();
        beasts.flag = 31;
        Assert.Equal((byte)9, beasts.YellowRuneSlots());
    }

    [Fact]
    public void YellowSlotsKeepNullCacheAndPresentNullRowFaults()
    {
        using var scope = new CacheScope();
        var beasts = Create();
        Cache.Attributes = null!;
        Assert.Throws<NullReferenceException>(() => beasts.YellowRuneSlots());
        Cache.Attributes = new Dictionary<byte, Cache.Attribute> { [0] = null! };
        Assert.Throws<NullReferenceException>(() => beasts.YellowRuneSlots());
    }

    [Fact]
    public void ContainsFlagUsesAnyIntersectionAndAddFlagKeepsNativeCombinedFlagQuirk()
    {
        var beasts = Create();
        beasts.AddFlag(Beasts.HammerRunes.YellowRuneSlot1);
        beasts.AddFlag(Beasts.HammerRunes.YellowRuneSlot1);
        Assert.Equal(1u, beasts.flag);
        Assert.True(beasts.ContainsFlag(Beasts.HammerRunes.YellowRuneSlot1 | Beasts.HammerRunes.YellowRuneSlot2));
        beasts.AddFlag(Beasts.HammerRunes.YellowRuneSlot1 | Beasts.HammerRunes.YellowRuneSlot2);
        Assert.Equal(1u, beasts.flag);
        Assert.False(beasts.ContainsFlag(Beasts.HammerRunes.None));
        beasts.AddFlag(Beasts.HammerRunes.None);
        Assert.Equal(1u, beasts.flag);
        beasts.AddFlag(Beasts.HammerRunes.YellowRuneSlot2);
        Assert.Equal(3u, beasts.flag);
        Assert.Equal(new uint[] { 0, 1, 2, 4, 8, 16 },
            Enum.GetValues<Beasts.HammerRunes>().Select(value => (uint)value));
    }

    private static Beasts Create() => new(new LegacyNativeGameClientRuntime());

    private static Cache.Attribute Row(byte level, uint requiredPoints = 10, byte slots = 0)
        => new() { Level = level, RequiredPoints = requiredPoints, YellowRuneSlots = slots };

    private sealed class CacheScope : IDisposable
    {
        private readonly Dictionary<byte, Cache.Attribute> _previous = Cache.Attributes;

        public CacheScope(params Cache.Attribute[] rows)
        {
            var replacement = new Dictionary<byte, Cache.Attribute>();
            foreach (var row in rows)
                replacement.Add(row.Level, row);
            Cache.Attributes = replacement; // MODEL substitution; never a production reset.
        }

        public void Dispose() => Cache.Attributes = _previous;
    }
}
