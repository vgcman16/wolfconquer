using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task ActualClampedStartupVigorDoesNotPreventEarnedContinuationOrRewriteSavedVigor() => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(before.Character.Attributes.TryGetValue("Vigor", out var currentVigor));
        Assert.NotEqual("9999", currentVigor);
        var files = f.Data.Files();
        var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        var text = Encoding.UTF8.GetString(files[key]);
        Assert.Contains("Vigor=9999\r\n", text, StringComparison.Ordinal);
        text = Replace(text, "Level=138", "Level=140");
        text = Replace(text, "Atributes=12", "Atributes=18");
        text = Replace(text, "MinHitPoints=5000", "MinHitPoints=11136");
        files[key] = Encoding.UTF8.GetBytes(text);
        await Hit(f, monster);
        Assert.True(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _), Diagnostic(f));
        var after = f.Current();
        Assert.Equal(140, after.Character.Level); Assert.Equal(0, after.Character.Experience);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal((ushort)140, binding.Player.Level);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            after.AccountId, checked((uint)after.Character.Uid)));
        f.Data.AssertFiles(files);
    }, excessVigor: true);
}
