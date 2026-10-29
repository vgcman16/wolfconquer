using System.Buffers.Binary;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedPhysicalMutableStatusEquipmentTests
{
    private static async Task<DirectOverlayEquipmentResult> DirectOverlayEquipmentRunAsync(string scenario, bool admitted)
    {
        OwnedEquipmentFixture? fixture = null;
        DirectOverlayEquipmentResult? result = null;
        Exception? primary = null;
        try
        {
            fixture = new OwnedEquipmentFixture();
            await fixture.Initialize(scenario);
            result = await ExerciseAsync(fixture, scenario, admitted);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (fixture is not null)
            try { fixture.RetainEvidence = primary is not null; await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        return Assert.IsType<DirectOverlayEquipmentResult>(result);
    }

    private static async Task<DirectOverlayEquipmentResult> ExerciseAsync(OwnedEquipmentFixture f, string scenario, bool admitted)
    {
        var distinct = scenario == "distinct";
        var alternate = scenario is "normal-equip-while-alternate" or "active-alternate-equip" or "normal-unequip-while-alternate";
        var alternateEquip = scenario is "inactive-alternate-equip" or "active-alternate-equip";
        var unequip = scenario.Contains("unequip", StringComparison.Ordinal);
        var position = (ushort)(alternateEquip || scenario == "inactive-alternate-unequip" ? 51 : 1);
        var removedUid = position == 51 ? 91_002U : 91_001U;
        var initial = await f.QueryAsync();
        Assert.Equal(60U, initial.Breakthrough);
        Assert.Equal(50U, initial.Counteraction);
        // Actual14500 raw0 normalizes to100. The fixed clock and60s row are
        // owned test inputs, not a shipped balance or background-tick claim.
        await f.Dispatch(PlayerUid, new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
            X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
            ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: FineRainAttachStatusSpellId)).ToFrameBytes());
        var fineRain = f.Owner;
        Assert.True(HasStatus(fineRain, 0xD4), f.Log.ToString());
        Assert.True(HasStatus(fineRain, 0xD5), f.Log.ToString());
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fineRain).StampMilliseconds);
        Assert.Equal((uint?)60, LegacyPhysicalStatusOverlay.ForOwner(fineRain).Breakthrough);
        Assert.Equal((uint?)50, LegacyPhysicalStatusOverlay.ForOwner(fineRain).Counteraction);
        if (!distinct)
        {
            await f.Dispatch(TargetPlayerUid, new MsgAttackPacket(new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: PlayerUid,
                X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes());
            Assert.Equal(75U, (await f.QueryAsync()).Counteraction);
        }
        f.Clock.AdvanceMilliseconds(admitted ? 6_000 : 1_000);
        var beforeItems = File.ReadAllBytes(f.ItemsPath);
        var expectedItems = ExpectedMove(beforeItems, unequip, removedUid, position);
        var registry = File.ReadAllBytes(f.Data.State);
        var operationStart = f.Captures.Count;
        var itemCallback = 0;
        var acceptedBeforeQuery = false;
        var beforeQueryStamp = -1;
        var beforeQueryStatus = new LegacyPlayerStatusPacket(new uint[LegacyPlayerStatusPacket.FieldCount]);
        Exception? callbackFailure = null;
        f.OnSend = async (uid, bytes) =>
        {
            if (uid != PlayerUid || !MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var itemPacket) ||
                itemPacket.Action is not (MsgItemUsageAction.RemoveInventory or MsgItemUsageAction.Unequip) ||
                Interlocked.CompareExchange(ref itemCallback, 1, 0) != 0) return;
            try
            {
                // FSB178: the actual journal and Store item move precede the
                // first item response, while the old status/query stamp remains.
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.False(Monitor.IsEntered(LegacySecondaryPasswordRuntime.SyncRoot));
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                Assert.Equal(expectedItems, File.ReadAllBytes(f.ItemsPath));
                var current = f.Owner;
                acceptedBeforeQuery = unequip
                    ? current.PlayerItems.EquipmentItems.All(item => item.Uid != removedUid)
                    : current.PlayerItems.EquipmentItems.Any(item => item.Uid == 91_003 && item.Position == position);
                beforeQueryStamp = LegacyFineRainEquipmentAdmission.ForOwner(current).StampMilliseconds;
                // A real status-query packet is issued from the actual sender
                // observation point, before the equipment operation's query tail.
                beforeQueryStatus = await f.QueryAsync();
            }
            catch (Exception error) { callbackFailure = error; throw; }
        };
        await f.Dispatch(PlayerUid, new MsgItemUsagePacket(unequip ? removedUid : 91_003,
            position, unequip ? MsgItemUsageAction.Unequip : MsgItemUsageAction.Equip, 0, 0, 0, 0).ToFrameBytes());
        f.OnSend = null;
        // Frame catches handler/sender faults; it must not hide our assertions.
        if (callbackFailure is not null) ExceptionDispatchInfo.Capture(callbackFailure).Throw();
        var operationFrames = f.Captures.ToArray().Skip(operationStart).ToArray();
        var waits = operationFrames.Count(bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
            message.Content.Contains("You Shoud wait 6 seconds.", StringComparer.Ordinal));
        Assert.Equal(expectedItems, File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(registry, File.ReadAllBytes(f.Data.State));
        AssertExactMove(f, unequip, removedUid, position);
        var afterMutation = await f.QueryAsync();
        var afterMutationStamp = LegacyFineRainEquipmentAdmission.ForOwner(f.Owner).StampMilliseconds;
        var rawAfter = LegacyPlayerStatusCalculator.CalculateFields(f.Owner.Character,
            f.Owner.PlayerItems, f.World, 100_000, 0, alternateEquipmentActive: alternate);
        if (distinct && !admitted)
        {
            f.Clock.AdvanceMilliseconds(5_000);
            var beforeQuery = f.Captures.Count;
            await f.Dispatch(PlayerUid, LegacyRuneStoragePacket.Startup().ToFrameBytes());
            Assert.DoesNotContain(f.Captures.ToArray().Skip(beforeQuery), bytes =>
                LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
                message.Content.Contains("You Shoud wait 6 seconds.", StringComparer.Ordinal));
        }
        var finalStatus = await f.QueryAsync();
        Assert.DoesNotContain(f.AllFrames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
        return new(f.Owner, beforeQueryStatus, beforeQueryStamp, afterMutation, afterMutationStamp, finalStatus,
            LegacyFineRainEquipmentAdmission.ForOwner(f.Owner).StampMilliseconds, rawAfter,
            itemCallback != 0, acceptedBeforeQuery, waits);
    }

    private static byte[] ExpectedMove(byte[] before, bool unequip, uint removedUid, ushort position)
    {
        var expected = (byte[])before.Clone();
        var count = BinaryPrimitives.ReadInt32LittleEndian(expected);
        for (var i = 0; i < count; i++)
        {
            var offset = 4 + i * PlayerItemSnapshotLoader.LegacyDbItemSize;
            Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset + 136, 4)));
            var uid = BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset, 4));
            var slot = BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(offset + 12, 2));
            if (unequip ? uid == removedUid : uid == 91_003 || slot == position)
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset + 12, 2), !unequip && uid == 91_003 ? position : (ushort)0);
        }
        return expected;
    }

    private static void AssertExactMove(OwnedEquipmentFixture f, bool unequip, uint removedUid, ushort position)
    {
        var disk = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(PlayerUid);
        var current = f.Owner.PlayerItems;
        static (uint Uid, uint ItemId, ushort Position)[] Rows(PlayerItemSnapshot source) =>
            source.InventoryItems.Concat(source.EquipmentItems).Concat(source.RuneItems)
                .Select(row => (row.Uid, row.ItemId, row.Position)).OrderBy(row => row.Uid).ToArray();
        Assert.Equal(Rows(disk), Rows(current));
        Assert.Equal(new uint[] { 91_001, 91_002, 91_003, 91_004 }, Rows(current).Select(row => row.Uid));
        if (unequip)
        {
            Assert.Contains(current.InventoryItems, row => row.Uid == removedUid && row.Position == 0);
            Assert.DoesNotContain(current.EquipmentItems, row => row.Uid == removedUid);
        }
        else
        {
            Assert.Equal(91_003U, Assert.Single(current.EquipmentItems, row => row.Position == position).Uid);
            Assert.Contains(current.InventoryItems, row => row.Uid == removedUid && row.Position == 0);
        }
    }

    private static bool HasStatus(WorldSession owner, int flag)
    {
        if (!owner.Character.Attributes.TryGetValue("StatusFlags", out var text)) return false;
        var words = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return flag / 32 < words.Length && ulong.TryParse(words[flag / 32], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var word) && (word & (1UL << (flag % 32))) != 0;
    }

    private sealed record DirectOverlayEquipmentResult(WorldSession Owner, LegacyPlayerStatusPacket BeforeQueryStatus,
        int BeforeQueryStamp, LegacyPlayerStatusPacket AfterMutationStatus, int AfterMutationStamp,
        LegacyPlayerStatusPacket FinalStatus, int FinalStamp, uint[] RawAfterMutation,
        bool ItemCallbackRan, bool AcceptedItemsObservedBeforeQuery, int OperationWaitMessages);
}
