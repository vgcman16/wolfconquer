using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private static byte[] FirstUseProfsGolden()
    {
        // Literal count1 and native20-byte row410, level0, previous0, XP0,
        // needed0. No production progression/projector supplies this expectation.
        var image = new byte[24]; BinaryPrimitives.WriteInt32LittleEndian(image, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(4), 410);
        return image;
    }

    private static byte[] FirstUseProficiencyWireGolden()
    {
        var image = new byte[20]; BinaryPrimitives.WriteUInt16LittleEndian(image, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(2), 2041);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(4), 410);
        return image;
    }

    private static Dictionary<string, byte[]> FirstUseAfterImages(FirstUseHitFixture f, Dictionary<string, byte[]> before)
    {
        var after = before.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        var characterKey = Path.GetRelativePath(f.Owner.Data.Root, f.CharacterPath);
        var text = CharacterSnapshotFileEncoding.Decode(before[characterKey], out var encoding);
        var oldHp = f.Lethal ? "MinHitPoints=200\r\n" : "MinHitPoints=2000\r\n";
        var newHp = f.Lethal ? "MinHitPoints=0\r\n" : "MinHitPoints=1700\r\n";
        Assert.Equal(2, text.Split(oldHp, StringSplitOptions.None).Length);
        after[characterKey] = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace(oldHp, newHp, StringComparison.Ordinal))).ToArray();
        after[Path.GetRelativePath(f.Owner.Data.Root, f.ProfsPath)] = FirstUseProfsGolden();
        if (f.Lethal && f.MissingSpells) after[Path.GetRelativePath(f.Owner.Data.Root, f.SpellsPath)] = new byte[4];
        return after;
    }

    private static void AssertFirstUseNativeBook(FirstUseHitFixture f)
    {
        var actor = f.Actor; var binding = actor.NativeGameClient!.OrdinarySpellBinding!;
        var saved = Assert.Single(actor.PlayerProficiencies.Proficiencies);
        Assert.Equal(410U, saved.Id); Assert.Equal(0U, saved.Level); Assert.Equal((byte)0, saved.PreviousLevel);
        Assert.Equal(0U, saved.Experience); Assert.Equal(0U, saved.NeededExperience);
        var native = Assert.Single(binding.Proficiencies.ClientProf).Value;
        Assert.Equal(410U, native.ID); Assert.Equal(0U, native.Level); Assert.Equal(0U, native.Experience);
        Assert.Equal((byte)0, native.PreviouseLevel); Assert.Equal(0U, native.NeededExperience);
        Assert.True(binding.SavedProficiencies.Matches(FirstUseProfsGolden()));
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(f.Lethal ? 0 : 1700, f.Victim.NativeGameClient!.Player.HitPoints);
    }
}
