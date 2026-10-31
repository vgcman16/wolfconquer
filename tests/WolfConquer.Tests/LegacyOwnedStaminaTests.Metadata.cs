using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStaminaTests
{
    [Theory]
    [InlineData("raw-nan")]
    [InlineData("raw-negative")]
    [InlineData("native-mismatch")]
    [InlineData("coherent-byte-cap-overflow")]
    public Task ActualAdmittedMonkMetadataRefusalDoesNotConsumeTheFirstDueStep(string corruption) => Run(async f =>
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); AssertCoherent(f, 109);
        Assert.Equal(622003U, Assert.Single(f.Current.PlayerItems.EquipmentItems).ItemId);
        Assert.True(LegacyNativeMagicQueryClient.TryReadMonkCache(binding, out var cache));
        Assert.True(cache!.UsesMonkEpic);
        Assert.Contains(f.Current.PlayerSpells.Spells, spell => spell.Id == 12560 && spell.Level == 0);
        var row = Assert.Single(f.Catalog.Magic, value => value.TypeId == 12560);
        var fields = Assert.IsType<string[]>(row.Fields);
        var native = f.Catalog.GetNativeMagicRow(row);
        string beforeText = fields[10]; float beforeNative = native.Damage;
        Assert.Equal("10", beforeText); Assert.Equal(10F, beforeNative);
        var files = f.Data.Files(); var spells = f.Native.MySpells!.ClientSpells;
        try
        {
            // Declared NEGATIVE mutation of this fixture's actual adopted
            // catalog/cache only. No synthetic row grants a positive capacity.
            switch (corruption)
            {
                case "raw-nan": fields[10] = "NaN"; break;
                case "raw-negative": fields[10] = "-1"; break;
                case "native-mismatch": native.SetDamage(11); break;
                case "coherent-byte-cap-overflow": fields[10] = "200"; native.SetDamage(200); break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            Assert.Same(row, f.Catalog.MagicByTypeId[12560].Single());
            await TickHeld(f);
            Assert.Same(spells, f.Native.MySpells.ClientSpells); f.Data.AssertFiles(files);
        }
        finally { fields[10] = beforeText; native.SetDamage(beforeNative); }

        // Exact restoration of ONLY the negative input leaves the real original
        //10-point row/cache applicable and the MinValue first-due stamp unspent.
        await TickAndAssert(f, 110); await TickHeld(f);
        Assert.Equal(beforeText, fields[10]); Assert.Equal(beforeNative, native.Damage);
        f.Data.AssertFiles(files);
    }, new(Stamina: "109", Mode: "monk"));
}
