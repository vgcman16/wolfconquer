using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneEquippedQueriesTests
{
    [Theory]
    [InlineData("null-owner")]
    [InlineData("foreign-owner")]
    [InlineData("null-slot")]
    [InlineData("replacement-slot")]
    [InlineData("null-player")]
    [InlineData("foreign-player")]
    [InlineData("null-equipment")]
    [InlineData("foreign-equipment")]
    [InlineData("unowned-storage")]
    public void SanitizedEquippedQueriesRequireGenuineCurrentOwnershipWithoutRepair(string corruption)
    {
        using var f = new Fixture();
        f.Ready();
        var rune = f.Rune(1, 4_031_107, 101, "XPBooster");
        var foreign = new LegacyNativeGameClientRuntime();
        foreign.Equipment = new(foreign);
        foreign.Player.UID = f.Client.Player.UID; // Equal IDs do not establish physical ownership.
        foreign.Player.Reborn = 2;
        SetLevel(foreign.Player, 15);
        var queried = f.Storage;
        switch (corruption)
        {
            case "null-owner": f.Storage.Owner = null; break;
            case "foreign-owner": f.Storage.Owner = foreign; foreign.Rune = f.Storage; break;
            case "null-slot": f.Client.Rune = null; break;
            case "replacement-slot": f.Client.Rune = new(f.Client); break;
            case "null-player": f.Client.Player = null!; break;
            case "foreign-player": f.Client.Player = foreign.Player; break;
            case "null-equipment": f.Client.Equipment = null; break;
            case "foreign-equipment": f.Client.Equipment = foreign.Equipment; break;
            case "unowned-storage":
                queried = new(null) { Owner = f.Client };
                f.Client.Rune = queried;
                Assert.True(queried.Add(rune));
                break;
        }
        var owner = queried.Owner;
        var player = f.Client.Player;
        var equipment = f.Client.Equipment;
        var slot = f.Client.Rune;
        var cache = queried.Objects;
        Assert.False(queried.IsEquipped("XP", out var found));
        Assert.Null(found);
        byte level = 99;
        Assert.False(queried.IsEquipped("XP", ref level));
        Assert.Equal((byte)0, level);
        Assert.Empty(queried.WornObjects);
        Assert.Equal((byte)0, queried.EquippedCount);
        Assert.Null(queried.EquippedRedRune);
        Assert.Same(owner, queried.Owner);
        Assert.Same(player, f.Client.Player);
        Assert.Same(equipment, f.Client.Equipment);
        Assert.Same(slot, f.Client.Rune);
        Assert.Same(cache, queried.Objects);
        Assert.Same(rune, Assert.Single(cache));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("wrong-id")]
    [InlineData("null-name")]
    [InlineData("empty-name")]
    public void SanitizedInvalidMetadataIsSkippedWhileOtherRealRuneStillContributes(string corruption)
    {
        using var f = new Fixture();
        f.Ready();
        var invalid = f.Rune(1, 4_031_107, 101, "XPBooster");
        var valid = f.Rune(2, 4_031_109, 102, "XPBooster");
        var table = LegacyNativePoolItems.ItemsBase!;
        switch (corruption)
        {
            case "missing": Assert.True(table.Remove(invalid.ITEM_ID)); break;
            case "null": table[invalid.ITEM_ID] = null!; break;
            case "wrong-id": table[invalid.ITEM_ID].ID = valid.ITEM_ID; break;
            case "null-name": table[invalid.ITEM_ID].Name = null; break;
            case "empty-name": table[invalid.ITEM_ID].Name = ""; break;
        }
        var cached = f.Storage.Objects;
        Assert.True(f.Storage.IsEquipped("XP", out var found));
        Assert.Same(valid, found);
        byte level = 77;
        Assert.True(f.Storage.IsEquipped("xpBOOSTER", ref level));
        Assert.Equal((byte)9, level);
        Assert.Same(table, LegacyNativePoolItems.ItemsBase);
        Assert.Same(cached, f.Storage.Objects);
        Assert.Same(invalid, cached[0]);
        Assert.Same(valid, cached[1]);
        if (corruption == "missing") Assert.False(table.ContainsKey(invalid.ITEM_ID));
        if (corruption == "null") Assert.Null(table[invalid.ITEM_ID]);
        if (corruption == "wrong-id") Assert.Equal(valid.ITEM_ID, table[invalid.ITEM_ID].ID);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(254)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    public void SanitizedEquippedCountRejectsDuplicatedPhysicalSlotsInsteadOfWrapping(int count)
    {
        using var f = new Fixture();
        for (uint uid = 1; uid <= count; uid++)
            Assert.True(f.Storage.Add(new() { Uid = uid, Position = 101 }));
        var cache = f.Storage.Objects;
        Assert.Equal((byte)0, f.Storage.EquippedCount);
        Assert.Empty(f.Storage.WornObjects);
        Assert.Same(cache, f.Storage.Objects);
        Assert.Equal(count, cache.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SanitizedCountRetainsAllFifteenDistinctNativeActiveSlots(bool alternate)
    {
        using var f = new Fixture();
        f.Client.Equipment!.Alternante = alternate;
        for (ushort slot = 101; slot <= 115; slot++)
            Assert.True(f.Storage.Add(new() { Uid = slot, Position = (ushort)(slot + (alternate ? 20 : 0)) }));
        Assert.Equal((byte)15, f.Storage.EquippedCount);
        Assert.Equal(15, f.Storage.WornObjects.Length);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(99u)]
    public void SanitizedQueriesAllowGenuineBoundPlayersRegardlessOfUidAndUseTheirCurrentLevel(uint uid)
    {
        using var f = new Fixture();
        f.Ready();
        f.Client.Player.UID = uid;
        var rune = f.Rune(1, 4_031_107, 101, "XPBooster");
        Assert.True(f.Storage.IsEquipped("XP", out var found));
        Assert.Same(rune, found);
        Assert.Equal((byte)1, f.Storage.EquippedCount);
        SetLevel(f.Client.Player, 14);
        Assert.False(f.Storage.IsEquipped("XP"));
        Assert.Same(rune, Assert.Single(f.Storage.WornObjects)); // Level gate belongs to named effects.
    }
}
