using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static (string Character, string Spells, LegacyTradeJournalChange[] Changes) PrepareFullAccountClassImages(
        LegacyFullAccountClassOwner owner, WorldSession updated, LegacyPreparedSpellFileWrite spellWrite)
    {
        var characterPath = Path.Combine(owner.UsersRoot, owner.Expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var spellPath = Path.Combine(owner.SpellsRoot, owner.Expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        LegacyTradeJournalChange[]? changes = null;
        SnapshotFileLockCoordinator.ExecuteLocked([characterPath, spellPath], () =>
        {
            if (!CharacterSnapshotFileBaseline.TryRead(characterPath, "fullAccountClass", out var baseline, out var reason) || baseline is null)
                throw new FullAccountClassRefusal(reason);
            var text = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out var encoding);
            if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(baseline.Bytes))
                throw new FullAccountClassRefusal("fullAccountClassEncodingUnproven");
            var before = CharacterSnapshotParser.ParseText(text);
            var expected = owner.Inputs.Stable.Character;
            if (!FullAccountCharacterScalarsEqual(before, expected) || !FullAccountStoredFieldsMatch(before.Attributes, expected.Attributes) ||
                !FullAccountStoredFieldsMatch(before.PaidNobility, expected.PaidNobility))
                throw new FullAccountClassRefusal("fullAccountClassCharacterBaselineStale");
            var afterText = LegacyOrdinaryAdmissionCharacterText.Project(text,
                CreateLegacyFullAccountClassPersistenceValues(updated.Character), paid: null);
            var characterAfter = encoding.GetPreamble().Concat(encoding.GetBytes(afterText)).ToArray();
            var parsedAfter = CharacterSnapshotParser.ParseText(afterText);
            if (!FullAccountCharacterScalarsEqual(parsedAfter, updated.Character))
                throw new FullAccountClassRefusal("fullAccountClassAfterImageMismatch");
            var spellBefore = spellWrite.CopyBeforeImage();
            var spellAfter = spellWrite.CopyAfterImage();
            changes = [new(LegacyTradeJournalFileKind.Character, owner.Expected.AccountId, baseline.Bytes, characterAfter),
                new(LegacyTradeJournalFileKind.Spells, owner.Expected.Character.Uid, spellBefore, spellAfter)];
            return true;
        });
        return (characterPath, spellPath, changes!);
    }

    private static byte[]? ReadFullAccountClassSpells(string path)
    {
        try
        {
            if (File.Exists(path) && !CharacterSnapshotFileBaseline.TryValidateSafePath(path, "fullAccountClassSpells", out var reason))
                throw new FullAccountClassRefusal(reason);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw InvalidFullAccountClassState();
            var image = new byte[checked((int)stream.Length)]; stream.ReadExactly(image);
            if (stream.ReadByte() != -1 || stream.Length != image.Length) throw InvalidFullAccountClassState();
            return image;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static bool FullAccountStoredFieldsMatch(IReadOnlyDictionary<string, string> saved, IReadOnlyDictionary<string, string> expected) =>
        saved.All(pair => expected.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static bool FullAccountCharacterScalarsEqual(CharacterSnapshot first, CharacterSnapshot second)
    {
        var empty = new Dictionary<string, string>();
        return (first with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty }) ==
            (second with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty });
    }
}
