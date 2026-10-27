using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;
using ItemType = WolfConquer.Core.World.LegacyNativeNinja.ItemType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaTests
{
    [Theory]
    [InlineData(5051u, false, true)] [InlineData(5051u, true, true)]
    [InlineData(5052u, false, true)] [InlineData(5052u, true, true)]
    [InlineData(5057u, false, true)] [InlineData(5057u, true, true)]
    [InlineData(5058u, false, false)] [InlineData(5058u, true, false)]
    public void TargetNinjaStagesSelectRealNormalAndAlternateSigilsAndPreparedCombatValues(
        uint job, bool alternate, bool expected) => RunTargetNinjaTables(() =>
        {
            var user = Client(job, alternate);
            user.Player.UID = 41;
            var ninja = new LegacyNativeNinja(user);
            user.MyNinja = ninja;
            var row = Put(ninja, 1, alternate ? 9u : 1u);
            var metadata = new LegacyNativeNinjaFile.NinjaSprintItem
                { ItemID = 100, Level = 1, Power = 23, Damage = 456 };
            LegacyNativeNinjaFile.gouyu_type.Add(17, metadata);
            var items = ninja.Items;
            var equipment = user.Equipment;

            var capture = ninja.CaptureEquippedSigil(user, ItemType.WildSigil);

            Assert.Equal(expected, ninja.TryGetValueEquip(ItemType.WildSigil, out var selected));
            Assert.Same(expected ? row : null, selected);
            Assert.Equal(expected, capture.Found);
            Assert.Equal(expected ? 23 : 0, capture.Power);
            Assert.Equal(expected ? 456u : 0u, capture.Damage);
            Assert.True(capture.IsCurrent());
            Assert.Same(items, ninja.Items);
            Assert.Same(row, Assert.Single(items).Value);
            Assert.Same(metadata, Assert.Single(LegacyNativeNinjaFile.gouyu_type).Value);
            Assert.Same(equipment, user.Equipment);
            Assert.Same(ninja, user.MyNinja);
            Assert.Equal(alternate ? 9u : 1u, row.Position);
        });

    [Theory]
    [InlineData("missing-equipment")] [InlineData("foreign-equipment")]
    [InlineData("null-items")] [InlineData("null-tail")] [InlineData("missing-metadata")]
    public void TargetNinjaStageCannotTurnMissingOrForeignRequiredStateIntoAbsentCombatBonus(string fault) =>
        RunTargetNinjaTables(() =>
        {
            var user = Client(5057);
            user.Player.UID = 41;
            var ninja = new LegacyNativeNinja(user) { Exp = 222 };
            user.MyNinja = ninja;
            var row = Put(ninja, 1, 1);
            var metadata = new LegacyNativeNinjaFile.NinjaSprintItem
                { ItemID = 100, Level = 1, Power = 23, Damage = 456 };
            LegacyNativeNinjaFile.gouyu_type.Add(17, metadata);
            if (fault == "missing-equipment") user.Equipment = null;
            else if (fault == "foreign-equipment")
            {
                var other = Client(5057); other.Player.UID = 41;
                user.Equipment = other.Equipment;
            }
            else if (fault == "null-items") ninja.Items = null!;
            else if (fault == "null-tail") Assert.True(ninja.Items.TryAdd(7, null!));
            else LegacyNativeNinjaFile.gouyu_type.Clear();
            var items = ninja.Items;
            var rows = items?.ToArray();
            var equipment = user.Equipment;
            var table = LegacyNativeNinjaFile.gouyu_type;
            var savedMetadata = table.ToArray();

            var error = Record.Exception(() => ninja.CaptureEquippedSigil(user, ItemType.WildSigil));

            Assert.Same(items, ninja.Items);
            Assert.Equal(rows, ninja.Items?.ToArray());
            Assert.Same(equipment, user.Equipment);
            Assert.Same(ninja, user.MyNinja);
            Assert.Equal(222u, ninja.Exp);
            Assert.Equal((100u, 1u, 1u, 0u, 0u), (row.ItemID, row.Level, row.Position, row.Exp, row.UK));
            Assert.Same(table, LegacyNativeNinjaFile.gouyu_type);
            Assert.Equal(savedMetadata, table.ToArray());
            Assert.Equal((100u, 1u, 23, 456u), (metadata.ItemID, metadata.Level, metadata.Power, metadata.Damage));
            Assert.IsType<InvalidDataException>(error);
        });

    private static void RunTargetNinjaTables(Action action)
    {
        Tables? tables = null;
        var errors = new List<Exception>();
        try { tables = new(); action(); } catch (Exception error) { errors.Add(error); }
        try { tables?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Target Ninja assertion and real metadata restore failed.", errors);
    }
}
