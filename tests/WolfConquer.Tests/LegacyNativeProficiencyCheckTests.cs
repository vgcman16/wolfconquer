using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeProficiencyCheckTests
{
    [Fact]
    public void NullRequiredConstructorOwnerIsRejectedExplicitly()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new LegacyNativeProficiencyStorage(null!));
        Assert.Equal("_own", error.ParamName);
    }

    [Fact]
    public void MissingIdsReturnFalseIncludingZeroRequiredLevelWithTheActualOwner()
    {
        var storage = CreateStorage();
        var map = storage.ClientProf;
        Assert.False(storage.CheckProf(0, 0));
        Assert.False(storage.CheckProf(ushort.MaxValue, byte.MaxValue));
        var outsideQueryKeyspace = new LegacyNativeMsgProficiency { ID = 0, Level = uint.MaxValue };
        map[uint.MaxValue] = outsideQueryKeyspace;
        Assert.False(storage.CheckProf(ushort.MaxValue, 0));
        Assert.Same(map, storage.ClientProf);
        Assert.Same(outsideQueryKeyspace, Assert.Single(map).Value);
        Assert.Equal(uint.MaxValue, outsideQueryKeyspace.Level);
    }

    [Fact]
    public void CheckUsesTheLiveDictionaryValueLevelAtFullUIntWidthNotItsIdOrPreviousLevel()
    {
        var storage = CreateStorage();
        var prof = new LegacyNativeMsgProficiency { ID = 7, Level = 254, PreviouseLevel = byte.MaxValue };
        storage.ClientProf[ushort.MaxValue] = prof;
        Assert.False(storage.CheckProf(ushort.MaxValue, 255));
        Assert.True(storage.CheckProf(ushort.MaxValue, 254));
        prof.Level = 255;
        Assert.True(storage.CheckProf(ushort.MaxValue, 255));
        prof.Level = uint.MaxValue;
        Assert.True(storage.CheckProf(ushort.MaxValue, 255));
        prof.Level = 0;
        Assert.True(storage.CheckProf(ushort.MaxValue, 0));
        Assert.False(storage.CheckProf(ushort.MaxValue, 1));
        Assert.Same(prof, storage.ClientProf[ushort.MaxValue]);
        Assert.Equal((uint)7, prof.ID);
        Assert.Equal(byte.MaxValue, prof.PreviouseLevel);
        Assert.Equal((uint)0, prof.Level);
    }

    [Fact]
    public void InvalidPhysicalMapOrQueriedRowIsAnExplicitFailureNotAnAbsentKeyResult()
    {
        var storage = CreateStorage();
        var original = storage.ClientProf;
        original[7] = null!;
        Assert.Throws<InvalidOperationException>(() => storage.CheckProf(7, 0));
        Assert.Same(original, storage.ClientProf);
        Assert.Null(original[7]);
        // A genuinely absent query key keeps its native false semantics.
        Assert.False(storage.CheckProf(8, 0));

        storage.ClientProf = null!;
        Assert.Throws<InvalidOperationException>(() => storage.CheckProf(8, 0));
        Assert.Null(storage.ClientProf);
        Assert.Null(original[7]);

        var replacement = new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>();
        var row = new LegacyNativeMsgProficiency { ID = 8, Level = uint.MaxValue };
        replacement[8] = row;
        storage.ClientProf = replacement;
        Assert.True(storage.CheckProf(8, byte.MaxValue));
        Assert.Same(replacement, storage.ClientProf);
        Assert.Same(row, replacement[8]);
    }

    private static LegacyNativeProficiencyStorage CreateStorage()
    {
        var client = new LegacyNativeGameClientRuntime { Fake = true, FullLoading = false };
        client.Player.UID = 1293700;
        var storage = new LegacyNativeProficiencyStorage(client);
        client.MyProfs = storage;
        return storage;
    }
}
