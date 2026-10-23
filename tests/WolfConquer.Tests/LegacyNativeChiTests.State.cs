using System.Collections;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Chi = WolfConquer.Core.World.LegacyNativeChiRuntime;
using Attr = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiAttribute;
using Power = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiPower;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeChiTests
{
    [Fact]
    public Task ConstructorCreatesFourIndependentPowersButDoesNotPublishOrInitializePlayerChi() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(uint.MaxValue);
        Assert.Equal(uint.MaxValue, chi.UID);
        Assert.Equal(4000, chi.ChiPoints);
        Assert.Equal("", chi.Name);
        Assert.Null(scope.Client.Player.MyChi);
        Assert.Empty(Chi.ChiPool);
        Assert.Empty(LegacyNativeFateExpTable.Values);
        Assert.Equal(new[] { Kind.Dragon, Kind.Phoenix, Kind.Tiger, Kind.Turtle }, chi.Select(power => power.Type));
        Assert.Equal(new[] { chi.Dragon, chi.Phoenix, chi.Tiger, chi.Turtle }, ((IEnumerable)chi).Cast<Power>());
        Assert.Equal(4, chi.Select(power => power.Attributes).Distinct().Count());
        foreach (var power in chi)
        {
            Assert.False(power.UnLocked);
            Assert.Equal(0u, power.Exp);
            Assert.Equal(0u, power.UID);
            Assert.Equal("", power.Name);
            Assert.Equal(0, power.Rank);
            Assert.Equal(4, power.Attributes.Length);
            Assert.All(power.Attributes, attr => { Assert.Equal(Stat.None, attr.Type); Assert.Equal((ushort)0, attr.Value); Assert.False(attr.Locked); });
        }
        Assert.Null(chi.DragonPowers); Assert.Null(chi.PhoenixPowers); Assert.Null(chi.TigerPowers); Assert.Null(chi.TurtlePowers);
        Assert.Equal(0L, chi.DragonTime); Assert.Equal(0L, chi.PhoenixTime); Assert.Equal(0L, chi.TigerTime); Assert.Equal(0L, chi.TurtleTime);
        Assert.All(Totals(chi), value => Assert.Equal(0u, value));
        Assert.Equal(4000u, chi.MaxChiPoints);
        Assert.Equal(0u, chi.AllScore());
    });

    [Fact]
    public Task ChiEnumeratorLazilyReadsCurrentFieldsAndRetainsNativeTigerBeforeTurtleOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7);
        using var iterator = chi.GetEnumerator();
        var dragon = new Power(Kind.None);
        chi.Dragon = dragon;
        Assert.True(iterator.MoveNext()); Assert.Same(dragon, iterator.Current);
        var phoenix = new Power((Kind)255);
        chi.Phoenix = phoenix;
        Assert.True(iterator.MoveNext()); Assert.Same(phoenix, iterator.Current);
        chi.Tiger = null!;
        Assert.True(iterator.MoveNext()); Assert.Null(iterator.Current);
        var turtle = new Power(Kind.Dragon);
        chi.Turtle = turtle;
        Assert.True(iterator.MoveNext()); Assert.Same(turtle, iterator.Current);
        Assert.False(iterator.MoveNext());
    });

    [Fact]
    public Task PlayerChiSlotStoresExactlyAssignedPhysicalOwnerWithoutPoolOrPowerRemapping() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = new Chi(987);
        var second = new Chi(654);
        scope.Client.Player.MyChi = first;
        Assert.Same(first, scope.Client.Player.MyChi);
        Assert.Equal(987u, scope.Client.Player.MyChi.UID);
        scope.Client.Player.MyChi = second;
        Assert.Same(second, scope.Client.Player.MyChi);
        Assert.Equal(987u, first.UID);
        Assert.Empty(Chi.ChiPool);
        scope.Client.Player.MyChi = null;
        Assert.Null(scope.Client.Player.MyChi);
    });

    [Fact]
    public Task FateSelectionUsesLastEligiblePhysicalEnumerationEntryNotMaxLevelOrRequiredPoints() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = MinimumPower(Kind.Dragon); power.Exp = 100;
        var high = new LegacyNativeFateExpTable.Value { Index = 50, ChiType = Kind.Dragon, RequiredPoints = 90, Level = 10, MaxChiPoints = 100 };
        var wrong = new LegacyNativeFateExpTable.Value { ChiType = Kind.Phoenix, RequiredPoints = 0, MaxChiPoints = 999 };
        var laterLow = new LegacyNativeFateExpTable.Value { Index = 3, ChiType = Kind.Dragon, RequiredPoints = 10, Level = 1, MaxChiPoints = 7 };
        LegacyNativeFateExpTable.Values = new() { [99] = high, [1] = wrong, [8] = laterLow };
        Assert.Same(laterLow, power.ChiAttributes);
        laterLow.ChiType = Kind.Turtle;
        Assert.Same(high, power.ChiAttributes);
        high.RequiredPoints = 101;
        Assert.Null(power.ChiAttributes);
        power.Exp = 101;
        Assert.Same(high, power.ChiAttributes);
        power.Type = Kind.Phoenix;
        Assert.Same(wrong, power.ChiAttributes);
    });

    [Fact]
    public Task MaximumPointsSkipsNullLockedAndAbsentMetadataButAddsEachCurrentUnlockedFateBonus() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7);
        chi.Dragon = MinimumPower(Kind.Dragon);
        chi.Phoenix = MinimumPower(Kind.Phoenix);
        chi.Tiger = null!;
        chi.Turtle = new Power(Kind.Turtle);
        LegacyNativeFateExpTable.Values = new()
        {
            [1] = new() { ChiType = Kind.Dragon, MaxChiPoints = 11 },
            [2] = new() { ChiType = Kind.Phoenix, MaxChiPoints = 23 },
            [3] = new() { ChiType = Kind.Turtle, MaxChiPoints = 999 },
        };
        Assert.Equal(4034u, chi.MaxChiPoints);
        LegacyNativeFateExpTable.Values[2].MaxChiPoints = uint.MaxValue;
        Assert.Equal(4010u, chi.MaxChiPoints); // Native unchecked UInt32 addition.
        LegacyNativeFateExpTable.Values = null!;
        chi.Dragon.UnLocked = false; chi.Phoenix.UnLocked = false;
        Assert.Equal(4000u, chi.MaxChiPoints); // No metadata dereference for locked powers.
        chi.Dragon.UnLocked = true;
        Assert.Throws<NullReferenceException>(() => chi.MaxChiPoints);
    });

    [Fact]
    public Task ScoresHonorUnlockGateCurrentArrayLengthTruncationAndSamePowerAliases() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power(Kind.Dragon) { Attributes = null! };
        Assert.Equal(0, power.Score);
        power.UnLocked = true;
        Assert.Throws<ArgumentNullException>(() => power.Score);
        power.Attributes = new[] { new Attr(Stat.CriticalStrike, 11, false), new Attr(Stat.CriticalStrike, 12, true), new Attr(Stat.HPAdd, 3500, true) };
        Assert.Equal(101, power.Score); // Each attribute truncates independently: 0 + 1 +100.
        var chi = new Chi(7) { Dragon = power, Phoenix = power, Tiger = power, Turtle = power };
        Assert.Equal(404u, chi.AllScore());
        power.Attributes = Array.Empty<Attr>();
        Assert.Equal(0, power.Score);
        chi.Tiger = null!;
        Assert.Throws<NullReferenceException>(() => chi.AllScore());
    });

    [Fact]
    public Task NestedFixtureRestoresEscapedGlobalOwnersAndBothActualRandomStreams() => NativePacketTestThread.Run(() =>
    {
        using var outer = new Scope();
        var pool = Chi.ChiPool; var fate = LegacyNativeFateExpTable.Values; var clients = LegacyNativePoolClients.GamePoll;
        var synchronization = Chi.SynRoot; var sharedRandom = LegacyNativePoolRandom.GetRandom;
        var reset = LegacyNativePoolRandom.ResetRandom;
        var row = new LegacyNativeFateExpTable.Value {Index=7}; fate[7]=row;
        var chi = new Chi(7); pool[7]=chi;
        var control = new LegacyNativeSafeRandom(71); _=control.Next();
        Assert.Equal(273262797,Power.Rand.Next());
        using (var inner = new Scope())
        {
            Assert.NotSame(pool,Chi.ChiPool); Assert.NotSame(fate,LegacyNativeFateExpTable.Values);
            Assert.NotSame(clients,LegacyNativePoolClients.GamePoll); Assert.NotSame(synchronization,Chi.SynRoot);
            Assert.NotSame(sharedRandom,LegacyNativePoolRandom.GetRandom);
            for(int index=0;index<7;index++) _=Power.Rand.Next();
            _=LegacyNativePoolRandom.GetRandom.Next();
            LegacyNativePoolRandom.ResetRandom=DateTime.MaxValue;
            System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
        }
        Assert.Same(pool,Chi.ChiPool); Assert.Same(fate,LegacyNativeFateExpTable.Values);
        Assert.Same(clients,LegacyNativePoolClients.GamePoll); Assert.Same(synchronization,Chi.SynRoot);
        Assert.Same(sharedRandom,LegacyNativePoolRandom.GetRandom); Assert.Equal(reset,LegacyNativePoolRandom.ResetRandom);
        Assert.Same(row,fate[7]); Assert.Same(chi,pool[7]);
        Assert.Equal(System.Globalization.CultureInfo.InvariantCulture,System.Globalization.CultureInfo.CurrentCulture);
        Assert.Equal(512644276,Power.Rand.Next());
        Assert.Equal(control.Next(),LegacyNativePoolRandom.GetRandom.Next());
    });
}
