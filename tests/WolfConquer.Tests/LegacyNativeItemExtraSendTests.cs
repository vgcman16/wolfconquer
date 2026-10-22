using System.Buffers.Binary;
using System.Reflection;
using WolfConquer.Core.World;
using NativeExtra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

// Source-component proof for MsgGameItem.SendItemExtra and MsgItemExtra.CreateArray.
// These use the native mutable structs, not PlayerItemSnapshotItem or its serializer.
// They do not prove Send's socket integration, raw loading, SQL, or live-client rendering.
public sealed class LegacyNativeItemExtraSendTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothStoredExtraUidsZeroReturnsNullBeforeEvaluatingPayloads(bool invalidExpiry)
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Uid = 999,
            Purification = Purification(uid: 0),
            Refinary = Refinery(uid: 0),
        };
        if (invalidExpiry)
        {
            item.Purification.PurificationDuration = 1;
            item.Purification.AddedOn = DateTime.MaxValue;
            item.Refinary.EffectDuration = 1;
            item.Refinary.AddedOn = DateTime.MaxValue;
        }

        Assert.Null(item.CreateItemExtraFrame());

        Assert.Equal(NativeExtra.Typing.ExpireTime, item.Purification.Typ);
        Assert.Equal(NativeExtra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
        Assert.Equal(999u, item.Uid);
    }

    [Fact]
    public void PermanentPurificationUsesItsOwnUidAndMutatesOriginalTypeAndAvailability()
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Uid = 999,
            Purification = Purification(0x01020304),
        };
        var before = item.Purification;

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(Convert.FromHexString(
            "28005608" + "01000000" +
            "04030201" + "08000000" + "01350C00" + "03000000" +
            "17000000" + "00000000" + "00000000" + "00000000"), actual);
        Assert.Equal(NativeExtra.Typing.Stabilization, item.Purification.Typ);
        Assert.True(PurificationAvailable(item.Purification));
        AssertPurificationPayloadUnchanged(before, item.Purification);
        Assert.Equal(999u, item.Uid);
    }

    [Fact]
    public void PermanentRefineryMapsClientEffectAndMutatesOriginalTypeAndAvailability()
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Uid = 999,
            Refinary = Refinery(0x090A0B0C),
        };
        var before = item.Refinary;

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(Convert.FromHexString(
            "24005608" + "01000000" +
            "0C0B0A09" + "03000000" + "2D010000" + "05000000" +
            "0B000000" + "16000000" + "00000000"), actual);
        Assert.Equal(NativeExtra.Typing.PermanentRefinery, item.Refinary.Typ);
        Assert.True(item.Refinary.Available);
        AssertRefineryPayloadUnchanged(before, item.Refinary);
        Assert.Equal(999u, item.Uid);
    }

    [Fact]
    public void BothPermanentExtrasWritePurificationThenRefineryWithoutInventingSharedUid()
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Uid = 999,
            Purification = Purification(0x01020304),
            Refinary = Refinery(0x090A0B0C),
        };

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(Convert.FromHexString(
            "44005608" + "02000000" +
            "04030201" + "08000000" + "01350C00" + "03000000" +
            "17000000" + "00000000" + "00000000" + "00000000" +
            "0C0B0A09" + "03000000" + "2D010000" + "05000000" +
            "0B000000" + "16000000" + "00000000"), actual);
        Assert.Equal(0x01020304u, item.Purification.ItemUID);
        Assert.Equal(0x090A0B0Cu, item.Refinary.ItemUID);
        Assert.Equal(999u, item.Uid);
    }

    [Fact]
    public void TimedBothExtrasUseAddingTypesAndBoundedSecondsWithFullWirePayloads()
    {
        DateTime addedOn = DateTime.Now.AddSeconds(-7);
        var purification = Purification(71);
        purification.AddedOn = addedOn;
        purification.PurificationDuration = 300;
        var refinery = Refinery(82);
        refinery.AddedOn = addedOn;
        refinery.EffectDuration = 600;
        var item = new LegacyNativeGameItemRuntime
        {
            Purification = purification,
            Refinary = refinery,
        };
        DateTime earliest = DateTime.Now;

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        DateTime latest = DateTime.Now;
        int purificationSeconds = ReadInt32(actual, 32);
        int refinerySeconds = ReadInt32(actual, 64);
        AssertSecondsInWindow(addedOn, 300, earliest, latest, purificationSeconds);
        AssertSecondsInWindow(addedOn, 600, earliest, latest, refinerySeconds);
        Assert.Equal(ExpectedFrame(
            2,
            71, 6, 800001, 3, 23, 300, unchecked((uint)purificationSeconds), 0,
            82, 2, 301, 5, 11, 22, unchecked((uint)refinerySeconds)), actual);
        Assert.Equal(NativeExtra.Typing.PurificationAdding, item.Purification.Typ);
        Assert.Equal(NativeExtra.Typing.RefinaryAdding, item.Refinary.Typ);
        Assert.True(PurificationAvailable(item.Purification));
        Assert.True(item.Refinary.Available);
        AssertPurificationPayloadUnchanged(purification, item.Purification);
        AssertRefineryPayloadUnchanged(refinery, item.Refinary);
    }

    [Fact]
    public void RetainedUidsWithBothExpiredExtrasEmitCountZeroAndClearOriginalAvailability()
    {
        var purification = PreviouslyAvailableExpiredPurification(71);
        var refinery = Refinery(82);
        refinery.EffectDuration = 1;
        refinery.AddedOn = DateTime.Now.AddMinutes(-1);
        refinery.Available = true;
        var item = new LegacyNativeGameItemRuntime
        {
            Purification = purification,
            Refinary = refinery,
        };

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(Convert.FromHexString("0800560800000000"), actual);
        Assert.Equal(NativeExtra.Typing.ExpireTime, item.Purification.Typ);
        Assert.Equal(NativeExtra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
        AssertPurificationPayloadUnchanged(purification, item.Purification);
        AssertRefineryPayloadUnchanged(refinery, item.Refinary);
    }

    [Fact]
    public void ExpiredPurificationIsOmittedWhilePermanentRefineryRemains()
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Purification = PreviouslyAvailableExpiredPurification(71),
            Refinary = Refinery(82),
        };

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(ExpectedFrame(1, 82, 3, 301, 5, 11, 22, 0), actual);
        Assert.Equal(NativeExtra.Typing.ExpireTime, item.Purification.Typ);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.Equal(NativeExtra.Typing.PermanentRefinery, item.Refinary.Typ);
        Assert.True(item.Refinary.Available);
    }

    [Fact]
    public void ExpiredRefineryIsOmittedWhilePermanentPurificationRemains()
    {
        var refinery = Refinery(82);
        refinery.EffectDuration = 1;
        refinery.AddedOn = DateTime.Now.AddMinutes(-1);
        refinery.Available = true;
        var item = new LegacyNativeGameItemRuntime
        {
            Purification = Purification(71),
            Refinary = refinery,
        };

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(ExpectedFrame(1, 71, 8, 800001, 3, 23, 0, 0, 0), actual);
        Assert.Equal(NativeExtra.Typing.Stabilization, item.Purification.Typ);
        Assert.True(PurificationAvailable(item.Purification));
        Assert.Equal(NativeExtra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
        Assert.False(item.Refinary.Available);
    }

    [Fact]
    public void OneNonzeroUidAllowsOtherLivePayloadToKeepItsStoredZeroUid()
    {
        var item = new LegacyNativeGameItemRuntime
        {
            Uid = 999,
            Purification = Purification(0),
            Refinary = Refinery(82),
        };

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(ExpectedFrame(
            2, 0, 8, 800001, 3, 23, 0, 0, 0,
            82, 3, 301, 5, 11, 22, 0), actual);
        Assert.Equal(0u, item.Purification.ItemUID);
        Assert.Equal(82u, item.Refinary.ItemUID);
        Assert.Equal(999u, item.Uid);
    }

    [Theory]
    [InlineData(724439u, 724439u)]
    [InlineData(724440u, 301u)]
    [InlineData(724444u, 301u)]
    [InlineData(724445u, 724445u)]
    [InlineData(3004139u, 3004139u)]
    [InlineData(3004140u, 301u)]
    [InlineData(3004141u, 3004141u)]
    [InlineData(3006164u, 3006164u)]
    [InlineData(3006165u, 301u)]
    [InlineData(3006170u, 301u)]
    [InlineData(3006171u, 3006171u)]
    [InlineData(uint.MaxValue, uint.MaxValue)]
    public void ClientRefineryMappingUsesExactNativeRangeEdges(uint effectId, uint wireEffectId)
    {
        var refinery = Refinery(82);
        refinery.EffectID = effectId;
        var item = new LegacyNativeGameItemRuntime { Refinary = refinery };

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(ExpectedFrame(1, 82, 3, wireEffectId, 5, 11, 22, 0), actual);
        Assert.Equal(effectId, item.Refinary.EffectID);
    }

    [Theory]
    [InlineData(724440u)]
    [InlineData(724444u)]
    [InlineData(3004140u)]
    [InlineData(3006165u)]
    [InlineData(3006170u)]
    public void InterserverSerializerKeepsRawRefineryEffectAndSuppliedType(uint effectId)
    {
        var refinery = Refinery(82);
        refinery.EffectID = effectId;
        refinery.Typ = NativeExtra.Typing.StabilizationEffectRefined;
        NativeExtra.Refinery[] refinerys = [refinery];

        byte[] actual = LegacyNativeItemExtraFrame.CreateArray([], refinerys, tointer: true);

        Assert.Equal(ExpectedFrame(1, 82, 4, effectId, 5, 11, 22, 0), actual);
        Assert.Equal(NativeExtra.Typing.StabilizationEffectRefined, refinerys[0].Typ);
        Assert.False(refinerys[0].Available);
        Assert.Equal(effectId, refinerys[0].EffectID);
    }

    [Fact]
    public void SerializerWritesAllPurificationsBeforeRefineriesWithoutFilteringOrMutatingLists()
    {
        var firstPurification = Purification(71);
        firstPurification.Typ = (NativeExtra.Typing)uint.MaxValue;
        var secondPurification = Purification(0);
        secondPurification.PurificationItemID = 800002;
        secondPurification.PurificationLevel = 4;
        secondPurification.EffectPercent2 = 24;
        var firstRefinery = Refinery(82);
        var secondRefinery = Refinery(0);
        secondRefinery.EffectID = 3005000;
        secondRefinery.EffectLevel = 6;
        secondRefinery.EffectPercent = 12;
        secondRefinery.EffectPercent2 = 25;
        NativeExtra.Purification[] purifications = [firstPurification, secondPurification];
        NativeExtra.Refinery[] refinerys = [firstRefinery, secondRefinery];

        byte[] actual = LegacyNativeItemExtraFrame.CreateArray(purifications, refinerys);

        Assert.Equal(ExpectedFrame(
            4,
            71, uint.MaxValue, 800001, 3, 23, 0, 0, 0,
            0, 7, 800002, 4, 24, 0, 0, 0,
            82, 4, 301, 5, 11, 22, 0,
            0, 4, 3005000, 6, 12, 25, 0), actual);
        Assert.Equal((NativeExtra.Typing)uint.MaxValue, purifications[0].Typ);
        Assert.Equal(NativeExtra.Typing.ExpireTime, purifications[1].Typ);
        Assert.All(purifications, x => Assert.False(PurificationAvailable(x)));
        Assert.All(refinerys, x => Assert.False(x.Available));
        Assert.Equal(724440u, refinerys[0].EffectID);
        Assert.Equal(0u, purifications[1].ItemUID);
        Assert.Equal(0u, refinerys[1].ItemUID);
    }

    [Fact]
    public void ZeroDurationLowEffectIdsKeepNativePreviouslyAvailableLatch()
    {
        var purification = Purification(71);
        Assert.True(purification.InLife);
        purification.PurificationItemID = 1000;
        var refinery = Refinery(82);
        refinery.EffectID = 1000;
        refinery.Available = true;
        var item = new LegacyNativeGameItemRuntime
        {
            Purification = purification,
            Refinary = refinery,
        };

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(ExpectedFrame(
            2,
            71, 8, 1000, 3, 23, 0, 0, 0,
            82, 3, 1000, 5, 11, 22, 0), actual);
        Assert.True(PurificationAvailable(item.Purification));
        Assert.True(item.Refinary.Available);
    }

    [Fact]
    public void ZeroDurationLowEffectIdsWithoutAvailableLatchEmitCountZero()
    {
        var purification = Purification(71);
        purification.PurificationItemID = 1000;
        var refinery = Refinery(82);
        refinery.EffectID = 1000;
        var item = new LegacyNativeGameItemRuntime
        {
            Purification = purification,
            Refinary = refinery,
        };

        byte[] actual = Assert.IsType<byte[]>(item.CreateItemExtraFrame());

        Assert.Equal(Convert.FromHexString("0800560800000000"), actual);
        Assert.Equal(NativeExtra.Typing.ExpireTime, item.Purification.Typ);
        Assert.Equal(NativeExtra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
    }

    private static NativeExtra.Purification Purification(uint uid) => new()
    {
        ItemUID = uid,
        Typ = NativeExtra.Typing.ExpireTime,
        PurificationItemID = 800001,
        PurificationLevel = 3,
        EffectPercent2 = 23,
        AddedOn = new DateTime(2026, 10, 21, 12, 34, 56),
    };

    private static NativeExtra.Refinery Refinery(uint uid) => new()
    {
        ItemUID = uid,
        Typ = NativeExtra.Typing.StabilizationEffectRefined,
        EffectID = 724440,
        EffectLevel = 5,
        EffectPercent = 11,
        EffectPercent2 = 22,
        AddedOn = new DateTime(2026, 10, 21, 12, 34, 56),
    };

    private static NativeExtra.Purification PreviouslyAvailableExpiredPurification(uint uid)
    {
        var purification = Purification(uid);
        Assert.True(purification.InLife);
        purification.PurificationDuration = 1;
        purification.AddedOn = DateTime.Now.AddMinutes(-1);
        return purification;
    }

    // Independent literal-word oracle: no production packet, snapshot, mapping,
    // or native extra helper is called to construct the expected complete frame.
    private static byte[] ExpectedFrame(params uint[] words)
    {
        var expected = new byte[4 + words.Length * 4];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0, 2), checked((ushort)expected.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2, 2), 2134);
        for (int index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4 + index * 4, 4), words[index]);
        return expected;
    }

    private static int ReadInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static void AssertSecondsInWindow(
        DateTime addedOn, uint duration, DateTime earliest, DateTime latest, int actual)
    {
        DateTime expires = addedOn.AddSeconds(duration);
        int minimum = (int)(expires - latest).TotalSeconds - 1;
        int maximum = (int)(expires - earliest).TotalSeconds + 1;
        Assert.InRange(actual, Math.Max(1, minimum), maximum);
    }

    private static bool PurificationAvailable(NativeExtra.Purification purification) =>
        (bool)typeof(NativeExtra.Purification)
            .GetField("Available", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(purification)!;

    private static void AssertPurificationPayloadUnchanged(
        NativeExtra.Purification expected, NativeExtra.Purification actual)
    {
        Assert.Equal(expected.ItemUID, actual.ItemUID);
        Assert.Equal(expected.PurificationItemID, actual.PurificationItemID);
        Assert.Equal(expected.PurificationLevel, actual.PurificationLevel);
        Assert.Equal(expected.EffectPercent2, actual.EffectPercent2);
        Assert.Equal(expected.PurificationDuration, actual.PurificationDuration);
        Assert.Equal(expected.AddedOn, actual.AddedOn);
    }

    private static void AssertRefineryPayloadUnchanged(NativeExtra.Refinery expected, NativeExtra.Refinery actual)
    {
        Assert.Equal(expected.ItemUID, actual.ItemUID);
        Assert.Equal(expected.EffectID, actual.EffectID);
        Assert.Equal(expected.EffectLevel, actual.EffectLevel);
        Assert.Equal(expected.EffectPercent, actual.EffectPercent);
        Assert.Equal(expected.EffectPercent2, actual.EffectPercent2);
        Assert.Equal(expected.EffectDuration, actual.EffectDuration);
        Assert.Equal(expected.AddedOn, actual.AddedOn);
    }
}
