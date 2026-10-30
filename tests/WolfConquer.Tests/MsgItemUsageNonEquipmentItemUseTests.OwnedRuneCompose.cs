using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private Task WithOwnedRuneComposeAsync(WorldDataCatalog source, Func<OwnedEquipmentBehaviorFixture, Task> body,
        params uint[] uids)
    {
        var loader = new CharacterSnapshotLoader(_usersRoot, _itemsRoot);
        var inputs = uids.Select(uid =>
        {
            var character = Assert.IsType<CharacterSnapshot>(loader.TryLoadByAccountIdForAdmission(uid).Snapshot);
            // These actual Monk blue runes require6000. This declared saved input
            // changes the old7051 fixture class, never the live owner's rules.
            character = character with { Class = 6000 };
            return new OwnedEquipmentBehaviorInput(character, new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(uid),
                new PlayerSpellSnapshotLoader(loader.PlayerSpellsRoot!).LoadByCharacterUid(uid));
        }).ToArray();
        Assert.NotEmpty(inputs);
        // Active saved runes intentionally have no injected origin or spell.
        // Calling Equip here would eagerly update the old B-first rank scenario.
        return OwnedEquipmentBehaviorFixture.RunAsync(inputs[0], OwnedRuneComposeCatalog(source), body, inputs.Skip(1).ToArray());
    }

    private static WorldDataCatalog OwnedRuneComposeCatalog(WorldDataCatalog source)
    {
        var actual = new Dictionary<int, (uint Class, uint Spell)>
        {
            [4_020_501] = (6000, 1416000), [4_020_502] = (6000, 1416001),
            [4_020_503] = (6000, 1416002), [4_020_504] = (6000, 1416003),
            [4_020_514] = (6000, 1416013), [4_020_515] = (6000, 1416014),
            [4_020_516] = (6000, 1416015), [4_020_517] = (6000, 1416016),
            [4_020_615] = (7000, 1444014), [4_030_101] = (0, 4030101),
        };
        var items = source.Items.Select(item =>
        {
            var fields = item.Fields.ToArray();
            if (actual.TryGetValue(item.Id, out var row))
            {
                fields[ItemType.RequiredClassFieldIndex] = row.Class.ToString(CultureInfo.InvariantCulture);
                fields[ItemType.RequiredLevelFieldIndex] = "0";
                fields[ItemType.RuneSpellIdFieldIndex] = row.Spell.ToString(CultureInfo.InvariantCulture);
            }
            if (item.Id == 4_040_001) fields[ItemType.StackSizeFieldIndex] = "10000";
            return new ItemType(item.Id, item.Name, fields);
        }).ToArray();
        // Actual DBServer cumulative curves. The old synthetic250/500 material
        // points are replaced by explicit valid essence stack quantities.
        var progress = new[] { "2@@402@@1@@100@@", "3@@402@@2@@500@@", "4@@402@@3@@1300@@", "5@@402@@4@@2500@@",
            "15@@402@@14@@58000@@", "16@@402@@15@@68500@@", "17@@402@@16@@80500@@", "18@@402@@17@@94000@@", "38@@404@@1@@10@@" }
            .Select((row, index) => RuneParser.ParseLevelExperienceRow("owned-actual-rune_levexp.txt", index + 1, row)).ToArray();
        return NativeCallerWorldDataFixture.Create(items, [], runeRankings: source.RuneRankings, runeLevelExperience: progress,
            magicEffects: MagicEffectParser.Parse("[1416000]\r\nIsRuneSkill=1\r\n[1444000]\r\nIsRuneSkill=1\r\n", "owned-compose-MagicEffect.ini"));
    }

    private void WriteOwnedRuneComposeRankingPlayer(long uid, uint targetLevel, bool material)
    {
        WriteRuneRankingPlayer(uid, targetLevel, material: false);
        if (!material) return;
        // Real4040001 points10 *2250 matches actual14→16 total22500.
        var items = new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(uid);
        new PlayerItemSnapshotLoader(_itemsRoot).SaveByCharacterUid(uid, items with
        { InventoryItems = [new PlayerItemSnapshotItem { Uid = RuneRankMaterialUid, ItemId = RuneRankMaterialId,
            Durability = 100, MaximumDurability = 100, StackSize = 2250 }] });
    }
}
