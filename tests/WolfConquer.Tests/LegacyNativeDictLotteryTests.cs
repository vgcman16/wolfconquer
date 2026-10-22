using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Table = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeDictLotteryTests
{
    [Theory]
    [InlineData("default", "08001000")]
    [InlineData("null-items", "08111000")]
    [InlineData("zero-item", "080010001A0408001000")]
    [InlineData("false-item", "080110011A0408111000")]
    [InlineData("true-item", "080110011A0408111001")]
    [InlineData("null-item", "080010001A00")]
    [InlineData("mixed-items", "080010001A04080010001A001A04081110011A0808FFFFFFFF0F1000")]
    [InlineData("max", "08FFFFFFFF0F10FFFFFFFF0F1A0808FFFFFFFF0F1001")]
    [InlineData("null-outer", "")]
    public void WireMatchesNinePinnedRegisteredNativeOraclePayloads(string kind, string expected)
    {
        using var s = new Scope();
        // Root observed these nine payloads with the pinned original protobuf-net2.0.0.668,
        // the actual two runtime registrations and Stream/object-typed Serialize.
        Dict? info = kind switch
        {
            "null-items" => new Dict { Type = 17, Items2 = null! },
            "zero-item" => new Dict { Items2 = [new()] },
            "false-item" => new Dict { Type = 1, Done = 1, Items2 = [new() { Type = 17 }] },
            "true-item" => new Dict { Type = 1, Done = 1, Items2 = [new() { Type = 17, Exp = true }] },
            "null-item" => new Dict { Items2 = [null!] },
            "mixed-items" => new Dict { Items2 = [new(), null!, new() { Type = 17, Exp = true },
                new() { Type = uint.MaxValue }] },
            "max" => new Dict { Type = uint.MaxValue, Done = uint.MaxValue,
                Items2 = [new() { Type = uint.MaxValue, Exp = true }] },
            "null-outer" => null,
            _ => new Dict()
        };
        Assert.Equal(expected, Payload(info!, 2465));
        if (kind == "default")
        {
            Assert.Equal(0u, info!.Type);
            Assert.Equal(0u, info.Done);
            Assert.Empty(info.Items2);
            Assert.NotSame(info.Items2, new Dict().Items2);
            Assert.Equal(0u, new Dict.Lottery().Type);
            Assert.False(new Dict.Lottery().Exp);
        }
    }

    [Fact]
    public void UnloadedGatePrecedesAllNullOwnersAndWireConversion()
    {
        using var s = new Scope();
        GearTable.m_Load = false;
        Table.Info = null!;

        Assert.Null(Dict.Process(null!, null!, uint.MaxValue, uint.MaxValue));
        Assert.Null((byte[])new Dict { Items2 = [null!] });
        Assert.Null((byte[])(Dict)null!);
        Assert.Empty(s.Output.ToString());
        Assert.Null(Table.Info);
        Assert.Null(Gear.GetRandom);
    }

    [Theory]
    [InlineData(257u, "1")]
    [InlineData(uint.MaxValue, "255")]
    public void UnknownTypeOnlyNarrowsDiagnosticAndStillAcknowledgesUnchangedRetainedRows(uint type, string diagnostic)
    {
        using var s = new Scope();
        Table.Info = null!;
        s.User.AirPowers.Points = 3;
        var retained = new Dict.Lottery { Type = 67, Exp = true };
        var rows = new List<Dict.Lottery> { retained };
        var info = new Dict { Type = type, Done = 99, Items2 = rows };

        var frames = Dict.Process(info, s.User, 17, 0);

        Assert.Equal(2, frames.Count);
        Air(frames[0], 3);
        Ack(frames[1], type, (67, true));
        Assert.Equal("MsgDictLottery Type->" + diagnostic + s.Output.NewLine, s.Output.ToString());
        Assert.Same(rows, info.Items2);
        Assert.Same(retained, Assert.Single(rows));
        Assert.Equal(1u, info.Done);
        Assert.Equal(0u, s.User.Lottery);
        Assert.Null(Table.Info);
    }

    [Fact]
    public void FirstFreeTenConsumesOnlyTenSailFlagAndEmitsAllTenUncheckedExperienceRewards()
    {
        using var s = new Scope();
        Scope.Row(value: uint.MaxValue);
        var info = new Dict { Type = 1 };
        s.User.PirateExp = 5;

        var frames = Dict.Process(info, s.User, 17, 0);

        Assert.Equal(13, frames.Count);
        Assert.Equal(NativeCombatGearSupportFixture.Frame(12, lottery: 4), frames[0]);
        Air(frames[1], 0);
        for (int i = 0; i < 10; i++) Pirate(frames[i + 2], unchecked(4u - (uint)i));
        Ack(frames[^1], 1, Enumerable.Repeat((41u, false), 10).ToArray());
        Assert.Equal(4u, s.User.Lottery);
        Assert.Equal(unchecked(5u - 10u), s.User.PirateExp);
        Assert.Equal(10, info.Items2.Count);
        Assert.Equal(10, info.Items2.Distinct().Count());
        Assert.Null(Gear.GetRandom);
    }

    [Fact]
    public void FirstFreeSingleAddsItsFlagBeforeBalanceAndRewardAndKeepsTheActualPowerOwner()
    {
        using var s = new Scope();
        Scope.Row();
        var powers = s.User.AirPowers;
        powers.Id = 99;
        powers.ArmedLevel = 6;
        var info = new Dict();

        var frames = Dict.Process(info, s.User, 17, 0);

        Assert.Equal(4, frames.Count);
        Assert.Equal(NativeCombatGearSupportFixture.Frame(12, lottery: 8), frames[0]);
        Air(frames[1], 0);
        Pirate(frames[2], 7);
        Ack(frames[3], 0, (41, false));
        Assert.Same(powers, s.User.AirPowers);
        Assert.Equal(99u, powers.Id);
        Assert.Equal(6u, powers.ArmedLevel);
        Assert.Equal(0u, powers.SenseLevel); // AddPoints is not Send's normalizing path.
    }

    [Theory]
    [InlineData(0u, 405u, 1, false)]
    [InlineData(0u, 405u, 1, true)]
    [InlineData(1u, 4050u, 10, false)]
    [InlineData(1u, 4050u, 10, true)]
    public void UsedFreeFlagsApplyExactNativePriceAndDenyBeforeMetadataOrDoneMutation(
        uint type, uint cost, int draws, bool enough)
    {
        using var s = new Scope();
        if (enough) Scope.Row();
        else Table.Info = null!;
        s.User.Lottery = 12;
        s.User.AirPowers.Points = enough ? cost : cost - 1;
        var info = new Dict { Type = type, Done = 99 };
        var rows = info.Items2;

        var frames = Dict.Process(info, s.User, 17, 0);

        Assert.Same(rows, info.Items2);
        Assert.Equal(12u, s.User.Lottery);
        if (enough)
        {
            Assert.Equal(draws + 2, frames.Count);
            Air(frames[0], 0);
            for (int i = 0; i < draws; i++) Pirate(frames[i + 1], (uint)(7 * (i + 1)));
            Ack(frames[^1], type, Enumerable.Repeat((41u, false), draws).ToArray());
            Assert.Equal(0u, s.User.AirPowers.Points);
            Assert.Equal(1u, info.Done);
        }
        else
        {
            Assert.Empty(frames);
            Assert.Empty(rows);
            Assert.Equal(99u, info.Done);
            Assert.Equal(cost - 1, s.User.AirPowers.Points);
            Assert.Equal(0u, s.User.PirateExp);
        }
    }

    [Fact]
    public void FreeSingleFlagIsConsumedBeforeAnUnaffordableTenDrawAdmissionFails()
    {
        using var s = new Scope();
        Table.Info = null!;
        s.User.Lottery = 4;
        s.User.AirPowers.Points = 3644;
        var info = new Dict { Type = 1, Done = 99 };

        var frames = Dict.Process(info, s.User, 17, 0);

        Assert.Equal(NativeCombatGearSupportFixture.Frame(12, lottery: 12), Assert.Single(frames));
        Assert.Equal(12u, s.User.Lottery);
        Assert.Equal(3644u, s.User.AirPowers.Points);
        Assert.Equal(99u, info.Done);
        Assert.Empty(info.Items2);
    }

    [Fact]
    public void NonzeroCountBypassesAffordabilityAndAllChargingWithoutChangingDrawCount()
    {
        using var s = new Scope();
        Scope.Row();
        s.User.Lottery = 12;
        s.User.AirPowers.Points = 3;
        var info = new Dict { Type = 1 };

        var frames = Dict.Process(info, s.User, 17, uint.MaxValue);

        Assert.Equal(12, frames.Count);
        Air(frames[0], 3);
        Ack(frames[^1], 1, Enumerable.Repeat((41u, false), 10).ToArray());
        Assert.Equal(3u, s.User.AirPowers.Points);
        Assert.Equal(70u, s.User.PirateExp);
        Assert.Equal(10, info.Items2.Count);
    }
}
