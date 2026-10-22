using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed class LegacyNativeOuterItemRecordSanitationTests
{
    [Fact]
    public void OrdinaryOuterRecordClampsOnlyBlessAndPlusAndKeepsByteEnchant()
    {
        var record = Record(410009, bless: 255, plus: 255, enchant: 255);
        var expected = record;
        expected.Bless = 7;
        expected.Plus = 12;
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(Bytes(ref expected), Bytes(ref record));
    }

    [Fact]
    public void AlreadyLegalOuterRecordRetainsItsEntireRawImage()
    {
        var record = Record(410009, bless: 7, plus: 12, enchant: 255);
        var before = Bytes(ref record);
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(before, Bytes(ref record));
    }

    [Fact]
    public void Band203FirstResetMakesExcessBlessZeroNotTheLaterSharedCapOne()
    {
        var record = Record(203999, bless: 8, plus: 15, enchant: 22);
        var expected = record;
        expected.Bless = 0;
        expected.Plus = 12;
        expected.Enchant = 0;
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(Bytes(ref expected), Bytes(ref record));
    }

    [Fact]
    public void Band203KeepsBlessOneWhileRemovingEnchant()
    {
        var record = Record(203000, bless: 1, plus: 1, enchant: 1);
        var expected = record;
        expected.Enchant = 0;
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(Bytes(ref expected), Bytes(ref record));
    }

    [Fact]
    public void Band201CapsBlessOneAndRemovesEnchantWithoutThe203ZeroReset()
    {
        var record = Record(201999, bless: 255, plus: 255, enchant: 255);
        var expected = record;
        expected.Bless = 1;
        expected.Plus = 12;
        expected.Enchant = 0;
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(Bytes(ref expected), Bytes(ref record));
    }

    [Fact]
    public void Band202CapsBlessOneAndRemovesEnchant()
    {
        var record = Record(202000, bless: 2, plus: 0, enchant: 1);
        var expected = record;
        expected.Bless = 1;
        expected.Enchant = 0;
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(Bytes(ref expected), Bytes(ref record));
    }

    [Fact]
    public void Band204RemovesAnyBlessAndEnchantAfterOrdinaryClamps()
    {
        var record = Record(204999, bless: 255, plus: 255, enchant: 255);
        var expected = record;
        expected.Bless = 0;
        expected.Plus = 12;
        expected.Enchant = 0;
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(Bytes(ref expected), Bytes(ref record));
    }

    [Fact]
    public void ValuesImmediatelyOutsideSpecialBandsDoNotGainTheirReset()
    {
        foreach (uint id in new uint[] { 200999, 205000, uint.MaxValue })
        {
            var record = Record(id, bless: 2, plus: 1, enchant: 1);
            var before = Bytes(ref record);
            LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
            Assert.Equal(before, Bytes(ref record));
        }
    }

    [Fact]
    public void ZeroRecordStaysExactlyZeroWithoutManufacturingAnItem()
    {
        var record = default(LegacyNativeClientItemRecord);
        var before = Bytes(ref record);
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(before, Bytes(ref record));
    }

    [Fact]
    public void SanitationDoesNotDecodeInvalidDatesOrPerformRuneMetadataLookup()
    {
        var record = Record(4010000, bless: 255, plus: 255, enchant: 255);
        record.Expiration = long.MaxValue;
        record.EffectAddedOn = long.MaxValue;
        record.PurificationAddedOn = long.MinValue;
        var expected = record;
        expected.Bless = 7;
        expected.Plus = 12;
        LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
        Assert.Equal(Bytes(ref expected), Bytes(ref record));
    }

    [Fact]
    public void ThisPhaseDoesNotPretendToImplementTheEarlier205901SkipOrDemonWrite()
    {
        foreach (uint id in new uint[] { 205901, 750000 })
        {
            var record = Record(id, bless: 8, plus: 13, enchant: 2);
            var expected = record;
            expected.Bless = 7;
            expected.Plus = 12;
            LegacyNativeOuterItemRecordSanitation.SanitizeBeforeConversion(ref record);
            Assert.Equal(Bytes(ref expected), Bytes(ref record));
        }
    }

    private static LegacyNativeClientItemRecord Record(uint id, byte bless, byte plus, byte enchant) => new()
    {
        UID = uint.MaxValue, ITEM_ID = id, Position = 215, WH_ID = 100,
        Bless = bless, Plus = plus, Enchant = enchant,
        DepositeCount = 9, Inscribed = ushort.MaxValue, RemainingTime = 123,
        test3 = 7, test4 = 8, Attribute1 = uint.MaxValue, MythSoul = 9,
    };

    private static byte[] Bytes(ref LegacyNativeClientItemRecord value) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();
}
