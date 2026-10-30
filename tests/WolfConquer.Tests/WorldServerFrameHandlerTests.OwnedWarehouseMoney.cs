using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// The real native fixture owns shared Beast metadata; the established
// nonparallel collection protects that cache for this complete test class.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class WorldServerFrameHandlerTests
{
    // Spec: docs/protocol/specs/7899-owned-warehouse-money-publication.md.
    // Keep the two original successful wire cases, now through the actual
    // configured Runtime -> Frame -> Store -> Character/Items v2 WAL owner.
    private Task AssertOwnedWarehouseMoneyMutationAsync(bool withdraw)
    {
        const uint uid = OwnedEquipmentBehaviorFixture.Actor; // Original golden UID1293600.
        var initialWarehouse = withdraw ? 350L : 50L;
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(uid, uid, warehouseMoney: initialWarehouse, money: 1_000);
        var original = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(_usersRoot, "1293600.ini")), "1293600.ini");
        return OwnedEquipmentBehaviorFixture.RunAsync(new(original, PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), async f =>
            {
                var before = f.Current(); var native = before.NativeGameClient!;
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.Same(f.Store, binding.Store); Assert.Same(f.Connection(uid), binding.Connection);
                Assert.True(f.Runtime.Ordinary.IsReconciliationComplete);
                Assert.True(f.Recovery.IsReadyFor(f.Data.Users, f.Data.Items));
                Assert.Equal((1_000L, initialWarehouse), (native.Player.Money, native.Player.WHMoney));
                Assert.Equal(original.ConquerPoints, before.Character.ConquerPoints);
                Assert.Equal(original.BoundConquerPoints, before.Character.BoundConquerPoints);
                Assert.Equal(original.BankConquerPoints, before.Character.BankConquerPoints);
                Assert.NotEmpty(f.ActorFrames); // Only legitimate completed login output is cleared.
                f.ActorFrames.Clear();

                var money = withdraw ? 1_100L : 700L; var warehouse = withdraw ? 250L : 350L;
                var amount = withdraw ? 100UL : 300UL;
                var request = new MsgItemUsagePacket(12345, amount,
                    withdraw ? MsgItemUsageAction.WarehouseWithdraw : MsgItemUsageAction.DepositWarehouse,
                    112233, 44, 55, 66);
                var moneyGolden = Convert.FromHexString(withdraw
                    ? "0F00800808A0FA4E1205080410CC08" : "0F00800808A0FA4E1205080410BC05");
                var warehouseGolden = Convert.FromHexString("0F00800808A0FA4E1205080910FA01");
                var requestGolden = OwnedWarehouseUsageGolden(amount, withdraw ? (ushort)11 : (ushort)10, 112233);
                Assert.Equal(requestGolden, request.ToFrameBytes());
                byte[][] expectedFrames = withdraw
                    ? [moneyGolden, moneyGolden, warehouseGolden, requestGolden]
                    : [moneyGolden, moneyGolden, requestGolden];
                var files = f.Data.Files();
                var characterRelative = Path.GetRelativePath(f.Data.Root, f.CharacterPath(uid));
                var expectedFiles = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase)
                { [characterRelative] = OwnedWarehouseExpectedCharacter(files[characterRelative], money, warehouse) };
                var state = File.ReadAllBytes(f.Data.State);
                var otherWallets = (native.Player.ConquerPoints, native.Player.BoundConquerPoints, native.ConquerPointsSave);

                void AssertCommitted()
                {
                    var current = f.Current();
                    Assert.NotSame(before, current); Assert.Equal(money, current.Character.Money);
                    Assert.Equal(warehouse.ToString(CultureInfo.InvariantCulture), current.Character.Attributes["WHMoney"]);
                    Assert.Equal((money, warehouse), (native.Player.Money, native.Player.WHMoney));
                    Assert.Equal(otherWallets, (native.Player.ConquerPoints, native.Player.BoundConquerPoints, native.ConquerPointsSave));
                    Assert.Same(binding, native.OrdinarySpellBinding); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                    Assert.Equal(original.ConquerPoints, current.Character.ConquerPoints);
                    Assert.Equal(original.BoundConquerPoints, current.Character.BoundConquerPoints);
                    Assert.Equal(original.BankConquerPoints, current.Character.BankConquerPoints);
                    Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems),
                        PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
                    f.Data.AssertFiles(expectedFiles); // Exact Character projection, Items, sidecars and complete UID ledger.
                    Assert.Equal(state, File.ReadAllBytes(f.Data.State));
                    Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                    Assert.False(f.Recovery.IsBlocked);
                }

                int observations = 0;
                f.BeforeSend = (recipient, bytes) =>
                {
                    Assert.Equal(uid, recipient); AssertCommitted();
                    Assert.True(observations < expectedFrames.Length);
                    Assert.Equal(expectedFrames[observations++], bytes);
                };
                try { await f.DispatchAsync(request); }
                finally { f.BeforeSend = null; }

                Assert.Equal(withdraw ? 4 : 3, observations);
                Assert.Equal(withdraw ? 4 : 3, f.ActorFrames.Count);
                for (var index = 0; index < expectedFrames.Length; index++) Assert.Equal(expectedFrames[index], f.ActorFrames[index]);
                AssertSingleUpdate(f.ActorFrames[0], uid, LegacyMsgUpdateDataType.Money, checked((ulong)money));
                AssertSingleUpdate(f.ActorFrames[1], uid, LegacyMsgUpdateDataType.Money, checked((ulong)money));
                if (withdraw) AssertSingleUpdate(f.ActorFrames[2], uid, LegacyMsgUpdateDataType.WarehouseMoney, 250);
                AssertItemUsage(f.ActorFrames[^1], request);
                AssertCommitted();

                var showRequest = request with { DwParam = 0, Action = MsgItemUsageAction.ShowWarehouseMoney, Timestamp = 112244 };
                int showBefore = f.ActorFrames.Count;
                await f.DispatchAsync(showRequest);
                Assert.Equal(showBefore + 1, f.ActorFrames.Count);
                AssertItemUsage(f.ActorFrames[^1], showRequest with { DwParam = checked((ulong)warehouse) });
                Assert.Equal(OwnedWarehouseUsageGolden(checked((ulong)warehouse), 9, 112244), f.ActorFrames[^1]);
                var persisted = f.Data.Loader.LoadByAccountId(uid);
                Assert.NotNull(persisted); Assert.Equal(money, persisted.Money);
                Assert.Equal(warehouse.ToString(CultureInfo.InvariantCulture), persisted.Attributes["WHMoney"]);
                AssertCommitted();
                Assert.Contains(withdraw ? "warehouse withdraw" : "warehouse deposit", f.Log.ToString());
            });
    }

    private static byte[] OwnedWarehouseExpectedCharacter(byte[] before, long money, long warehouse)
    {
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        foreach (var (key, value) in new[] { ("Money", money), ("WHMoney", warehouse) })
        {
            var pattern = "(?m)^" + key + "=[^\\r\\n]*";
            Assert.Single(Regex.Matches(text, pattern).Cast<Match>());
            text = Regex.Replace(text, pattern, _ => key + "=" + value.ToString(CultureInfo.InvariantCulture));
        }
        return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
    }

    private static byte[] OwnedWarehouseUsageGolden(ulong amount, ushort action, uint timestamp)
    {
        // Independent D13 opcode2137 frame: fixed91-byte size/opcode, original
        // request metadata and every reserved tail byte, without its serializer.
        var bytes = new byte[91];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), 91);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, 2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 12345);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), amount);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16, 2), action);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(18, 4), timestamp);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(22, 4), 44);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(26, 4), 55);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(30, 4), 66);
        return bytes;
    }
}
