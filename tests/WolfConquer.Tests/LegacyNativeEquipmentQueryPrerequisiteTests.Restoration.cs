using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEquipmentQueryPrerequisiteTests
{
    [Fact]
    public Task NestedEquipmentFixtureRestoresEscapedRealCatalogsRandomAndStatusIdentity() => NativePacketTestThread.Run(()=>
    {
        using var outer=new Scope();
        var items=LegacyNativePoolItems.ItemsBase;var benefits=LegacyNativeProfessionTable.Benefits;
        var effects=LegacyNativeItemRefineUpgrade.Effects;var beasts=LegacyNativeBeastsAtrribute.Attributes;
        var special=LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute;var random=LegacyNativePoolRandom.GetRandom;
        var status=outer.Status;var control=new LegacyNativeSafeRandom(71);_=control.Next();
        var row=new LegacyNativeItemRuneMetadata{Name="retained"};items![7]=row;
        using(var inner=new Scope())
        {
            Assert.NotSame(items,LegacyNativePoolItems.ItemsBase);Assert.NotSame(benefits,LegacyNativeProfessionTable.Benefits);
            Assert.NotSame(effects,LegacyNativeItemRefineUpgrade.Effects);Assert.NotSame(beasts,LegacyNativeBeastsAtrribute.Attributes);
            Assert.NotSame(special,LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute);Assert.NotSame(random,LegacyNativePoolRandom.GetRandom);
            inner.Equipment.AddChiAtribute(ChiValues());_=LegacyNativePoolRandom.GetRandom.Next();
        }
        Assert.Same(items,LegacyNativePoolItems.ItemsBase);Assert.Same(benefits,LegacyNativeProfessionTable.Benefits);
        Assert.Same(effects,LegacyNativeItemRefineUpgrade.Effects);Assert.Same(beasts,LegacyNativeBeastsAtrribute.Attributes);
        Assert.Same(special,LegacyNativeSpecialEffectAttributeTable.SpEffectAttribute);Assert.Same(random,LegacyNativePoolRandom.GetRandom);
        Assert.Same(row,items[7]);Assert.Equal(control.Next(),random.Next());
        Assert.Same(status,outer.Status);Assert.All(StatusValues(status),value=>Assert.Equal(0u,value));
    });
}
