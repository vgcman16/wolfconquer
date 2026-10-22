using System.Reflection;
using System.Threading;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LegacyNativeDbPurePrerequisiteTests
{
    [Fact]
    public void DefaultConstructorRetainsIndependentPublicNativeState()
    {
        var first = new LegacyNativeDbSafeDictionary<byte, string>();
        var second = new LegacyNativeDbSafeDictionary<byte, string>();
        Assert.Empty(first);
        Assert.False(first.Update);
        Assert.NotNull(first.SyncRoot);
        Assert.Empty(first.MyArray);
        Assert.NotSame(first.SyncRoot, second.SyncRoot);
        Assert.NotSame(first.MyArray, second.MyArray);
        Assert.Same(first.MyArray, first.GetValues());
    }

    [Fact]
    public void CapacityConstructorPreservesTheBclCapacityFault()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>(4);
        Assert.Empty(dictionary);
        Assert.False(dictionary.Update);
        Assert.NotNull(dictionary.SyncRoot);
        Assert.Empty(dictionary.MyArray);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LegacyNativeDbSafeDictionary<byte, string>(-1));
    }

    [Fact]
    public void MissingIndexerReturnsGenericDefaultWithoutAdmittingAKey()
    {
        var references = new LegacyNativeDbSafeDictionary<byte, string>();
        var values = new LegacyNativeDbSafeDictionary<int, int>();
        Assert.Null(references[1]);
        Assert.Equal(0, values[1]);
        Assert.Empty(references);
        Assert.Empty(values);
        Assert.False(references.Update);
        Assert.False(values.Update);
    }

    [Fact]
    public void NullReferenceKeyKeepsTheDictionaryContainsKeyFault()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<string, string>();
        Assert.Throws<ArgumentNullException>(() => _ = dictionary[null!]);
        Assert.Empty(dictionary);
        Assert.False(dictionary.Update);
    }

    [Fact]
    public void NativeAddOverwritesTheExistingKeyAndMarksDirty()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        var first = new string("first".ToCharArray());
        var second = new string("second".ToCharArray());
        dictionary.Add(7, first);
        Assert.True(dictionary.Update);
        Assert.Same(first, dictionary[7]);
        dictionary.GetValues();
        Assert.False(dictionary.Update);
        dictionary.Add(7, second);
        Assert.Single(dictionary);
        Assert.True(dictionary.Update);
        Assert.Same(second, dictionary[7]);
    }

    [Fact]
    public void GetValuesRebuildsOnceWithTheSameActualReferenceValues()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, object>();
        var value = new object();
        var old = dictionary.MyArray;
        dictionary.Add(9, value);
        var rebuilt = dictionary.GetValues();
        Assert.False(dictionary.Update);
        Assert.NotSame(old, rebuilt);
        Assert.Single(rebuilt);
        Assert.Same(value, rebuilt[0]);
        Assert.Same(rebuilt, dictionary.MyArray);
        Assert.Same(rebuilt, dictionary.GetValues());
    }

    [Fact]
    public void IndexerSetterDeliberatelyLeavesThePreviousCacheStale()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "old");
        var cached = dictionary.GetValues();
        dictionary[1] = "new";
        dictionary[2] = "second";
        Assert.False(dictionary.Update);
        Assert.Equal("new", dictionary[1]);
        Assert.Equal(2, dictionary.Count);
        Assert.Same(cached, dictionary.GetValues());
        Assert.Equal(new[] { "old" }, cached);
    }

    [Fact]
    public void InheritedAddRetainsDuplicateFailureAndBypassesNativeDirtyState()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        Dictionary<byte, string> inherited = dictionary;
        inherited.Add(1, "first");
        Assert.False(dictionary.Update);
        Assert.Empty(dictionary.GetValues());
        Assert.Throws<ArgumentException>(() => inherited.Add(1, "second"));
        Assert.Equal("first", dictionary[1]);
        dictionary.Add(1, "native-overwrite");
        Assert.Equal("native-overwrite", inherited[1]);
        Assert.True(dictionary.Update);
    }

    [Fact]
    public void NativeRemoveMarksDirtyEvenForAMissingKey()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one");
        dictionary.GetValues();
        dictionary.Remove(99);
        Assert.True(dictionary.Update);
        Assert.Equal("one", dictionary[1]);
        Assert.Equal(new[] { "one" }, dictionary.GetValues());
        dictionary.Remove(1);
        Assert.True(dictionary.Update);
        Assert.Empty(dictionary.GetValues());
        Assert.Empty(dictionary);
    }

    [Fact]
    public void InheritedRemoveLeavesTheNativeSnapshotStale()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one");
        var cached = dictionary.GetValues();
        Dictionary<byte, string> inherited = dictionary;
        Assert.True(inherited.Remove(1));
        Assert.Empty(dictionary);
        Assert.False(dictionary.Update);
        Assert.Same(cached, dictionary.GetValues());
        Assert.Equal(new[] { "one" }, cached);
    }

    [Fact]
    public void NativeClearMarksDirtyAndDefersSnapshotReplacement()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one");
        var cached = dictionary.GetValues();
        dictionary.Clear();
        Assert.Empty(dictionary);
        Assert.True(dictionary.Update);
        Assert.Same(cached, dictionary.MyArray);
        var rebuilt = dictionary.GetValues();
        Assert.Empty(rebuilt);
        Assert.NotSame(cached, rebuilt);
        Assert.False(dictionary.Update);
        dictionary.Clear();
        Assert.True(dictionary.Update);
    }

    [Fact]
    public void InheritedClearBypassesNativeDirtyAndCacheUpdates()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one");
        var cached = dictionary.GetValues();
        ((Dictionary<byte, string>)dictionary).Clear();
        Assert.Empty(dictionary);
        Assert.False(dictionary.Update);
        Assert.Same(cached, dictionary.GetValues());
        Assert.Equal(new[] { "one" }, cached);
    }

    [Fact]
    public void PublicArrayAndDirtyFieldsKeepNativeUnrepairedStateModels()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        var injected = new[] { "external" };
        dictionary.MyArray = injected;
        Assert.Same(injected, dictionary.GetValues());
        injected[0] = "changed";
        Assert.Equal("changed", dictionary.GetValues()[0]);
        Assert.Empty(dictionary);
        // Explicit native public-field MODEL, not a reachable SQL caller assertion.
        dictionary.MyArray = null!;
        Assert.Null(dictionary.GetValues());
        dictionary.Update = true;
        Assert.Empty(dictionary.GetValues());
        Assert.False(dictionary.Update);
        Assert.NotNull(dictionary.MyArray);
    }

    [Fact]
    public void PublicNullSyncRootKeepsLockFaultsAndPartialOrder()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one");
        var cached = dictionary.GetValues();
        // Explicit native public-field MODEL; no production repair is introduced.
        dictionary.SyncRoot = null!;
        Assert.Throws<ArgumentNullException>(() => dictionary.Add(2, "two"));
        Assert.False(dictionary.Update);
        Assert.False(dictionary.ContainsKey(2));
        Assert.Same(cached, dictionary.MyArray);
        dictionary.Update = true;
        Assert.Throws<ArgumentNullException>(() => dictionary.GetValues());
        Assert.True(dictionary.Update);
        dictionary.Update = false;
        Assert.Throws<ArgumentNullException>(() => dictionary.Remove(1));
        Assert.False(dictionary.Update);
        Assert.True(dictionary.ContainsKey(1));
        Assert.Throws<ArgumentNullException>(() => dictionary.Clear());
        Assert.False(dictionary.Update);
        Assert.True(dictionary.ContainsKey(1));
    }

    [Fact]
    public void NullKeyWriteFaultReleasesTheActualChosenMonitor()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<string, string>();
        var sync = new object();
        dictionary.SyncRoot = sync;
        Assert.Throws<ArgumentNullException>(() => dictionary.Add(null!, "value"));
        Assert.False(Monitor.IsEntered(sync));
        Assert.False(dictionary.Update);
        Assert.Empty(dictionary);
        dictionary.Add("key", "value");
        Assert.False(Monitor.IsEntered(sync));
        dictionary.GetValues();
        Assert.False(Monitor.IsEntered(sync));
        dictionary.Remove("key");
        Assert.False(Monitor.IsEntered(sync));
        dictionary.Clear();
        Assert.False(Monitor.IsEntered(sync));
    }

    [Fact]
    public void ContainerKeepsNativeGenericInheritanceAndNoInventedKeyConstraint()
    {
        var type = typeof(LegacyNativeDbSafeDictionary<,>);
        Assert.False(type.IsPublic);
        Assert.False(type.IsSealed);
        Assert.Equal(typeof(Dictionary<,>), type.BaseType!.GetGenericTypeDefinition());
        Assert.Null(type.TypeInitializer);
        var key = type.GetGenericArguments()[0];
        Assert.Equal(GenericParameterAttributes.None, key.GenericParameterAttributes);
        Assert.Empty(key.GetGenericParameterConstraints());
        Assert.NotNull(type.GetField("SyncRoot"));
        Assert.NotNull(type.GetField("Update"));
        Assert.NotNull(type.GetField("MyArray"));
    }

    [Fact]
    public void SqlEscapeReturnsTheExactSamePotentiallyUnsafeStringReference()
    {
        var value = new string("quote' slash\\ semicolon; --\r\n\0中文".ToCharArray());
        var result = value.MySqlEscape();
        Assert.Same(value, result);
        Assert.Equal(value, result);
        Assert.Contains("'", result);
        Assert.Contains(";", result);
        Assert.Contains("\0", result);
    }

    [Fact]
    public void SqlEscapeReturnsNullWithoutAnInventedValidationOrFallback()
    {
        string value = null!;
        Assert.Null(value.MySqlEscape());
        Assert.Same(string.Empty, string.Empty.MySqlEscape());
    }

    [Fact]
    public void CommandTypeRetainsTheFiveNativeNamesAndImplicitInt32Order()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(LegacyNativeMySqlCommandType)));
        Assert.Equal(new[] { "DELETE", "INSERT", "SELECT", "UPDATE", "COUNT" },
            Enum.GetNames<LegacyNativeMySqlCommandType>());
        Assert.Equal(0, (int)LegacyNativeMySqlCommandType.DELETE);
        Assert.Equal(1, (int)LegacyNativeMySqlCommandType.INSERT);
        Assert.Equal(2, (int)LegacyNativeMySqlCommandType.SELECT);
        Assert.Equal(3, (int)LegacyNativeMySqlCommandType.UPDATE);
        Assert.Equal(4, (int)LegacyNativeMySqlCommandType.COUNT);
        Assert.Equal(LegacyNativeMySqlCommandType.DELETE, default);
    }

    [Fact]
    public void CommandTypeAddsNeitherFlagsSemanticsNorUnknownValueValidation()
    {
        var type = typeof(LegacyNativeMySqlCommandType);
        Assert.False(type.IsPublic);
        Assert.False(type.IsDefined(typeof(FlagsAttribute), inherit: false));
        var unknown = (LegacyNativeMySqlCommandType)int.MaxValue;
        Assert.Equal(int.MaxValue, (int)unknown);
        Assert.False(Enum.IsDefined(unknown));
    }
}
