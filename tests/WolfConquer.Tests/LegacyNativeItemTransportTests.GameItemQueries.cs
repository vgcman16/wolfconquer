using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemTransportTests
{
    [Theory]
    [InlineData(410003u, true)]
    [InlineData(900000u, true)]
    [InlineData(1050000u, false)]
    [InlineData(1051000u, false)]
    [InlineData(601000u, true)]
    [InlineData(622000u, true)]
    [InlineData(111003u, false)]
    [InlineData(0u, false)]
    public Task WholeItemIsWeaponKeepsNativeSlotAndArrowRulesIncludingShields(uint id, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = new LegacyNativeGameItemRuntime { ITEM_ID = id };
        LegacyNativePoolItems.ItemsBase = null;
        Assert.Equal(expected, item.IsWeapon); Assert.Equal(id, item.ITEM_ID);
        Assert.Equal(40u, LegacyNativePoolItems.ItemCounter.Count);
    });

    [Theory]
    [InlineData(499999u, false)]
    [InlineData(500000u, true)]
    [InlineData(580999u, true)]
    [InlineData(581000u, false)]
    [InlineData(900000u, false)]
    [InlineData(601000u, false)]
    [InlineData(610000u, false)]
    [InlineData(66036000u, true)]
    [InlineData(66116999u, true)]
    public Task WholeItemIsTwoHanderKeepsUShortQuotientWrapRatherThanUsingOtherWeaponClassifiers(uint id, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = new LegacyNativeGameItemRuntime { ITEM_ID = id };
        Assert.Equal(expected, item.IsTwoHander()); Assert.Equal(id, item.ITEM_ID); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task WholeChatItemConstructorCreatesARealFreshItemWithoutCloningTheReceiverOrAllocatingAUid() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var receiver = new LegacyNativeGameItemRuntime { Uid = 999, Plus = 12, Bless = 7 };
        LegacyNativePoolItems.ItemsBase = null;
        var result = receiver.FakeChatItem(410003, 555);
        Assert.NotSame(receiver, result); Assert.Equal(1u, result.Uid); Assert.Equal(410003u, result.ITEM_ID);
        Assert.Equal((ushort)555, result.Durability); Assert.Equal((ushort)555, result.MaximDurability);
        Assert.Equal(LegacyNativeGameItemFlags.ItemMode.ChatItem, result.Mode);
        Assert.Equal((byte)0, result.Plus); Assert.Equal((byte)0, result.Bless);
        Assert.Empty(result.Deposite); Assert.NotSame(receiver.Deposite, result.Deposite);
        Assert.Equal(5, result.RelicAttributes.Length); Assert.NotSame(receiver.RelicAttributes, result.RelicAttributes);
        Assert.Equal(999u, receiver.Uid); Assert.Equal((byte)12, receiver.Plus); Assert.Equal((byte)7, receiver.Bless);
        Assert.Equal(40u, LegacyNativePoolItems.ItemCounter.Count); Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task WholeChatItemUsesTheGenuineRuneSetterAndRetainsMissingMetadataFailure() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var receiver = new LegacyNativeGameItemRuntime();
        LegacyNativePoolItems.ItemsBase![4010001] = new LegacyNativeItemRuneMetadata { RuneSpellID = 4010001 };
        var rune = receiver.FakeChatItem(4010001, 55);
        Assert.Equal(4010001u, rune.SpellID); Assert.Equal(LegacyNativeGameItemFlags.ItemEffect.RuneEffect, rune.Effect);
        Assert.Equal(LegacyNativeGameItemFlags.ItemMode.ChatItem, rune.Mode);
        LegacyNativePoolItems.ItemsBase.Clear();
        Assert.Throws<KeyNotFoundException>(() => receiver.FakeChatItem(4010001, 55));
        Assert.Equal(40u, LegacyNativePoolItems.ItemCounter.Count); Assert.Equal(0u, receiver.ITEM_ID);
    });
}
