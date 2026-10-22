using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEpicTrojanTests
{
    [Fact]
    public void AllSeventeenNativeFieldsRetainExactNamesWidthsSourceOrderAndMutableStorage()
    {
        var names = new[] { "EpicTrojanItemChange", "StageEpicTrojanQuest", "ChangeEpicTrojan", "CanChangeEpicMaterial",
            "EpicTrojanEvilArrayPoints", "EpicTrojanAbysalStage", "ChangeArrayEpicTrojan", "CanChangeArrayEpicMaterial",
            "EpicTrojanArrayItemChange", "EpicTrijanKillGhostReaver", "EpicTrojanMr_MirrorItemChange",
            "ChangeMr_MirrorEpicTrojan", "CanChangeMr_MirrorEpicMaterial", "EpicTrojanGeneralPakItemChange",
            "ChangeGeneralPakEpicTrojan", "CanChangGeneralPakMaterial", "EpicTrojanMrMirrorPrograss" };
        var fields = names.Select(name => typeof(LegacyNativePlayerRuntime).GetField(name, BindingFlags.Public | BindingFlags.Instance)!).ToArray();
        Assert.All(fields, field => { Assert.NotNull(field); Assert.False(field.IsInitOnly); });
        Assert.Equal(names, fields.OrderBy(field => field.MetadataToken).Select(field => field.Name));
        Assert.Equal(new[] { typeof(uint[]), typeof(byte), typeof(uint), typeof(byte), typeof(uint), typeof(uint), typeof(uint),
            typeof(byte), typeof(uint[]), typeof(byte), typeof(uint[]), typeof(uint), typeof(byte), typeof(uint[]), typeof(uint),
            typeof(byte), typeof(byte) }, fields.Select(field => field.FieldType));
        Assert.All(names, name => Assert.Null(typeof(LegacyNativePlayerRuntime).GetProperty(name)));
    }

    [Fact]
    public void PlayerBirthGivesFourIndependentTwoElementArraysAndAllThirteenNativeScalarDefaults()
    {
        var first = Player(); var second = Player();
        var arrays = Arrays(first);
        Assert.All(arrays, array => Assert.Equal(new uint[] { 0, 0 }, array));
        for (int i = 0; i < 4; i++)
        {
            Assert.NotSame(arrays[i], Arrays(second)[i]);
            for (int j = i + 1; j < 4; j++) Assert.NotSame(arrays[i], arrays[j]);
        }
        Assert.Equal(new uint[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 1, 0 }, Scalars(first));
        Assert.Same(first, first.Owner.Player);
    }

    [Fact]
    public void DefaultSaveHasExactlyTwentyTokensAndTrailingSlashWithoutPersistingGhostReaver()
    {
        using var culture = new CultureScope();
        var player = Player();
        player.EpicTrijanKillGhostReaver = 255;
        string saved = player.SaveEpicTrojan();
        Assert.Equal(DefaultRecord, saved);
        Assert.Equal(20, saved.Count(value => value == '/'));
        Assert.Equal(21, saved.Split('/').Length);
        Assert.Equal((byte)255, player.EpicTrijanKillGhostReaver);
    }
}
