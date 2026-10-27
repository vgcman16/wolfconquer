using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyOwnedAdmissionFixture
{
    private static void WriteSnapshots(string root, uint account, uint character, uint item)
    {
        foreach (var name in new[] { "Users", "PlayersItems", "PlayersSpells", "PlayersProfs", "Quests", "PlayersCombatGear", "PlayersMailList" })
            Directory.CreateDirectory(Path.Combine(root, name));
        var text = CharacterText(character, DateTime.Now.DayOfYear);
        if (CharacterSnapshotParser.ParseText(text).Uid != character)
            throw new InvalidDataException("Synthetic Character image is not coherent.");
        using (var output = new FileStream(Path.Combine(root, "Users", account.ToString(CultureInfo.InvariantCulture) + ".ini"),
                   FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { output.Write(new UTF8Encoding(false).GetBytes(text)); output.Flush(flushToDisk: true); }
        var items = new PlayerItemSnapshot([new PlayerItemSnapshotItem
        {
            Uid = item, ItemId = 1_001_000, StackSize = 1, Durability = 1, MaximumDurability = 1,
        }], [], [], [], []);
        new PlayerItemSnapshotLoader(Path.Combine(root, "PlayersItems")).SaveByCharacterUid(character, items);
        new PlayerSpellSnapshotLoader(Path.Combine(root, "PlayersSpells")).SaveByCharacterUid(character, PlayerSpellSnapshot.Empty);
        new PlayerProficiencySnapshotLoader(Path.Combine(root, "PlayersProfs")).SaveByCharacterUid(character, PlayerProficiencySnapshot.Empty);
        new PlayerQuestSnapshotLoader(Path.Combine(root, "Quests")).SaveByCharacterUid(character, PlayerQuestSnapshot.Empty);
        new CombatGearSnapshotLoader(Path.Combine(root, "PlayersCombatGear")).SaveByCharacterUid(character, CombatGearSnapshot.Empty);
        var loader = new CharacterSnapshotLoader(Path.Combine(root, "Users"));
        if (loader.LoadByAccountId(account)?.Uid != character ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(items).AsSpan().SequenceEqual(
                PlayerItemSnapshotLoader.ToLegacyBytes(loader.LoadPlayerItemsByCharacterUid(character))))
            throw new InvalidDataException("Synthetic Character/items failed canonical writer roundtrip.");
    }

    // Synthetic loot-roundtrip state with body 1008 for the D13 look 8 assets.
    // The older body 1003 fixture requests look 3, absent from the staged D13 set.
    // These values are test data, not imported saves, credentials or live grants.
    private static string CharacterText(uint character, int day) => $$"""
        [Character]
        UID={{character.ToString(CultureInfo.InvariantCulture)}}
        Name=LootFixture
        Body=1008
        Face=154
        Haire=27
        Class=10
        FirstClass=0
        SecoundeClass=0
        Reborn=0
        Level=120
        Experience=0
        Map=1002
        X=410
        Y=354
        PMap=1002
        PMapX=410
        PMapY=354
        Strength=100
        Agility=50
        Vitaliti=100
        Spirit=0
        Atributes=0
        MinHitPoints=1000
        MinMana=100
        Money=100
        ConquerPoints=50
        BoundConquerPoints=0
        BankCps=0
        PkPoints=0
        Online=0
        SecurityPass=0,0,0
        WHMoney=0
        DynamicID=0
        CollectionID=0
        CollectionStorage=0/0/
        Day={{day.ToString(CultureInfo.InvariantCulture)}}

        """;
}
