using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemSendTests
{
    [Fact]
    public Task DefaultItemCreateReturnsSameStreamResetsCursorAndPreservesBufferTail() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = new LegacyNativeGameItemRuntime();
            Assert.Same(scope.Packet, item.ItemCreate(scope.Packet, item));
            Assert.Equal(EmptyItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(206, scope.Packet.Size);
            Assert.Equal(4, scope.Packet.Position);
            AssertTail(scope, 206);
        });

    [Fact]
    public Task ItemCreateWritesEveryFieldInNativeOrderAndNarrowsPositionEffectAndRelicType() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = new LegacyNativeGameItemRuntime
            {
                Uid = 0x01020304, ITEM_ID = 1_000_000,
                Durability = 0x1112, MaximDurability = 0x1314,
                Mode = (Flags.ItemMode)0x1516, Position = 0x1718,
                SocketProgress = 0x191A1B1C, SocketOne = Flags.Gem.SuperPhoenixGem,
                SocketTwo = Flags.Gem.EmptySocket, SpellID = 0x21222324,
                Effect = (Flags.ItemEffect)0x2526, Plus = 0x27, Bless = 0x28,
                Bound = 0x29, Enchant = 0x2A, ProgresGreen = 0x2B2C2D2E,
                Suspicious = 0x2F, Locked = 0x30, PlusProgress = 0x31323334,
                TimeLeftInMinutes = 0x35363738, StackSize = 0x393A,
                PerfectionLevel = 0x3B3C3D3E, PerfectionProgress = 0x41424344,
                OwnerUID = 0x45464748, OwnerName = "A", Signature = "B",
                RuneEXP = 0x51525354,
                RelicAttributes =
                [
                    new(LegacyNativeChiAttribute.CriticalStrike, 1, false),
                    new(LegacyNativeChiAttribute.SkillCriticalStrike, 2, true),
                    new((LegacyNativeChiAttribute)0x123, 0, false),
                    new((LegacyNativeChiAttribute)(-1), 0, true),
                    new(1006u),
                ],
                AnimaItemID = 0x61626364, MythSoul = 0x65666768, MythSoulPoints = 0x71727374,
            };

            item.ItemCreate(scope.Packet, item);

            byte[] expected = Convert.FromHexString(
                "CE0044080403020140420F00121114131615181C1B1A1903FF2423222126" +
                "2728292A2E2D2C2B2F0030003433323100000000383736353A39" +
                "3E3D3C3B4443424148474645" +
                "41" + new string('0', 62) + "42" + new string('0', 126) +
                "000054535251E9030000360800002300000063010000EE030000" +
                "64636261000000006867666574737271");
            Assert.Equal(206, expected.Length);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal((ushort)0x1718, item.Position);
            Assert.Equal((Flags.ItemEffect)0x2526, item.Effect);
            Assert.Equal((LegacyNativeChiAttribute)0x123, item.RelicAttributes[2].Type);
            Assert.Equal(4, scope.Packet.Position);
        });

    [Fact]
    public Task ItemCreateUsesCallerTimerStorageButSuppliedItemForAllOtherFields() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var caller = new LegacyNativeGameItemRuntime { Uid = 999, TimeLeftInMinutes = 54321 };
            var supplied = Item();
            supplied.Activate = 1;
            supplied.EndDate = DateTime.Now.AddHours(2);
            supplied.TimeLeftInMinutes = 12345;

            caller.ItemCreate(scope.Packet, supplied);

            byte[] expected = PlainItemFrame();
            Put32(expected, 50, 54321);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(54321u, caller.TimeLeftInMinutes);
            Assert.Equal(0u, supplied.TimeLeftInMinutes);
            Assert.Equal(12345u, (uint)typeof(LegacyNativeGameItemRuntime)
                .GetField("_TimeLeftInMinutes", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!.GetValue(supplied)!);
        });

    [Fact]
    public Task ActivatedCallerClockUsesCallerEndDateAndSuppressesBothBackingMinuteValues() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var caller = new LegacyNativeGameItemRuntime
            {
                Activate = 1, EndDate = DateTime.Now.AddHours(1), TimeLeftInMinutes = 123,
            };
            var supplied = Item();
            supplied.TimeLeftInMinutes = 456;
            DateTime earliest = DateTime.Now;
            caller.ItemCreate(scope.Packet, supplied);
            DateTime latest = DateTime.Now;

            byte[] actual = NativeSpellTestFixture.Bytes(scope.Packet);
            uint remaining = Read32(actual, 46);
            Assert.InRange(remaining,
                (uint)Math.Max(0, (caller.EndDate - latest).TotalSeconds - 1),
                (uint)((caller.EndDate - earliest).TotalSeconds + 1));
            byte[] expected = PlainItemFrame();
            Put32(expected, 46, remaining);
            Assert.Equal(expected, actual);
            Assert.Equal(456u, supplied.TimeLeftInMinutes);
        });

    [Theory]
    [InlineData(2u)]
    [InlineData(uint.MaxValue)]
    public Task OtherActivateValuesSuppressBothTimerWords(uint activate) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var caller = new LegacyNativeGameItemRuntime
        {
            Activate = activate, TimeLeftInMinutes = uint.MaxValue, EndDate = DateTime.MaxValue,
        };
        caller.ItemCreate(scope.Packet, new LegacyNativeGameItemRuntime());
        Assert.Equal(EmptyItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public Task RelicArrayLengthIsNotNormalizedToFive(int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = new LegacyNativeGameItemRuntime
        {
            RelicAttributes = new LegacyNativeRelicAttribute[count],
            AnimaItemID = 101, MythSoul = 202, MythSoulPoints = 303,
        };
        var originalArray = item.RelicAttributes;
        item.ItemCreate(scope.Packet, item);
        byte[] expected = EmptyItemFrame(count);
        Put32(expected, 170 + 4 * count, 101);
        Put32(expected, 178 + 4 * count, 202);
        Put32(expected, 182 + 4 * count, 303);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Same(originalArray, item.RelicAttributes);
        AssertTail(scope, expected.Length);
    });

    [Theory]
    [InlineData(false, "é", "C3")]
    [InlineData(false, "😀", "F09F")]
    [InlineData(true, "é", "C3")]
    [InlineData(true, "😀", "F09F")]
    public Task FixedStringsCopyUtf16CharacterCountOfUtf8Bytes(bool signature, string value, string wirePrefix) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = new LegacyNativeGameItemRuntime();
            if (signature) item.Signature = value;
            else item.OwnerName = value;
            item.ItemCreate(scope.Packet, item);
            byte[] expected = EmptyItemFrame();
            Convert.FromHexString(wirePrefix).CopyTo(expected, signature ? 100 : 68);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(value, signature ? item.Signature : item.OwnerName);
        });

    [Fact]
    public Task FixedAsciiStringsTruncateAtNativeWidthsAndRetainEmbeddedNullBytes() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = new LegacyNativeGameItemRuntime
            {
                OwnerName = new string('X', 40), Signature = "A\0B" + new string('Y', 70),
            };
            item.ItemCreate(scope.Packet, item);
            byte[] expected = EmptyItemFrame();
            Array.Fill(expected, (byte)'X', 68, 32);
            expected[100] = (byte)'A';
            expected[102] = (byte)'B';
            Array.Fill(expected, (byte)'Y', 103, 61);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(40, item.OwnerName.Length);
            Assert.Equal(73, item.Signature.Length);
        });

    [Theory]
    [InlineData("item", 4)]
    [InlineData("owner", 68)]
    [InlineData("signature", 100)]
    [InlineData("relics", 170)]
    public Task NullInputsLeaveNativePartialWritesAndDoNotFinalize(string missing, int cursor) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = new LegacyNativeGameItemRuntime();
            if (missing == "owner") item.OwnerName = null!;
            if (missing == "signature") item.Signature = null!;
            if (missing == "relics") item.RelicAttributes = null!;

            Assert.Throws<NullReferenceException>(() => item.ItemCreate(scope.Packet, missing == "item" ? null! : item));

            Assert.Equal(cursor, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
            byte[] expected = Enumerable.Repeat((byte)0xA5, cursor + 16).ToArray();
            Array.Clear(expected, 4, cursor - 4);
            Assert.Equal(expected, scope.Memory(cursor + 16));
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task UnserializedStorageDoesNotLeakIntoTheFrame() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = new LegacyNativeGameItemRuntime
        {
            ItemPoints = uint.MaxValue, Leng = ushort.MaxValue, PacketID = ushort.MaxValue,
            Color = (Flags.Color)uint.MaxValue, Inscribed = ushort.MaxValue,
            UnKnow = ushort.MaxValue, WH_ID = uint.MaxValue, PerfectionRank = ushort.MaxValue,
            UnLockTimer = int.MaxValue, EndDate = DateTime.MaxValue,
        };
        item.Deposite.TryAdd(17, Item());
        item.ItemCreate(scope.Packet, item);
        Assert.Equal(EmptyItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Single(item.Deposite);
    });

    [Fact]
    public Task ItemCreateHonorsActualNativeSealAndHeaderExcludesIt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        LegacyNativePacket.SealString = "TQServer";
        var item = new LegacyNativeGameItemRuntime();
        item.ItemCreate(scope.Packet, item);
        byte[] expected = [.. EmptyItemFrame(), .. Convert.FromHexString("5451536572766572")];
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(214, scope.Packet.Size);
        Assert.Equal((ushort)206, Read16(expected, 0));
        Assert.Equal(4, scope.Packet.Position);
        AssertTail(scope, 214);
    });

    [Fact]
    public Task OversizedRelicArrayRetainsNativeSilentWriterBoundaryOnOwnedMaxBuffer() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = new LegacyNativeGameItemRuntime
            {
                RelicAttributes = new LegacyNativeRelicAttribute[5000],
                AnimaItemID = 111, MythSoul = 222, MythSoulPoints = 333,
            };
            item.ItemCreate(scope.Packet, item);
            var expected = new byte[16382];
            Put16(expected, 0, 16382);
            Put16(expected, 2, 2116);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.Equal(new byte[] { 0xA5, 0xA5 }, scope.Memory(16384)[16382..]);
            Assert.Equal(5000, item.RelicAttributes.Length);
        });
}
