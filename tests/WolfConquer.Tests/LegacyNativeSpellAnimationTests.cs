using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;
using Animation = WolfConquer.Core.World.LegacyNativeMsgSpellAnimation;
using Effect = WolfConquer.Core.World.LegacyNativeMsgAttackPacket.AttackEffect;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSpellAnimationTests
{
    [Fact]
    public void ConstructorsCopiesAndDisposeKeepTheNativeMutableStateAndOddCoordinateAssignment()
    {
        var empty = new Animation();
        Assert.Empty(empty.Targets);
        Assert.Null(empty._Stream);
        Assert.Equal(0u, empty.bomb);
        var animation = new Animation(1, 2, 3, 4, 5, 6, 7, 8);
        Assert.Equal((1u, 2u, (ushort)3, (ushort)4, (ushort)5, (ushort)6, (byte)7, 8u),
            (animation.UID, animation.OpponentUID, animation.X, animation.Y, animation.SpellID,
                animation.SpellLevel, animation.LevelSoul, animation.bomb));
        var target = new Animation.SpellObj(12, 13, Effect.CriticalStrike, 14, 15);
        Assert.Equal(1u, target.Hit);
        Assert.Equal((14u, 14u, 14u, 0u), (target.MoveX, target.x1, target.xx1, target.xxx1));
        Assert.Equal((15u, 15u, 15u, 15u), (target.MoveY, target.y2, target.yy2, target.yyy2));
        target.TailedBeast = 19;
        var copy = Animation.SpellObj.ShallowCopy(target);
        Assert.NotSame(target, copy);
        Assert.Equal(target.TailedBeast, copy.TailedBeast);
        Assert.Equal(target.Effect, copy.Effect);
        Assert.Equal(1u, new Animation.SpellObj().Hit);
        Assert.Equal(1u, new Animation.SpellObj(1, 2, Effect.None).Hit);
        Assert.Throws<NullReferenceException>(() => Animation.SpellObj.ShallowCopy(null!));
        animation.Dispose();
        animation.Dispose();
        Assert.Null(animation.Targets);
        Assert.Equal(1u, animation.UID);
        Assert.Empty(empty.Targets);
    }

    [Fact]
    public void AttackEffectRetainsAllLiteralFlagsIncludingNonPowerOfTwoBreak()
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Effect)));
        Assert.True(typeof(Effect).IsDefined(typeof(FlagsAttribute), false));
        var fields = typeof(Effect).GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(f => f.MetadataToken).ToArray();
        Assert.Equal(new[] { "None", "Block", "Penetration", "CriticalStrike", "Imunity", "Break", "ResistMetal",
            "ResistWood", "ResistWater", "ResistFire", "ResistEarth", "AddStudyPoint", "LuckyStrike", "NatureShield",
            "Assasssin", "BurningSky", "FireCurse", "BonePulseDestroy", "TailedBeast", "Destroy", "Dash" },
            fields.Select(field => field.Name));
        Assert.Equal(new uint[] { 0, 1, 2, 4, 8, 10, 16, 32, 64, 128, 256, 512, 1024, 8192, 16384,
            32768, 65536, 0x4000000, 0x40000, 0x1000000, 0x8000000 },
            fields.Select(field => (uint)field.GetRawConstantValue()!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(60)]
    [InlineData(61)]
    public Task JustMeSplitsOnlyAboveThirtyWithoutDequeuingOrReplacingTargets(int count) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var animation = f.Animation();
        animation.SpellLevel = 260;
        animation.bomb = 257;
        animation.NextSpell = 123;
        var original = animation.Targets;
        for (uint index = 0; index < count; index++)
            original.Enqueue(new(index + 1, 0, Effect.CriticalStrike));
        var rows = original.ToArray();
        animation.JustMe(f.Native.Owner);
        var frames = f.Native.Queue.ToArray();
        Assert.Equal(count == 0 ? 1 : (count + 29) / 30, frames.Length);
        for (int group = 0; group < frames.Length; group++)
        {
            var selected = rows.Skip(group * 30).Take(30).Select(row => Target(row.UID, row.Damage, row.Hit != 0,
                (uint)row.Effect)).ToArray();
            Assert.Equal(Frame(Payload(17, 0, 3, 4, 21520, 4, 2, 123, 1, selected)), frames[group]);
        }
        Assert.Same(original, animation.Targets);
        Assert.Equal(rows, original.ToArray());
        Assert.Same(f.Native.Packet, animation._Stream);
        Assert.Equal(4, f.Native.Packet.Position);
        Assert.False(f.Native.Owner.FullLoading);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OpponentSuppressesCoordinatesAndTargetProjectionNarrowsMovesAndIgnoresStoredBeast(bool opponent) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var animation = f.Animation();
        animation.OpponentUID = opponent ? 99u : 0;
        var target = new Animation.SpellObj(25, 0, (Effect)uint.MaxValue)
        { Hit = uint.MaxValue, MoveX = 65537, MoveY = uint.MaxValue, TailedBeast = 99999 };
        animation.Targets.Enqueue(target);
        animation.JustMe(f.Native.Owner);
        Assert.Equal(Frame(Payload(17, opponent ? 99u : 0, opponent ? (ushort)0 : (ushort)3,
            opponent ? (ushort)0 : (ushort)4, 21520, 4, 2, 0, 0,
            [Target(25, 0, true, uint.MaxValue, 1, ushort.MaxValue)])), Assert.Single(f.Native.Queue));
        Assert.Equal(99999u, target.TailedBeast);
        Assert.Equal(uint.MaxValue, target.MoveY);
    });

    [Theory]
    [InlineData((byte)0, 1500u)]
    [InlineData((byte)19, 1650u)]
    [InlineData((byte)255, 5250u)]
    public Task PositiveDamageUsesTheCurrentPoolClientBeastRatherThanTargetOrRecipientState(byte level, uint beast) => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var source = new LegacyNativeGameClientRuntime();
        source.Beasts = new(source) { Activated = true };
        typeof(LegacyNativeBeastsRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(source.Beasts, level);
        LegacyNativePoolClients.GamePoll![17] = source;
        var animation = f.Animation();
        animation.Targets.Enqueue(new(7, 8, Effect.None) { TailedBeast = 1234 });
        animation.JustMe(f.Native.Owner);
        Assert.Equal(Frame(Payload(17, 0, 3, 4, 21520, 4, 2, 0, 0, [Target(7, 8, true, 0, beast: beast)])),
            Assert.Single(f.Native.Queue));
        Assert.Null(f.Native.Owner.Beasts);
    });

    [Fact]
    public Task MissingPoolFaultsEvenForZeroTargetsAfterResettingWriterWithoutChangingQueueOrSize() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var animation = f.Animation();
        LegacyNativePoolClients.GamePoll = null!;
        f.Native.Packet.Seek(44);
        f.Native.Packet.Size = 91;
        Assert.Throws<NullReferenceException>(() => animation.JustMe(f.Native.Owner));
        Assert.Equal(4, f.Native.Packet.Position);
        Assert.Equal(91, f.Native.Packet.Size);
        Assert.Empty(f.Native.Queue);
        Assert.Empty(animation.Targets);
        Assert.Null(LegacyNativePoolClients.GamePoll);
    });

    [Fact]
    public Task ZeroDamageSkipsMissingBeastButPositiveDamageFaultsBeforeSerialization() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        LegacyNativePoolClients.GamePoll![17] = new();
        var animation = f.Animation();
        var target = new Animation.SpellObj(7, 0, Effect.None);
        animation.Targets.Enqueue(target);
        animation.JustMe(f.Native.Owner);
        Assert.Single(f.Native.Queue);
        f.Native.Queue.Clear();
        target.Damage = 1;
        f.Native.Packet.Size = 77;
        Assert.Throws<NullReferenceException>(() => animation.JustMe(f.Native.Owner));
        Assert.Empty(f.Native.Queue);
        Assert.Equal(77, f.Native.Packet.Size);
        Assert.Equal(4, f.Native.Packet.Position);
        Assert.Same(target, Assert.Single(animation.Targets));
    });

    [Fact]
    public Task InactiveBeastAndMissingPoolEntryProduceZeroEvenWithPositiveDamage() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var animation = f.Animation();
        animation.Targets.Enqueue(new(7, 8, Effect.None));
        animation.JustMe(f.Native.Owner);
        var expected = Assert.Single(f.Native.Queue);
        var source = new LegacyNativeGameClientRuntime();
        source.Beasts = new(source) { Activated = false };
        LegacyNativePoolClients.GamePoll![17] = source;
        animation.JustMe(f.Native.Owner);
        Assert.Equal(expected, f.Native.Queue.Last());
        Assert.Equal(Frame(Payload(17, 0, 3, 4, 21520, 4, 2, 0, 0, [Target(7, 8, true, 0)])), expected);
    });

    [Fact]
    public Task ScreenUsesForeignViewOwnerWhileJustMeUsesTheSuppliedClientAndFalseStillSerializes() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var viewQueue = new Queue<byte[]>();
        var viewOwner = NativeSpellTestFixture.CreateOwner(viewQueue);
        f.Native.Owner.Player.View = viewOwner.Player.View;
        var animation = f.Animation();
        animation.Send(f.Native.Owner);
        Assert.Empty(f.Native.Queue);
        var expected = Assert.Single(viewQueue);
        animation.Send(f.Native.Owner, false);
        Assert.Single(viewQueue);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(f.Native.Packet));
        f.Native.Owner.Player.View = null!;
        animation.JustMe(f.Native.Owner);
        Assert.Equal(expected, Assert.Single(f.Native.Queue));
        f.Native.Queue.Clear();
        f.Native.Packet.Seek(99);
        Assert.Throws<NullReferenceException>(() => animation.Send(f.Native.Owner));
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(f.Native.Packet));
        Assert.Equal(4, f.Native.Packet.Position);
        Assert.Empty(f.Native.Queue);
        Assert.Same(viewOwner, viewOwner.Player.View.Owner);
    });

    [Fact]
    public Task NullTargetAndDisposedQueueFaultWithoutRepairAndDoNotDisposeTheSuppliedStream() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        var animation = f.Animation();
        animation.Targets.Enqueue(null!);
        f.Native.Packet.Size = 71;
        Assert.Throws<NullReferenceException>(() => animation.JustMe(f.Native.Owner));
        Assert.Equal(4, f.Native.Packet.Position);
        Assert.Equal(71, f.Native.Packet.Size);
        Assert.Empty(f.Native.Queue);
        animation.Dispose();
        f.Native.Packet.Seek(80);
        Assert.Throws<NullReferenceException>(() => animation.JustMe(f.Native.Owner));
        Assert.Equal(80, f.Native.Packet.Position); // Count access faults before CreateAnimation.InitWriter.
        Assert.Null(animation.Targets);
        Assert.Same(f.Native.Packet, animation._Stream);
        f.Native.Packet.Write((byte)1);
        Assert.Equal(81, f.Native.Packet.Position);
    });

    [Fact]
    public Task NativeSealFollowsTheWholePayloadAndIsExcludedFromHeaderLength() => NativePacketTestThread.Run(() =>
    {
        using var f = new Fixture();
        WolfConquer.Core.Networking.LegacyNativePacket.SealString = "TQServer";
        var animation = f.Animation();
        animation.JustMe(f.Native.Owner);
        byte[] body = Frame(Payload(17, 0, 3, 4, 21520, 4, 2, 0, 0, []));
        Assert.Equal(body.Concat(System.Text.Encoding.ASCII.GetBytes("TQServer")), Assert.Single(f.Native.Queue));
        Assert.Equal(body.Length, BinaryPrimitives.ReadUInt16LittleEndian(f.Native.Queue.Peek()));
        Assert.Equal(body.Length + 8, f.Native.Packet.Size);
        Assert.Equal(4, f.Native.Packet.Position);
    });

    internal static byte[] Frame(byte[] payload)
    {
        byte[] frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2239);
        payload.CopyTo(frame, 4);
        return frame;
    }

    // Independent literal contract writer for projection expectations, not production serialization.
    internal static byte[] Payload(uint attacker, uint attacked, ushort x, ushort y, ushort spell, byte level,
        byte hu, ushort next, byte effect, byte[][] targets)
    {
        List<byte> bytes = [];
        Field(bytes, 1, attacker);
        if (attacked != 0) Field(bytes, 2, attacked);
        if (x != 0) Field(bytes, 3, x);
        if (y != 0) Field(bytes, 4, y);
        Field(bytes, 5, spell); Field(bytes, 6, level); Field(bytes, 7, next); Field(bytes, 8, hu); Field(bytes, 9, effect);
        foreach (byte[] target in targets)
        {
            Varint(bytes, 82); Varint(bytes, (ulong)target.Length); bytes.AddRange(target);
        }
        return bytes.ToArray();
    }

    internal static byte[] Target(uint uid, uint damage, bool hit, uint flags, ushort x = 0, ushort y = 0,
        uint unknown = 0, uint beast = 0)
    {
        List<byte> bytes = [];
        Field(bytes, 1, uid); Field(bytes, 2, damage); Field(bytes, 3, hit ? 1u : 0u); Field(bytes, 4, flags);
        Field(bytes, 6, x); Field(bytes, 7, y); Field(bytes, 8, unknown); Field(bytes, 9, beast);
        return bytes.ToArray();
    }

    private static void Field(List<byte> bytes, uint field, ulong value) { Varint(bytes, (ulong)field << 3); Varint(bytes, value); }
    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? prior = LegacyNativePoolClients.GamePoll;
        internal NativeSpellTestFixture Native { get; } = new();
        internal Fixture() => LegacyNativePoolClients.GamePoll = new();
        internal Animation Animation()
        {
            var value = new Animation(17, 0, 3, 4, 21520, 4, 2);
            value.SetStream(Native.Packet);
            return value;
        }
        public void Dispose() { Native.Dispose(); LegacyNativePoolClients.GamePoll = prior!; }
    }
}
