using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeRuneStorageSafetyTests
{
    private static readonly FieldInfo StorageItems = typeof(LegacyNativeRuneItemStorage)
        .GetField("items", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo StorageCache = typeof(LegacyNativeRuneItemStorage)
        .GetField("objects", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static LegacyNativeGameItemRuntime Item(uint uid, ushort position = 212, uint id = 181305) =>
        new() { Uid = uid, Position = position, ITEM_ID = id };

    private sealed class Scope : LegacyNativeRuneStorageTestOwner
    {
        internal Dictionary<uint, LegacyNativeGameItemRuntime> Dictionary =>
            (Dictionary<uint, LegacyNativeGameItemRuntime>)StorageItems.GetValue(Rune)!;
        internal LegacyNativeGameItemRuntime[] Cache =>
            (LegacyNativeGameItemRuntime[])StorageCache.GetValue(Rune)!;
    }

    private sealed class ArmedComparer : IEqualityComparer<uint>
    {
        internal Action? OnHash;
        internal uint Trigger;
        public bool Equals(uint x, uint y) => x == y;
        public int GetHashCode(uint value)
        {
            if (value == Trigger && OnHash is { } action)
            {
                OnHash = null;
                action();
            }
            return value.GetHashCode();
        }
    }

    private static ArmedComparer InstallComparer(Scope scope)
    {
        var comparer = new ArmedComparer();
        var replacement = new Dictionary<uint, LegacyNativeGameItemRuntime>(comparer);
        foreach (var entry in scope.Dictionary) replacement.Add(entry.Key, entry.Value);
        StorageItems.SetValue(scope.Rune, replacement);
        return comparer;
    }

    private static void AssertUnavailable(Scope scope)
    {
        Assert.False(scope.Rune.Add(Item(88)));
        Assert.False(scope.Rune.Free(101));
        Assert.False(scope.Rune.Free(211));
        Assert.False(scope.Rune.Free(212));
        Assert.Throws<InvalidDataException>(() => scope.Rune.Count);
        Assert.Throws<InvalidDataException>(() => scope.Rune.Objects);
        Assert.Throws<InvalidDataException>(() => scope.Rune.ContainsUID(7));
        Assert.Throws<InvalidDataException>(() => scope.Rune.TryGetItem(7u, out _));
        Assert.Throws<InvalidDataException>(() => scope.Rune.TryGetItem((byte)101));
        Assert.Throws<InvalidDataException>(() => scope.Rune.TryGetItem(181305u));
        Assert.Empty(scope.Rune.WornObjects);
        Assert.Equal((byte)0, scope.Rune.EquippedCount);
    }
}

// Shared only by these source-owned tests and the narrow migrated old storage controls.
internal class LegacyNativeRuneStorageTestOwner : IDisposable
{
    private readonly LegacyNativeSafeRandom priorRandom = LegacyNativePoolRandom.GetRandom;
    private readonly DateTime priorReset = LegacyNativePoolRandom.ResetRandom;
    internal LegacyNativeGameClientRuntime Client { get; }
    internal LegacyNativeRuneItemStorage Rune { get; }
    internal LegacyNativeRuneStorageTestOwner()
    {
        LegacyNativePoolRandom.GetRandom = new(91);
        LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
        Client = new();
        Rune = new(Client);
        Client.Rune = Rune;
        Client.Equipment = new(Client);
    }
    public void Dispose()
    {
        LegacyNativePoolRandom.GetRandom = priorRandom;
        LegacyNativePoolRandom.ResetRandom = priorReset;
    }
}
