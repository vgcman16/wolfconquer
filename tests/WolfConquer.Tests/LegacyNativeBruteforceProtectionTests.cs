using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Native = WolfConquer.Core.Networking.LegacyNativeBruteforceProtection;
using Entry = WolfConquer.Core.Networking.LegacyNativeBruteForceEntry;
using Groups = WolfConquer.Core.World.LegacyNativeGroupServerList;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeBruteforceProtectionTests
{
    [Fact]
    public void EntryKeepsFourPublicMutableFieldsAndBornDefaults()
    {
        var fields = typeof(Entry).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.True(typeof(Entry).IsPublic);
        Assert.Equal(new (string, Type)[]
        {
            ("IPAddress", typeof(string)), ("WatchCheck", typeof(int)),
            ("Unbantime", typeof(DateTime)), ("AddedTimeRemove", typeof(DateTime)),
        }, fields.Select(field => (field.Name, field.FieldType)));
        Assert.All(fields, field => Assert.False(field.IsInitOnly || field.IsStatic));
        var row = new Entry();
        Assert.Null(row.IPAddress);
        Assert.Equal(0, row.WatchCheck);
        Assert.Equal(new DateTime(0L), row.Unbantime);
        Assert.Equal(new DateTime(0L), row.AddedTimeRemove);
    }

    [Fact]
    public void ProtectionKeepsPrivateStateAndAllFiveMethodSignaturesWithoutStartingWorker()
    {
        Assert.True(typeof(Native).IsPublic);
        var fields = typeof(Native).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "collection", "BanOnWatch" }, fields.OrderBy(field => field.MetadataToken).Select(field => field.Name));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.Equal(typeof(LegacyNativeDbSafeDictionary<string, Entry>), CollectionField.FieldType);
        Assert.Equal(typeof(int), ThresholdField.FieldType);
        var owner = new Native();
        Assert.Equal(0, Assert.IsType<int>(ThresholdField.GetValue(owner)));
        var methods = typeof(Native).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "AddWatch", "AllowAddress", "Init", "IsBanned", "_internalInit" },
            methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
        var worker = Assert.Single(methods.Where(method => method.Name == "_internalInit"));
        Assert.True(worker.IsPrivate);
        Assert.Equal(typeof(void), worker.ReturnType);
        Assert.Empty(worker.GetParameters());
        var init = Assert.Single(methods.Where(method => method.Name == "Init"));
        Assert.True(init.IsPublic);
        Assert.Equal(typeof(void), init.ReturnType);
        Assert.Equal("WatchBeforeBan", Assert.Single(init.GetParameters()).Name);
        Assert.Equal(typeof(int), Assert.Single(init.GetParameters()).ParameterType);
        foreach (var name in new[] { "AddWatch", "AllowAddress", "IsBanned" })
        {
            var method = Assert.Single(methods.Where(method => method.Name == name));
            Assert.True(method.IsPublic);
            Assert.Equal(name == "AddWatch" ? typeof(void) : typeof(bool), method.ReturnType);
            Assert.Equal(("IPAddress", typeof(string)), (Assert.Single(method.GetParameters()).Name, Assert.Single(method.GetParameters()).ParameterType));
        }
    }

    [Fact]
    public void SeparateInstancesOwnDistinctRealDictionariesAndSyncRoots()
    {
        var first = Entries(new Native());
        var second = Entries(new Native());
        Assert.NotSame(first, second);
        Assert.NotSame(first.SyncRoot, second.SyncRoot);
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.False(first.Update);
        Assert.Empty(first.MyArray);
    }

    [Fact]
    public void UnknownAddressIsNotBannedAndDoesNotMutateTheDictionary()
    {
        var owner = new Native();
        var entries = Entries(owner);
        Assert.False(owner.IsBanned("MODEL-UNKNOWN"));
        Assert.Empty(entries);
        Assert.False(entries.Update);
    }

    [Fact]
    public void NullKeysKeepRealDictionaryFaultsAndReleaseTheAddWatchMonitor()
    {
        var owner = new Native();
        var entries = Entries(owner);
        Assert.Throws<ArgumentNullException>(() => owner.AddWatch(null!));
        Assert.False(System.Threading.Monitor.IsEntered(entries));
        Assert.Throws<ArgumentNullException>(() => owner.IsBanned(null!));
        Assert.Empty(entries);
    }

    [Fact]
    public void FirstWatchRetainsInputReferenceAndUsesRealThreeMinuteExpiryAndMinDate()
    {
        var owner = new Native();
        var key = new string("MODEL-FIRST".ToCharArray());
        var before = DateTime.Now;
        owner.AddWatch(key);
        var after = DateTime.Now;
        AssertForwardClock(before, after);
        var row = Assert.Single(Entries(owner)).Value;
        Assert.Same(key, row.IPAddress);
        Assert.Equal(1, row.WatchCheck);
        Assert.InRange(row.AddedTimeRemove, before.AddMinutes(3.0), after.AddMinutes(3.0));
        Assert.Equal(new DateTime(0L), row.Unbantime);
    }

    [Fact]
    public void ZeroThresholdStillInitializesFirstWatchThenAssignsSecondWatchTime()
    {
        var owner = new Native();
        owner.AddWatch("MODEL-ZERO");
        var row = Entries(owner)["MODEL-ZERO"];
        var expiry = row.AddedTimeRemove;
        Assert.Equal(new DateTime(0L), row.Unbantime);
        var before = DateTime.Now;
        owner.AddWatch("MODEL-ZERO");
        var after = DateTime.Now;
        AssertForwardClock(before, after);
        Assert.Equal(2, row.WatchCheck);
        Assert.Equal(expiry, row.AddedTimeRemove);
        Assert.InRange(row.Unbantime, before.AddMinutes(3.0), after.AddMinutes(3.0));
    }

    [Fact]
    public void BelowThresholdIncrementsSameRowWithoutRefreshingEitherDateOrArray()
    {
        var owner = new Native();
        ThresholdField.SetValue(owner, 3);
        var row = new Entry { IPAddress = "MODEL-ROW", WatchCheck = 1, AddedTimeRemove = new DateTime(123), Unbantime = new DateTime(456) };
        var entries = Entries(owner);
        entries.Add("MODEL-KEY", row);
        var snapshot = entries.GetValues();
        owner.AddWatch("MODEL-KEY");
        Assert.Same(row, entries["MODEL-KEY"]);
        Assert.Equal(2, row.WatchCheck);
        Assert.Equal(new DateTime(123), row.AddedTimeRemove);
        Assert.Equal(new DateTime(456), row.Unbantime);
        Assert.False(entries.Update);
        Assert.Same(snapshot, entries.GetValues());
    }

    [Fact]
    public void ExactThresholdAssignsUnbanTimeButKeepsExpiryAndEntryReference()
    {
        var owner = new Native();
        ThresholdField.SetValue(owner, 3);
        var row = new Entry { IPAddress = "MODEL", WatchCheck = 2, AddedTimeRemove = new DateTime(123) };
        Entries(owner).Add("MODEL", row);
        var before = DateTime.Now;
        owner.AddWatch("MODEL");
        var after = DateTime.Now;
        AssertForwardClock(before, after);
        Assert.Same(row, Entries(owner)["MODEL"]);
        Assert.Equal(3, row.WatchCheck);
        Assert.Equal(new DateTime(123), row.AddedTimeRemove);
        Assert.InRange(row.Unbantime, before.AddMinutes(3.0), after.AddMinutes(3.0));
    }

    [Fact]
    public void AboveThresholdReassignsTimeOnEachFurtherWatchWithoutExpiryRefresh()
    {
        var owner = new Native();
        ThresholdField.SetValue(owner, 3);
        var row = new Entry { IPAddress = "MODEL", WatchCheck = 3, AddedTimeRemove = new DateTime(123), Unbantime = new DateTime(456) };
        Entries(owner).Add("MODEL", row);
        for (var count = 4; count <= 5; count++)
        {
            var before = DateTime.Now;
            owner.AddWatch("MODEL");
            var after = DateTime.Now;
            AssertForwardClock(before, after);
            Assert.Equal(count, row.WatchCheck);
            Assert.Equal(new DateTime(123), row.AddedTimeRemove);
            Assert.InRange(row.Unbantime, before.AddMinutes(3.0), after.AddMinutes(3.0));
        }
    }

    [Fact]
    public void NegativeThresholdIsNotClampedAndOnlyAffectsTheExistingRowBranch()
    {
        var owner = new Native();
        ThresholdField.SetValue(owner, -7);
        owner.AddWatch("MODEL");
        var row = Entries(owner)["MODEL"];
        Assert.Equal(new DateTime(0L), row.Unbantime);
        var before = DateTime.Now;
        owner.AddWatch("MODEL");
        var after = DateTime.Now;
        AssertForwardClock(before, after);
        Assert.Equal(2, row.WatchCheck);
        Assert.InRange(row.Unbantime, before.AddMinutes(3.0), after.AddMinutes(3.0));
    }

    [Fact]
    public void WatchCounterWrapsBeforeTheThresholdComparisonWithoutRepair()
    {
        var owner = new Native();
        var row = new Entry { IPAddress = "MODEL", WatchCheck = int.MaxValue, AddedTimeRemove = new DateTime(123), Unbantime = new DateTime(456) };
        Entries(owner).Add("MODEL", row);
        owner.AddWatch("MODEL");
        Assert.Equal(int.MinValue, row.WatchCheck);
        Assert.Equal(new DateTime(123), row.AddedTimeRemove);
        Assert.Equal(new DateTime(456), row.Unbantime);
    }

    [Fact]
    public void DictionaryKeyRatherThanMutableEntryAddressSelectsTheExistingRow()
    {
        var owner = new Native();
        ThresholdField.SetValue(owner, 100);
        var row = new Entry { IPAddress = "MODEL-OTHER", WatchCheck = 1 };
        Entries(owner).Add("MODEL-KEY", row);
        owner.AddWatch("MODEL-KEY");
        Assert.Same(row, Entries(owner)["MODEL-KEY"]);
        Assert.Equal("MODEL-OTHER", row.IPAddress);
        Assert.Equal(2, row.WatchCheck);
        owner.AddWatch("MODEL-OTHER");
        Assert.NotSame(row, Entries(owner)["MODEL-OTHER"]);
        Assert.Equal(2, Entries(owner).Count);
    }

    [Fact]
    public void RealDictionaryCaseSensitivityRetainsSeparateRows()
    {
        var owner = new Native();
        owner.AddWatch("MODEL-A");
        owner.AddWatch("model-a");
        Assert.Equal(2, Entries(owner).Count);
        Assert.NotSame(Entries(owner)["MODEL-A"], Entries(owner)["model-a"]);
        Assert.Equal(1, Entries(owner)["MODEL-A"].WatchCheck);
        Assert.Equal(1, Entries(owner)["model-a"].WatchCheck);
    }

    [Fact]
    public void ReplacingOrNullingTheActualCollectionIsNotHiddenByAnotherOwner()
    {
        var owner = new Native();
        var replacement = new LegacyNativeDbSafeDictionary<string, Entry>();
        CollectionField.SetValue(owner, replacement);
        owner.AddWatch("MODEL");
        Assert.Same(replacement, Entries(owner));
        Assert.Single(replacement);
        CollectionField.SetValue(owner, null);
        Assert.Throws<ArgumentNullException>(() => owner.AddWatch("MODEL"));
        Assert.Throws<NullReferenceException>(() => owner.IsBanned("MODEL"));
        Assert.Null(CollectionField.GetValue(owner));
    }

    [Fact]
    public void NullEntryFaultsAreNotRepairedByWatchOrBanQueries()
    {
        var owner = new Native();
        Entries(owner).Add("MODEL", null!);
        Assert.Throws<NullReferenceException>(() => owner.AddWatch("MODEL"));
        Assert.Throws<NullReferenceException>(() => owner.IsBanned("MODEL"));
        Assert.Null(Entries(owner)["MODEL"]);
    }

    [Fact]
    public void IsBannedUsesTheRealSignedValueHelperForMinDateAndExpiredRows()
    {
        var owner = new Native();
        var row = new Entry { IPAddress = "MODEL", AddedTimeRemove = new DateTime(0L), Unbantime = new DateTime(0L) };
        Entries(owner).Add("MODEL", row);
        AssertRealValueOracle(owner, "MODEL", row);
        row.Unbantime = DateTime.Now.AddYears(-1);
        AssertRealValueOracle(owner, "MODEL", row);
        Assert.Same(row, Entries(owner)["MODEL"]);
        Entries(owner).Remove("MODEL");
        Assert.False(owner.IsBanned("MODEL"));
    }

    [Fact]
    public void EmptyGlobalCacheReturnsFalseEvenWithNoWatchCollection()
    {
        var saved = Groups.GroupServers;
        try
        {
            Groups.GroupServers = new LegacyNativeDbSafeDictionary<uint, Groups.Server>();
            var owner = new Native();
            CollectionField.SetValue(owner, null);
            Assert.False(owner.AllowAddress("MODEL"));
            Assert.False(owner.AllowAddress(null!));
        }
        finally { Groups.GroupServers = saved; }
    }

    [Fact]
    public void NullGlobalCacheFaultsBeforeReturningFalse()
    {
        var saved = Groups.GroupServers;
        try
        {
            Groups.GroupServers = null!;
            Assert.Throws<NullReferenceException>(() => new Native().AllowAddress("MODEL"));
        }
        finally { Groups.GroupServers = saved; }
    }

    [Fact]
    public void FirstNullValueFaultsBeforeALaterAddressMatch()
    {
        var saved = Groups.GroupServers;
        try
        {
            var rows = new LegacyNativeDbSafeDictionary<uint, Groups.Server>();
            Groups.GroupServers = rows;
            rows.Add(1, null!);
            rows.Add(2, new Groups.Server { IPAddress = "MODEL" });
            Assert.Null(rows.Values.First());
            Assert.Throws<NullReferenceException>(() => new Native().AllowAddress("MODEL"));
        }
        finally { Groups.GroupServers = saved; }
    }

    [Fact]
    public void ActualFirstMatchingValueBypassesALaterNullAndRetainsLiveMutation()
    {
        var saved = Groups.GroupServers;
        try
        {
            var rows = new LegacyNativeDbSafeDictionary<uint, Groups.Server>();
            Groups.GroupServers = rows;
            var row = new Groups.Server { IPAddress = "MODEL" };
            rows.Add(1, row);
            rows.Add(2, null!);
            Assert.Same(row, rows.Values.First());
            var owner = new Native();
            Assert.False(owner.AllowAddress("MODEL"));
            row.IPAddress = "MODEL-CHANGED";
            Assert.Throws<NullReferenceException>(() => owner.AllowAddress("MODEL"));
        }
        finally { Groups.GroupServers = saved; }
    }

    [Fact]
    public void NullAddressEqualityCanBypassALaterNullButCaseMismatchCannot()
    {
        var saved = Groups.GroupServers;
        try
        {
            var rows = new LegacyNativeDbSafeDictionary<uint, Groups.Server>();
            Groups.GroupServers = rows;
            var row = new Groups.Server { IPAddress = null! };
            rows.Add(1, row);
            rows.Add(2, null!);
            Assert.Same(row, rows.Values.First());
            var owner = new Native();
            Assert.False(owner.AllowAddress(null!));
            row.IPAddress = "MODEL";
            Assert.False(owner.AllowAddress("MODEL"));
            Assert.Throws<NullReferenceException>(() => owner.AllowAddress("model"));
        }
        finally { Groups.GroupServers = saved; }
    }

    [Fact]
    public void AllowAddressUsesActualValuesRatherThanTheStaleGetValuesArray()
    {
        var saved = Groups.GroupServers;
        try
        {
            var rows = new LegacyNativeDbSafeDictionary<uint, Groups.Server>();
            Groups.GroupServers = rows;
            rows.Add(1, new Groups.Server { IPAddress = "MODEL-OLD" });
            rows.Add(2, null!);
            var stale = rows.GetValues();
            var replacement = new Groups.Server { IPAddress = "MODEL-NEW" };
            rows[1] = replacement;
            Assert.False(rows.Update);
            Assert.NotSame(stale[0], rows.Values.First());
            Assert.Same(replacement, rows.Values.First());
            var owner = new Native();
            Assert.False(owner.AllowAddress("MODEL-NEW"));
            Assert.Throws<NullReferenceException>(() => owner.AllowAddress("MODEL-OLD"));
        }
        finally { Groups.GroupServers = saved; }
    }

    private static readonly FieldInfo CollectionField = typeof(Native).GetField("collection", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ThresholdField = typeof(Native).GetField("BanOnWatch", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static LegacyNativeDbSafeDictionary<string, Entry> Entries(Native owner) =>
        Assert.IsType<LegacyNativeDbSafeDictionary<string, Entry>>(CollectionField.GetValue(owner));

    private static void AssertForwardClock(DateTime before, DateTime after) =>
        Assert.True(after >= before, "A backwards real local clock invalidates this bounded DateTime.Now observation; no replacement clock is used.");

    private static void AssertRealValueOracle(Native owner, string key, Entry row)
    {
        var clockBefore = DateTime.Now;
        var valueBefore = row.Unbantime.Value();
        var actual = owner.IsBanned(key);
        var valueAfter = row.Unbantime.Value();
        var clockAfter = DateTime.Now;
        AssertForwardClock(clockBefore, clockAfter);
        Assert.Equal(valueBefore != 0, valueAfter != 0);
        Assert.Equal(valueBefore != 0, actual);
    }
}
