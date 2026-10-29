using System.Collections;
using System.Reflection;
using System.Threading;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LegacyNativeDbPurePrerequisiteTests
{
    [Fact]
    public void DefaultConstructorOwnsOneIndependentStableGateAndEmptySnapshots()
    {
        var first = new LegacyNativeDbSafeDictionary<byte, string>();
        var second = new LegacyNativeDbSafeDictionary<byte, string>();
        Assert.Empty(first); Assert.False(first.Update); Assert.Equal(0, first.Revision);
        Assert.NotNull(first.SyncRoot); Assert.NotSame(first.SyncRoot, second.SyncRoot);
        Assert.Empty(first.MyArray); Assert.Empty(first.GetValues());
        Assert.NotSame(first.MyArray, first.MyArray);
        Assert.Same(first.SyncRoot, ((ICollection)first).SyncRoot);
    }

    [Fact]
    public void CapacityConstructorKeepsValidCapacityAndRejectsNegativeCapacity()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>(4);
        Assert.Empty(dictionary); Assert.False(dictionary.Update); Assert.Empty(dictionary.MyArray);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LegacyNativeDbSafeDictionary<byte, string>(-1));
    }

    [Fact]
    public void MissingNativeIndexerReturnsDefaultWithoutAdmittingAKey()
    {
        var references = new LegacyNativeDbSafeDictionary<byte, string>();
        var values = new LegacyNativeDbSafeDictionary<int, int>();
        Assert.Null(references[1]); Assert.Equal(0, values[1]);
        Assert.Empty(references); Assert.Empty(values);
        Assert.False(references.Update); Assert.False(values.Update);
        Assert.Throws<KeyNotFoundException>(() => _ = ((IDictionary<byte, string>)references)[1]);
        Assert.Throws<KeyNotFoundException>(() => _ = ((IReadOnlyDictionary<int, int>)values)[1]);
    }

    [Fact]
    public void NullReferenceKeysRejectWithoutChangingOwnerState()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<string, string>();
        Assert.Throws<ArgumentNullException>(() => _ = dictionary[null!]);
        Assert.Throws<ArgumentNullException>(() => dictionary.Add(null!, "value"));
        Assert.Empty(dictionary); Assert.False(dictionary.Update); Assert.Equal(0, dictionary.Revision);
    }

    [Fact]
    public void NativeAddKeepsUpsertAndInvalidatesTheCurrentValueSnapshot()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(7, "first"); Assert.True(dictionary.Update); Assert.Equal(1, dictionary.Revision);
        var first = dictionary.GetValues(); Assert.False(dictionary.Update);
        dictionary.Add(7, "second");
        Assert.Single(dictionary); Assert.True(dictionary.Update); Assert.Equal(2, dictionary.Revision);
        Assert.Equal("second", dictionary[7]); Assert.Equal(new[] { "second" }, dictionary.GetValues());
        Assert.Equal(new[] { "first" }, first);
    }

    [Fact]
    public void ValueSnapshotsKeepActualReferencesAndDefendTheCachedArray()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, object>();
        var value = new object(); dictionary.Add(9, value);
        var first = dictionary.GetValues(); var second = dictionary.GetValues();
        Assert.False(dictionary.Update); Assert.NotSame(first, second); Assert.Same(value, first[0]);
        first[0] = new object();
        Assert.Same(value, second[0]); Assert.Same(value, dictionary.GetValues()[0]);
        var escapedCache = dictionary.MyArray; escapedCache[0] = null!;
        Assert.Same(value, dictionary.MyArray[0]); Assert.Same(value, dictionary[9]);
    }

    [Fact]
    public void IndexerAssignmentsInvalidatePreviouslyMaterializedValues()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "old"); var cached = dictionary.GetValues();
        dictionary[1] = "new"; dictionary[2] = "second";
        Assert.True(dictionary.Update); Assert.Equal(3, dictionary.Revision);
        Assert.Equal(new[] { "new", "second" }, dictionary.GetValues());
        Assert.Equal(new[] { "old" }, cached); Assert.False(dictionary.Update);
    }

    [Fact]
    public void InterfaceAddIsDuplicateRejectingAndCannotBypassSnapshotInvalidation()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        IDictionary<byte, string> contract = dictionary;
        contract.Add(1, "first"); Assert.True(dictionary.Update);
        dictionary.GetValues(); long revision = dictionary.Revision;
        Assert.Throws<ArgumentException>(() => contract.Add(1, "second"));
        Assert.False(dictionary.Update); Assert.Equal(revision, dictionary.Revision);
        Assert.Equal(new[] { "first" }, dictionary.GetValues());
        dictionary.Add(1, "native-upsert");
        Assert.Equal("native-upsert", contract[1]); Assert.Equal(revision + 1, dictionary.Revision);
    }

    [Fact]
    public void MissingRemoveAndDuplicateTryAddPreserveRevisionAndCleanCache()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one"); dictionary.GetValues(); long revision = dictionary.Revision;
        Assert.False(dictionary.Remove(99)); Assert.False(dictionary.TryAdd(1, "ignored"));
        Assert.False(dictionary.Update); Assert.Equal(revision, dictionary.Revision);
        Assert.Equal(new[] { "one" }, dictionary.GetValues());
        Assert.True(dictionary.Remove(1)); Assert.True(dictionary.Update);
        Assert.Equal(revision + 1, dictionary.Revision); Assert.Empty(dictionary.GetValues());
    }

    [Fact]
    public void InterfaceRemoveRefreshesSnapshotAndLeavesEscapedOldCopyUnchanged()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one"); var cached = dictionary.GetValues();
        Assert.True(((IDictionary<byte, string>)dictionary).Remove(1));
        Assert.Empty(dictionary); Assert.True(dictionary.Update); Assert.Empty(dictionary.GetValues());
        Assert.Equal(new[] { "one" }, cached);
    }

    [Fact]
    public void ClearInvalidatesValuesAndAnEmptyClearDoesNotInventAMutation()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one"); var cached = dictionary.GetValues();
        dictionary.Clear(); Assert.Empty(dictionary); Assert.True(dictionary.Update);
        Assert.Empty(dictionary.GetValues()); Assert.Equal(new[] { "one" }, cached);
        long revision = dictionary.Revision; dictionary.Clear();
        Assert.Equal(revision, dictionary.Revision); Assert.False(dictionary.Update);
    }

    [Fact]
    public void NonGenericClearUsesTheSameOwnerAndInvalidatesItsSnapshot()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<byte, string>();
        dictionary.Add(1, "one"); dictionary.GetValues();
        ((IDictionary)dictionary).Clear();
        Assert.Empty(dictionary); Assert.True(dictionary.Update); Assert.Empty(dictionary.GetValues());
        Assert.Equal(2, dictionary.Revision);
    }

    [Fact]
    public void CacheAndGateAreReadOnlyOwnerPropertiesInsteadOfPublicMutationFields()
    {
        var type = typeof(LegacyNativeDbSafeDictionary<,>);
        foreach (string property in new[] { "SyncRoot", "Update", "MyArray", "Revision" })
        {
            Assert.Null(type.GetField(property));
            Assert.NotNull(type.GetProperty(property)); Assert.Null(type.GetProperty(property)!.SetMethod);
        }
        Assert.Equal(typeof(object), type.BaseType);
        Assert.True(typeof(IDictionary<int, string>).IsAssignableFrom(typeof(LegacyNativeDbSafeDictionary<int, string>)));
        Assert.False(typeof(Dictionary<int, string>).IsAssignableFrom(typeof(LegacyNativeDbSafeDictionary<int, string>)));
        Assert.Empty(type.GetGenericArguments()[0].GetGenericParameterConstraints());
        Assert.Equal(GenericParameterAttributes.None, type.GetGenericArguments()[0].GenericParameterAttributes);
    }

    [Fact]
    public void NullKeyWriteReleasesTheStableMonitorAndKeepsSubsequentWritesUsable()
    {
        var dictionary = new LegacyNativeDbSafeDictionary<string, string>(); var gate = dictionary.SyncRoot;
        Assert.Throws<ArgumentNullException>(() => dictionary.Add(null!, "value"));
        Assert.False(Monitor.IsEntered(gate)); Assert.False(dictionary.Update); Assert.Empty(dictionary);
        dictionary.Add("key", "value"); Assert.False(Monitor.IsEntered(gate));
        dictionary.GetValues(); dictionary.Remove("key"); dictionary.Clear();
        Assert.False(Monitor.IsEntered(gate)); Assert.Same(gate, dictionary.SyncRoot);
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
