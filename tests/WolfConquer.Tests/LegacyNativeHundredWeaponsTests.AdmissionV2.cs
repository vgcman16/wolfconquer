using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Info = WolfConquer.Core.World.LegacyNativeHundredWeaponInfo;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    private sealed record SlotRowState(Info Row, Weapon Subtype, byte Level, uint Progress,
        LegacyNativeBitVector32? Vector, uint[]? Words, uint[]? Bits, byte Appearance,
        Dictionary<Attr, int>? Attributes, KeyValuePair<Attr, int>[]? Values);
    private sealed record SlotState(object? Storage, KeyValuePair<Weapon, Info>[]? Entries,
        SlotRowState[] Rows, byte[][] Frames, byte[][] FrameBytes);

    private static SlotState CaptureSlotState(Scope scope, params Info[] physicalRows)
    {
        var frames = scope.Queue.ToArray();
        return new(scope.Owner.Objects, scope.Owner.Objects?.ToArray(), physicalRows.Select(row => new SlotRowState(
            row, row.WeaponSubtype, row.Level, row.Progress, row.BitVector, row.BitVector?.bits,
            row.BitVector?.bits?.ToArray(), row.AppearancePosition, row.Attributes, row.Attributes?.ToArray())).ToArray(),
            frames, frames.Select(static frame => frame.ToArray()).ToArray());
    }

    private static void AssertSlotStateUnchanged(Scope scope, SlotState before)
    {
        Assert.Same(before.Storage, scope.Owner.Objects);
        var entries = scope.Owner.Objects?.ToArray();
        if (before.Entries is null) Assert.Null(entries);
        else
        {
            Assert.NotNull(entries); Assert.Equal(before.Entries.Length, entries.Length);
            for (int index = 0; index < entries.Length; index++)
            { Assert.Equal(before.Entries[index].Key, entries[index].Key); Assert.Same(before.Entries[index].Value, entries[index].Value); }
        }
        foreach (var saved in before.Rows)
        {
            Assert.Equal(saved.Subtype, saved.Row.WeaponSubtype); Assert.Equal(saved.Level, saved.Row.Level);
            Assert.Equal(saved.Progress, saved.Row.Progress); Assert.Equal(saved.Appearance, saved.Row.AppearancePosition);
            Assert.Same(saved.Vector, saved.Row.BitVector); Assert.Same(saved.Words, saved.Row.BitVector?.bits);
            Assert.Equal(saved.Bits, saved.Row.BitVector?.bits);
            Assert.Same(saved.Attributes, saved.Row.Attributes); Assert.Equal(saved.Values, saved.Row.Attributes?.ToArray());
        }
        var frames = scope.Queue.ToArray(); Assert.Equal(before.Frames.Length, frames.Length);
        for (int index = 0; index < frames.Length; index++)
        {
            Assert.Same(before.Frames[index], frames[index]);
            Assert.Equal(before.FrameBytes[index], frames[index]);
        }
    }

    private static (Info Left, Info Right, Info Tail) ReadySlotPrefix(Scope scope)
    {
        var left = scope.Item(Weapon.Blade, 3); var right = scope.Item(Weapon.Sword, 3);
        var tail = scope.Item(Weapon.Club, 3);
        left.BitVector.Add(2); right.BitVector.Add(3); left.AppearancePosition = 7; right.AppearancePosition = 8;
        // Preserve a real pre-existing packet, not an empty-output-only assertion.
        Assert.True(scope.Owner.Equip(Weapon.Club, 4)); Assert.Single(scope.Queue);
        return (left, right, tail);
    }

    [Theory]
    [InlineData("null-row", false)] [InlineData("null-row", true)]
    [InlineData("null-bitmap", false)] [InlineData("null-bitmap", true)]
    [InlineData("null-words", false)] [InlineData("null-words", true)]
    [InlineData("empty-words", false)] [InlineData("empty-words", true)]
    [InlineData("wide-words", false)] [InlineData("wide-words", true)]
    [InlineData("wrong-key", false)] [InlineData("wrong-key", true)]
    [InlineData("bad-level", false)] [InlineData("bad-level", true)]
    [InlineData("bad-subtype", false)] [InlineData("bad-subtype", true)]
    [InlineData("overlap", false)] [InlineData("overlap", true)]
    [InlineData("appearance-overlap", false)] [InlineData("appearance-overlap", true)]
    [InlineData("alias", false)] [InlineData("alias", true)]
    [InlineData("missing-storage", false)] [InlineData("missing-storage", true)]
    public Task AdmissionV2WholeSwapRejectsMalformedTailWithoutAnyPrefixMutation(string malformed, bool update)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var (left, right, tail) = ReadySlotPrefix(scope);
        switch (malformed)
        {
            case "null-row": scope.Owner.Objects[Weapon.Club] = null!; break;
            case "null-bitmap": tail.BitVector = null!; break;
            case "null-words": tail.BitVector.bits = null!; break;
            case "empty-words": tail.BitVector.bits = []; break;
            case "wide-words": tail.BitVector.bits = [16u, 0u]; break;
            case "wrong-key": tail.WeaponSubtype = Weapon.Whip; break;
            case "bad-level": tail.Level = 0; break;
            case "bad-subtype": tail.WeaponSubtype = (Weapon)0; break;
            case "overlap": tail.BitVector.Add(2); break;
            case "appearance-overlap": tail.AppearancePosition = 7; break;
            case "alias": scope.Owner.Objects[Weapon.Club] = left; break;
            case "missing-storage": scope.Owner.Objects = null!; break;
        }
        var before = CaptureSlotState(scope, left, right, tail); int available = scope.Available;
        Assert.False(scope.Owner.Swap(2, 3, update));
        AssertSlotStateUnchanged(scope, before); Assert.Equal(available, scope.Available);
        Assert.False(scope.Owner.Free(2)); Assert.False(scope.Owner.Free(3));
        Assert.Null(scope.Owner.TryGetItem(2)); Assert.Null(scope.Owner.TryGetItem(3));
    });

    [Theory]
    [InlineData("equip")] [InlineData("unequip")] [InlineData("swap")]
    [InlineData("show")] [InlineData("hide-position")] [InlineData("hide-subtype")]
    [InlineData("held-equip-expression")] [InlineData("held-unequip-expression")]
    [InlineData("held-guardian-expression")]
    public Task AdmissionV2EveryDestructiveConsumerRefusesUnknownTailEvenAfterAnOccupiedPrefix(string operation)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var (left, right, tail) = ReadySlotPrefix(scope);
        scope.Owner.Objects[Weapon.Club] = null!;
        var before = CaptureSlotState(scope, left, right, tail); int available = scope.Available;
        bool accepted = operation switch
        {
            "equip" => scope.Owner.Equip(Weapon.Blade, 5),
            "unequip" => scope.Owner.Unequip(2),
            "swap" => scope.Owner.Swap(2, 3),
            "show" => scope.Owner.Show(Weapon.Blade, 9),
            "hide-position" => scope.Owner.Hide((byte)7),
            "hide-subtype" => scope.Owner.Hide(Weapon.Blade),
            "held-equip-expression" => scope.Owner.ContainsWeapon(Weapon.Blade) &&
                (scope.Owner.Free(2) || scope.Owner.Unequip(2)) &&
                (!scope.Owner.checkDuplication(Weapon.Blade, 2, out var duplicate) || scope.Owner.Unequip(duplicate)) &&
                scope.Owner.Equip(Weapon.Blade, 2),
            "held-unequip-expression" => !scope.Owner.Free(2) && scope.Owner.Unequip(2),
            _ => (scope.Owner.FreeAppearance(7) || scope.Owner.Hide((byte)7)) &&
                (!scope.Owner.checkDuplicatedAppearance(Weapon.Blade, 7, out var appearance) || scope.Owner.Hide(appearance)) &&
                scope.Owner.Show(Weapon.Blade, 7),
        };
        Assert.False(accepted); AssertSlotStateUnchanged(scope, before); Assert.Equal(available, scope.Available);
        Assert.False(scope.Owner.checkDuplication(Weapon.Blade, 2, out var ignored)); Assert.Equal(0, ignored);
        Assert.False(scope.Owner.checkDuplicatedAppearance(Weapon.Blade, 7, out var hidden)); Assert.Equal((byte)0, hidden);
    });

    [Theory]
    [InlineData("storage")] [InlineData("player")] [InlineData("foreign-player")]
    public Task AdmissionV2MutationsRequireTheBoundStorageAndActualPlayerOwner(string missing)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var (left, right, tail) = ReadySlotPrefix(scope);
        var player = scope.Client.Player; var storage = scope.Client.HundredWeapons;
        if (missing == "storage") scope.Client.HundredWeapons = null;
        else if (missing == "player") scope.Client.Player = null!;
        else scope.Client.Player = new LegacyNativeGameClientRuntime().Player;
        try
        {
            var before = CaptureSlotState(scope, left, right, tail); int available = scope.Available;
            Assert.False(scope.Owner.Equip(Weapon.Blade, 5)); Assert.False(scope.Owner.Unequip(2));
            Assert.False(scope.Owner.Swap(2, 3)); Assert.False(scope.Owner.Show(Weapon.Blade, 9));
            Assert.False(scope.Owner.Hide((byte)7)); Assert.False(scope.Owner.Hide(Weapon.Blade));
            AssertSlotStateUnchanged(scope, before); Assert.Equal(available, scope.Available);
        }
        finally { scope.Client.Player = player; scope.Client.HundredWeapons = storage; }
    });

    [Fact]
    public Task AdmissionV2DirectEquipAndShowCannotCreateAmbiguousOccupiedSlots() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var (left, right, tail) = ReadySlotPrefix(scope);
        var before = CaptureSlotState(scope, left, right, tail);
        Assert.False(scope.Owner.Equip(Weapon.Blade, 3)); Assert.False(scope.Owner.Show(Weapon.Blade, 8));
        AssertSlotStateUnchanged(scope, before);
        Assert.True(scope.Owner.Equip(Weapon.Blade, 2)); AssertSlotStateUnchanged(scope, before);
    });

    [Fact]
    public Task AdmissionV2ValidSameRowSwapAndFullByteAppearanceRemainSupported() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade, 3);
        row.BitVector.Add(2); row.BitVector.Add(3);
        Assert.True(scope.Owner.Swap(2, 3)); Assert.Equal(12u, row.BitVector.bits[0]);
        Assert.Equal(Hex("1200310908021000180020003A0408021003"), Assert.Single(scope.Queue));
        Assert.True(scope.Owner.Swap(2, 2)); Assert.Equal(12u, row.BitVector.bits[0]);
        Assert.Equal(Hex("1200310908021000180020003A0408021002"), scope.Queue.Last());
        Assert.True(scope.Owner.Show(Weapon.Blade, byte.MaxValue)); Assert.Equal(byte.MaxValue, row.AppearancePosition);
        Assert.False(scope.Owner.FreeAppearance(byte.MaxValue)); Assert.True(scope.Owner.Hide(byte.MaxValue));
        Assert.True(scope.Owner.FreeAppearance(byte.MaxValue)); Assert.Equal(2, scope.Queue.Count);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task AdmissionV2EmptyAliasedBitmapOwnersCannotTurnOneEquipIntoTwoPhysicalItems(bool sameVector)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var left = scope.Item(Weapon.Blade); var right = scope.Item(Weapon.Sword);
        if (sameVector) right.BitVector = left.BitVector;
        else right.BitVector.bits = left.BitVector.bits;
        var before = CaptureSlotState(scope, left, right); int available = scope.Available;
        Assert.False(scope.Owner.Free(2)); Assert.False(scope.Owner.Equip(Weapon.Blade, 2));
        Assert.False(scope.Owner.Show(Weapon.Blade, 7)); AssertSlotStateUnchanged(scope, before);
        Assert.Equal(available, scope.Available);
    });
}
