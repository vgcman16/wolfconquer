using System.Buffers.Binary;
using System.Collections.Concurrent;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using ItemEffect = WolfConquer.Core.World.LegacyNativeGameItemFlags.ItemEffect;
using Status = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

// Actual offline native components and owned queue/packet prestates, not a live attack dispatcher.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeExtraEffectTests
{
    [Fact]
    public Task NoneEffectsDoNotConsultNullRandomOrSuppliedPacket() => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(7013);
        LegacyNativeAttackBase.MyRandom = null!;

        LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, null!);

        Assert.Null(LegacyNativeAttackBase.MyRandom);
        Assert.Empty(f.Queue);
        Assert.Empty(f.Owner.Player.BitVector.ArrayFlags);
        Assert.Equal(ItemEffect.None, f.Equipment.RingEffect);
        Assert.Equal(ItemEffect.None, f.Equipment.NecklaceEffect);
        f.AssertCallerPacketUnchanged();
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task EitherIndependentTwentyPercentGateCanPublishItsOwnFlagAndAnimation(bool ring) => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(1, r => r[0] < 20));
        Enable(f, ring);
        var before = DateTime.Now;

        LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet);

        var after = DateTime.Now;
        AssertRegistration(f, ring, before, after);
        Assert.False(f.Owner.Player.ContainFlag(ring ? Status.MagicShield : Status.Stigma));
        var frames = f.Queue.ToArray();
        Assert.Equal(2, frames.Length);
        AssertStatusFrame(frames[0], Mask(ring));
        AssertAnimationFrame(frames[1], ring ? 1095 : 1020);
        Assert.Equal(frames[1], NativeSpellTestFixture.Bytes(f.Packet));
        Assert.Equal(4, f.Packet.Position);
        f.Random.AssertDrawCount(1);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task EqualityAtTwentyRejectsEitherGateWithoutTouchingTheCallerPacket(bool ring) => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(1, r => r[0] == 20));
        Enable(f, ring);

        LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet);

        Assert.Empty(f.Queue);
        Assert.Empty(f.Owner.Player.BitVector.ArrayFlags);
        f.AssertCallerPacketUnchanged();
        f.Random.AssertDrawCount(1);
    });

    [Fact]
    public Task BothFailedGatesConsumeSeparateDraws() => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(2, r => r[0] >= 20 && r[1] >= 20));
        EnableBoth(f);

        LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet);

        Assert.Empty(f.Queue);
        Assert.Empty(f.Owner.Player.BitVector.ArrayFlags);
        f.AssertCallerPacketUnchanged();
        f.Random.AssertDrawCount(2);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ARejectedGateDoesNotPreventTheOtherIndependentGate(bool firstSucceeds) => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(2,
            r => firstSucceeds ? r[0] < 20 && r[1] >= 20 : r[0] >= 20 && r[1] < 20));
        EnableBoth(f);

        LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet);

        Assert.Single(f.Owner.Player.BitVector.ArrayFlags);
        Assert.True(f.Owner.Player.ContainFlag(firstSucceeds ? Status.Stigma : Status.MagicShield));
        var frames = f.Queue.ToArray();
        Assert.Equal(2, frames.Length);
        AssertStatusFrame(frames[0], Mask(firstSucceeds));
        AssertAnimationFrame(frames[1], firstSucceeds ? 1095 : 1020);
        f.Random.AssertDrawCount(2);
    });

    [Fact]
    public Task BothSuccessesPublishStatusThenAnimationInRingNecklaceOrderAndReuseOnlyTheCallerAnimationPacket() => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(2, r => r[0] < 20 && r[1] < 20));
        EnableBoth(f);
        var before = DateTime.Now;

        LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet);

        var after = DateTime.Now;
        AssertRegistration(f, true, before, after);
        AssertRegistration(f, false, before, after);
        var frames = f.Queue.ToArray();
        Assert.Equal(4, frames.Length);
        AssertStatusFrame(frames[0], Mask(true));
        AssertAnimationFrame(frames[1], 1095);
        AssertStatusFrame(frames[2], Mask(true) | Mask(false));
        AssertAnimationFrame(frames[3], 1020);
        Assert.NotEqual(frames[1], frames[3]);
        for (var i = 1; i < frames.Length; i++) Assert.NotSame(frames[i - 1], frames[i]);
        Assert.Equal(frames[3], NativeSpellTestFixture.Bytes(f.Packet));
        Assert.Equal(frames[3].Length, f.Packet.Size);
        Assert.Equal(4, f.Packet.Position);
        f.Random.AssertDrawCount(2);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task AnExistingRegistrationStillConsumesRateBeforeContainmentAndIsNeverReplaced(bool ring) => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(1, r => r[0] < 20));
        Enable(f, ring);
        var key = (int)(ring ? Status.Stigma : Status.MagicShield);
        Assert.True(f.Owner.Player.BitVector.TryAdd(key, 77, false, 3));
        var retained = f.Owner.Player.BitVector.ArrayFlags[key];
        var timer = retained.Timer;
        var invokeTimer = retained.TimerInvoker;

        LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, null!);

        Assert.Same(retained, f.Owner.Player.BitVector.ArrayFlags[key]);
        Assert.Equal(timer, retained.Timer);
        Assert.Equal(invokeTimer, retained.TimerInvoker);
        Assert.Equal(77, retained.Secounds);
        Assert.Equal(3, retained.InvokerSecouds);
        Assert.False(retained.RemoveOnDead);
        Assert.Empty(f.Queue);
        f.AssertCallerPacketUnchanged();
        f.Random.AssertDrawCount(1);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task IncoherentVisibleStatusCannotAuthorizeExtraEffectAdmissionOrSuccessAnimation(bool ring) => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(1, r => r[0] < 20));
        Enable(f, ring);
        var key = (int)(ring ? Status.Stigma : Status.MagicShield);
        f.Owner.Player.BitVector.Add(key);
        var vector = f.Owner.Player.BitVector; var registry = vector.ArrayFlags;
        var words = vector.bits; var before = words.ToArray();
        Assert.Throws<InvalidDataException>(() => vector.ContainFlag(key));
        Assert.Throws<InvalidDataException>(() => LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet));
        Assert.Same(registry, vector.ArrayFlags); Assert.Empty(registry);
        Assert.Same(words, vector.bits); Assert.Equal(before, vector.bits);
        Assert.True(f.Owner.Player.BitVector.Contain(key));
        Assert.Empty(f.Queue); f.AssertCallerPacketUnchanged();
        // Pre-query RNG and later proc publication are separate transaction debt.
        // Do not require a dirty RNG prefix as sanitized acceptance behavior.
    });

    [Fact]
    public Task NullGamePollFaultsAfterFlagAndStatusPublicationButBeforeAnimationAndLaterNecklaceDraw() => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(2, r => r[0] < 20 && r[1] < 20));
        EnableBoth(f);
        LegacyNativePoolClients.GamePoll = null!;

        Assert.Throws<NullReferenceException>(() => LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet));

        Assert.True(f.Owner.Player.ContainFlag(Status.Stigma));
        Assert.False(f.Owner.Player.ContainFlag(Status.MagicShield));
        AssertStatusFrame(Assert.Single(f.Queue), Mask(true));
        Assert.Null(LegacyNativePoolClients.GamePoll);
        Assert.Equal(f.OriginalPacket, NativeSpellTestFixture.Bytes(f.Packet));
        Assert.Equal(4, f.Packet.Position); // Animation's InitWriter precedes GamePoll access.
        f.Random.AssertDrawCount(1);
    });

    [Fact]
    public Task NullEquipmentFaultsBeforeAnyDrawAndIsNotAllocated() => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(7159);
        f.Owner.Equipment = null;

        Assert.Throws<NullReferenceException>(() => LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet));

        Assert.Null(f.Owner.Equipment);
        Assert.Empty(f.Queue);
        f.AssertCallerPacketUnchanged();
        f.Random.AssertDrawCount(0);
    });

    [Fact]
    public Task NullClientFaultsBeforeAnyDraw() => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(7177);
        Assert.Throws<NullReferenceException>(() => LegacyNativeGetWeaponSpell.CheckExtraEffects(null!, f.Packet));
        Assert.Empty(f.Queue);
        f.AssertCallerPacketUnchanged();
        f.Random.AssertDrawCount(0);
    });

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public Task RateIsEvaluatedBeforeAccessingThePotentiallyNullBitVector(bool ring, bool passes) => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(1, r => passes ? r[0] < 20 : r[0] >= 20));
        Enable(f, ring);
        f.Owner.Player.BitVector = null!;

        if (passes)
            Assert.Throws<NullReferenceException>(() => LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet));
        else
            LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, f.Packet);

        Assert.Null(f.Owner.Player.BitVector);
        Assert.Empty(f.Queue);
        f.AssertCallerPacketUnchanged();
        f.Random.AssertDrawCount(1);
    });

    [Fact]
    public Task NullSuppliedPacketDoesNotPreventPriorFlagAndStatusPublication() => NativePacketTestThread.Run(() =>
    {
        using var f = new ExtraFixture(NativeAttackRandomScope.FindSeed(2, r => r[0] < 20 && r[1] < 20));
        EnableBoth(f);

        Assert.Throws<NullReferenceException>(() => LegacyNativeGetWeaponSpell.CheckExtraEffects(f.Owner, null!));

        Assert.True(f.Owner.Player.ContainFlag(Status.Stigma));
        Assert.False(f.Owner.Player.ContainFlag(Status.MagicShield));
        AssertStatusFrame(Assert.Single(f.Queue), Mask(true));
        f.AssertCallerPacketUnchanged();
        f.Random.AssertDrawCount(1);
    });

    private static void Enable(ExtraFixture f, bool ring)
    {
        if (ring) f.Equipment.RingEffect = (ItemEffect)1;
        else f.Equipment.NecklaceEffect = (ItemEffect)1;
    }

    private static void EnableBoth(ExtraFixture f) { Enable(f, true); Enable(f, false); }
    private static uint Mask(bool ring) => 1u << (ring ? 9 : 8);

    private static void AssertRegistration(ExtraFixture f, bool ring, DateTime before, DateTime after)
    {
        var row = f.Owner.Player.BitVector.ArrayFlags[(int)(ring ? Status.Stigma : Status.MagicShield)];
        Assert.Equal(ring ? 9 : 8, row.Key);
        Assert.Equal(ring ? 20 : 15, row.Secounds);
        Assert.True(row.RemoveOnDead);
        Assert.Equal(0, row.InvokerSecouds);
        Assert.InRange(row.Timer, before, after);
    }

    private static void AssertStatusFrame(byte[] frame, uint mask)
    {
        var root = ReadFrame(frame, 2176);
        Assert.Equal(17UL, Scalar(root, 1));
        var update = Nested(root, 2);
        Assert.Equal(25UL, Scalar(update, 1));
        var words = update.Where(field => field.Number == 3).Select(field => field.Value).ToArray();
        Assert.Equal(18, words.Length);
        Assert.Equal((ulong)mask, words[0]);
        Assert.All(words.Skip(1), word => Assert.Equal(0UL, word));
        Assert.Equal(2, root.Count);
        Assert.Equal(19, update.Count);
    }

    private static void AssertAnimationFrame(byte[] frame, int spell)
    {
        var root = ReadFrame(frame, 2239);
        Assert.Equal(17UL, Scalar(root, 1));
        Assert.DoesNotContain(root, field => field.Number == 2);
        Assert.Equal(101UL, Scalar(root, 3));
        Assert.Equal(202UL, Scalar(root, 4));
        Assert.Equal((ulong)spell, Scalar(root, 5));
        Assert.Equal(4UL, Scalar(root, 6));
        Assert.Equal(0UL, Scalar(root, 7));
        Assert.Equal(0UL, Scalar(root, 8));
        Assert.Equal(0UL, Scalar(root, 9));
        var target = Nested(root, 10);
        Assert.Equal(17UL, Scalar(target, 1));
        Assert.Equal(0UL, Scalar(target, 2));
        Assert.Equal(1UL, Scalar(target, 3));
        foreach (var field in new[] { 4, 6, 7, 8, 9 }) Assert.Equal(0UL, Scalar(target, field));
        Assert.Equal(9, root.Count);
        Assert.Equal(8, target.Count);
    }

    // Independent bounded reader for these two test contracts: varints and nested messages only.
    private readonly record struct Field(int Number, ulong Value, byte[]? Data);
    private static ulong Scalar(List<Field> fields, int number) => Assert.Single(fields, f => f.Number == number).Value;
    private static List<Field> Nested(List<Field> fields, int number) => ReadFields(Assert.Single(fields, f => f.Number == number).Data!);
    private static List<Field> ReadFrame(byte[] frame, ushort packetId)
    {
        Assert.Equal(frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(packetId, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        return ReadFields(frame.AsSpan(4));
    }

    private static List<Field> ReadFields(ReadOnlySpan<byte> bytes)
    {
        var result = new List<Field>();
        var offset = 0;
        while (offset < bytes.Length)
        {
            var tag = Varint(bytes, ref offset);
            var number = checked((int)(tag >> 3));
            Assert.True(number > 0);
            if ((tag & 7) == 2)
            {
                var length = checked((int)Varint(bytes, ref offset));
                Assert.InRange(length, 0, bytes.Length - offset);
                result.Add(new Field(number, 0, bytes.Slice(offset, length).ToArray()));
                offset += length;
            }
            else
            {
                Assert.Equal(0UL, tag & 7);
                result.Add(new Field(number, Varint(bytes, ref offset), null));
            }
        }
        return result;
    }

    private static ulong Varint(ReadOnlySpan<byte> bytes, ref int offset)
    {
        ulong value = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            Assert.True(offset < bytes.Length);
            var next = bytes[offset++];
            value |= (ulong)(next & 127) << shift;
            if ((next & 128) == 0) return value;
        }
        throw new InvalidDataException("Overlong test protobuf varint.");
    }

    private sealed class ExtraFixture : IDisposable
    {
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? priorPoll = LegacyNativePoolClients.GamePoll;
        private readonly NativeSpellTestFixture native = new();
        internal NativeAttackRandomScope Random { get; }
        internal LegacyNativeGameClientRuntime Owner => native.Owner;
        internal Queue<byte[]> Queue => native.Queue;
        internal LegacyNativePacket Packet => native.Packet;
        internal LegacyNativeEquipItemStorage Equipment { get; }
        internal byte[] OriginalPacket { get; }

        internal ExtraFixture(int seed)
        {
            Random = new NativeAttackRandomScope(seed);
            LegacyNativePoolClients.GamePoll = new ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>();
            Equipment = new LegacyNativeEquipItemStorage(Owner);
            Owner.Equipment = Equipment;
            Owner.Player.X = 101;
            Owner.Player.Y = 202;
            Packet.Seek(0);
            Packet.Write(0x12345678u);
            Packet.Write(0x90ABCDEFu);
            Packet.Write(0x76543210u);
            Packet.Size = 12;
            Packet.Seek(7);
            OriginalPacket = NativeSpellTestFixture.Bytes(Packet);
        }

        internal void AssertCallerPacketUnchanged()
        {
            Assert.Equal(OriginalPacket, NativeSpellTestFixture.Bytes(Packet));
            Assert.Equal(12, Packet.Size);
            Assert.Equal(7, Packet.Position);
        }

        public void Dispose()
        {
            LegacyNativePoolClients.GamePoll = priorPoll;
            try { native.Dispose(); }
            finally { Random.Dispose(); }
        }
    }
}
