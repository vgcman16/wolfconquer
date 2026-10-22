using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeProficiencyCheckTests
{
    [Fact]
    public void MissingIdsReturnFalseIncludingZeroRequiredLevelWithoutReadingTheNullOwner()
    {
        var storage = new LegacyNativeProficiencyStorage(null!);
        Assert.False(storage.CheckProf(0, 0));
        Assert.False(storage.CheckProf(ushort.MaxValue, byte.MaxValue));
        storage.ClientProf[uint.MaxValue] = new LegacyNativeMsgProficiency { ID = 0, Level = uint.MaxValue };
        Assert.False(storage.CheckProf(ushort.MaxValue, 0));
    }

    [Fact]
    public void CheckUsesTheLiveDictionaryValueLevelAtFullUIntWidthNotItsIdOrPreviousLevel()
    {
        var storage = new LegacyNativeProficiencyStorage(null!);
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
    }

    [Fact]
    public void PresentNullRowsAndNullPhysicalDictionaryRetainTheirNativeDereferenceFaults()
    {
        var storage = new LegacyNativeProficiencyStorage(null!);
        storage.ClientProf[7] = null!;
        Assert.Throws<NullReferenceException>(() => storage.CheckProf(7, 0));
        Assert.False(storage.CheckProf(8, 0));
        storage.ClientProf = null!;
        Assert.Throws<NullReferenceException>(() => storage.CheckProf(8, 0));
    }
}
