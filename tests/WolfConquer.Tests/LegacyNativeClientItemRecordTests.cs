using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClientItemRecordTests
{
    private const long InvalidDateBinary = 3155378976000000000L;

    private static readonly (string Name, Type Type, int Offset)[] Fields =
    [
        ("UID", typeof(uint), 0), ("ITEM_ID", typeof(uint), 4),
        ("Durability", typeof(ushort), 8), ("MaximDurability", typeof(ushort), 10),
        ("Position", typeof(ushort), 12), ("SocketProgress", typeof(uint), 16),
        ("SocketOne", typeof(Flags.Gem), 20), ("SocketTwo", typeof(Flags.Gem), 21),
        ("Effect", typeof(Flags.ItemEffect), 24), ("Plus", typeof(byte), 28),
        ("Bless", typeof(byte), 29), ("Bound", typeof(byte), 30),
        ("Enchant", typeof(byte), 31), ("Suspicious", typeof(byte), 32),
        ("Locked", typeof(byte), 33), ("PlusProgress", typeof(uint), 36),
        ("Inscribed", typeof(ushort), 40), ("Activate", typeof(uint), 44),
        ("TimeLeftInMinutes", typeof(uint), 48), ("StackSize", typeof(ushort), 52),
        ("WH_ID", typeof(uint), 56), ("Color", typeof(Flags.Color), 60),
        ("UnLockTimer", typeof(int), 64), ("ItemPoints", typeof(uint), 68),
        ("RemainingTime", typeof(uint), 72), ("test3", typeof(uint), 76),
        ("test4", typeof(uint), 80), ("PurificationItemID", typeof(uint), 84),
        ("PurificationLevel", typeof(uint), 88), ("PurificationDuration", typeof(uint), 92),
        ("PurificationAddedOn", typeof(long), 96), ("EffectID", typeof(uint), 104),
        ("EffectLevel", typeof(uint), 108), ("EffectPercent", typeof(uint), 112),
        ("EffectPercent2", typeof(uint), 116), ("EffectDuration", typeof(uint), 120),
        ("EffectAddedOn", typeof(long), 128), ("DepositeCount", typeof(uint), 136),
        ("AnimaItemID", typeof(uint), 140), ("RuneEXP", typeof(uint), 144),
        ("Attribute1", typeof(uint), 148), ("Attribute2", typeof(uint), 152),
        ("Attribute3", typeof(uint), 156), ("Attribute4", typeof(uint), 160),
        ("Attribute5", typeof(uint), 164), ("Expiration", typeof(long), 168),
        ("MythSoul", typeof(uint), 176), ("MythSoulPoints", typeof(uint), 180),
    ];

    [Fact]
    public void RawValueStorageRetainsAllFortyEightNativeFieldsWidthsAndOffsets()
    {
        var type = typeof(LegacyNativeClientItemRecord);
        var actual = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(LayoutKind.Sequential, type.StructLayoutAttribute!.Value);
        Assert.Equal(184, Marshal.SizeOf<LegacyNativeClientItemRecord>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<LegacyNativeClientItemRecord>());
        Assert.Equal(48, actual.Length);
        for (var index = 0; index < Fields.Length; index++)
        {
            Assert.Equal(Fields[index].Name, actual[index].Name);
            Assert.Equal(Fields[index].Type, actual[index].FieldType);
            Assert.Equal(Fields[index].Offset, Marshal.OffsetOf<LegacyNativeClientItemRecord>(Fields[index].Name).ToInt32());
        }
    }

    [Fact]
    public void CompleteRawImageProbeReadsAllFieldsWithoutDtoOrSignedWidthLoss()
    {
        // Test-only layout probe, NOT an authored native file-reader/short-read policy.
        var bytes = Enumerable.Range(0, 184).Select(index => unchecked((byte)(index * 37 + 11))).ToArray();
        var record = MemoryMarshal.Read<LegacyNativeClientItemRecord>(bytes);
        foreach (var field in Fields)
        {
            var storageType = field.Type.IsEnum ? Enum.GetUnderlyingType(field.Type) : field.Type;
            object expected;
            if (storageType == typeof(byte)) expected = bytes[field.Offset];
            else if (storageType == typeof(ushort)) expected = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(field.Offset));
            else if (storageType == typeof(uint)) expected = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(field.Offset));
            else if (storageType == typeof(int)) expected = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(field.Offset));
            else expected = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(field.Offset));
            if (field.Type.IsEnum) expected = Enum.ToObject(field.Type, expected);
            Assert.Equal(expected, typeof(LegacyNativeClientItemRecord).GetField(field.Name)!.GetValue(record));
        }
    }

    [Fact]
    public void NonRuneConversionCopiesScalarsWithoutServerLoaderClamps()
    {
        var record = new LegacyNativeClientItemRecord
        {
            UID = uint.MaxValue, ITEM_ID = 600000u,
            Durability = ushort.MaxValue, MaximDurability = 1, Position = ushort.MaxValue,
            SocketProgress = uint.MaxValue, SocketOne = (Flags.Gem)255, SocketTwo = (Flags.Gem)133,
            Effect = (Flags.ItemEffect)uint.MaxValue, Plus = 255, Bless = 254, Bound = 253,
            Enchant = 252, Suspicious = 251, Locked = 250, PlusProgress = uint.MaxValue,
            Inscribed = ushort.MaxValue, Activate = 0, TimeLeftInMinutes = uint.MaxValue,
            StackSize = 0, WH_ID = uint.MaxValue, Color = (Flags.Color)uint.MaxValue,
            UnLockTimer = int.MinValue, ItemPoints = uint.MaxValue, AnimaItemID = uint.MaxValue,
            RuneEXP = uint.MaxValue, MythSoul = uint.MaxValue, MythSoulPoints = uint.MaxValue,
        };
        var item = record.GetDataItem();
        Assert.Equal(record.UID, item.Uid);
        Assert.Equal(record.ITEM_ID, item.ITEM_ID);
        Assert.Equal(record.Durability, item.Durability);
        Assert.Equal(record.MaximDurability, item.MaximDurability);
        Assert.Equal(record.Position, item.Position);
        Assert.Equal(record.SocketProgress, item.SocketProgress);
        Assert.Equal(record.SocketOne, item.SocketOne);
        Assert.Equal(record.SocketTwo, item.SocketTwo);
        Assert.Equal(record.Effect, item.Effect);
        Assert.Equal(record.Plus, item.Plus);
        Assert.Equal(record.Bless, item.Bless);
        Assert.Equal(record.Bound, item.Bound);
        Assert.Equal(record.Enchant, item.Enchant);
        Assert.Equal(record.Suspicious, item.Suspicious);
        Assert.Equal(record.Locked, item.Locked);
        Assert.Equal(record.PlusProgress, item.PlusProgress);
        Assert.Equal(record.Inscribed, item.Inscribed);
        Assert.Equal(record.Activate, item.Activate);
        Assert.Equal(record.TimeLeftInMinutes, item.TimeLeftInMinutes);
        Assert.Equal(record.StackSize, item.StackSize);
        Assert.Equal(record.WH_ID, item.WH_ID);
        Assert.Equal(record.Color, item.Color);
        Assert.Equal(record.UnLockTimer, item.UnLockTimer);
        Assert.Equal(record.ItemPoints, item.ItemPoints);
        Assert.Equal(record.AnimaItemID, item.AnimaItemID);
        Assert.Equal(record.RuneEXP, item.RuneEXP);
        Assert.Equal(record.MythSoul, item.MythSoul);
        Assert.Equal(record.MythSoulPoints, item.MythSoulPoints);
    }

    [Fact]
    public void EveryConversionCreatesFreshConcreteItemWithoutAllocatingAUid()
    {
        var counter = LegacyNativePoolItems.ItemCounter;
        var table = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemCounter = null!;
            LegacyNativePoolItems.ItemsBase = null;
            var record = new LegacyNativeClientItemRecord { UID = 0u, ITEM_ID = 600000u };
            var first = record.GetDataItem();
            var second = record.GetDataItem();
            Assert.IsType<LegacyNativeGameItemRuntime>(first);
            Assert.NotSame(first, second);
            Assert.Equal(0u, first.Uid);
            Assert.Equal(0u, second.Uid);
            Assert.NotSame(first.Deposite, second.Deposite);
            Assert.NotSame(first.RelicAttributes, second.RelicAttributes);
            Assert.Empty(first.Deposite);
            Assert.Empty(second.Deposite);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        }
        finally { LegacyNativePoolItems.ItemsBase = table; LegacyNativePoolItems.ItemCounter = counter; }
    }

    [Fact]
    public void ActiveRecordStoresLoadedTimeLeftEvenWhileItsGetterReturnsZero()
    {
        var item = new LegacyNativeClientItemRecord { Activate = 1u, TimeLeftInMinutes = uint.MaxValue }.GetDataItem();
        Assert.Equal(0u, item.TimeLeftInMinutes);
        item.Activate = 0u;
        Assert.Equal(uint.MaxValue, item.TimeLeftInMinutes);
    }

    [Fact]
    public void DefaultRawRecordRetainsZeroWidthsAndNativeUnserializedConstructionState()
    {
        var item = default(LegacyNativeClientItemRecord).GetDataItem();
        Assert.Equal(0u, item.Uid);
        Assert.Equal(0u, item.ITEM_ID);
        Assert.Equal((ushort)0, item.StackSize);
        Assert.Equal((Flags.Color)0, item.Color);
        Assert.Equal((Flags.Gem)0, item.SocketOne);
        Assert.Equal((Flags.Gem)0, item.SocketTwo);
        Assert.Equal((Flags.ItemMode)0, item.Mode);
        Assert.Equal((ushort)0, item.Leng);
        Assert.Equal((ushort)0, item.PacketID);
        Assert.Equal((ushort)0, item.UnKnow);
        Assert.Equal(0u, item.ProgresGreen);
        Assert.Equal(0u, item.OwnerUID);
        Assert.Equal(0u, item.PerfectionLevel);
        Assert.Equal((ushort)0, item.PerfectionRank);
        Assert.Equal(0u, item.PerfectionProgress);
        Assert.Equal("", item.OwnerName);
        Assert.Equal("", item.Signature);
        Assert.Equal(DateTime.FromBinary(0L), item.EndDate);
    }

    [Fact]
    public void RuneSelfMetadataOverwritesEffectThatWasLoadedBeforeItemId()
    {
        var table = LegacyNativePoolItems.ItemsBase;
        try
        {
            const uint id = 4010010u;
            LegacyNativePoolItems.ItemsBase = new() { [id] = new() { RuneSpellID = id } };
            var item = new LegacyNativeClientItemRecord { ITEM_ID = id, Effect = Flags.ItemEffect.Poison }.GetDataItem();
            Assert.Equal(4010001u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
        }
        finally { LegacyNativePoolItems.ItemsBase = table; }
    }

    [Fact]
    public void DifferentRuneSpellMetadataClearsLoadedEffectAndUsesDirectSpellId()
    {
        var table = LegacyNativePoolItems.ItemsBase;
        try
        {
            const uint id = 4020010u;
            LegacyNativePoolItems.ItemsBase = new() { [id] = new() { RuneSpellID = 12300u } };
            var item = new LegacyNativeClientItemRecord { ITEM_ID = id, Effect = Flags.ItemEffect.Shield }.GetDataItem();
            Assert.Equal(12300u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.None, item.Effect);
        }
        finally { LegacyNativePoolItems.ItemsBase = table; }
    }

    [Fact]
    public void NonRuneNeverReadsNullMetadataOrRewritesArbitraryLoadedEffect()
    {
        var table = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = null;
            var item = new LegacyNativeClientItemRecord { ITEM_ID = 4009999u, Effect = (Flags.ItemEffect)uint.MaxValue }.GetDataItem();
            Assert.Equal((Flags.ItemEffect)uint.MaxValue, item.Effect);
            Assert.Equal(0u, item.SpellID);
        }
        finally { LegacyNativePoolItems.ItemsBase = table; }
    }

    [Fact]
    public void NullRuneTableFaultPrecedesInvalidRefineryAndExpirationBinary()
    {
        var table = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = null;
            var record = new LegacyNativeClientItemRecord { ITEM_ID = 4010001u, EffectAddedOn = InvalidDateBinary, Expiration = InvalidDateBinary };
            Assert.Throws<NullReferenceException>(() => record.GetDataItem());
        }
        finally { LegacyNativePoolItems.ItemsBase = table; }
    }

    [Fact]
    public void MissingRuneKeyFaultPrecedesLaterDateDecoding()
    {
        var table = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = new();
            var record = new LegacyNativeClientItemRecord { ITEM_ID = 4010001u, EffectAddedOn = InvalidDateBinary };
            Assert.Throws<KeyNotFoundException>(() => record.GetDataItem());
        }
        finally { LegacyNativePoolItems.ItemsBase = table; }
    }

    [Fact]
    public void NullRuneRowFaultPrecedesLaterDateDecoding()
    {
        var table = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = new() { [4010001u] = null! };
            var record = new LegacyNativeClientItemRecord { ITEM_ID = 4010001u, EffectAddedOn = InvalidDateBinary };
            Assert.Throws<NullReferenceException>(() => record.GetDataItem());
        }
        finally { LegacyNativePoolItems.ItemsBase = table; }
    }

    [Fact]
    public void PurificationDecodesValidBinaryKindsAndRefusesInvalidBinaryWithoutTickFallback()
    {
        DateTime[] times = [DateTime.MinValue, DateTime.MaxValue,
            new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Local)];
        foreach (var time in times)
        {
            var item = new LegacyNativeClientItemRecord { UID = 17, PurificationItemID = 1001,
                PurificationAddedOn = time.ToBinary() }.GetDataItem();
            Assert.Equal(time.ToBinary(), item.Purification.AddedOn.ToBinary());
            Assert.Equal(time.Kind, item.Purification.AddedOn.Kind);
            Assert.Equal(17u, item.Purification.ItemUID); Assert.True(item.Purification.InLife);
        }
        foreach (long binary in new[] { InvalidDateBinary, long.MaxValue })
            Assert.Throws<ArgumentException>(() => new LegacyNativeClientItemRecord
                { PurificationItemID = 1001, PurificationAddedOn = binary }.GetDataItem());
    }

    [Fact]
    public void PurificationRetainsInteriorBinaryTicksExactly()
    {
        foreach (var added in new[] { 1L, 123456789L, DateTime.MaxValue.Ticks - 1 })
        {
            var item = new LegacyNativeClientItemRecord { PurificationItemID = 1001u, PurificationAddedOn = added }.GetDataItem();
            Assert.Equal(DateTime.FromBinary(added), item.Purification.AddedOn);
        }
    }

    [Fact]
    public void InactivePurificationResetsEntireLoadedStruct()
    {
        var item = new LegacyNativeClientItemRecord { UID = 99u, PurificationItemID = 1000u, PurificationLevel = 7u, PurificationAddedOn = 123L }.GetDataItem();
        Assert.Equal(default(LegacyNativeItemExtra.Purification), item.Purification);
    }

    [Fact]
    public void ExpiredPurificationResetsEvenWhenItsItemIdWouldOtherwiseBePermanent()
    {
        var item = new LegacyNativeClientItemRecord { UID = 99u, PurificationItemID = 2000u, PurificationDuration = 1u, PurificationAddedOn = 1L }.GetDataItem();
        Assert.Equal(default(LegacyNativeItemExtra.Purification), item.Purification);
    }

    [Fact]
    public void PurificationDurationOverflowPrecedesInvalidRefineryBinary()
    {
        var record = new LegacyNativeClientItemRecord { PurificationItemID = 1001u, PurificationDuration = 1u, PurificationAddedOn = DateTime.MaxValue.Ticks - 1, EffectAddedOn = InvalidDateBinary };
        Assert.Throws<ArgumentOutOfRangeException>(() => record.GetDataItem());
    }

    [Fact]
    public void InvalidRefineryBinaryIsNotGivenPurificationTickFallback()
    {
        var record = new LegacyNativeClientItemRecord { EffectAddedOn = InvalidDateBinary, Expiration = InvalidDateBinary };
        Assert.Throws<ArgumentException>(() => record.GetDataItem());
    }

    [Fact]
    public void PermanentRefineryRetainsAllFieldsAndMutatedAvailability()
    {
        var record = new LegacyNativeClientItemRecord { UID = 55u, EffectID = 1001u, EffectLevel = uint.MaxValue, EffectPercent = 91u, EffectPercent2 = 92u, EffectAddedOn = 123L };
        var item = record.GetDataItem();
        Assert.Equal(55u, item.Refinary.ItemUID);
        Assert.Equal(1001u, item.Refinary.EffectID);
        Assert.Equal(uint.MaxValue, item.Refinary.EffectLevel);
        Assert.Equal(91u, item.Refinary.EffectPercent);
        Assert.Equal(92u, item.Refinary.EffectPercent2);
        Assert.Equal(0u, item.Refinary.EffectDuration);
        Assert.Equal(DateTime.FromBinary(123L), item.Refinary.AddedOn);
        Assert.Equal((LegacyNativeItemExtra.Typing)0, item.Refinary.Typ);
        Assert.True(item.Refinary.Available);
    }

    [Fact]
    public void ExpiredRefineryResetsEntireLoadedStruct()
    {
        var item = new LegacyNativeClientItemRecord { UID = 55u, EffectID = 2000u, EffectLevel = 7u, EffectDuration = 1u, EffectAddedOn = 1L }.GetDataItem();
        Assert.Equal(default(LegacyNativeItemExtra.Refinery), item.Refinary);
    }

    [Fact]
    public void RefineryDurationOverflowPrecedesInvalidExpirationBinary()
    {
        var record = new LegacyNativeClientItemRecord { EffectID = 1001u, EffectDuration = 1u, EffectAddedOn = DateTime.MaxValue.Ticks - 1, Expiration = InvalidDateBinary };
        Assert.Throws<ArgumentOutOfRangeException>(() => record.GetDataItem());
    }

    [Fact]
    public void BinaryKindsRemainConsistentForPurificationRefineryAndExpiration()
    {
        var time = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var binary = time.ToBinary();
        var item = new LegacyNativeClientItemRecord { PurificationItemID = 1001u, PurificationAddedOn = binary, EffectID = 1001u, EffectAddedOn = binary, Expiration = binary }.GetDataItem();
        Assert.Equal(time, item.Purification.AddedOn);
        Assert.Equal(DateTimeKind.Utc, item.Purification.AddedOn.Kind);
        Assert.Equal(time, item.Refinary.AddedOn);
        Assert.Equal(DateTimeKind.Utc, item.Refinary.AddedOn.Kind);
        Assert.Equal(time, item.EndDate);
        Assert.Equal(DateTimeKind.Utc, item.EndDate.Kind);
    }

    [Fact]
    public void AllFiveRelicWordsAreConvertedInOrderWithNativeNarrowing()
    {
        uint[] words = [0u, 1101u, 65666014u, uint.MaxValue, 900013u];
        var item = new LegacyNativeClientItemRecord { Attribute1 = words[0], Attribute2 = words[1], Attribute3 = words[2], Attribute4 = words[3], Attribute5 = words[4] }.GetDataItem();
        Assert.Equal(5, item.RelicAttributes.Length);
        for (var index = 0; index < words.Length; index++)
        {
            Assert.Equal((LegacyNativeChiAttribute)(words[index] % 100), item.RelicAttributes[index].Type);
            Assert.Equal(unchecked((ushort)(words[index] / 1000)), item.RelicAttributes[index].Value);
            Assert.Equal(words[index] % 1000 - words[index] % 100 == 100, item.RelicAttributes[index].Epic);
        }
    }

    [Fact]
    public void InvalidExpirationBinaryThrowsRatherThanFallingBackToDefaultDate()
    {
        var record = new LegacyNativeClientItemRecord { Expiration = InvalidDateBinary };
        Assert.Throws<ArgumentException>(() => record.GetDataItem());
    }

    [Fact]
    public void RawRemainingTimeReservedWordsAndDepositCountAreNotNewItemAssignments()
    {
        var record = new LegacyNativeClientItemRecord { RemainingTime = uint.MaxValue, test3 = uint.MaxValue, test4 = uint.MaxValue, DepositeCount = uint.MaxValue };
        var item = record.GetDataItem();
        Assert.Empty(item.Deposite);
        Assert.Equal(0u, item.RemainingTime);
        Assert.Equal(0u, item.Activate);
        Assert.Equal((ushort)0, item.UnKnow);
        Assert.Equal(0u, item.ProgresGreen);
        Assert.Equal(DateTime.FromBinary(0L), item.EndDate);
    }

    [Fact]
    public void ConversionDoesNotRewriteAnyRawRecordFieldOnSuccessOrFault()
    {
        var record = new LegacyNativeClientItemRecord { UID = 77u, ITEM_ID = 600000u, Plus = 255, PurificationItemID = 1u, PurificationAddedOn = long.MinValue };
        var before = Fields.Select(field => typeof(LegacyNativeClientItemRecord).GetField(field.Name)!.GetValue(record)).ToArray();
        _ = record.GetDataItem();
        Assert.Equal(before, Fields.Select(field => typeof(LegacyNativeClientItemRecord).GetField(field.Name)!.GetValue(record)).ToArray());
        record.Expiration = InvalidDateBinary;
        before = Fields.Select(field => typeof(LegacyNativeClientItemRecord).GetField(field.Name)!.GetValue(record)).ToArray();
        Assert.Throws<ArgumentException>(() => record.GetDataItem());
        Assert.Equal(before, Fields.Select(field => typeof(LegacyNativeClientItemRecord).GetField(field.Name)!.GetValue(record)).ToArray());
    }
}
