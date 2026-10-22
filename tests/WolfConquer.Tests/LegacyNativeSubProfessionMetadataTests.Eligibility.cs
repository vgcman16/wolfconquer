using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubProfessionMetadataTests
{
    [Fact]
    public void AllowUpdateUsesActualLevelAndRebornWithInclusiveEqualityAndFullWidthNeedLevel()
    {
        var row = new LegacyNativeSubProfessionInfo.ProfessionInfo
        {
            Damage = [7], NeedLevel = 120, NeedReborn = 2,
        };
        var metadata = new LegacyNativeSubProfessionInfo
        {
            [LegacyNativeDbLevExp.Sort.MartialArtist] = new() { [3] = row },
        };
        Assert.True(metadata.AllowUpdate(Client(120, 2), LegacyNativeDbLevExp.Sort.MartialArtist, 3));
        Assert.False(metadata.AllowUpdate(Client(119, 2), LegacyNativeDbLevExp.Sort.MartialArtist, 3));
        Assert.False(metadata.AllowUpdate(Client(121, 1), LegacyNativeDbLevExp.Sort.MartialArtist, 3));
        Assert.True(metadata.AllowUpdate(Client(121, 3), LegacyNativeDbLevExp.Sort.MartialArtist, 3));
        row.NeedLevel = 65536;
        Assert.False(metadata.AllowUpdate(Client(ushort.MaxValue, byte.MaxValue), LegacyNativeDbLevExp.Sort.MartialArtist, 3));
        row.NeedLevel = ushort.MaxValue;
        row.NeedReborn = byte.MaxValue;
        Assert.True(metadata.AllowUpdate(Client(ushort.MaxValue, byte.MaxValue), LegacyNativeDbLevExp.Sort.MartialArtist, 3));
    }

    [Fact]
    public void AllowUpdatePreservesDictionaryAndNullFaultsAndDoesNotConsultDamage()
    {
        var metadata = new LegacyNativeSubProfessionInfo();
        var client = Client(0, 0);
        Assert.Throws<KeyNotFoundException>(() => metadata.AllowUpdate(null!, LegacyNativeDbLevExp.Sort.MartialArtist, 0));
        metadata[LegacyNativeDbLevExp.Sort.MartialArtist] = new();
        Assert.Throws<KeyNotFoundException>(() => metadata.AllowUpdate(client, LegacyNativeDbLevExp.Sort.MartialArtist, 0));
        metadata[LegacyNativeDbLevExp.Sort.MartialArtist][0] = new() { Damage = null!, NeedLevel = 0, NeedReborn = 0 };
        Assert.True(metadata.AllowUpdate(client, LegacyNativeDbLevExp.Sort.MartialArtist, 0));
        Assert.Throws<NullReferenceException>(() => metadata.AllowUpdate(null!, LegacyNativeDbLevExp.Sort.MartialArtist, 0));
        metadata[LegacyNativeDbLevExp.Sort.MartialArtist][0] = null!;
        Assert.Throws<NullReferenceException>(() => metadata.AllowUpdate(client, LegacyNativeDbLevExp.Sort.MartialArtist, 0));
        metadata[LegacyNativeDbLevExp.Sort.MartialArtist] = null!;
        Assert.Throws<NullReferenceException>(() => metadata.AllowUpdate(client, LegacyNativeDbLevExp.Sort.MartialArtist, 0));
        metadata[LegacyNativeDbLevExp.Sort.MartialArtist] = new() { [0] = new() { Damage = [1] } };
        client.Player = null!;
        Assert.Throws<NullReferenceException>(() => metadata.AllowUpdate(client, LegacyNativeDbLevExp.Sort.MartialArtist, 0));
    }
}
