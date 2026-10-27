using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerMapTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public Task PrestigeIconUsesActualRankingAndOnlyRanksOneThroughThree(bool effect, int position) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.SpawinEffect = effect;
        Set(scope.Player, "_class", 13002u); // Genuine known class; rank badge is the subject of this control.
        var rank = Ranking.Ranks[Ranking.GetIndex(scope.Player.Class)];
        for (uint index = 1; index <= position; index++)
        {
            uint uid = index == position ? scope.Player.UID : 100 + index;
            rank.Items.Add(uid, new Ranking.Entry { UID = uid });
        }
        scope.Player.GetArray(scope.Packet, false, false);
        Assert.Equal(position is >= 1 and <= 3 ? (byte)position : (byte)0, Read(scope.Packet).PrestigeRank);
    });

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 5)]
    [InlineData(true, 5)]
    public Task RelicIconCountsOnlyNonzeroEpicAttributesFromActualEquippedItem(bool effect, int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.SpawinEffect = effect;
        scope.Client.Equipment!.ClientItems[999] = new LegacyNativeGameItemRuntime
        {
            Position = (ushort)LegacyNativeGameItemFlags.ConquerItem.Relics,
            RelicAttributes = Enumerable.Range(0, 5).Select(index => index < count ?
                new LegacyNativeRelicAttribute(LegacyNativeChiAttribute.CriticalStrike, 1, true) : default).ToArray(),
        };
        scope.Player.GetArray(scope.Packet, false, false);
        Assert.Equal((byte)(count * 10 + 1), Read(scope.Packet).Relic);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HundredWeaponAppearanceUsesRealScoreAndNativeBinaryConcatenationPadding(bool effect) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.SpawinEffect = effect;
        LegacyNativeHundredWeaponsTable.HundredWeaponsList[123] = new()
        {
            WeaponSubtype = LegacyNativeMagicType.WeaponsType.Blade, Level = 1, Attributes = new(),
        };
        var weapon = new LegacyNativeHundredWeaponInfo
        {
            WeaponSubtype = LegacyNativeMagicType.WeaponsType.Blade, Level = 1,
            Attributes = new(), AppearancePosition = 2,
        };
        // Actual values, not dictionary key, carry the weapon identity; unlocked/bit flags are not checked.
        scope.Client.HundredWeapons!.Objects[(LegacyNativeMagicType.WeaponsType)1] = weapon;
        scope.Client.HundredWeapons.Objects[(LegacyNativeMagicType.WeaponsType)2] = weapon;
        scope.Player.GetArray(scope.Packet, false, false);
        Assert.Equal(1000u, weapon.Score);
        Assert.Equal(new uint[] { 26933760, 26933760, 0, 0, 0 }, Read(scope.Packet).HundredWeapons);
    });

    [Theory]
    [InlineData(false, 2000u, 201u)]
    [InlineData(true, 2000u, 201u)]
    [InlineData(false, 4000u, 401u)]
    [InlineData(true, 4000u, 401u)]
    [InlineData(false, 7000u, 701u)]
    [InlineData(true, 7000u, 701u)]
    [InlineData(false, 13000u, 1301u)]
    [InlineData(true, 13000u, 1301u)]
    [InlineData(false, 14000u, 1301u)]
    [InlineData(true, 14000u, 1301u)]
    public Task ActualCombatGearEffectReplacesFiveSlotHundredWeaponArrayWithNativeShape(bool effect, uint profession, uint itemId) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.SpawinEffect = effect; Set(scope.Player, "_class", profession);
        LegacyNativeCombatGearTable.m_Load = true;
        scope.Client.CombatGear!.Items[(LegacyNativeCombatGear.TypeID)itemId] = new()
        { ItemID = (LegacyNativeCombatGear.TypeID)itemId, Equip = 1, Exp = 17, Transform = 3 };
        scope.Player.GetArray(scope.Packet, false, false);
        uint[] expected = profession switch
        {
            2000 => [0, itemId * 10 + 3, 17, 0],
            4000 => [0, itemId, 17],
            _ => [itemId, 17],
        };
        Assert.Equal(expected, Read(scope.Packet).HundredWeapons);
    });

    [Theory]
    [InlineData(false, "inner")]
    [InlineData(true, "inner")]
    [InlineData(false, "equipment")]
    [InlineData(true, "equipment")]
    [InlineData(false, "hundred")]
    [InlineData(true, "hundred")]
    [InlineData(false, "combat")]
    [InlineData(true, "combat")]
    [InlineData(false, "bits")]
    [InlineData(true, "bits")]
    public Task MissingGenuineOwnerFaultsBeforeSerializingOrFinalizingInsteadOfProducingDefaultSpawn(bool effect, string owner) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.SpawinEffect = effect;
        switch (owner)
        {
            case "inner": scope.Player.InnerPower = null; break;
            case "equipment": scope.Client.Equipment = null; break;
            case "hundred": scope.Client.HundredWeapons = null; break;
            case "combat": scope.Client.CombatGear = null; break;
            case "bits": scope.Player.BitVector = null!; break;
        }
        Assert.Throws<NullReferenceException>(() => scope.Player.GetArray(scope.Packet, false, false));
        Assert.Equal(4, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.All(NativeSpellTestFixture.Bytes(scope.Packet), value => Assert.Equal((byte)0xA5, value));
    });

    [Fact]
    public Task NullCurrentOwnerPlayerFaultsBeforeWriterInitializationAndNoOptionalOwnerIsAutoBorn() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var subject = scope.Player;
        scope.Client.Player = null!;
        Assert.Throws<NullReferenceException>(() => subject.GetArray(scope.Packet, false, false));
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Native.Queue);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OversizedHundredWeaponBinaryValueFaultsWithoutReplacingItByModernPacking(bool effect) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.SpawinEffect = effect;
        LegacyNativeHundredWeaponsTable.HundredWeaponsList[1] = new()
        { WeaponSubtype = LegacyNativeMagicType.WeaponsType.Blade, Level = 1, Attributes = new() };
        scope.Client.HundredWeapons!.Objects[LegacyNativeMagicType.WeaponsType.Blade] = new()
        {
            WeaponSubtype = LegacyNativeMagicType.WeaponsType.Blade, Level = 1, AppearancePosition = 1,
            Attributes = new() { [LegacyNativeHundredWeaponsTable.AttributeType.Hitpoints] = int.MaxValue },
        };
        Assert.Throws<OverflowException>(() => scope.Player.GetArray(scope.Packet, false, false));
        Assert.Equal(4, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HundredWeaponAppearanceSortsRealPositionsThenTruncatesToFive(bool effect) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.SpawinEffect = effect;
        LegacyNativeHundredWeaponsTable.HundredWeaponsList[1] = new()
        { WeaponSubtype = LegacyNativeMagicType.WeaponsType.Blade, Level = 1, Attributes = new() };
        for (byte position = 6; position >= 1; position--)
        {
            scope.Client.HundredWeapons!.Objects[(LegacyNativeMagicType.WeaponsType)position] = new()
            {
                WeaponSubtype = LegacyNativeMagicType.WeaponsType.Blade, Level = 1, AppearancePosition = position,
                Attributes = new() { [LegacyNativeHundredWeaponsTable.AttributeType.Hitpoints] = -position },
            };
        }
        scope.Player.GetArray(scope.Packet, false, false);
        Assert.Equal(new uint[] { 26933440, 26933120, 26932800, 26932480, 26932160 }, Read(scope.Packet).HundredWeapons);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullStreamFaultsAfterReadingRealBranchOwnerWithoutPublishingPackets(bool effect) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.SpawinEffect = effect;
        Assert.Throws<NullReferenceException>(() => scope.Player.GetArray(null!, false, false));
        Assert.Empty(scope.Native.Queue); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
    });
}
