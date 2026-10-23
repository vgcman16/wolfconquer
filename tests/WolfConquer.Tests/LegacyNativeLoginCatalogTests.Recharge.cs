using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Recharge = WolfConquer.Core.Data.LegacyNativeRechargeShop;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLoginCatalogTests
{
    [Fact]
    public void RechargeWholeUpdateAddsThenReplacesSameClientKeyWithoutChangingOtherAccounts()
    {
        using var scope = new Scope();
        var client = scope.Client(runeCount: 0);
        var cache = Recharge.RechargeAccounts;
        cache.TryAdd(99, LegacyNativePlayerRuntime.RechargeType.OneMount);
        client.Player.RechargeProgress = LegacyNativePlayerRuntime.RechargeType.OneGarment;
        Recharge.UpdateRecharge(client);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.OneGarment, cache[17]);
        client.Player.RechargeProgress = LegacyNativePlayerRuntime.RechargeType.InnerPower10000;
        Recharge.UpdateRecharge(client);
        Assert.Same(cache, Recharge.RechargeAccounts);
        Assert.Equal(2, cache.Count);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.InnerPower10000, cache[17]);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.OneMount, cache[99]);
    }

    [Fact]
    public void RechargeWholeLoadMergesWithoutClearingOrOverwritingFirstPublishedKeys()
    {
        using var scope = new Scope();
        Recharge.RechargeAccounts.TryAdd(17, LegacyNativePlayerRuntime.RechargeType.OneMount);
        scope.Rows("RechargeShop.txt", "17/1", "23/18446744073709551615", "23/4", "24/1048576");
        var cache = Recharge.RechargeAccounts;
        Recharge.Load();
        Assert.Same(cache, Recharge.RechargeAccounts);
        Assert.Equal(3, cache.Count);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.OneMount, cache[17]);
        Assert.Equal(ulong.MaxValue, (ulong)cache[23]);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.InnerPower10000, cache[24]);
        Recharge.Load();
        Assert.Equal(3, cache.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RechargeMissingOrZeroCountFileRetainsExistingCache(bool emptyFile)
    {
        using var scope = new Scope();
        if (emptyFile) scope.Rows("RechargeShop.txt");
        Recharge.RechargeAccounts.TryAdd(17, LegacyNativePlayerRuntime.RechargeType.OneGarment);
        var cache = Recharge.RechargeAccounts;
        Recharge.Load();
        Assert.Same(cache, Recharge.RechargeAccounts);
        Assert.Single(cache);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.OneGarment, cache[17]);
    }

    [Fact]
    public void RechargeLoadKeepsNativeUnsignedParsingDefaultsAndDuplicatePrecedence()
    {
        using var scope = new Scope();
        scope.Rows("RechargeShop.txt", "bad/not-number", "0/1048576", "23/-1", "24/18446744073709551616", "not-delimited");
        Recharge.Load();
        Assert.Equal(3, Recharge.RechargeAccounts.Count);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.None, Recharge.RechargeAccounts[0]);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.None, Recharge.RechargeAccounts[23]);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.None, Recharge.RechargeAccounts[24]);
    }

    [Theory]
    [InlineData(1UL, "OneGarment", 0UL)]
    [InlineData(1048576UL, "InnerPower10000", 0UL)]
    [InlineData(3UL, "3", 3UL)]
    [InlineData(ulong.MaxValue, "18446744073709551615", ulong.MaxValue)]
    public void RechargeWholeSavePreservesOriginalEnumTextAndItsNamedValueReloadLoss(ulong value, string text, ulong loaded)
    {
        using var scope = new Scope();
        string path = scope.Target("RechargeShop.txt");
        File.WriteAllText(path, "old-file-must-be-overwritten");
        Recharge.RechargeAccounts.TryAdd(17, (LegacyNativePlayerRuntime.RechargeType)value);
        Recharge.Save();
        Assert.Equal(new[] { "Count=1", "17/" + text }, File.ReadAllLines(path));
        Recharge.RechargeAccounts.Clear();
        Recharge.Load();
        Assert.Equal(loaded, (ulong)Recharge.RechargeAccounts[17]);
    }

    [Fact]
    public void RechargeEmptySaveWritesLiteralCountHeaderAndNullCacheDoesNotFallback()
    {
        using var scope = new Scope();
        string path = scope.Target("RechargeShop.txt");
        Recharge.Save();
        Assert.Equal(new[] { "Count=0" }, File.ReadAllLines(path));
        Recharge.RechargeAccounts = null!;
        Assert.Throws<NullReferenceException>(() => Recharge.Save());
        Assert.Equal(new[] { "Count=0" }, File.ReadAllLines(path));
        // Reader returns before ContainsKey for a missing/zero-count source.
        Recharge.Load();
        Assert.Null(Recharge.RechargeAccounts);
    }

    [Fact]
    public void QueueNameCacheIsExactReplaceableImplicitNullListAndAddsNoInitializer()
    {
        FieldInfo field = typeof(LegacyNativePoolClients).GetField("NameUsed", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(typeof(List<int>), field.FieldType);
        Assert.False(field.IsInitOnly);
        Assert.Null(typeof(LegacyNativePoolClients).TypeInitializer);
        var previous = LegacyNativePoolClients.NameUsed;
        try
        {
            var owned = new List<int> { int.MinValue, 0, int.MaxValue };
            LegacyNativePoolClients.NameUsed = owned;
            Assert.Same(owned, field.GetValue(null));
            LegacyNativePoolClients.NameUsed = null;
            Assert.Null(field.GetValue(null));
        }
        finally { LegacyNativePoolClients.NameUsed = previous; }
    }
}
