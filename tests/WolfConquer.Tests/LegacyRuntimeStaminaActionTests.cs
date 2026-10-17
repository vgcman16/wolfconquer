using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRuntimeStaminaActionTests
{
    [Fact]
    public void SittingStaminaTicksEverySecondAndAddsFifteen()
    {
        var start = new DateTimeOffset(2026, 6, 21, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(new Dictionary<string, string>
            {
                ["Action"] = "250",
                ["Stamina"] = "20",
            }),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        AssertStaminaUpdate(ticker.CreateDueUpdates(start), 35);
        AssertNoStaminaUpdate(
            ticker.CreateDueUpdates(
                start.AddMilliseconds(999),
                CreateSnapshot(new Dictionary<string, string>
                {
                    ["Action"] = "250",
                    ["Stamina"] = "35",
                })));
        AssertStaminaUpdate(
            ticker.CreateDueUpdates(
                start.AddSeconds(1),
                CreateSnapshot(new Dictionary<string, string>
                {
                    ["Action"] = "250",
                    ["Stamina"] = "35",
                })),
            50);
    }

    [Fact]
    public void StandingStaminaTicksEveryThreeSecondsAndAddsFive()
    {
        var start = new DateTimeOffset(2026, 6, 21, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(new Dictionary<string, string>
            {
                ["Action"] = "0",
                ["Stamina"] = "20",
            }),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        AssertStaminaUpdate(ticker.CreateDueUpdates(start), 25);
        AssertNoStaminaUpdate(
            ticker.CreateDueUpdates(
                start.AddSeconds(2),
                CreateSnapshot(new Dictionary<string, string>
                {
                    ["Action"] = "0",
                    ["Stamina"] = "25",
                })));
        AssertStaminaUpdate(
            ticker.CreateDueUpdates(
                start.AddSeconds(3),
                CreateSnapshot(new Dictionary<string, string>
                {
                    ["Action"] = "0",
                    ["Stamina"] = "25",
                })),
            30);
    }

    [Fact]
    public void RuntimeStaminaUsesLatestSnapshotActionAfterStanceChange()
    {
        var start = new DateTimeOffset(2026, 6, 21, 8, 0, 0, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(new Dictionary<string, string>
            {
                ["Action"] = "0",
                ["Stamina"] = "20",
            }),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        AssertStaminaUpdate(ticker.CreateDueUpdates(start), 25);
        var update = AssertStaminaUpdate(
            ticker.CreateDueUpdates(
                start.AddSeconds(1),
                CreateSnapshot(new Dictionary<string, string>
                {
                    ["Action"] = "250",
                    ["Stamina"] = "25",
                })),
            40);

        Assert.Equal((ushort)40, update.RuntimeStamina);
    }

    private static LegacyRuntimeUpdate AssertStaminaUpdate(
        IReadOnlyList<LegacyRuntimeUpdate> updates,
        ulong value)
    {
        LegacyRuntimeUpdate? staminaUpdate = null;
        foreach (var update in updates)
        {
            if (!TryReadSingleUpdate(update.FrameBytes, out var entry) ||
                entry.Type != LegacyMsgUpdateDataType.Stamina)
            {
                continue;
            }

            Assert.Null(staminaUpdate);
            staminaUpdate = update;
            Assert.Equal(value, entry.Value);
            Assert.Equal((ushort)value, update.RuntimeStamina);
        }

        Assert.NotNull(staminaUpdate);
        return staminaUpdate;
    }

    private static void AssertNoStaminaUpdate(IReadOnlyList<LegacyRuntimeUpdate> updates)
    {
        foreach (var update in updates)
        {
            if (TryReadSingleUpdate(update.FrameBytes, out var entry))
            {
                Assert.NotEqual(LegacyMsgUpdateDataType.Stamina, entry.Type);
            }
        }
    }

    private static bool TryReadSingleUpdate(byte[] bytes, out LegacyMsgUpdateEntry entry)
    {
        entry = default;
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var parsed) ||
            parsed.Updates.Count != 1)
        {
            return false;
        }

        entry = parsed.Updates[0];
        return true;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 705,
            Y: 577,
            PreviousMapId: 1002,
            PreviousMapX: 705,
            PreviousMapY: 577,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 61_229,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "",
            Attributes: new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
