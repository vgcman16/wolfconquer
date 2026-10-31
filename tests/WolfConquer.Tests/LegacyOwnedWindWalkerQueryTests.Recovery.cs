using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(2, false)] [InlineData(3, false)] [InlineData(3, true)]
    public Task ActualBagWalRecoveryNeverReplaysTheQueryFlagOrAllocatesAnotherUid(int boundary, bool flushed) => Run(async f =>
    {
        AssertJoined(f, true); var current = f.Current; var native = f.Native; var oldRow = f.Row!; var timer = oldRow.Timer;
        var image = native.OrdinarySpellBinding!.Items.Image; var nativeSpells = native.MySpells!.ClientSpells;
        var data = f.Owner.Data; var files = data.Files(); var ledger = File.ReadAllBytes(data.State);
        var targets = new[] { f.Owner.CharacterPath(Uid), f.Owner.ItemPath(Uid), Path.Combine(data.Spells, Uid.ToString(CultureInfo.InvariantCulture) + ".bin") };
        var before = targets.Select(File.ReadAllBytes).ToArray();
        var moved = Assert.Single(current.PlayerItems.InventoryItems, row => row.Uid == BagItem).WithPosition(211);
        var afterItems = current.PlayerItems with
        {
            InventoryItems = current.PlayerItems.InventoryItems.Where(row => row.Uid != BagItem).ToArray(),
            RuneItems = current.PlayerItems.RuneItems.Append(moved).ToArray(),
        };
        var expectedAfter = new[] { before[0], PlayerItemSnapshotLoader.ToLegacyBytes(afterItems), before[2] };
        int first = f.Sent.Count;
        using (var interruption = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Owner.Handler.ItemSpellJournalInterruption = interruption;
            try { await f.BagAsync(); } finally { f.Owner.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(interruption.PendingObserved, f.Owner.Log.ToString());
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), interruption.Visited);
        }
        Assert.Equal(first, f.Sent.Count); Assert.Same(current, f.Current); Assert.True(f.Owner.Recovery.IsBlocked);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(native.OrdinarySpellBinding));
        Assert.Same(image, native.OrdinarySpellBinding.Items.Image); Assert.Same(nativeSpells, native.MySpells.ClientSpells);
        Assert.Same(oldRow, f.Row); Assert.Equal(timer, oldRow.Timer); Assert.Equal(ledger, File.ReadAllBytes(data.State));
        var pendingPath = Path.Combine(data.Users, ".managed-trade-journal", "pending.json");
        var record = JsonNode.Parse(File.ReadAllBytes(pendingPath))!.AsObject();
        Assert.Equal(20, record["Version"]!.GetValue<int>());
        Assert.True(record["RuneBag"]!["IntoBag"]!.GetValue<bool>()); Assert.Equal(BagItem, record["RuneBag"]!["SelectedUid"]!.GetValue<uint>());
        Assert.Null(record["RuneBag"]!["Request"]); Assert.Null(record["RuneBag"]!["Allocation"]);
        var changes = record["Changes"]!.AsArray(); Assert.Equal(3, changes.Count);
        for (int index = 0; index < targets.Length; index++)
        {
            var change = changes[index]!;
            Assert.Equal((long)Uid, change["FileId"]!.GetValue<long>());
            Assert.Equal((int)new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items, LegacyTradeJournalFileKind.Spells }[index],
                change["Kind"]!.GetValue<int>());
            Assert.Equal(before[index], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(expectedAfter[index], Convert.FromBase64String(change["After"]!.GetValue<string>()));
            Assert.Equal(!flushed && index < boundary ? expectedAfter[index] : before[index], File.ReadAllBytes(targets[index]));
            if (flushed)
            {
                var stage = targets[index] + ".managed-trade-" + record["TransactionId"]!.GetValue<string>() + ".tmp";
                Assert.Equal(expectedAfter[index], File.ReadAllBytes(stage));
            }
            files[Path.GetRelativePath(data.Root, targets[index])] = expectedAfter[index];
        }
        await f.CloseAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(data.Loader, data.Options(), out var opened, out var reason), reason);
            await using var owner = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            Assert.True(owner.Recovery.IsReadyForCharacterItemsSpells(data.Users, data.Items, data.Spells));
            data.AssertFiles(files); Assert.Equal(ledger, File.ReadAllBytes(data.State)); Assert.False(LegacyTradeJournal.HasPending(data.Users));
            Assert.Equal(first, f.Sent.Count); Assert.Equal(timer, oldRow.Timer);
            if (restart == 1) await AdmitFreshAfterRawProof(f, owner, files, oldRow);
        }
    }, mode: "pair");

    private static async Task AdmitFreshAfterRawProof(WindFixture previous, LegacyOwnedAdmissionRuntime owner,
        Dictionary<string, byte[]> expectedFiles, LegacyNativeStatusFlagsBigVector32.Flag previousRow)
    {
        var data = previous.Owner.Data; var sent = new List<byte[]>(); var native = new LegacyNativeGameClientRuntime();
        using var lifetime = new CancellationTokenSource(); using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(data.Loader, null, Catalog(), LegacyRuntimeUpdateLoopOptions.Disabled,
            log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: owner);
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (memory, token) => { token.ThrowIfCancellationRequested(); sent.Add(memory.ToArray()); return ValueTask.CompletedTask; },
            _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, native);
        try
        {
            data.AssertFiles(expectedFiles); Assert.Empty(native.Player!.BitVector.ArrayFlags); Assert.Empty(sent);
            var earliest = DateTime.Now;
            await handler.HandleFrameAsync(connection, OwnedEquipmentBehaviorFixture.ReadFrame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()), lifetime.Token);
            var latest = DateTime.Now;
            var store = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
            Assert.True(store.TryGet(connection, out var current), log.ToString()); Assert.NotNull(current);
            Assert.True(store.IsBoundSpellOwnerCurrent(native.OrdinarySpellBinding!));
            var fresh = Assert.IsType<LegacyNativeStatusFlagsBigVector32.Flag>(native.Player.BitVector.ArrayFlags[Fan]);
            Assert.NotSame(previousRow, fresh); Assert.InRange(fresh.Timer, earliest, latest); Assert.Equal(2592000, fresh.Secounds); Assert.False(fresh.RemoveOnDead);
            Assert.True(LegacyPlayerDeathState.HasStatusFlag(current!.Character.Attributes, Fan));
            Assert.Equal((ushort)211, Assert.Single(current.PlayerItems.RuneItems, row => row.Uid == BagItem).Position);
            data.AssertFiles(expectedFiles); Assert.False(LegacyTradeJournal.HasPending(data.Users));
        }
        finally { await connection.DisconnectAsync(); await connection.CompleteTransportCloseAsync(); }
    }
}
