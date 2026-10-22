using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Declared shared-owner MODEL substitutions only; no Loading/Parse/host publication.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypeNameTests
{
    [Fact]
    public void MethodRetainsPublicInstanceStringReturnAndExactUintIdParameter()
    {
        var method = typeof(LegacyNativeItemTypeRuneMetadata).GetMethod(
            "GetItemName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotNull(method);
        Assert.False(method.IsStatic);
        Assert.False(method.IsVirtual);
        Assert.Equal(typeof(string), method.ReturnType);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(uint), parameter.ParameterType);
        Assert.Equal("ID", parameter.Name);
    }

    [Fact]
    public void DifferentGlobalOwnerWinsOverReceiverAndReturnsExactNameReference()
    {
        var globalName = new string(new[] { ' ', 'G', '\0', ' ' });
        var receiverName = new string(new[] { 'R', 'e', 'c' });
        var globalRow = new LegacyNativeItemRuneMetadata { ID = 999u, Name = globalName };
        var receiverRow = new LegacyNativeItemRuneMetadata { ID = 41u, Name = receiverName };
        var global = new LegacyNativeItemTypeRuneMetadata { [41u] = globalRow };
        var receiver = new LegacyNativeItemTypeRuneMetadata { [41u] = receiverRow };
        using var scope = new ItemsBaseScope(global);

        Assert.Same(globalName, receiver.GetItemName(41u));
        Assert.Same(globalName, receiver.GetItemName(41u));
        Assert.Same(global, LegacyNativePoolItems.ItemsBase);
        Assert.Same(globalRow, global[41u]);
        Assert.Same(receiverRow, receiver[41u]);
        Assert.Same(receiverName, receiverRow.Name);
        Assert.Equal(999u, globalRow.ID);
        Assert.Single(global);
        Assert.Single(receiver);
    }

    [Fact]
    public void GlobalMissReturnsLiteralEmptyDespiteReceiverHitAndDoesNotAdmitRows()
    {
        var receiverRow = new LegacyNativeItemRuneMetadata { ID = 47u, Name = "receiver-only" };
        var unrelated = new LegacyNativeItemRuneMetadata { ID = 48u, Name = "unrelated-global" };
        var global = new LegacyNativeItemTypeRuneMetadata { [48u] = unrelated };
        var receiver = new LegacyNativeItemTypeRuneMetadata { [47u] = receiverRow };
        using var scope = new ItemsBaseScope(global);

        Assert.Same(string.Empty, receiver.GetItemName(47u));
        Assert.Same(string.Empty, receiver.GetItemName(47u));
        Assert.False(global.ContainsKey(47u));
        Assert.Single(global);
        Assert.Same(unrelated, global[48u]);
        Assert.Single(receiver);
        Assert.Same(receiverRow, receiver[47u]);
    }

    [Fact]
    public void ReplacingGlobalIsObservedWithoutPublishingReceiverOrReusingOldAlias()
    {
        var firstName = new string(new[] { 'o', 'l', 'd' });
        var nextName = new string(new[] { 'n', 'e', 'w' });
        var firstRow = new LegacyNativeItemRuneMetadata { Name = firstName };
        var first = new LegacyNativeItemTypeRuneMetadata { [7u] = firstRow };
        using var scope = new ItemsBaseScope(first);
        var receiver = new LegacyNativeItemTypeRuneMetadata();
        Assert.Same(first, LegacyNativePoolItems.ItemsBase);
        var escapedName = receiver.GetItemName(7u);
        Assert.Same(firstName, escapedName);
        var replacementRow = new LegacyNativeItemRuneMetadata { Name = nextName };
        var replacement = new LegacyNativeItemTypeRuneMetadata { [7u] = replacementRow };

        LegacyNativePoolItems.ItemsBase = replacement;
        _ = new LegacyNativeItemTypeRuneMetadata();
        firstRow.Name = "old-owner-mutated";

        Assert.Same(replacement, LegacyNativePoolItems.ItemsBase);
        Assert.Same(nextName, receiver.GetItemName(7u));
        Assert.Same(firstName, escapedName);
        Assert.Same(firstRow, first[7u]);
        Assert.Equal("old-owner-mutated", firstRow.Name);
        Assert.Same(replacementRow, replacement[7u]);
        Assert.Empty(receiver);
    }

    [Fact]
    public void LiveNameChangesRowReplacementRemovalAndReadmissionAreNotCached()
    {
        var firstName = new string(new[] { 'a' });
        var secondName = new string(new[] { 'b' });
        var thirdName = new string(new[] { 'c' });
        var row = new LegacyNativeItemRuneMetadata { ID = 12u, Name = firstName };
        var global = new LegacyNativeItemTypeRuneMetadata { [12u] = row };
        var receiver = new LegacyNativeItemTypeRuneMetadata();
        using var scope = new ItemsBaseScope(global);
        Assert.Same(firstName, receiver.GetItemName(12u));

        row.Name = secondName;
        row.ID = uint.MaxValue;
        Assert.Same(secondName, receiver.GetItemName(12u));
        var replacement = new LegacyNativeItemRuneMetadata { ID = 0u, Name = thirdName };
        global[12u] = replacement;
        Assert.Same(thirdName, receiver.GetItemName(12u));
        Assert.Same(secondName, row.Name);
        Assert.True(global.Remove(12u));
        Assert.Same(string.Empty, receiver.GetItemName(12u));
        global.Add(12u, row);
        Assert.Same(secondName, receiver.GetItemName(12u));
        Assert.Same(row, global[12u]);
        Assert.Empty(receiver);
    }

    [Fact]
    public void StoredNullEmptyAndUntrimmedNamesPassThroughWithoutMissingKeyFallback()
    {
        var row = new LegacyNativeItemRuneMetadata();
        var global = new LegacyNativeItemTypeRuneMetadata { [5u] = row };
        var receiver = new LegacyNativeItemTypeRuneMetadata();
        using var scope = new ItemsBaseScope(global);

        Assert.Null(row.Name);
        Assert.Null(receiver.GetItemName(5u));
        row.Name = "";
        Assert.Same(row.Name, receiver.GetItemName(5u));
        var rawName = new string(new[] { ' ', '\t', '\0', ' ', 'Z', ' ' });
        row.Name = rawName;
        Assert.Same(rawName, receiver.GetItemName(5u));
        Assert.Same(row, global[5u]);
        Assert.Single(global);
        Assert.Empty(receiver);
    }

    [Fact]
    public void NullGlobalFaultsEvenWhenReceiverContainsKeyAndIsNeverRepaired()
    {
        var row = new LegacyNativeItemRuneMetadata { ID = 5u, Name = "receiver-hit" };
        var receiver = new LegacyNativeItemTypeRuneMetadata { [5u] = row };
        using var scope = new ItemsBaseScope(null);

        Assert.Throws<NullReferenceException>(() => receiver.GetItemName(5u));
        Assert.Throws<NullReferenceException>(() => receiver.GetItemName(6u));
        Assert.Null(LegacyNativePoolItems.ItemsBase);
        Assert.Single(receiver);
        Assert.Same(row, receiver[5u]);
        Assert.Equal("receiver-hit", row.Name);
    }

    [Fact]
    public void FoundNullGlobalRowFaultsInsteadOfUsingReceiverOrEmptyFallback()
    {
        var global = new LegacyNativeItemTypeRuneMetadata { [5u] = null! };
        var receiverRow = new LegacyNativeItemRuneMetadata { Name = "receiver-hit" };
        var receiver = new LegacyNativeItemTypeRuneMetadata { [5u] = receiverRow };
        using var scope = new ItemsBaseScope(global);

        Assert.Throws<NullReferenceException>(() => receiver.GetItemName(5u));
        Assert.Same(string.Empty, receiver.GetItemName(6u));
        Assert.Same(global, LegacyNativePoolItems.ItemsBase);
        Assert.Single(global);
        Assert.Null(global[5u]);
        Assert.Same(receiverRow, receiver[5u]);
    }

    [Fact]
    public void FullUintKeysStayExactAndCanShareOneRowRegardlessOfItsMutableId()
    {
        var name = new string(new[] { 's', 'h', 'a', 'r', 'e', 'd' });
        var row = new LegacyNativeItemRuneMetadata { ID = 123u, Name = name };
        var global = new LegacyNativeItemTypeRuneMetadata { [0u] = row, [uint.MaxValue] = row };
        var receiver = new LegacyNativeItemTypeRuneMetadata();
        using var scope = new ItemsBaseScope(global);

        Assert.Same(name, receiver.GetItemName(0u));
        Assert.Same(name, receiver.GetItemName(uint.MaxValue));
        Assert.Same(string.Empty, receiver.GetItemName(1u));
        row.ID = uint.MaxValue;
        Assert.Same(name, receiver.GetItemName(0u));
        Assert.Same(row, global[0u]);
        Assert.Same(row, global[uint.MaxValue]);
        Assert.Equal(2, global.Count);
        Assert.Empty(receiver);
    }

    private sealed class ItemsBaseScope : IDisposable
    {
        private readonly LegacyNativeItemTypeRuneMetadata? original = LegacyNativePoolItems.ItemsBase;

        public ItemsBaseScope(LegacyNativeItemTypeRuneMetadata? declared)
        {
            LegacyNativePoolItems.ItemsBase = declared;
        }

        public void Dispose()
        {
            LegacyNativePoolItems.ItemsBase = original;
        }
    }
}
