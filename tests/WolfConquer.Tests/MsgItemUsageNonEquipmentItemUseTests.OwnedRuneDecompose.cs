using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private Task WithOwnedLegacyDecomposeAsync(WorldDataCatalog source, Func<OwnedEquipmentBehaviorFixture, Task> exercise, params uint[] uids)
    {
        var loader = new CharacterSnapshotLoader(_usersRoot, _itemsRoot);
        var inputs = uids.Select(uid => new OwnedEquipmentBehaviorInput(
            Assert.IsType<CharacterSnapshot>(loader.TryLoadByAccountIdForAdmission(uid).Snapshot) with { Class = 6000 },
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(uid),
            new PlayerSpellSnapshotLoader(loader.PlayerSpellsRoot!).LoadByCharacterUid(uid))).ToArray();
        // All saved input precedes actual bootstrap. Do not Equip these old
        // unmarked rows: that would change their spell and stale-rank premise.
        return OwnedEquipmentBehaviorFixture.RunAsync(inputs[0], OwnedLegacyDecomposeCatalog(source), exercise, inputs.Skip(1).ToArray());
    }

    private static WorldDataCatalog OwnedLegacyDecomposeCatalog(WorldDataCatalog source)
    {
        var known = new Dictionary<uint, (uint Class, uint Spell)>
        { [4_010_501] = (7000, 1425000), [4_020_501] = (6000, 1416000), [4_020_514] = (6000, 1416013),
          [4_020_515] = (6000, 1416014), [4_020_516] = (6000, 1416015), [4_020_517] = (6000, 1416016), [4_020_615] = (7000, 1444014) };
        uint[] rewards = [4_040_001, 4_050_001, 4_060_001, 3_306_366, 3_306_367, 3_306_368, 3_306_369, 3_306_370, 3_306_371];
        var rows = source.Items.ToDictionary(row => checked((uint)row.Id), row => row);
        foreach (var pair in known)
        {
            var fields = rows.TryGetValue(pair.Key, out var old) ? old.Fields.ToArray()
                : OwnedEquipmentBehaviorFixture.Item(pair.Key, "OwnedLegacyRune" + pair.Key).Fields.ToArray();
            fields[ItemType.RequiredClassFieldIndex] = pair.Value.Class.ToString(CultureInfo.InvariantCulture);
            fields[ItemType.RequiredLevelFieldIndex] = "0"; fields[ItemType.RuneSpellIdFieldIndex] = pair.Value.Spell.ToString(CultureInfo.InvariantCulture);
            fields[ItemType.StackSizeFieldIndex] = "0";
            rows[pair.Key] = new(checked((int)pair.Key), old?.Name ?? "OwnedLegacyRune" + pair.Key, fields);
        }
        foreach (var id in rewards)
            rows[id] = OwnedEquipmentBehaviorFixture.Item(id, "OwnedLegacyReward" + id, new Dictionary<int, string>
            { [ItemType.RequiredClassFieldIndex] = "0", [ItemType.RuneSpellIdFieldIndex] = "0", [ItemType.DurabilityFieldIndex] = "1",
              [ItemType.TimeFieldIndex] = "0", [ItemType.StackSizeFieldIndex] = "10000" });
        var progress = new[] { "1@@401@@1@@100@@", "2@@402@@1@@100@@", "15@@402@@14@@58000@@",
            "16@@402@@15@@68500@@", "17@@402@@16@@80500@@", "18@@402@@17@@94000@@" }
            .Select((row, index) => RuneParser.ParseLevelExperienceRow("owned-actual-rune_levexp.txt", index + 1, row)).ToArray();
        return NativeCallerWorldDataFixture.Create(rows.Values.ToArray(), [], runeRankings: source.RuneRankings, runeLevelExperience: progress,
            magicEffects: MagicEffectParser.Parse("[1416000]\r\nIsRuneSkill=1\r\n[1444000]\r\nIsRuneSkill=1\r\n", "owned-legacy-decompose-MagicEffect.ini"));
    }

    private async Task AssertOwnedLegacyDecomposeMainSuccessAsync()
    {
        const uint actor = 1_293_600, target = 5_337_580;
        WriteSnapshot(accountId: actor, uid: actor, mapId: 1_015, x: 826, y: 565);
        // Actual cumulative100 +XP150 preserves the original intended20-unit
        // payout, replacing the old synthetic200+50 inputs.
        WriteItems(actor, PlayerItemSnapshotLoaderTests.DbItem(target, 4_020_501, position: 211, runeExperience: 150));
        await WithOwnedLegacyDecomposeAsync(CreateWorldDataCatalog(), async f =>
        {
            var expected = await CommitOwnedLegacyDecompose(f, actor, target, 20, CreateRuneDecomposeUse(9_999_999, target));
            Assert.Equal(4_040_001U, expected.Essence.ItemId); Assert.Equal((ushort)211, expected.Essence.Position);
            Assert.Equal((ushort)20, expected.Essence.StackSize); Assert.Equal(3_306_368U, expected.Fragment.ItemId);
            Assert.Equal((byte)1, expected.Fragment.Bound); Assert.Equal((ushort)10, expected.Fragment.StackSize);
            Assert.DoesNotContain(f.Current(actor).PlayerItems.RuneItems, row => row.Uid == target);
        }, actor);
    }

    private async Task AssertOwnedLegacyDecomposeInventoryFullAsync()
    {
        const uint actor = 1_293_600, target = 5_337_590;
        WriteSnapshot(accountId: actor, uid: actor, mapId: 1_015, x: 826, y: 565);
        var fillers = Enumerable.Range(0, 40).Select(index => PlayerItemSnapshotLoaderTests.DbItem(
            checked((uint)(5_338_000 + index)), checked((uint)(1_000_000 + index)), position: 0));
        WriteItems(actor, fillers.Append(PlayerItemSnapshotLoaderTests.DbItem(target, 4_010_501, position: 211)).ToArray());
        await WithOwnedLegacyDecomposeAsync(CreateWorldDataCatalog(), async f =>
        {
            Assert.Equal(40, f.Current(actor).PlayerItems.InventoryItems.Count);
            var request = CreateRuneDecomposeUse(0, target); AssertOwnedLegacyDecomposeRequestCodec(request, 0, 1, target);
            await RefuseOwnedLegacyDecompose(f, actor, request);
            Assert.Contains(f.Current(actor).PlayerItems.RuneItems, row => row.Uid == target && row.Position == 211);
        }, actor);
    }
}
