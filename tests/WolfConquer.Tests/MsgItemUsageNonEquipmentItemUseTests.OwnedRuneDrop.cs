using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// The real native Beast prerequisite changes shared metadata during admission.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private Task WithOwnedRuneDropAsync(WorldDataCatalog catalog, Func<OwnedEquipmentBehaviorFixture, Task> body,
        params uint[] uids)
    {
        var loader = new CharacterSnapshotLoader(_usersRoot, _itemsRoot);
        var inputs = uids.Select(uid => new OwnedEquipmentBehaviorInput(
            Assert.IsType<CharacterSnapshot>(loader.TryLoadByAccountIdForAdmission(uid).Snapshot),
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(uid),
            new PlayerSpellSnapshotLoader(loader.PlayerSpellsRoot!).LoadByCharacterUid(uid))).ToArray();
        Assert.NotEmpty(inputs);
        return OwnedEquipmentBehaviorFixture.RunAsync(inputs[0], OwnedRuneDropCatalog(catalog), body, inputs.Skip(1).ToArray());
    }

    private static WorldDataCatalog OwnedRuneDropCatalog(WorldDataCatalog source)
    {
        // Original DBServer itemtype rows, not a synthetic ordinary-Rune mapping.
        var actual = new Dictionary<int, (uint Class, uint Spell)>
        {
            [4_020_514] = (6000, 1416013), [4_020_515] = (6000, 1416014),
            [4_020_516] = (6000, 1416015), [4_020_517] = (6000, 1416016),
            [4_020_615] = (7000, 1444014), [4_030_501] = (0, 4030501),
        };
        var items = source.Items.Select(item =>
        {
            if (!actual.TryGetValue(item.Id, out var row)) return item;
            var fields = item.Fields.ToArray();
            fields[ItemType.RequiredClassFieldIndex] = row.Class.ToString(CultureInfo.InvariantCulture);
            fields[ItemType.RequiredLevelFieldIndex] = "0";
            fields[ItemType.RuneSpellIdFieldIndex] = row.Spell.ToString(CultureInfo.InvariantCulture);
            return new ItemType(item.Id, item.Name, fields);
        }).ToArray();
        return NativeCallerWorldDataFixture.Create(items, [],
            magicEffects: MagicEffectParser.Parse("[1416000]\r\nIsRuneSkill=1\r\n[1444000]\r\nIsRuneSkill=1\r\n", "owned-MagicEffect.ini"),
            runeRankings: source.RuneRankings);
    }
}
