using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task ActualSavedOptionalDefaultsAreCanonicalizedThroughEarnedContinuation() => Run(async (f, monster, catalog) =>
    {
        var original = Assert.IsType<byte[]>(f.OmittedOptionalScalarInput);
        var input = CharacterSnapshotFileEncoding.Decode(original, out _);
        Assert.DoesNotContain("\r\nMaxMana=", input, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\r\nExpBallUsed=", input, StringComparison.OrdinalIgnoreCase);
        var parsed = CharacterSnapshotParser.ParseText(input);
        Assert.Equal(parsed.MinMana, parsed.MaxMana); Assert.Equal(0, parsed.ExpBallUsed);

        var before = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var expected = f.Data.Files();
        var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        var text = Encoding.UTF8.GetString(expected[key]);
        text = Replace(text, "Level=138", "Level=140");
        text = Replace(text, "Atributes=12", "Atributes=18");
        text = Replace(text, "MinHitPoints=5000", "MinHitPoints=11136");
        if (!text.Contains("\r\nMaxMana=", StringComparison.OrdinalIgnoreCase)) text += "MaxMana=0\r\n";
        if (!text.Contains("\r\nExpBallUsed=", StringComparison.OrdinalIgnoreCase)) text += "ExpBallUsed=0\r\n";
        expected[key] = Encoding.UTF8.GetBytes(text);

        await Hit(f, monster);

        Assert.True(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _), Diagnostic(f));
        var after = f.Current();
        Assert.Equal(140, after.Character.Level); Assert.Equal(0, after.Character.Experience);
        Assert.Equal(18, after.Character.AttributePoints); Assert.Equal(11136, after.Character.MinHitPoints);
        Assert.Equal((ushort)140, binding.Player.Level); Assert.Equal(0UL, binding.Player.Experience);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            after.AccountId, checked((uint)after.Character.Uid)));
        f.Data.AssertFiles(expected);
    }, omitOptionalScalars: true);
}
