using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Air = WolfConquer.Core.World.LegacyNativeMsgAirPowerUp;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeAirPowerTests
{
    [Fact]
    public void MutableModelHasNativeDefaultsDistinctListsAndSignedFlagsEnum()
    {
        var first = new Air();
        var second = new Air();
        Assert.Equal(Air.Types.INFO, first.Type);
        Assert.Equal(0U, first.PowerType);
        Assert.Equal(0U, first.UserId);
        Assert.Empty(first.Powers);
        Assert.NotSame(first.Powers, second.Powers);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Air.Types)));
        Assert.True(typeof(Air.Types).IsDefined(typeof(FlagsAttribute), false));
        Assert.Equal(new[] { 0, 1, 3, 4, 5 }, Enum.GetValues<Air.Types>().Select(x => (int)x));
        var power = new Air.Power();
        Assert.Equal(0U, power.Id);
        Assert.Equal(0U, power.SenseLevel);
        Assert.Equal(0U, power.SenseAipPower);
        Assert.Equal(0U, power.ArmedLevel);
        Assert.Equal(0U, power.ArmedAipPower);
        Assert.Equal(0U, power.OverLoadLevel);
        Assert.Equal(0U, power.OverLoadAipPower);
        Assert.Equal(0U, power.Points);
        first.Powers.Add(power);
        power.Points = 13;
        Assert.Same(power, Assert.Single(first.Powers));
        Assert.Equal(13U, first.Powers[0].Points);
        Assert.Empty(second.Powers);
    }

    [Theory]
    [InlineData(0U, 1U)]
    [InlineData(5U, 5U)]
    public void SendUsesPhysicalIdAndNormalizesTheSamePowersOwner(uint before, uint after)
    {
        using var load = new LoadScope(true);
        var powers = new Gear.Powers { Id = 17, SenseLevel = before, SenseAipPower = 19,
            ArmedLevel = 3, ArmedAipPower = 23, OverLoadLevel = 4, OverLoadAipPower = 29, Points = 31 };
        var firstHolder = new Gear { AirPowers = powers };
        var secondHolder = new Gear { AirPowers = powers };

        var packet = Read(secondHolder.AirPowers.Send(Air.Types.VIEW, uint.MaxValue));

        Assert.Same(powers, firstHolder.AirPowers);
        Assert.Same(powers, secondHolder.AirPowers);
        Assert.Equal(after, powers.SenseLevel);
        Assert.Equal(LegacyAirPowerUpType.View, packet.Type);
        Assert.Equal(uint.MaxValue, packet.UserId);
        Assert.Equal(0U, packet.PowerType);
        Assert.Equal(new LegacyAirPowerUpPower(17, after, 19, 3, 23, 4, 29, 31), Assert.Single(packet.Powers));
    }

    [Theory]
    [InlineData(4294967294U, 3U, 1U)]
    [InlineData(0U, uint.MaxValue, uint.MaxValue)]
    public void AddPointsWrapsBeforeEncodingAndDoesNotNormalizeSense(uint before, uint add, uint expected)
    {
        using var load = new LoadScope(true);
        var powers = new Gear.Powers { Id = 99, Points = before, SenseAipPower = 19,
            ArmedLevel = 3, ArmedAipPower = 23, OverLoadLevel = 4, OverLoadAipPower = 29 };

        var packet = Read(powers.AddPoints(add, 73));

        Assert.Equal(expected, powers.Points);
        Assert.Equal(0U, powers.SenseLevel);
        Assert.Equal(99U, powers.Id);
        Assert.Equal(19U, powers.SenseAipPower);
        Assert.Equal(3U, powers.ArmedLevel);
        Assert.Equal(29U, powers.OverLoadAipPower);
        Assert.Equal(LegacyAirPowerUpType.AddPoints, packet.Type);
        Assert.Equal(73U, packet.UserId);
        Assert.Equal(new LegacyAirPowerUpPower(7, 0, 0, 0, 0, 0, 0, expected), Assert.Single(packet.Powers));
    }

    [Fact]
    public void DisabledLoadReturnsNullBeforeReadingEitherArgument()
    {
        using var load = new LoadScope(false);
        Assert.Null(Air.Process(null!, null!, 91));
    }

    [Fact]
    public void SendAndAddPointsMutateBeforeTheDisabledWireGateReturnsNull()
    {
        using var load = new LoadScope(false);
        var powers = new Gear.Powers { Points = uint.MaxValue };
        Assert.Null(powers.Send(Air.Types.INFO, 91));
        Assert.Equal(1U, powers.SenseLevel);
        powers.SenseLevel = 0;
        Assert.Null(powers.AddPoints(2, 91));
        Assert.Equal(1U, powers.Points);
        Assert.Equal(0U, powers.SenseLevel);
    }

    [Fact]
    public void NonPracticeTypesReturnEmptyWithoutReadingThePowersOwner()
    {
        using var load = new LoadScope(true);
        foreach (var type in new[] { Air.Types.INFO, Air.Types.VIEW, Air.Types.UPGRADE,
                     Air.Types.ADDPOINTS, (Air.Types)999 })
        {
            var request = new Air { Type = type, Powers = null! };
            Assert.Empty(Air.Process(request, null!, 91));
            Assert.Equal(type, request.Type);
            Assert.Null(request.Powers);
        }
    }

    private static LegacyAirPowerUpPacket Read(byte[] bytes)
    {
        // Native TQServer reserves an eight-byte seal outside the header length.
        var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        Assert.Equal(bytes.Length - 8, length);
        Assert.Equal((ushort)2464, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), bytes[^8..]);
        var frame = new PacketFrame(new PacketHeader(length, 2464),
            new ReadOnlySequence<byte>(bytes.AsMemory(0, length)),
            new ReadOnlySequence<byte>(bytes.AsMemory(4, length - 4)));
        Assert.True(LegacyAirPowerUpPacket.TryParse(frame, out var packet));
        return packet;
    }

    private sealed class LoadScope : IDisposable
    {
        private readonly bool before = LegacyNativeCombatGearTable.m_Load;
        internal LoadScope(bool loaded) => LegacyNativeCombatGearTable.m_Load = loaded;
        public void Dispose() => LegacyNativeCombatGearTable.m_Load = before;
    }
}
