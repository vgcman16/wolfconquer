using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static ItemType CreateRetainedNativeMagic421Helmet(
        bool nativeMana500 = false, uint itemId = 111_098,
        string name = "RetainedNativeMagic421Helmet")
    {
        // Legal custom raw item input, not stock item/acquisition or a status seed.
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[18] = nativeMana500 ? "4700" : "4100";
        fields[29] = nativeMana500 ? "200" : "0";
        return new ItemType(checked((int)itemId), name, fields);
    }

    private static WorldSession CreateRetainedNativeMagic421Input(
        WorldSession original, bool nativeMana500 = false)
    {
        AssertRetainedNativeMagic421RawCharacter(original, expectedSpirit: 300);
        var position = (ushort)(original.AlternateEquipmentActive ? 51 : 1);
        Assert.DoesNotContain(original.PlayerItems.EquipmentItems,
            item => item.Position == position);
        var uid = checked((uint)original.Character.Uid + 60_000U);
        Assert.DoesNotContain(original.PlayerItems.EquipmentItems, item => item.Uid == uid);
        return original with
        {
            // Only the explicitly selected resource500 mode changes raw Spirit.
            // Class, FirstClass, attributes, runes and all other inputs are retained.
            Character = nativeMana500 ? original.Character with { Spirit = 100 } : original.Character,
            PlayerItems = original.PlayerItems with
            {
                EquipmentItems = original.PlayerItems.EquipmentItems.Concat(
                    new[] { new PlayerItemSnapshotItem
                    {
                        Uid = uid, ItemId = 111_098, Position = position,
                        Durability = 100, MaximumDurability = 100,
                    } }).ToArray(),
            },
        };
    }

    private static async Task<WorldPacketHandlerContext> PrepareRetainedNativeMagic421QueriesAsync(
        WorldPacketHandlerContext context, ConquerTcpConnection targetConnection,
        List<byte[]> ownerFrames, List<byte[]> targetFrames, bool nativeMana500 = false)
    {
        var sessions = Assert.IsType<WorldSessionStore>(context.Sessions);
        WorldSession Current(ConquerTcpConnection connection) =>
            Assert.IsType<WorldSession>(sessions.TryGet(connection, out var current) ? current : null);
        async Task Query(ConquerTcpConnection connection, List<byte[]> frames)
        {
            var before = Current(connection);
            AssertRetainedNativeMagic421RawCharacter(before, nativeMana500 ? 100 : 300);
            // GameClient.CalculateMana: exact class multiplier, not the target calculator.
            var job = before.Character.Class;
            var multiplier = job is >= 13_005 and <= 13_051 or >= 14_005 and <= 14_051
                ? 30 : job switch
                {
                    13_002 or 14_002 => 15,
                    13_003 or 14_003 => 20,
                    13_004 or 14_004 => 25,
                    _ => 5,
                };
            var maximumMana = checked((uint)(before.Character.Spirit * multiplier));
            // Native Equip.AddRuneAtributes1813-1817: Rise adds HP/MaxHP50000 and MA13000.
            var riseHitpoints = HasLegacyStatusFlag(before.Character.Attributes, LegacyRiseOfTaoismFlag)
                ? 50_000U : 0U;
            await PrepareNativeOrdinaryMagicInputQueryAsync(
                context with { Connection = connection, Session = before }, () => frames.ToArray(),
                expectedMagicAttack: riseHitpoints == 0 ? 301U : 13_301U,
                expectedMaximumHitpoints: checked(5_000U + riseHitpoints),
                expectedMaximumMana: maximumMana, expectedHitpointIncrease: riseHitpoints);
        }
        await Query(context.Connection, ownerFrames);
        await Query(targetConnection, targetFrames);
        // Both completed real251 proofs precede the public cast/callback under test.
        // The shared query support proves the expected current HP increase and unchanged MP.
        ownerFrames.Clear(); targetFrames.Clear();
        return context with { Session = Current(context.Connection) };
    }

    private static void AssertRetainedNativeMagic421RawCharacter(
        WorldSession session, long expectedSpirit)
    {
        Assert.Equal(expectedSpirit, session.Character.Spirit);
        Assert.Equal(0L, session.Character.Strength);
        Assert.Equal(0L, session.Character.Agility);
        Assert.Equal(0L, session.Character.Vitality);
        // These promoted Trojan classes have a different native base-HP coefficient.
        Assert.False(session.Character.Class is >= 1_001 and <= 1_051);
    }
}
